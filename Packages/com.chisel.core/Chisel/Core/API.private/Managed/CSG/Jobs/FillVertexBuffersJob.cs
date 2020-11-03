using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    internal struct SubMeshCounts
    {
        public MeshQuery meshQuery;
        public int		surfaceParameter;

        public int		meshQueryIndex;
        public int		subMeshQueryIndex;
            
        public uint	    geometryHashValue;  // used to detect changes in vertex positions  
        public uint	    surfaceHashValue;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
            
        public int		vertexCount;
        public int		indexCount;
            
        public int      surfacesOffset;
        public int      surfacesCount;
    };

    public struct SubMeshSection
    {
        public MeshQuery meshQuery;
        public int startIndex;
        public int endIndex;
        public int totalVertexCount;
        public int totalIndexCount;
    }
    
    public struct BrushData
    {
        public IndexOrder                                   brushIndexOrder; //<- TODO: if we use NodeOrder maybe this could be explicit based on the order in array?
        public int                                          brushSurfaceOffset;
        public int                                          brushSurfaceCount;
        public BlobAssetReference<ChiselBrushRenderBuffer>  brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FindBrushRenderBuffersJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int meshQueryLength;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBufferCache;

        // Write
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<BrushData>         brushRenderData;
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<SubMeshCounts>     subMeshCounts;
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<SubMeshSection>    subMeshSections;

        public void Execute()
        {
            int surfaceCount = 0;
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

                surfaceCount += brushSurfaceCount;
            }
            
            var subMeshCapacity = surfaceCount * meshQueryLength;
            if (subMeshCounts.Capacity < subMeshCapacity)
                subMeshCounts.Capacity = subMeshCapacity;
            
            if (subMeshSections.Capacity < subMeshCapacity)
                subMeshSections.Capacity = subMeshCapacity;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AllocateVertexBuffersJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>        subMeshSections;

        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<GeneratedSubMesh> subMeshesArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int> 	            indicesArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int> 	            triangleBrushIndices;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<RenderVertex>     renderVerticesArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float3>           colliderVerticesArray;

        public void Execute()
        {
            if (subMeshSections.Length == 0)
                return;

            subMeshesArray.         ResizeExact(subMeshSections.Length);
            indicesArray.           ResizeExact(subMeshSections.Length);
            triangleBrushIndices.   ResizeExact(subMeshSections.Length);
            renderVerticesArray.    ResizeExact(subMeshSections.Length);
            colliderVerticesArray.  ResizeExact(subMeshSections.Length);
            for (int i = 0; i < subMeshSections.Length; i++)
            {
                var section = subMeshSections[i];
                var numberOfSubMeshes   = section.endIndex - section.startIndex;
                var totalVertexCount    = section.totalVertexCount;
                var totalIndexCount     = section.totalIndexCount;
                
                if (section.meshQuery.LayerParameterIndex == LayerParameterIndex.None ||
                    section.meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                { 
                    subMeshesArray          .AllocateWithCapacityForIndex(i, numberOfSubMeshes);
                    triangleBrushIndices    .AllocateWithCapacityForIndex(i, totalIndexCount / 3);
                    indicesArray            .AllocateWithCapacityForIndex(i, totalIndexCount);
                    renderVerticesArray     .AllocateWithCapacityForIndex(i, totalVertexCount);
                        
                    subMeshesArray          [i].Clear();
                    triangleBrushIndices    [i].Clear();
                    indicesArray            [i].Clear();
                    renderVerticesArray     [i].Clear();

                    subMeshesArray          [i].Resize(numberOfSubMeshes, NativeArrayOptions.ClearMemory);
                    triangleBrushIndices    [i].Resize(totalIndexCount / 3, NativeArrayOptions.ClearMemory);
                    indicesArray            [i].Resize(totalIndexCount, NativeArrayOptions.ClearMemory);
                    renderVerticesArray     [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                } else
                if (section.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                {
                    subMeshesArray          .AllocateWithCapacityForIndex(i, 1);
                    indicesArray            .AllocateWithCapacityForIndex(i, totalIndexCount);
                    colliderVerticesArray   .AllocateWithCapacityForIndex(i, totalVertexCount);
                        
                    subMeshesArray          [i].Clear();
                    indicesArray            [i].Clear();
                    colliderVerticesArray   [i].Clear();

                    subMeshesArray          [i].Resize(1, NativeArrayOptions.ClearMemory);
                    indicesArray            [i].Resize(totalIndexCount, NativeArrayOptions.ClearMemory);
                    colliderVerticesArray   [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);                    
                }
            }
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct FillVertexBuffersJob : IJobParallelFor
    {
        // Read Only
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>  subMeshSections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts>   subMeshCounts;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>  subMeshSurfaces;

        // Read / Write 
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<GeneratedSubMesh> subMeshesArray;         // numberOfSubMeshes
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int>              triangleBrushIndices;   // indexCount / 3
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int>		        indicesArray;           // indexCount
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<RenderVertex>     renderVerticesArray;    // vertexCount
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float3>           colliderVerticesArray;  // vertexCount

        public void Execute(int index)
        {
            var vertexBufferInit = subMeshSections[index];

            var layerParameterIndex = vertexBufferInit.meshQuery.LayerParameterIndex;
            var startIndex          = vertexBufferInit.startIndex;
            var endIndex            = vertexBufferInit.endIndex;
            var totalVertexCount    = vertexBufferInit.totalVertexCount;
            var totalIndexCount     = vertexBufferInit.totalVertexCount;
            if (layerParameterIndex == LayerParameterIndex.None ||
                layerParameterIndex == LayerParameterIndex.RenderMaterial)
            {
                if (vertexBufferInit.endIndex - vertexBufferInit.startIndex == 0)
                    return;
                var numberOfSubMeshes = endIndex - startIndex;


#if false
                const long kHashMagicValue = (long)1099511628211ul;
                UInt64 combinedGeometryHashValue = 0;
                UInt64 combinedSurfaceHashValue = 0;

                for (int i = startIndex; i < endIndex; i++)
                {
                    ref var meshDescription = ref subMeshCounts[i];
                    if (meshDescription.vertexCount < 3 ||
                        meshDescription.indexCount < 3)
                        continue;

                    combinedGeometryHashValue   = (combinedGeometryHashValue ^ meshDescription.geometryHashValue) * kHashMagicValue;
                    combinedSurfaceHashValue    = (combinedSurfaceHashValue  ^ meshDescription.surfaceHashValue) * kHashMagicValue;
                }
                        
                if (geometryHashValue != combinedGeometryHashValue ||
                    surfaceHashValue != combinedSurfaceHashValue)
                {
                    geometryHashValue != combinedGeometryHashValue ||
                    surfaceHashValue != combinedSurfaceHashValue)
#endif

                var subMeshes            = this.subMeshesArray      [index].AsArray();
                var triangleBrushIndices = this.triangleBrushIndices[index].AsArray();
                var indices              = this.indicesArray        [index].AsArray();
                var renderVertices       = this.renderVerticesArray [index].AsArray();

                int currentBaseVertex = 0;
                int currentBaseIndex = 0;

                for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
                {
                    var subMeshCount        = subMeshCounts[d];
                    var vertexCount		    = subMeshCount.vertexCount;
                    var indexCount		    = subMeshCount.indexCount;
                    var surfacesOffset      = subMeshCount.surfacesOffset;
                    var surfacesCount       = subMeshCount.surfacesCount;

                    var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                    // copy all the vertices & indices to the sub-meshes for each material
                    for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = currentBaseIndex / 3, indexOffset = currentBaseIndex, indexVertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                            surfaceIndex < lastSurfaceIndex;
                            ++surfaceIndex)
                    {
                        var subMeshSurface      = subMeshSurfaces[surfaceIndex];
                        var brushNodeIndex      = subMeshSurface.brushNodeIndex;
                        ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            
                        ref var sourceIndices   = ref sourceBuffer.indices;
                        ref var sourceVertices  = ref sourceBuffer.renderVertices;

                        var sourceIndexCount    = sourceIndices.Length;
                        var sourceVertexCount   = sourceVertices.Length;
                        var sourceBrushCount    = sourceIndexCount / 3;

                        if (sourceIndexCount == 0 ||
                            sourceVertexCount == 0)
                            continue;
                        
                        var brushNodeID = brushNodeIndex + 1;                        
                        for (int last = brushIDIndexOffset + sourceBrushCount; brushIDIndexOffset < last; brushIDIndexOffset++)
                            triangleBrushIndices[brushIDIndexOffset] = brushNodeID;

                        for (int i = 0; i < sourceIndexCount; i++, indexOffset++)
                            indices[indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);

                        var vertexOffset = currentBaseVertex + indexVertexOffset;
                        renderVertices  .CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);

                        min = math.min(min, sourceBuffer.min);
                        max = math.max(max, sourceBuffer.max);

                        indexVertexOffset += sourceVertexCount;
                    }
                    
                    subMeshes[subMeshIndex] = new GeneratedSubMesh
                    { 
                        baseVertex          = currentBaseVertex,
                        baseIndex           = currentBaseIndex,
                        indexCount          = indexCount,
                        vertexCount         = vertexCount,
                        bounds              = new MinMaxAABB { Min = min, Max = max }
                    };

                    currentBaseVertex += vertexCount;
                    currentBaseIndex += indexCount;
                }
            } else
            if (layerParameterIndex == LayerParameterIndex.PhysicsMaterial)
            {
                var subMeshCount    = subMeshCounts[startIndex];
                //var meshIndex		= subMeshCount.meshQueryIndex;
                var subMeshIndex	= subMeshCount.subMeshQueryIndex;

                var surfacesOffset  = subMeshCount.surfacesOffset;
                var surfacesCount   = subMeshCount.surfacesCount;
                var vertexCount		= subMeshCount.vertexCount;
                var indexCount		= subMeshCount.indexCount;
                
                var subMeshes          = this.subMeshesArray       [index].AsArray();
                var indices            = this.indicesArray         [index].AsArray();
                var colliderVertices   = this.colliderVerticesArray[index].AsArray();

                var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                // copy all the vertices & indices to a mesh for the collider
                for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                        surfaceIndex < lastSurfaceIndex;
                        ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaces[surfaceIndex];
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                    ref var sourceIndices   = ref sourceBuffer.indices;
                    ref var sourceVertices  = ref sourceBuffer.colliderVertices;

                    var sourceIndexCount    = sourceIndices.Length;
                    var sourceVertexCount   = sourceVertices.Length;
                    var sourceBrushCount    = sourceIndexCount / 3;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;

                    brushIDIndexOffset += sourceBrushCount;

                    for (int i = 0; i < sourceIndexCount; i++, indexOffset++)
                        indices[indexOffset] = (int)(sourceIndices[i] + vertexOffset);

                    colliderVertices.CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);
                    
                    min = math.min(min, sourceBuffer.min);
                    max = math.max(max, sourceBuffer.max);

                    vertexOffset += sourceVertexCount;
                }

                subMeshes[0] = new GeneratedSubMesh
                { 
                    baseVertex          = 0,
                    baseIndex           = 0,
                    indexCount          = indexCount,
                    vertexCount         = vertexCount,
                    bounds              = new MinMaxAABB { Min = min, Max = max }
                };
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
    
    public struct SectionData
    {
        public int surfacesOffset;
        public int surfacesCount;
        public MeshQuery meshQuery;
    }

    internal struct SubMeshSurface
    {
        public int      brushNodeIndex;
        public int      surfaceIndex;
        public int      surfaceParameter;

        public int      vertexCount;
        public int      indexCount;

        public uint     surfaceHash;
        public uint     geometryHash;
        public BlobAssetReference<ChiselBrushRenderBuffer> brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareSubSectionsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>.ReadOnly  meshQueries;
        [NoAlias, ReadOnly] public NativeArray<BrushData>           brushRenderData;

        // Read, Write
        [NoAlias] public NativeList<SectionData>            sections;
        [NoAlias] public NativeList<SubMeshSurface>         subMeshSurfaces;

        struct SubMeshSurfaceComparer : IComparer<SubMeshSurface>
        {
            public int Compare(SubMeshSurface x, SubMeshSurface y)
            {
                return x.surfaceParameter.CompareTo(y.surfaceParameter);
            }
        }

        static readonly SubMeshSurfaceComparer subMeshSurfaceComparer = new SubMeshSurfaceComparer();

        public void Execute()
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
            // TODO: AllocateVertexBuffersJob/FillVertexBuffersJob and CopyToRenderMeshJob could be combined (one copy only)


            var maximumLength = meshQueries.Length * requiredSurfaceCount;
            if (subMeshSurfaces.Capacity < maximumLength)
                subMeshSurfaces.Capacity = maximumLength;
            subMeshSurfaces.ResizeUninitialized(maximumLength);
            var subMeshSurfaceArray = subMeshSurfaces.AsArray();
            sections.ResizeUninitialized(meshQueries.Length);
            var sectionsArray = sections.AsArray();

            var surfacesLength = 0;
            var sectionCount = 0;
            //Debug.Log($"{meshQueries.Length} x {requiredSurfaceCount}");//11 x 33761
            for (int t = 0; t < meshQueries.Length; t++)
            {
                var surfacesOffset          = surfacesLength;
                var meshQuery               = meshQueries[t];

                for (int b = 0, count_b = brushRenderData.Length; b < count_b; b++)
                {
                    var brushData           = brushRenderData[b];
                    var brushRenderBuffer   = brushData.brushRenderBuffer;
                    ref var querySurfaces   = ref brushRenderBuffer.Value.querySurfaces[t]; // <-- 1. somehow this needs to 
                                                                                            //     be in outer loop
                    ref var brushNodeIndex  = ref querySurfaces.brushNodeIndex;
                    ref var surfaces        = ref querySurfaces.surfaces;

                    for (int s = 0; s < surfaces.Length; s++) 
                    {
                        subMeshSurfaceArray[surfacesLength] = new SubMeshSurface
                        {
                            brushNodeIndex      = brushNodeIndex,
                            surfaceIndex        = surfaces[s].surfaceIndex,
                            surfaceParameter    = surfaces[s].surfaceParameter, // <-- 2. store array per surfaceParameter => no sort
                            vertexCount         = surfaces[s].vertexCount,
                            indexCount          = surfaces[s].indexCount,
                            surfaceHash         = surfaces[s].surfaceHash,
                            geometryHash        = surfaces[s].geometryHash,
                            brushRenderBuffer   = brushRenderBuffer, // <-- 3. Get rid of this somehow => memcpy
                        };
                        surfacesLength++;
                    }
                    // ^ do those 3 points (mentioned in comments) 
                    // v and we basically already have our sections, and the whole job becomes unnecessary
                }

                var surfacesCount = surfacesLength - surfacesOffset;
                if (surfacesCount == 0)
                    continue;

                var slice = subMeshSurfaceArray.Slice(surfacesOffset, surfacesCount);
                slice.Sort(subMeshSurfaceComparer);

                sectionsArray[sectionCount] = new SectionData
                { 
                    surfacesOffset  = surfacesOffset,
                    surfacesCount   = surfacesCount,
                    meshQuery       = meshQuery
                };
                sectionCount++;
            }
            subMeshSurfaces.ResizeUninitialized(surfacesLength);
            sections.ResizeUninitialized(sectionCount);

            if (sections.Length == 0)
            {
                // Cannot Schedule using list length, when its 0 (throws exception)
                sections.AddNoResize(new SectionData
                {
                    surfacesCount = 0 // will early out
                });
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct SortSurfacesParallelJob : IJob
    //struct SortSurfacesParallelJob : IJobParallelFor
    {
        const int kMaxPhysicsVertexCount = 64000;

        // Read
        [NoAlias, ReadOnly] public NativeArray<SectionData>     sections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>  subMeshSurfaces;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshCounts>//.ParallelWriter <-- crashes when IJob
                                        subMeshCounts;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<SubMeshCounts> sectionSubMeshCounts;

        public void Execute()
        {
            // TODO: figure out why this order matters
            for (int t = 0; t < sections.Length; t++)
            {
                Execute(t);
            }
        }

        public void Execute(int t)
        {
            var section         = sections[t];
            var surfaceCount    = section.surfacesCount;
            if (surfaceCount == 0)
                return;

            var meshQuery       = section.meshQuery;
            var queryOffset     = section.surfacesOffset;
            var isPhysics       = meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial;

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
                surfaceParameter    = subMeshSurfaces[queryOffset].surfaceParameter
            };
            for (int b = 0; b < surfaceCount; b++)
            {
                var subMeshSurface              = subMeshSurfaces[queryOffset + b];
                var surfaceParameter            = subMeshSurface.surfaceParameter;
                var surfaceVertexCount          = subMeshSurface.vertexCount;
                var surfaceIndexCount           = subMeshSurface.indexCount;

                if (currentSubMesh.surfaceParameter != surfaceParameter || 
                    (isPhysics && currentSubMesh.vertexCount >= kMaxPhysicsVertexCount))
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

            for (int i = 1; i < subMeshCounts.Length; i++)
            {
                var currCount = subMeshCounts[i];
                currCount.surfacesOffset = subMeshCounts[i - 1].surfacesOffset + subMeshCounts[i - 1].surfacesCount;
                subMeshCounts[i] = currCount;
            }

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
}
