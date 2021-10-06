using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    struct SubMeshCounts
    {
        public MeshQuery    meshQuery;
        public int		    surfaceParameter;

        public int		    meshQueryIndex;
        public int		    subMeshQueryIndex;
            
        public uint	        geometryHashValue;  // used to detect changes in vertex positions  
        public uint	        surfaceHashValue;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
            
        public int		    vertexCount;
        public int		    indexCount;
            
        public int          surfacesOffset;
        public int          surfacesCount;
    };
    
    struct BrushData
    {
        public IndexOrder                                   brushIndexOrder; //<- TODO: if we use NodeOrder maybe this could be explicit based on the order in array?
        public int                                          brushSurfaceOffset;
        public int                                          brushSurfaceCount;
        public ChiselBlobAssetReference<ChiselBrushRenderBuffer>  brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AllocateSubMeshesJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int                  meshQueryLength;
        [NoAlias, ReadOnly] public NativeReference<int> surfaceCountRef;

        // Write
        [NoAlias] public NativeList<SubMeshCounts>      subMeshCounts;
        [NoAlias] public NativeList<SubMeshSection>     subMeshSections;

        public void Execute()
        {
            var subMeshCapacity = surfaceCountRef.Value * meshQueryLength;
            if (subMeshCounts.Capacity < subMeshCapacity)
                subMeshCounts.Capacity = subMeshCapacity;
            
            if (subMeshSections.Capacity < subMeshCapacity)
                subMeshSections.Capacity = subMeshCapacity;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FindBrushRenderBuffersJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int meshQueryLength;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBufferCache;

        // Write
        [NoAlias, WriteOnly] public NativeList<BrushData>.ParallelWriter brushRenderData;

        public void Execute()
        {
            for (int b = 0, count_b = allTreeBrushIndexOrders.Length; b < count_b; b++)
            {
                var brushIndexOrder     = allTreeBrushIndexOrders[b];
                var brushNodeOrder      = allTreeBrushIndexOrders[b].nodeOrder;
                var brushRenderBuffer   = brushRenderBufferCache[brushNodeOrder];
                if (!brushRenderBuffer.IsCreated)
                    continue;

                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;

                var brushSurfaceCount = brushRenderBufferRef.surfaceCount;
                if (brushSurfaceCount == 0)
                    continue;

                var brushSurfaceOffset = brushRenderBufferRef.surfaceOffset;
                brushRenderData.AddNoResize(new BrushData{
                    brushIndexOrder     = brushIndexOrder,
                    brushSurfaceOffset  = brushSurfaceOffset,
                    brushSurfaceCount   = brushSurfaceCount,
                    brushRenderBuffer   = brushRenderBuffer
                });
            }
        }
    }

    struct SubMeshSurface
    {
        public CompactNodeID    brushNodeID;
        public int              surfaceIndex;
        public int              surfaceParameter;

        public int              vertexCount;
        public int              indexCount;

        public uint             surfaceHash;
        public uint             geometryHash;
        public ChiselBlobAssetReference<ChiselBrushRenderBuffer> brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareSubSectionsJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>   meshQueries;
        [NoAlias, ReadOnly] public NativeArray<BrushData>   brushRenderData;

        // Read, Write
        public Allocator allocator;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

        struct SubMeshSurfaceComparer : System.Collections.Generic.IComparer<SubMeshSurface>
        {
            public int Compare(SubMeshSurface x, SubMeshSurface y)
            {
                return x.surfaceParameter.CompareTo(y.surfaceParameter);
            }
        }
        static readonly SubMeshSurfaceComparer subMeshSurfaceComparer = new SubMeshSurfaceComparer();

        public void Execute(int t)
        {
            int requiredSurfaceCount = 0;
            for (int b = 0, count_b = brushRenderData.Length; b < count_b; b++)
                requiredSurfaceCount += brushRenderData[b].brushSurfaceCount;

            // THIS IS THE SLOWDOWN
            // TODO: store surface separately from brushes, *somehow* make lifetime work
            //              => multiple arrays, one for each meshQuery!
            //              => sorted by surface.layerParameters[meshQuery.layerParameterIndex]!
            //              => this whole job could be removed
            // TODO: store surface info and its vertices/indices separately, both sequentially in arrays
            // TODO: store surface vertices/indices sequentially in a big array, *somehow* make ordering work

            if (subMeshSurfaces[t].IsCreated)
            {
                // should not happen
                subMeshSurfaces[t].Dispose();
                subMeshSurfaces[t] = default;
            }
            var subMeshSurfaceList = new UnsafeList<SubMeshSurface>(requiredSurfaceCount, allocator);

            for (int b = 0, count_b = brushRenderData.Length; b < count_b; b++)
            {
                var brushData           = brushRenderData[b];
                var brushRenderBuffer   = brushData.brushRenderBuffer;
                ref var querySurfaces   = ref brushRenderBuffer.Value.querySurfaces[t]; // <-- 1. somehow this needs to 
                                                                                        //     be in outer loop
                ref var brushNodeID     = ref querySurfaces.brushNodeID;
                ref var surfaces        = ref querySurfaces.surfaces;

                for (int s = 0; s < surfaces.Length; s++) 
                {
                    subMeshSurfaceList.AddNoResize(new SubMeshSurface
                    {
                        brushNodeID         = brushNodeID,
                        surfaceIndex        = surfaces[s].surfaceIndex,
                        surfaceParameter    = surfaces[s].surfaceParameter, // <-- 2. store array per surfaceParameter => no sort
                        vertexCount         = surfaces[s].vertexCount,
                        indexCount          = surfaces[s].indexCount,
                        surfaceHash         = surfaces[s].surfaceHash,
                        geometryHash        = surfaces[s].geometryHash,
                        brushRenderBuffer   = brushRenderBuffer, // <-- 3. Get rid of this somehow => memcpy
                    });
                }
                // ^ do those 3 points (mentioned in comments)
            }
            
            subMeshSurfaceList.Sort(subMeshSurfaceComparer);
            subMeshSurfaces[t] = subMeshSurfaceList;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct SortSurfacesParallelJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>           meshQueries;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>> subMeshSurfaces;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshCounts> subMeshCounts;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<SubMeshCounts> sectionSubMeshCounts;

        public void Execute()
        {
            // TODO: figure out why this order matters
            for (int t = 0; t < subMeshSurfaces.Length; t++)
            {
                Execute(t);
            }
        }

        public void Execute(int t)
        {
            var subMeshSurfaceList  = subMeshSurfaces[t];
            var surfaceCount        = subMeshSurfaceList.Length;
            if (surfaceCount == 0)
                return;

            var meshQuery       = meshQueries[t];

            if (sectionSubMeshCounts.IsCreated)
            {
                sectionSubMeshCounts.Clear();
                if (sectionSubMeshCounts.Capacity < surfaceCount)
                    sectionSubMeshCounts.Capacity = surfaceCount;
            } else
                sectionSubMeshCounts = new NativeList<SubMeshCounts>(surfaceCount, Allocator.Temp);

            var currentSubMesh  = new SubMeshCounts
            {
                meshQueryIndex      = t,
                subMeshQueryIndex   = 0,
                meshQuery           = meshQuery,
                surfacesOffset      = 0,
                surfaceParameter    = subMeshSurfaceList[0].surfaceParameter
            };
            for (int b = 0; b < surfaceCount; b++)
            {
                var subMeshSurface              = subMeshSurfaceList[b];
                var surfaceParameter            = subMeshSurface.surfaceParameter;
                var surfaceVertexCount          = subMeshSurface.vertexCount;
                var surfaceIndexCount           = subMeshSurface.indexCount;

                if (currentSubMesh.surfaceParameter != surfaceParameter)
                {
                    // Store the previous subMeshCount
                    if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                        sectionSubMeshCounts.AddNoResize(currentSubMesh);
                        
                    // Create the new SubMeshCount
                    currentSubMesh.surfaceParameter     = surfaceParameter;
                    currentSubMesh.subMeshQueryIndex++;
                    currentSubMesh.surfaceHashValue     = 0;
                    currentSubMesh.geometryHashValue    = 0;
                    currentSubMesh.indexCount           = 0;
                    currentSubMesh.vertexCount          = 0;
                    currentSubMesh.surfacesOffset       += currentSubMesh.surfacesCount;
                    currentSubMesh.surfacesCount        = 0;
                } 

                currentSubMesh.indexCount   += surfaceIndexCount;
                currentSubMesh.vertexCount  += surfaceVertexCount;
                currentSubMesh.surfaceHashValue  = math.hash(new uint2(currentSubMesh.surfaceHashValue, subMeshSurface.surfaceHash));
                currentSubMesh.geometryHashValue = math.hash(new uint2(currentSubMesh.geometryHashValue, subMeshSurface.geometryHash));
                currentSubMesh.surfacesCount++;
            }

            // Store the last subMeshCount
            if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                sectionSubMeshCounts.AddNoResize(currentSubMesh);

            subMeshCounts.AddRangeNoResize(sectionSubMeshCounts);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct GatherSurfacesJob : IJob
    {
        // Read / Write
        [NoAlias] public NativeList<SubMeshCounts>              subMeshCounts;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshSection>  subMeshSections;
            
        public void Execute()
        {
            if (subMeshCounts.Length == 0)
                return;

            int descriptionIndex = 0;
            //var contentsIndex = 0;
            if (subMeshCounts[0].meshQuery.LayerParameterIndex == LayerParameterIndex.None ||
                subMeshCounts[0].meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
            {
                var prevQuery = subMeshCounts[0].meshQuery;
                var startIndex = 0;
                for (; descriptionIndex < subMeshCounts.Length; descriptionIndex++)
                {
                    var subMeshCount = subMeshCounts[descriptionIndex];
                    // Exit when layerParameterIndex is no longer LayerParameter1/None
                    if (subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.None &&
                        subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.RenderMaterial)
                        break;

                    var currQuery = subMeshCount.meshQuery;
                    if (prevQuery == currQuery)
                    {
                        continue;
                    }

                    int totalVertexCount = 0;
                    int totalIndexCount = 0;
                    for (int i = startIndex; i < descriptionIndex; i++)
                    {
                        totalVertexCount += subMeshCounts[i].vertexCount;
                        totalIndexCount += subMeshCounts[i].indexCount;
                    }

                    // Group by all subMeshCounts with same query
                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCounts[startIndex].meshQuery,
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
                        totalIndexCount  += subMeshCounts[i].indexCount;
                    }

                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCounts[startIndex].meshQuery,
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

                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCount.meshQuery,
                        startIndex          = descriptionIndex,
                        endIndex            = descriptionIndex,
                        totalVertexCount    = subMeshCount.vertexCount,
                        totalIndexCount     = subMeshCount.indexCount
                    });
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AllocateVertexBuffersJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>                      subMeshSections;

        // Read / Write
        public Allocator allocator;
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<UnsafeList<CompactNodeID>> triangleBrushIndices;

        public void Execute()
        {
            if (subMeshSections.Length == 0)
                return;

            triangleBrushIndices.Resize(subMeshSections.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < subMeshSections.Length; i++)
            {
                var section             = subMeshSections[i];
                if (section.meshQuery.LayerParameterIndex != LayerParameterIndex.None &&
                    section.meshQuery.LayerParameterIndex != LayerParameterIndex.RenderMaterial)
                    continue;

                var totalIndexCount = section.totalIndexCount;
                var newBrushIndices = new UnsafeList<CompactNodeID>(totalIndexCount / 3, allocator);
                newBrushIndices.Resize(totalIndexCount / 3, NativeArrayOptions.ClearMemory);
                triangleBrushIndices[i] = newBrushIndices;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct GenerateMeshDescriptionJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts> subMeshCounts;

        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<GeneratedMeshDescription> meshDescriptions;

        public void Execute()
        {
            if (meshDescriptions.Capacity < subMeshCounts.Length)
                meshDescriptions.Capacity = subMeshCounts.Length;

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

                meshDescriptions.Add(description);
            }
        }
    }    
    


    public struct ChiselMeshUpdate
    {
        public int contentsIndex;
        public int colliderIndex;
        public int meshIndex;
        public int objectIndex;
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public struct AssignMeshesJob : IJob
    {
        public const int kDebugHelperCount = 6;
        public struct DebugRenderFlags { public LayerUsageFlags Item1; public LayerUsageFlags Item2; };
        public static readonly DebugRenderFlags[] kGeneratedDebugRendererFlags = new DebugRenderFlags[kDebugHelperCount]
        {
            new DebugRenderFlags{ Item1 = LayerUsageFlags.None                  , Item2 = LayerUsageFlags.Renderable },              // is explicitly set to "not visible"
            new DebugRenderFlags{ Item1 = LayerUsageFlags.RenderCastShadows     , Item2 = LayerUsageFlags.RenderCastShadows },       // casts Shadows and is renderered
            new DebugRenderFlags{ Item1 = LayerUsageFlags.CastShadows           , Item2 = LayerUsageFlags.RenderCastShadows },       // casts Shadows and is NOT renderered (shadowOnly)
            new DebugRenderFlags{ Item1 = LayerUsageFlags.RenderReceiveShadows  , Item2 = LayerUsageFlags.RenderReceiveShadows },    // any surface that receives shadows (must be rendered)
            new DebugRenderFlags{ Item1 = LayerUsageFlags.Collidable            , Item2 = LayerUsageFlags.Collidable },              // collider surfaces
            new DebugRenderFlags{ Item1 = LayerUsageFlags.Culled                , Item2 = LayerUsageFlags.Culled }                   // all surfaces removed by the CSG algorithm
        };

        // Read
        [NoAlias, ReadOnly] public NativeList<GeneratedMeshDescription> meshDescriptions;
        [NoAlias, ReadOnly] public NativeList<SubMeshSection>           subMeshSections;
        [NoAlias, ReadOnly] public NativeList<Mesh.MeshData>            meshDatas;

        // Write
        [NoAlias, WriteOnly] public NativeList<Mesh.MeshData>           meshes;
        [NoAlias, WriteOnly] public NativeList<ChiselMeshUpdate>        debugHelperMeshes;
        [NoAlias, WriteOnly] public NativeList<ChiselMeshUpdate>        renderMeshes;

        // Read / Write
        [NoAlias] public NativeList<ChiselMeshUpdate>                   colliderMeshUpdates;


        [BurstDiscard]
        public static void InvalidQuery(LayerUsageFlags query, LayerUsageFlags mask)
        {
            Debug.Assert(false, $"Invalid helper query used (query: {query}, mask: {mask})");

        }

        public void Execute() 
        {
            int meshIndex = 0;
            int colliderCount = 0;
            if (meshDescriptions.IsCreated)
            {
                for (int i = 0; i < subMeshSections.Length; i++)
                {
                    var subMeshSection = subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.None)
                    {
                        int helperIndex = -1;
                        var query   = subMeshSection.meshQuery.LayerQuery;
                        var mask    = subMeshSection.meshQuery.LayerQueryMask;
                        for (int f = 0; f < kGeneratedDebugRendererFlags.Length; f++)
                        {
                            if (kGeneratedDebugRendererFlags[f].Item1 != query ||
                                kGeneratedDebugRendererFlags[f].Item2 != mask)
                                continue;

                            helperIndex = f;
                            break;
                        }
                        if (helperIndex == -1)
                        {
                            InvalidQuery(query, mask);
                            continue;
                        }

                        meshes.Add(meshDatas[meshIndex]);
                        debugHelperMeshes.Add(new ChiselMeshUpdate
                        {
                            contentsIndex       = i,
                            meshIndex           = meshIndex,
                            objectIndex         = helperIndex
                        });
                        meshIndex++; 
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                    {
                        var renderIndex = (int)(subMeshSection.meshQuery.LayerQuery & LayerUsageFlags.RenderReceiveCastShadows);
                        meshes.Add(meshDatas[meshIndex]);
                        renderMeshes.Add(new ChiselMeshUpdate
                        {
                            contentsIndex       = i,
                            meshIndex           = meshIndex,
                            objectIndex         = renderIndex
                        });
                        meshIndex++;
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                        colliderCount++;
                }
            }

            if (colliderMeshUpdates.Capacity < colliderCount)
                colliderMeshUpdates.Capacity = colliderCount;
            if (meshDescriptions.IsCreated)
            {
                var colliderIndex = 0;
                for (int i = 0; i < subMeshSections.Length; i++)
                {
                    var subMeshSection = subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex != LayerParameterIndex.PhysicsMaterial)
                        continue;

                    var surfaceParameter = meshDescriptions[subMeshSection.startIndex].surfaceParameter;

                    meshes.Add(meshDatas[meshIndex]);
                    colliderMeshUpdates.Add(new ChiselMeshUpdate
                    {
                        contentsIndex   = i,
                        colliderIndex   = colliderIndex,
                        meshIndex       = meshIndex,
                        objectIndex     = surfaceParameter
                    });
                    colliderIndex++;
                    meshIndex++;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    struct CopyToRenderMeshJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>              subMeshSections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts>               subMeshCounts;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>>  subMeshSurfaces;

        [NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor>   renderDescriptors;
        [NoAlias, ReadOnly] public NativeArray<ChiselMeshUpdate>            renderMeshes;

        // Read / Write
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeList<UnsafeList<CompactNodeID>> triangleBrushIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeList<Mesh.MeshData>             meshes;

        public void Execute(int renderIndex)
        {
            var update          = renderMeshes[renderIndex];
            var contentsIndex   = update.contentsIndex;
            var meshIndex       = update.meshIndex;
            
            var vertexBufferInit    = subMeshSections[contentsIndex];
            var startIndex          = vertexBufferInit.startIndex;
            var endIndex            = vertexBufferInit.endIndex;
            var numberOfSubMeshes   = endIndex - startIndex;
            var totalVertexCount    = vertexBufferInit.totalVertexCount;
            var totalIndexCount     = vertexBufferInit.totalIndexCount;
            
            var meshData        = meshes[meshIndex];
            if (numberOfSubMeshes == 0 ||
                totalVertexCount == 0 ||
                totalIndexCount == 0)
            {
                meshData.SetVertexBufferParams(0, renderDescriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }

            meshData.SetVertexBufferParams(totalVertexCount, renderDescriptors);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);
            meshData.subMeshCount = numberOfSubMeshes;

            var vertices    = meshData.GetVertexData<RenderVertex>(stream: 0);
            var indices     = meshData.GetIndexData<int>();

            var triangleBrushIndices = this.triangleBrushIndices[contentsIndex];
                
            int currentBaseVertex = 0;
            int currentBaseIndex = 0;

            for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
            {
                var subMeshCount        = subMeshCounts[d];
                var vertexCount		    = subMeshCount.vertexCount;
                var indexCount		    = subMeshCount.indexCount;
                var surfacesOffset      = subMeshCount.surfacesOffset;
                var surfacesCount       = subMeshCount.surfacesCount;
                var meshQueryIndex      = subMeshCount.meshQueryIndex;
                var subMeshSurfaceArray = subMeshSurfaces[meshQueryIndex];

                var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                // copy all the vertices & indices to the sub-meshes, one sub-mesh per material
                for (int surfaceIndex       = surfacesOffset, 
                         brushIDIndexOffset = currentBaseIndex / 3, 
                         indexOffset        = currentBaseIndex, 
                         indexVertexOffset  = 0, 
                         lastSurfaceIndex   = surfacesCount + surfacesOffset;

                         surfaceIndex < lastSurfaceIndex;

                         ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                    var brushNodeID         = subMeshSurface.brushNodeID;
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            
                    ref var sourceIndices   = ref sourceBuffer.indices;
                    ref var sourceVertices  = ref sourceBuffer.renderVertices;

                    var sourceIndexCount    = sourceIndices.Length;
                    var sourceVertexCount   = sourceVertices.Length;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;
                        
                    var sourceBrushCount    = sourceIndexCount / 3;
                    for (int n = 0; n < sourceBrushCount; n++)
                        triangleBrushIndices[n + brushIDIndexOffset] = brushNodeID;
                    brushIDIndexOffset += sourceBrushCount;

                    for (int i = 0; i < sourceIndexCount; i++)
                        indices[i + indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);
                    indexOffset += sourceIndexCount;

                    vertices.CopyFrom(currentBaseVertex + indexVertexOffset, ref sourceVertices, 0, sourceVertexCount);

                    min = math.min(min, sourceBuffer.min);
                    max = math.max(max, sourceBuffer.max);

                    indexVertexOffset += sourceVertexCount;
                }
                
                var srcBounds   = new ChiselAABB { Min = min, Max = max };
                var center      = (Vector3)((srcBounds.Max + srcBounds.Min) * 0.5f);
                var size        = (Vector3)(srcBounds.Max - srcBounds.Min);
                var dstBounds   = new Bounds(center, size);
                meshData.SetSubMesh(subMeshIndex, new SubMeshDescriptor
                {
                    baseVertex  = currentBaseVertex,
                    firstVertex = 0,
                    vertexCount = vertexCount,
                    indexStart  = currentBaseIndex,
                    indexCount  = indexCount,
                    bounds      = dstBounds,
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds);

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct CopyToColliderMeshJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>              subMeshSections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts>               subMeshCounts;
        [NoAlias, ReadOnly] public NativeArray<UnsafeList<SubMeshSurface>>  subMeshSurfaces;

        [NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor>   colliderDescriptors;
        [NoAlias, ReadOnly] public NativeArray<ChiselMeshUpdate>            colliderMeshes;

        // Read / Write
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeList<Mesh.MeshData> meshes;



        public void Execute(int colliderIndex)
        {
            var update              = colliderMeshes[colliderIndex];
            var contentsIndex       = update.contentsIndex;
            var meshIndex           = update.meshIndex;
            
            var vertexBufferInit    = subMeshSections[contentsIndex];
            var totalVertexCount    = vertexBufferInit.totalVertexCount;
            var totalIndexCount     = vertexBufferInit.totalIndexCount;

            var meshData            = meshes[meshIndex];
            if (totalVertexCount == 0 ||
                totalIndexCount == 0)
            {
                meshData.SetVertexBufferParams(0, colliderDescriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }
            var startIndex          = vertexBufferInit.startIndex;


            meshData.SetVertexBufferParams(totalVertexCount, colliderDescriptors);
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

            var vertices            = meshData.GetVertexData<float3>(stream: 0);
            var indices             = meshData.GetIndexData<int>();

            var subMeshCount        = subMeshCounts[startIndex];
            var meshQueryIndex		= subMeshCount.meshQueryIndex;

            var surfacesOffset      = subMeshCount.surfacesOffset;
            var surfacesCount       = subMeshCount.surfacesCount;
            var vertexCount		    = subMeshCount.vertexCount;
            var indexCount		    = subMeshCount.indexCount;
            var subMeshSurfaceArray = subMeshSurfaces[meshQueryIndex];

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            // copy all the vertices & indices to a mesh for the collider
            int indexOffset = 0, vertexOffset = 0;
            for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                    surfaceIndex < lastSurfaceIndex;
                    ++surfaceIndex)
            {
                var subMeshSurface      = subMeshSurfaceArray[surfaceIndex];
                ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                ref var sourceIndices   = ref sourceBuffer.indices;
                ref var sourceVertices  = ref sourceBuffer.colliderVertices;

                var sourceIndexCount    = sourceIndices.Length;
                var sourceVertexCount   = sourceVertices.Length;

                if (sourceIndexCount == 0 ||
                    sourceVertexCount == 0)
                    continue;

                var sourceBrushCount    = sourceIndexCount / 3;
                brushIDIndexOffset += sourceBrushCount;

                for (int i = 0; i < sourceIndexCount; i++)
                    indices[i + indexOffset] = (int)(sourceIndices[i] + vertexOffset);
                indexOffset += sourceIndexCount;

                vertices.CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);
                    
                min = math.min(min, sourceBuffer.min);
                max = math.max(max, sourceBuffer.max);

                vertexOffset += sourceVertexCount;
            }
            Debug.Assert(indexOffset == totalIndexCount);
            Debug.Assert(vertexOffset == totalVertexCount);



                
            var srcBounds   = new ChiselAABB { Min = min, Max = max };
            var center      = (Vector3)((srcBounds.Max + srcBounds.Min) * 0.5f);
            var size        = (Vector3)(srcBounds.Max - srcBounds.Min);
            
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor
            {
                baseVertex  = 0,
                firstVertex = 0,
                vertexCount = vertexCount,
                indexStart  = 0,
                indexCount  = indexCount,
                bounds      = new Bounds(center, size),
                topology    = UnityEngine.MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds);
        }
    }
}
