using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
        const int kMaxVertexCount = HashedVertices.kMaxVertexCount;


        internal sealed class SubMeshCounts
        {
            public MeshQuery meshQuery;
            public int		surfaceIdentifier;

            public int		meshIndex;
            public int		subMeshIndex;
            
            public uint	    surfaceHash;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
            public uint	    geometryHash;  // used to detect changes in vertex positions / indices

            public int		indexCount;
            public int		vertexCount;

            public readonly List<SubMeshSurface> surfaces = new List<SubMeshSurface>();
        };

        private struct MeshID
        {
            public MeshQuery	meshQuery;
            public Int32		surfaceParameter;

            public static bool operator ==(MeshID left, MeshID right) { return (left.meshQuery == right.meshQuery && left.surfaceParameter == right.surfaceParameter); }
            public static bool operator !=(MeshID left, MeshID right) { return (left.meshQuery != right.meshQuery || left.surfaceParameter != right.surfaceParameter); }
            public override bool Equals(object obj) { if (ReferenceEquals(this, obj)) return true; if (!(obj is MeshID)) return false; var other = (MeshID)obj; return other == this; }
            public override int GetHashCode() { return surfaceParameter.GetHashCode() ^ meshQuery.GetHashCode(); }
        };

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




        internal static GeneratedMeshContents GetGeneratedMesh(int treeNodeID, GeneratedMeshDescription meshDescription)
        {
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return null;
            if (meshDescription.vertexCount <= 3 ||
                meshDescription.indexCount <= 3)
            {
                Debug.LogWarning(string.Format("{0} called with a {1} that isn't valid", typeof(CSGTree).Name, typeof(GeneratedMeshDescription).Name));
                return null;
            }

            var meshIndex		= meshDescription.meshQueryIndex;
            var subMeshIndex	= meshDescription.subMeshQueryIndex;
            if (meshIndex    < 0) { Debug.LogError("GetGeneratedMesh: MeshIndex cannot be negative"); return null; }
            if (subMeshIndex < 0) { Debug.LogError("GetGeneratedMesh: SubMeshIndex cannot be negative"); return null; }

            if (nodeHierarchies[treeNodeID - 1].treeInfo == null) { Debug.LogWarning("Tree has not been initialized properly"); return null;			}

            TreeInfo tree = nodeHierarchies[treeNodeID - 1].treeInfo;
            if (tree == null) { Debug.LogError("GetGeneratedMesh: Invalid node index used"); return null; }
            if (tree.subMeshCounts == null) { Debug.LogWarning("Tree has not been initialized properly"); return null; }
            



            int subMeshCountSize = (int)tree.subMeshCounts.Count;
            if (subMeshIndex >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: SubMeshIndex is higher than the number of generated meshes"); return null; }
            if (meshIndex    >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: MeshIndex is higher than the number of generated meshes"); return null; }

            int foundIndex = -1;
            for (int i = 0; i < subMeshCountSize; i++)
            {
                if (meshIndex    == tree.subMeshCounts[i].meshIndex &&
                    subMeshIndex == tree.subMeshCounts[i].subMeshIndex)
                {
                    foundIndex = i;
                    break;
                }
            }
            if (foundIndex < 0 || foundIndex >= subMeshCountSize) { Debug.LogError("GetGeneratedMesh: Could not find mesh associated with MeshIndex/SubMeshIndex pair"); return null; }
            
            var subMeshCount = tree.subMeshCounts[foundIndex];
            if (subMeshCount.indexCount > meshDescription.indexCount) { Debug.LogError("GetGeneratedMesh: The destination indices array (" + meshDescription.indexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.indexCount + ")"); return null; }
            if (subMeshCount.vertexCount > meshDescription.vertexCount) { Debug.LogError("GetGeneratedMesh: The destination vertices array (" + meshDescription.vertexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.vertexCount + ")"); return null; }
            if (subMeshCount.indexCount == 0 || subMeshCount.vertexCount == 0) { Debug.LogWarning("GetGeneratedMesh: Mesh is empty"); return null; }


            var generatedMesh			= new GeneratedMeshContents();
            var usedVertexChannels		= meshDescription.meshQuery.UsedVertexChannels;
            var vertexCount				= meshDescription.vertexCount;
            var indexCount				= meshDescription.indexCount;
            generatedMesh.description	= meshDescription;

            // create our arrays on the managed side with the correct size

            bool useTangents   = ((usedVertexChannels & VertexChannelFlags.Tangent) == VertexChannelFlags.Tangent);
            bool useNormals    = ((usedVertexChannels & VertexChannelFlags.Normal ) == VertexChannelFlags.Normal);
            bool useUV0        = ((usedVertexChannels & VertexChannelFlags.UV0    ) == VertexChannelFlags.UV0);

            generatedMesh.tangents		= useTangents ? new NativeArray<float4>(vertexCount, Allocator.Persistent) : default;
            generatedMesh.normals		= useNormals  ? new NativeArray<float3>(vertexCount, Allocator.Persistent) : default;
            generatedMesh.uv0			= useUV0      ? new NativeArray<float2>(vertexCount, Allocator.Persistent) : default;
            generatedMesh.positions		= new NativeArray<float3>(vertexCount, Allocator.Persistent);
            generatedMesh.indices		= new NativeArray<int>   (indexCount, Allocator.Persistent);

            generatedMesh.bounds = new Bounds();
            
            var submeshVertexCount	= subMeshCount.vertexCount;
            var submeshIndexCount	= subMeshCount.indexCount;

            if (subMeshCount.surfaces == null ||
                submeshVertexCount != generatedMesh.positions.Length ||
                submeshIndexCount  != generatedMesh.indices.Length ||
                generatedMesh.indices == null ||
                generatedMesh.positions == null)
                return null;


            var submeshSurfaces          = new NativeArray<SubMeshSurface>(subMeshCount.surfaces.Count, Allocator.TempJob);
            for (int n = 0; n < subMeshCount.surfaces.Count; n++)
                submeshSurfaces[n] = subMeshCount.surfaces[n];

            var generateVertexBuffersJob = new GenerateVertexBuffersJob
            {   
                meshQuery               = subMeshCount.meshQuery,
                surfaceIdentifier       = subMeshCount.surfaceIdentifier,

                submeshIndexCount       = subMeshCount.indexCount, 
                submeshVertexCount      = subMeshCount.vertexCount,

                submeshSurfaces         = submeshSurfaces,

                generatedMeshIndices    = generatedMesh.indices,
                generatedMeshPositions  = generatedMesh.positions,
                generatedMeshTangents   = generatedMesh.tangents,
                generatedMeshNormals    = generatedMesh.normals,
                generatedMeshUV0        = generatedMesh.uv0
            };
            generateVertexBuffersJob.Run();

            generateVertexBuffersJob.submeshSurfaces.Dispose();
            generateVertexBuffersJob.submeshSurfaces = default;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0, count = subMeshCount.indexCount; i < count; i++)
            {
                var position = generatedMesh.positions[generatedMesh.indices[i]];
                min.x = Mathf.Min(min.x, position.x);
                min.y = Mathf.Min(min.y, position.y);
                min.z = Mathf.Min(min.z, position.z);

                max.x = Mathf.Max(max.x, position.x);
                max.y = Mathf.Max(max.y, position.y);
                max.z = Mathf.Max(max.z, position.z);
            }

            var boundsCenter = (max + min) * 0.5f;
            var boundsSize	 = (max - min);

            if (float.IsInfinity(boundsSize.x) || float.IsInfinity(boundsSize.y) || float.IsInfinity(boundsSize.z) ||
                float.IsNaN(boundsSize.x) || float.IsNaN(boundsSize.y) || float.IsNaN(boundsSize.z))
                return null;

            generatedMesh.bounds = new Bounds(boundsCenter, boundsSize);
            return generatedMesh;
        }

        internal static GeneratedMeshDescription[] GetMeshDescriptions(Int32                treeNodeID,
                                                                       MeshQuery[]          meshQueries,
                                                                       VertexChannelFlags   vertexChannelMask)
        {
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return null;
            if (meshQueries == null)
                throw new ArgumentNullException("meshTypes");

            if (meshQueries.Length == 0)
            {
                Debug.Log("meshQueries.Length == 0");
                return null;
            }

            if (!IsValidNodeID(treeNodeID))
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return null;
            }

            var treeNodeIndex = treeNodeID - 1;
            var treeInfo = nodeHierarchies[treeNodeIndex].treeInfo;
            if (treeInfo == null)
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return null;
            }

            treeInfo.subMeshCounts.Clear();
            treeInfo.meshDescriptions.Clear();

            if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
            {
                UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMesh");
                try
                {
                    var handle = UpdateTreeMeshes(new int[] { treeNodeID });
                    handle.Complete();
                } finally { UnityEngine.Profiling.Profiler.EndSample(); }
            }

            UnityEngine.Profiling.Profiler.BeginSample("CombineSubMeshes");
            try
            {
                CombineSubMeshes(treeInfo, meshQueries, vertexChannelMask);
            } finally { UnityEngine.Profiling.Profiler.EndSample(); }


            {
                var flags = nodeFlags[treeNodeIndex];
                flags.UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                nodeFlags[treeNodeIndex] = flags;
            }

            if (treeInfo.subMeshCounts.Count <= 0)
                return null;

            for (int i = (int)treeInfo.subMeshCounts.Count - 1; i >= 0; i--)
            {
                var subMesh = treeInfo.subMeshCounts[i];

                // Make sure the meshDescription actually holds a mesh
                if (subMesh.vertexCount == 0 ||
                    subMesh.indexCount == 0)
                    continue;

                // Make sure the mesh is valid
                if (subMesh.vertexCount >= kMaxVertexCount)
                {
                    Debug.LogError("Mesh has too many vertices (" + subMesh.vertexCount + " > " + kMaxVertexCount + ")");
                    continue;
                }

                var description = new GeneratedMeshDescription
                {
                    meshQuery           = subMesh.meshQuery,
                    surfaceParameter    = subMesh.surfaceIdentifier,
                    meshQueryIndex      = subMesh.meshIndex,
                    subMeshQueryIndex   = subMesh.subMeshIndex,

                    geometryHashValue   = subMesh.geometryHash,
                    surfaceHashValue    = subMesh.surfaceHash,

                    vertexCount         = subMesh.vertexCount,
                    indexCount          = subMesh.indexCount
                };

                treeInfo.meshDescriptions.Add(description);
            }

            if (treeInfo.meshDescriptions == null ||
                treeInfo.meshDescriptions.Count == 0 ||
                treeInfo.meshDescriptions[0].vertexCount <= 0 ||
                treeInfo.meshDescriptions[0].indexCount <= 0)
            {
                return null;
            }

            return treeInfo.meshDescriptions.ToArray();
        }

        static Dictionary<MeshID, int> uniqueMeshDescriptions = new Dictionary<MeshID, int>();
        internal static void CombineSubMeshes(TreeInfo treeInfo,
                                              MeshQuery[] meshQueries,
                                              VertexChannelFlags vertexChannelMask)
        {
            var subMeshCounts = treeInfo.subMeshCounts;
            subMeshCounts.Clear();

            var treeBrushNodeIDs = treeInfo.treeBrushes;
            var treeBrushNodeCount = (Int32)(treeBrushNodeIDs.Count);
            if (treeBrushNodeCount <= 0)
                return;

            uniqueMeshDescriptions.Clear();
            for (int b = 0, count_b = treeBrushNodeCount; b < count_b; b++)
            {
                var brushNodeID     = treeBrushNodeIDs[b];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                var brushNodeIndex  = brushNodeID - 1;
                var nodeType = nodeFlags[brushNodeIndex].nodeType;
                if (nodeType != CSGNodeType.Brush)
                    continue;

                var brushInfo = nodeHierarchies[brushNodeIndex].brushInfo;
                if (brushInfo == null)
                    continue;
            
                var treeNodeID          = nodeHierarchies[brushNodeIndex].treeNodeID;
                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeID - 1];

                if (!chiselLookupValues.brushRenderBuffers.TryGetValue(brushNodeIndex, out var brushRenderBuffer) ||
                    !brushRenderBuffer.IsCreated)
                    continue;

                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;
                ref var surfaces = ref brushRenderBufferRef.surfaces;

                if (surfaces.Length == 0)
                    continue;

                for (int j = 0, count_j = (int)surfaces.Length; j < count_j; j++)
                {
                    ref var brushSurfaceBuffer      = ref surfaces[j];
                    var surfaceVertexCount          = brushSurfaceBuffer.vertices.Length;
                    var surfaceIndexCount           = brushSurfaceBuffer.indices.Length;

                    if (surfaceVertexCount <= 0 || surfaceIndexCount <= 0)
                        continue;


                    ref var surfaceLayers = ref brushSurfaceBuffer.surfaceLayers;

                    for (int t=0;t< meshQueries.Length;t++)
                    { 
                        var meshQuery = meshQueries[t];

                        var core_surface_flags = surfaceLayers.layerUsage;
                        if ((core_surface_flags & meshQuery.LayerQueryMask) != meshQuery.LayerQuery)
                        {
                            continue;
                        }

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

                        var meshID = new MeshID() { meshQuery = meshQuery, surfaceParameter = surfaceParameter };
                     
                        if (!uniqueMeshDescriptions.TryGetValue(meshID, out int generatedMeshIndex))
                            generatedMeshIndex = -1;

                        if (generatedMeshIndex == -1 ||
                            (subMeshCounts[generatedMeshIndex].vertexCount + surfaceVertexCount) >= kMaxVertexCount)
                        {
                            int meshIndex, subMeshIndex;
                            if (generatedMeshIndex != -1)
                            {
                                var prevMeshCountIndex = generatedMeshIndex;
                                generatedMeshIndex = (int)subMeshCounts.Count;
                                subMeshIndex = subMeshCounts[prevMeshCountIndex].subMeshIndex + 1;
                                meshIndex = subMeshCounts[prevMeshCountIndex].meshIndex;
                            }
                            else
                            {
                                generatedMeshIndex = (int)subMeshCounts.Count;
                                meshIndex = generatedMeshIndex;
                                subMeshIndex = 0;
                            }

                            uniqueMeshDescriptions[meshID] = generatedMeshIndex;
                            var newSubMesh = new SubMeshCounts
                            {
                                meshIndex           = meshIndex,
                                subMeshIndex        = subMeshIndex,
                                meshQuery           = meshID.meshQuery,
                                surfaceIdentifier   = surfaceParameter,
                                indexCount          = surfaceIndexCount,
                                vertexCount         = surfaceVertexCount,
                                surfaceHash         = brushSurfaceBuffer.surfaceHash,
                                geometryHash        = brushSurfaceBuffer.geometryHash
                            };
                            newSubMesh.surfaces.Add(new SubMeshSurface
                            {
                                surfaceIndex = j,
                                brushRenderBuffer = brushRenderBuffer
                            });
                            subMeshCounts.Add(newSubMesh);
                            continue;
                        }

                        var currentSubMesh = subMeshCounts[generatedMeshIndex];
                        currentSubMesh.indexCount   += surfaceIndexCount;
                        currentSubMesh.vertexCount  += surfaceVertexCount;
                        currentSubMesh.surfaceHash  = math.hash(new uint2(currentSubMesh.surfaceHash, brushSurfaceBuffer.surfaceHash));
                        currentSubMesh.geometryHash = math.hash(new uint2(currentSubMesh.geometryHash, brushSurfaceBuffer.geometryHash));
                        currentSubMesh.surfaces.Add(new SubMeshSurface
                        {
                            surfaceIndex = j,
                            brushRenderBuffer = brushRenderBuffer
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