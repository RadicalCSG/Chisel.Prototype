using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public struct ChiselSurfaceRenderBuffer
    {
        public BlobArray<Int32>		indices;
        public BlobArray<float3>	vertices;
        public BlobArray<float3>	normals;
        public BlobArray<float2>    uv0;

        public uint             geometryHash;
        public uint             surfaceHash;

        public SurfaceLayers    surfaceLayers;
        public Int32		    surfaceIndex;
    };

    public struct ChiselBrushRenderBuffer
    {
        public BlobArray<ChiselSurfaceRenderBuffer> surfaces;
    };

    internal sealed class Outline
    {
        public Int32[] visibleOuterLines;
        public Int32[] visibleInnerLines;
        public Int32[] visibleTriangles;
        public Int32[] invisibleOuterLines;
        public Int32[] invisibleInnerLines;
        public Int32[] invalidLines;

        public void Reset()
        {
            visibleOuterLines   = new Int32[0];
            visibleInnerLines   = new Int32[0];
            visibleTriangles    = new Int32[0];
            invisibleOuterLines = new Int32[0];
            invisibleInnerLines = new Int32[0];
            invalidLines        = new Int32[0];
        }
    };

    internal sealed class BrushOutline
    {
        public Outline      brushOutline   = new Outline();
        public Outline[]    surfaceOutlines;
        public float3[]     vertices;

        public void Reset()
        {
            brushOutline.Reset();
            surfaceOutlines = new Outline[0];
            vertices = new float3[0];
        }
    };

    static partial class CSGManager
    {
        const int kMaxPhysicsVertexCount = 64000;

        internal sealed class BrushInfo
        {
            public int					    brushMeshInstanceID;
            public UInt64                   brushOutlineGeneration;
            public bool                     brushOutlineDirty = true;

            public BrushOutline             brushOutline        = new BrushOutline();


            public void Reset() 
            {
                brushOutlineDirty = true;
                brushOutlineGeneration  = 0;
                brushOutline.Reset();
            }
        }

        public static void GetMeshDescriptions(Int32                      treeNodeID,
                                               MeshQuery[]                meshQueries,
                                               VertexChannelFlags         vertexChannelMask,
                                               out List<GeneratedMeshDescription>   descriptions,
                                               out VertexBufferContents             meshContents)
        {
            descriptions = null;
            meshContents = default;
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return;
            if (meshQueries == null)
                throw new ArgumentNullException("meshTypes");

            if (meshQueries.Length == 0)
            {
                Debug.Log("meshQueries.Length == 0");
                return;
            }

            if (!IsValidNodeID(treeNodeID))
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return;
            }

            var treeNodeIndex = treeNodeID - 1;
            var treeInfo = nodeHierarchies[treeNodeIndex].treeInfo;
            if (treeInfo == null)
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return;
            }

            if (treeInfo.subMeshCounts.IsCreated)
                treeInfo.subMeshCounts.Clear();
            if (treeInfo.subMeshSections.IsCreated)
                treeInfo.subMeshSections.Clear();
            treeInfo.meshDescriptions.Clear();

            var treeFlags = nodeFlags[treeNodeIndex];

            JobHandle updateTreeMeshesJobHandle = default;
            if (treeFlags.IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
            {
                UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMesh");
                try
                {
                    updateTreeMeshesJobHandle = UpdateTreeMeshes(new int[] { treeNodeID });
                } finally { UnityEngine.Profiling.Profiler.EndSample(); }
                treeFlags.UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                nodeFlags[treeNodeIndex] = treeFlags;
            }

            JobHandle combineSubMeshesJobHandle;
            UnityEngine.Profiling.Profiler.BeginSample("CombineSubMeshes");
            try
            {
                combineSubMeshesJobHandle = CombineSubMeshes(treeNodeIndex, treeInfo, meshQueries, updateTreeMeshesJobHandle);
            } finally { UnityEngine.Profiling.Profiler.EndSample(); }


            var subMeshCounts = treeInfo.subMeshCounts;
            var subMeshSections = treeInfo.subMeshSections;

            ref var vertexBufferContents = ref treeInfo.vertexBufferContents;
            vertexBufferContents.EnsureAllocated();

            var allocateVertexBuffersJob = new AllocateVertexBuffersJob
            {
                // ReadOnly
                subMeshSections     = treeInfo.subMeshSections.AsDeferredJobArray(),

                // WriteOnly
                subMeshesArray      = treeInfo.vertexBufferContents.subMeshes,
                indicesArray        = treeInfo.vertexBufferContents.indices,
                brushIndicesArray   = treeInfo.vertexBufferContents.brushIndices,
                positionsArray      = treeInfo.vertexBufferContents.positions,
                tangentsArray       = treeInfo.vertexBufferContents.tangents,
                normalsArray        = treeInfo.vertexBufferContents.normals,
                uv0Array            = treeInfo.vertexBufferContents.uv0
            };
            var allocateVertexBufferJobHandle = allocateVertexBuffersJob.Schedule(combineSubMeshesJobHandle);

            var generateVertexBuffersJob = new FillVertexBuffersJob
            {
                subMeshSections             = subMeshSections.AsDeferredJobArray(),
               
                subMeshCounts               = treeInfo.subMeshCounts.AsDeferredJobArray(),
                subMeshSurfaces             = treeInfo.subMeshSurfaces.AsDeferredJobArray(),

                subMeshesArray              = vertexBufferContents.subMeshes,
                tangentsArray               = vertexBufferContents.tangents,
                normalsArray                = vertexBufferContents.normals,
                uv0Array                    = vertexBufferContents.uv0,
                positionsArray              = vertexBufferContents.positions,
                indicesArray                = vertexBufferContents.indices,
                brushIndicesArray           = vertexBufferContents.brushIndices,
            };
            var fillVertexBuffersJobHandle = generateVertexBuffersJob.Schedule(subMeshSections, 1, allocateVertexBufferJobHandle);
            
            Profiler.BeginSample("Complete");
            fillVertexBuffersJobHandle.Complete();
            Profiler.EndSample();

            if (!treeInfo.subMeshCounts.IsCreated || 
                treeInfo.subMeshCounts.Length == 0 ||
                treeInfo.subMeshCounts[0].vertexCount <= 0 ||
                treeInfo.subMeshCounts[0].indexCount <= 0)
            {
                return; 
            }
            
            for (int i = 0; i < subMeshCounts.Length; i++)
            {
                var subMesh = subMeshCounts[i];

                var description = new GeneratedMeshDescription
                {
                    meshQuery           = subMesh.meshQuery,
                    surfaceParameter    = subMesh.surfaceParameter,
                    meshQueryIndex      = subMesh.meshQueryIndex,
                    subMeshQueryIndex   = subMesh.subMeshQueryIndex,

                    geometryHashValue   = subMesh.geometryHashValue,
                    surfaceHashValue    = subMesh.surfaceHashValue,

                    vertexCount         = subMesh.vertexCount,
                    indexCount          = subMesh.indexCount
                };

                treeInfo.meshDescriptions.Add(description);
            }

            descriptions = treeInfo.meshDescriptions;
            meshContents = treeInfo.vertexBufferContents;
        }

        struct SubMeshCountsComparer : IComparer<SubMeshCounts>
        {
            public int Compare(SubMeshCounts x, SubMeshCounts y)
            {
                if (x.meshQuery.LayerParameterIndex != y.meshQuery.LayerParameterIndex) return ((int)x.meshQuery.LayerParameterIndex) - ((int)y.meshQuery.LayerParameterIndex);
                if (x.meshQuery.LayerQuery != y.meshQuery.LayerQuery) return ((int)x.meshQuery.LayerQuery) - ((int)y.meshQuery.LayerQuery);
                if (x.surfaceParameter != y.surfaceParameter) return ((int)x.surfaceParameter) - ((int)y.surfaceParameter);
                if (x.geometryHashValue != y.geometryHashValue) return ((int)x.geometryHashValue) - ((int)y.geometryHashValue);
                return 0;
            }
        }

        struct SubMeshSurfaceComparer : IComparer<SubMeshSurface>
        {
            public int Compare(SubMeshSurface x, SubMeshSurface y)
            {
                return x.surfaceParameter.CompareTo(y.surfaceParameter);
            }
        }

        internal static JobHandle CombineSubMeshes(int treeNodeIndex,
                                              TreeInfo treeInfo,
                                              MeshQuery[] meshQueriesArray,
                                              JobHandle dependencies)
        {
            var treeBrushNodeIDs = treeInfo.treeBrushes;
            var treeBrushNodeCount = (Int32)(treeBrushNodeIDs.Count);
            if (treeBrushNodeCount <= 0)
            {
                if (treeInfo.subMeshCounts.IsCreated)
                    treeInfo.subMeshCounts.Clear();
                if (treeInfo.subMeshSurfaces.IsCreated)
                    treeInfo.subMeshSurfaces.Clear();
                return dependencies;
            }

            var chiselLookupValues = ChiselTreeLookup.Value[treeNodeIndex];

            Profiler.BeginSample("Allocate");
            if (treeInfo.brushRenderBuffers.IsCreated)
            {
                if (treeInfo.brushRenderBuffers.Capacity < treeBrushNodeCount)
                    treeInfo.brushRenderBuffers.Capacity = treeBrushNodeCount;
            } else
                treeInfo.brushRenderBuffers = new NativeList<BrushData>(treeBrushNodeCount, Allocator.Persistent);

            if (treeInfo.meshQueries.IsCreated)
            {
                treeInfo.meshQueries.Clear();
                if (treeInfo.meshQueries.Capacity < meshQueriesArray.Length)
                    treeInfo.meshQueries.Capacity = meshQueriesArray.Length;
                treeInfo.meshQueries.Clear();
            } else
                treeInfo.meshQueries = new NativeList<MeshQuery>(meshQueriesArray.Length, Allocator.Persistent);
            treeInfo.meshQueries.ResizeUninitialized(meshQueriesArray.Length);

            for (int i = 0; i < meshQueriesArray.Length; i++)
                treeInfo.meshQueries[i] = meshQueriesArray[i];

            if (treeInfo.sections.IsCreated)
            {
                if (treeInfo.sections.Capacity < treeInfo.meshQueries.Length)
                    treeInfo.sections.Capacity = treeInfo.meshQueries.Length;
            } else
                treeInfo.sections = new NativeList<SectionData>(treeInfo.meshQueries.Length, Allocator.Persistent);
            Profiler.EndSample();

            Profiler.BeginSample("Find Surfaces");
            int surfaceCount = 0;
            for (int b = 0, count_b = treeBrushNodeCount; b < count_b; b++)
            {
                var brushNodeID     = treeBrushNodeIDs[b];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                var brushNodeIndex  = brushNodeID - 1;

                if (!chiselLookupValues.brushRenderBufferCache.TryGetValue(brushNodeIndex, out var brushRenderBuffer) ||
                    !brushRenderBuffer.IsCreated)
                    continue;

                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;
                ref var surfaces = ref brushRenderBufferRef.surfaces;

                if (surfaces.Length == 0)
                    continue;

                treeInfo.brushRenderBuffers.AddNoResize(new BrushData{
                    brushNodeID         = brushNodeID,
                    brushRenderBuffer   = brushRenderBuffer
                });

                surfaceCount += surfaces.Length;
            }
            Profiler.EndSample();

            Profiler.BeginSample("Allocate");
            var surfaceCapacity = surfaceCount * treeInfo.meshQueries.Length;
            if (treeInfo.subMeshSurfaces.IsCreated)
            {
                treeInfo.subMeshSurfaces.Clear();
                if (treeInfo.subMeshSurfaces.Capacity < surfaceCapacity)
                    treeInfo.subMeshSurfaces.Capacity = surfaceCapacity;
            } else
                treeInfo.subMeshSurfaces = new NativeList<SubMeshSurface>(surfaceCapacity, Allocator.Persistent);

            var subMeshCapacity = surfaceCount * treeInfo.meshQueries.Length;
            if (treeInfo.subMeshCounts.IsCreated)
            {
                treeInfo.subMeshCounts.Clear();
                if (treeInfo.subMeshCounts.Capacity < subMeshCapacity)
                    treeInfo.subMeshCounts.Capacity = subMeshCapacity;
            } else
                treeInfo.subMeshCounts = new NativeList<SubMeshCounts>(subMeshCapacity, Allocator.Persistent);

            if (treeInfo.subMeshSections.IsCreated)
            {
                treeInfo.subMeshSections.Clear();
                if (treeInfo.subMeshSections.Capacity < subMeshCapacity)
                    treeInfo.subMeshSections.Capacity = subMeshCapacity;
            } else
                treeInfo.subMeshSections = new NativeList<VertexBufferInit>(subMeshCapacity, Allocator.Persistent);

            
            Profiler.EndSample();

            Profiler.BeginSample("Sort");
            var prepareJob = new PrepareJob
            {
                meshQueries         = treeInfo.meshQueries.AsArray(),
                brushRenderBuffers  = treeInfo.brushRenderBuffers.AsArray(),

                sections            = treeInfo.sections,
                subMeshSurfaces     = treeInfo.subMeshSurfaces,
            };
            var prepareJobHandle = prepareJob.Schedule(dependencies);

            var sortJob = new SortSurfacesJob 
            {
                sections        = treeInfo.sections.AsDeferredJobArray(),
                subMeshSurfaces = treeInfo.subMeshSurfaces.AsDeferredJobArray(),
                subMeshCounts   = treeInfo.subMeshCounts,
                subMeshSections = treeInfo.subMeshSections,
            };
            var sortJobHandle = sortJob.Schedule(prepareJobHandle);
            Profiler.EndSample();
            return sortJobHandle;
        }

        [BurstCompile(CompileSynchronously = true)]
        struct PrepareJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<MeshQuery>       meshQueries;
            [NoAlias, ReadOnly] public NativeArray<BrushData>       brushRenderBuffers;
            
            [NoAlias, WriteOnly] public NativeList<SubMeshSurface>  subMeshSurfaces;
            [NoAlias, WriteOnly] public NativeList<SectionData>     sections;

            public void Execute()
            {
                var surfacesLength = 0;
                for (int t = 0; t < meshQueries.Length; t++)
                {
                    var meshQuery       = meshQueries[t];
                    var surfacesOffset  = surfacesLength;
                    for (int b = 0, count_b = brushRenderBuffers.Length; b < count_b; b++)
                    {
                        var brushData                   = brushRenderBuffers[b];
                        var brushNodeID                 = brushData.brushNodeID;
                        var brushRenderBuffer           = brushData.brushRenderBuffer;
                        ref var brushRenderBufferRef    = ref brushRenderBuffer.Value;
                        ref var surfaces                = ref brushRenderBufferRef.surfaces;

                        for (int j = 0, count_j = (int)surfaces.Length; j < count_j; j++)
                        {
                            ref var surface = ref surfaces[j];
                            if (surface.vertices.Length <= 0 || surface.indices.Length <= 0)
                                continue;

                            ref var surfaceLayers = ref surface.surfaceLayers;

                            var core_surface_flags = surfaceLayers.layerUsage;
                            if ((core_surface_flags & meshQuery.LayerQueryMask) != meshQuery.LayerQuery)
                                continue;

                            int surfaceParameter = 0;
                            if (meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1 &&
                                meshQuery.LayerParameterIndex <= LayerParameterIndex.MaxLayerParameterIndex)
                            {
                                // TODO: turn this into array lookup
                                switch (meshQuery.LayerParameterIndex)
                                {
                                    case LayerParameterIndex.LayerParameter1: surfaceParameter = surfaceLayers.layerParameter1; break;
                                    case LayerParameterIndex.LayerParameter2: surfaceParameter = surfaceLayers.layerParameter2; break;
                                    case LayerParameterIndex.LayerParameter3: surfaceParameter = surfaceLayers.layerParameter3; break;
                                }
                            }

                            subMeshSurfaces.AddNoResize(new SubMeshSurface
                            {
                                surfaceIndex        = j,
                                brushNodeID         = brushNodeID,
                                surfaceParameter    = surfaceParameter,
                                brushRenderBuffer   = brushRenderBuffer
                            });
                            surfacesLength++;
                        }
                    }
                    var surfacesCount = surfacesLength - surfacesOffset;
                    if (surfacesCount == 0)
                        continue;
                    sections.AddNoResize(new SectionData
                    { 
                        surfacesOffset  = surfacesOffset,
                        surfacesCount   = surfacesCount,
                        meshQuery       = meshQuery
                    });
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct SortSurfacesJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<SectionData>         sections;

            // Read/Write (Sort)
            [NoAlias] public NativeArray<SubMeshSurface>                subMeshSurfaces;
            [NoAlias] public NativeList<SubMeshCounts>                  subMeshCounts;

            [NoAlias, WriteOnly] public NativeList<VertexBufferInit>    subMeshSections;
            

            public void Execute()
            {
                var comparer = new SubMeshSurfaceComparer();
                for (int t = 0, meshIndex = 0, surfacesOffset = 0; t < sections.Length; t++)
                {
                    var section = sections[t];
                    if (section.surfacesCount == 0)
                        continue;
                    var slice = subMeshSurfaces.Slice(section.surfacesOffset, section.surfacesCount);
                    slice.Sort(comparer);


                    var meshQuery       = section.meshQuery;
                    var querySurfaces   = subMeshSurfaces.Slice(section.surfacesOffset, section.surfacesCount);
                    var isPhysics       = meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial;

                    var currentSubMesh = new SubMeshCounts
                    {
                        meshQueryIndex      = meshIndex,
                        subMeshQueryIndex   = 0,
                        meshQuery           = meshQuery,
                        surfaceParameter    = querySurfaces[0].surfaceParameter,
                        surfacesOffset      = surfacesOffset
                    };
                    for (int b = 0; b < querySurfaces.Length; b++)
                    {
                        var subMeshSurface              = querySurfaces[b];
                        var surfaceParameter            = subMeshSurface.surfaceParameter;
                        ref var brushRenderBufferRef    = ref subMeshSurface.brushRenderBuffer.Value;
                        ref var brushSurfaceBuffer      = ref brushRenderBufferRef.surfaces[subMeshSurface.surfaceIndex];
                        var surfaceVertexCount          = brushSurfaceBuffer.vertices.Length;
                        var surfaceIndexCount           = brushSurfaceBuffer.indices.Length;

                        if (currentSubMesh.surfaceParameter != surfaceParameter || 
                            (isPhysics && currentSubMesh.vertexCount >= kMaxPhysicsVertexCount))
                        {
                            // Store the previous subMeshCount
                            if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                                subMeshCounts.AddNoResize(currentSubMesh);
                        
                            // Create the new SubMeshCount
                            currentSubMesh.surfaceParameter   = surfaceParameter;
                            currentSubMesh.subMeshQueryIndex++;
                            currentSubMesh.surfaceHashValue         = 0;
                            currentSubMesh.geometryHashValue        = 0;
                            currentSubMesh.indexCount          = 0;
                            currentSubMesh.vertexCount         = 0;
                            currentSubMesh.surfacesOffset      += currentSubMesh.surfacesCount;
                            currentSubMesh.surfacesCount       = 0;
                        } 

                        currentSubMesh.indexCount   += surfaceIndexCount;
                        currentSubMesh.vertexCount  += surfaceVertexCount;
                        currentSubMesh.surfaceHashValue  = math.hash(new uint2(currentSubMesh.surfaceHashValue, brushSurfaceBuffer.surfaceHash));
                        currentSubMesh.geometryHashValue = math.hash(new uint2(currentSubMesh.geometryHashValue, brushSurfaceBuffer.geometryHash));
                        currentSubMesh.surfacesCount++;
                    }
                    // Store the last subMeshCount
                    if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                        subMeshCounts.AddNoResize(currentSubMesh);
                    surfacesOffset = currentSubMesh.surfacesOffset + currentSubMesh.surfacesCount;
                    meshIndex++;
                }

                // Sort all meshDescriptions so that meshes that can be merged are next to each other
                subMeshCounts.Sort(new SubMeshCountsComparer());


                int descriptionIndex = 0;
                //var contentsIndex = 0;
                if (subMeshCounts[0].meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                {
                    var prevQuery = subMeshCounts[0].meshQuery;
                    var startIndex = 0;
                    for (; descriptionIndex < subMeshCounts.Length; descriptionIndex++)
                    {
                        var subMeshCount = subMeshCounts[descriptionIndex];
                        // Exit when layerParameterIndex is no longer LayerParameter1
                        if (subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.RenderMaterial)
                            break;

                        var currQuery = subMeshCount.meshQuery;
                        if (prevQuery == currQuery)
                        {
                            continue;
                        }

                        int totalVertexCount = 0;
                        int totalIndexCount = 0;
                        for (int i=startIndex; i < descriptionIndex; i++)
                        {
                            totalVertexCount += subMeshCounts[i].vertexCount;
                            totalIndexCount += subMeshCounts[i].indexCount;
                        }

                        // Group by all subMeshCounts with same query
                        subMeshSections.AddNoResize(new VertexBufferInit
                        {
                            layerParameterIndex = subMeshCounts[startIndex].meshQuery.LayerParameterIndex,
                            startIndex          = startIndex, 
                            endIndex            = descriptionIndex,
                            totalVertexCount    = totalVertexCount,
                            totalIndexCount     = totalIndexCount,
                        });

                        startIndex = descriptionIndex;
                        prevQuery = currQuery;
                    }

                    {
                        int totalVertexCount = 0;
                        int totalIndexCount = 0;
                        for (int i = startIndex; i < descriptionIndex; i++)
                        {
                            totalVertexCount += subMeshCounts[i].vertexCount;
                            totalIndexCount += subMeshCounts[i].indexCount;
                        }

                        subMeshSections.AddNoResize(new VertexBufferInit
                        {
                            layerParameterIndex = subMeshCounts[startIndex].meshQuery.LayerParameterIndex,
                            startIndex          = startIndex,
                            endIndex            = descriptionIndex,
                            totalVertexCount    = totalVertexCount,
                            totalIndexCount     = totalIndexCount
                        });
                    }
                }
                

                if (descriptionIndex < subMeshCounts.Length &&
                    subMeshCounts[descriptionIndex].meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                {
                    Debug.Assert(subMeshCounts[subMeshCounts.Length - 1].meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial);

                    // Loop through all subMeshCounts with LayerParameter2, and create collider meshes from them
                    for (int i = 0; descriptionIndex < subMeshCounts.Length; descriptionIndex++, i++)
                    {
                        var subMeshCount = subMeshCounts[descriptionIndex];

                        // Exit when layerParameterIndex is no longer LayerParameter2
                        if (subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.PhysicsMaterial)
                            break;

                        subMeshSections.AddNoResize(new VertexBufferInit
                        {
                            layerParameterIndex = subMeshCount.meshQuery.LayerParameterIndex,
                            startIndex          = descriptionIndex,
                            endIndex            = descriptionIndex,
                            totalVertexCount    = subMeshCount.vertexCount,
                            totalIndexCount     = subMeshCount.indexCount
                        });
                    }
                }
            }
        }

        private static void UpdateDelayedHierarchyModifications()
        {
            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;

                {
                    var flags = nodeFlags[branchNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.OperationNeedsUpdate);
                    nodeFlags[branchNodeIndex] = flags;
                }
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedPreviousSiblingsUpdate))
                    continue;

                // TODO: implement
                //operation.RebuildPreviousSiblings();
                {
                    var flags = nodeFlags[branchNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.NeedPreviousSiblingsUpdate);
                    nodeFlags[branchNodeIndex] = flags;
                }
            }

            // TODO: implement
            /*
            var foundOperations = new List<int>();
            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                foundOperations.Add(branchNodeIndex);
            }

            for (int i = 0; i < foundOperations.Count; i++)
            {
                //UpdateChildOperationTouching(foundOperations[i]);
            }
            */

            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                // TODO: implement
                //UpdateChildBrushTouching(branchNodeID);
                {
                    var flags = nodeFlags[branchNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                    nodeFlags[branchNodeIndex] = flags;
                }
            }
        }
    }
}