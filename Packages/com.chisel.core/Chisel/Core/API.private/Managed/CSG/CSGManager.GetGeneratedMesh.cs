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
        const int kMaxPhysicsIndexCount = 32000;


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

        static void Realloc<T>(ref NativeArray<T> oldArray, int newSize, Allocator allocator, bool arrayIsUsed = true) where T:struct
        {
            if (oldArray.IsCreated)
            {
                if (!arrayIsUsed)
                {
                    oldArray.Dispose();
                    oldArray = default;
                    return;
                }

                if (oldArray.Length == newSize)
                {
                    oldArray.ClearStruct();
                    return;
                }

                oldArray.Dispose();
            }

            oldArray = new NativeArray<T>(newSize, allocator);
        }

        static void Realloc<T>(ref NativeList<T> oldList, int newSize, Allocator allocator, bool arrayIsUsed = true) where T : struct
        {
            if (oldList.IsCreated)
            {
                if (!arrayIsUsed)
                {
                    oldList.Dispose();
                    oldList = default;
                    return;
                }
            } else
                oldList = new NativeList<T>(newSize, allocator);
            oldList.Clear();
            oldList.Resize(newSize, NativeArrayOptions.ClearMemory);
        }

        static JobHandle GetGeneratedMeshPositionOnly(TreeInfo treeInfo, ref SubMeshCounts subMeshCount, ref GeneratedMeshContents generatedMeshContents, JobHandle dependencies = default)
        {
            var allocator = Allocator.Persistent;
            
            if (!generatedMeshContents.positions   .IsCreated) generatedMeshContents.positions    = new NativeList<float3>(allocator);
            if (!generatedMeshContents.indices     .IsCreated) generatedMeshContents.indices      = new NativeList<int>(allocator);
            if (!generatedMeshContents.brushIndices.IsCreated) generatedMeshContents.brushIndices = new NativeList<int>(allocator);
            
            var generateVertexBuffersJob = new GenerateVertexBuffersPositionOnlyJob
            {
                subMeshCount                = subMeshCount,
                subMeshSurfaces             = treeInfo.subMeshSurfaces,
                
                subMeshIndexCount           = generatedMeshContents.indexCount, 
                subMeshVertexCount          = generatedMeshContents.vertexCount,

                generatedMeshIndices        = generatedMeshContents.indices,
                generatedMeshBrushIndices   = generatedMeshContents.brushIndices,
                generatedMeshPositions      = generatedMeshContents.positions
            };
            return generateVertexBuffersJob.Schedule(dependencies);
        }
    
        [BurstCompile(CompileSynchronously = true)]
        struct GenerateVertexBuffersPositionOnlyJob : IJob
        {
            [NoAlias, ReadOnly] public SubMeshCounts subMeshCount;

            [NoAlias, ReadOnly] public NativeArray<SubMeshSurface> subMeshSurfaces;

            [NoAlias, ReadOnly] public int		    subMeshIndexCount;
            [NoAlias, ReadOnly] public int		    subMeshVertexCount;

            [NoAlias] public NativeList<int>        generatedMeshBrushIndices;
            [NoAlias] public NativeList<int>		generatedMeshIndices;
            [NoAlias] public NativeList<float3>     generatedMeshPositions;

            static void Realloc<T>(ref NativeList<T> oldList, int newSize) where T : struct
            {
                if (!oldList.IsCreated)
                    return;
                
                oldList.Clear();
                oldList.Resize(newSize, NativeArrayOptions.ClearMemory);
            }

            public void Execute()
            {
                if (subMeshCount.vertexCount < 3 ||
                    subMeshCount.indexCount < 3)
                    throw new Exception($"{nameof(CSGTree)} called with a {nameof(GeneratedMeshDescription)} that isn't valid");

                var meshIndex		= subMeshCount.meshQueryIndex;
                var subMeshIndex	= subMeshCount.subMeshQueryIndex;

                var vertexCount		= subMeshCount.vertexCount;
                var indexCount		= subMeshCount.indexCount;

                var surfacesOffset  = subMeshCount.surfacesOffset;
                var surfacesCount   = subMeshCount.surfacesCount;

                Realloc(ref generatedMeshPositions,    vertexCount);
                Realloc(ref generatedMeshIndices,      indexCount);
                Realloc(ref generatedMeshBrushIndices, indexCount / 3);

                if (subMeshIndexCount == 0 || subMeshVertexCount == 0)
                    return;

                var generatedMeshBrushIndicesArray  = generatedMeshBrushIndices.AsArray();
                var generatedMeshIndicesArray       = generatedMeshIndices.AsArray();
                var generatedMeshPositionsArray     = generatedMeshPositions.AsArray();

                // double snap_size = 1.0 / ants.SnapDistance();

                { 
                    // copy all the vertices & indices to the sub-meshes for each material
                    for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                            surfaceIndex < lastSurfaceIndex;
                            ++surfaceIndex)
                    {
                        var subMeshSurface = subMeshSurfaces[surfaceIndex];
                        ref var sourceBuffer = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                        if (sourceBuffer.indices.Length == 0 ||
                            sourceBuffer.vertices.Length == 0)
                            continue;

                        var brushNodeID = subMeshSurface.brushNodeID;

                        for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i += 3)
                        {
                            generatedMeshBrushIndicesArray[brushIDIndexOffset] = brushNodeID; brushIDIndexOffset++;
                        }

                        for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i ++)
                        {
                            generatedMeshIndicesArray[indexOffset] = (int)(sourceBuffer.indices[i] + vertexOffset); indexOffset++;
                        }

                        var sourceVertexCount = sourceBuffer.vertices.Length;

                        generatedMeshPositionsArray.CopyFrom(vertexOffset, ref sourceBuffer.vertices, 0, sourceVertexCount);

                        vertexOffset += sourceVertexCount;
                    }
                }
            }
        }

        static JobHandle GetGeneratedMeshes(TreeInfo treeInfo, int startIndex, int endIndex, ref GeneratedMeshContents generatedMeshContents, JobHandle dependencies = default)
        {
            const Allocator allocator = Allocator.Persistent;
            
            if (endIndex - startIndex == 0)
            {
                if (generatedMeshContents.subMeshes   .IsCreated) generatedMeshContents.subMeshes    .Clear();
                if (generatedMeshContents.tangents    .IsCreated) generatedMeshContents.tangents     .Clear();
                if (generatedMeshContents.normals     .IsCreated) generatedMeshContents.normals      .Clear();
                if (generatedMeshContents.uv0         .IsCreated) generatedMeshContents.uv0          .Clear();
                if (generatedMeshContents.positions   .IsCreated) generatedMeshContents.positions    .Clear();
                if (generatedMeshContents.indices     .IsCreated) generatedMeshContents.indices      .Clear();
                if (generatedMeshContents.brushIndices.IsCreated) generatedMeshContents.brushIndices .Clear();
                return default;
            }
            
            if (!generatedMeshContents.subMeshes   .IsCreated) generatedMeshContents.subMeshes    = new NativeList<GeneratedSubMesh>(allocator);
            if (!generatedMeshContents.tangents    .IsCreated) generatedMeshContents.tangents     = new NativeList<float4>(allocator);
            if (!generatedMeshContents.normals     .IsCreated) generatedMeshContents.normals      = new NativeList<float3>(allocator);
            if (!generatedMeshContents.uv0         .IsCreated) generatedMeshContents.uv0          = new NativeList<float2>(allocator);
            if (!generatedMeshContents.positions   .IsCreated) generatedMeshContents.positions    = new NativeList<float3>(allocator);
            if (!generatedMeshContents.indices     .IsCreated) generatedMeshContents.indices      = new NativeList<int>(allocator);
            if (!generatedMeshContents.brushIndices.IsCreated) generatedMeshContents.brushIndices = new NativeList<int>(allocator);

            var generateVertexBuffersJob = new GenerateVertexBuffersSlicedJob
            {
                startIndex                  = startIndex,
                endIndex                    = endIndex,
                    
                subMeshCounts               = treeInfo.subMeshCounts.AsArray(),
                subMeshSurfaces             = treeInfo.subMeshSurfaces.AsDeferredJobArray(),

                generatedSubMeshes          = generatedMeshContents.subMeshes,
                generatedMeshTangents       = generatedMeshContents.tangents,
                generatedMeshNormals        = generatedMeshContents.normals,
                generatedMeshUV0            = generatedMeshContents.uv0,
                generatedMeshPositions      = generatedMeshContents.positions,
                generatedMeshIndices        = generatedMeshContents.indices,
                generatedMeshBrushIndices   = generatedMeshContents.brushIndices,
            };
            return generateVertexBuffersJob.Schedule(dependencies);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct GenerateVertexBuffersSlicedJob : IJob
        {
            // Read Only
            [NoAlias, ReadOnly] public int                          startIndex;
            [NoAlias, ReadOnly] public int                          endIndex;
            [NoAlias, ReadOnly] public NativeArray<SubMeshCounts>   subMeshCounts;
            [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>  subMeshSurfaces;

            // Read / Write 
            [NoAlias] public NativeList<int>                generatedMeshBrushIndices;
            [NoAlias] public NativeList<float4>             generatedMeshTangents;
            [NoAlias] public NativeList<GeneratedSubMesh>   generatedSubMeshes;
            [NoAlias] public NativeList<int>		        generatedMeshIndices;
            [NoAlias] public NativeList<float3>             generatedMeshPositions;
            [NoAlias] public NativeList<float2>             generatedMeshUV0; 
            [NoAlias] public NativeList<float3>             generatedMeshNormals;
            
            static void ComputeTangents(NativeSlice<int>        meshIndices,
                                        NativeSlice<float3>	    positions,
                                        NativeSlice<float2>	    uvs,
                                        NativeSlice<float3>	    normals,
                                        NativeSlice<float4>	    tangents) 
            {
                if (meshIndices.Length == 0 ||
                    positions.Length == 0)
                    return;

                var tangentU = new NativeArray<float3>(positions.Length, Allocator.Temp);
                var tangentV = new NativeArray<float3>(positions.Length, Allocator.Temp);

                for (int i = 0; i < meshIndices.Length; i+=3) 
                {
                    int i0 = meshIndices[i + 0];
                    int i1 = meshIndices[i + 1];
                    int i2 = meshIndices[i + 2];

                    var v1 = positions[i0];
                    var v2 = positions[i1];
                    var v3 = positions[i2];
        
                    var w1 = uvs[i0];
                    var w2 = uvs[i1];
                    var w3 = uvs[i2];

                    var edge1 = v2 - v1;
                    var edge2 = v3 - v1;

                    var uv1 = w2 - w1;
                    var uv2 = w3 - w1;
        
                    var r = 1.0f / (uv1.x * uv2.y - uv1.y * uv2.x);
                    if (math.isnan(r) || math.isfinite(r))
                        r = 0.0f;

                    var udir = new float3(
                        ((edge1.x * uv2.y) - (edge2.x * uv1.y)) * r,
                        ((edge1.y * uv2.y) - (edge2.y * uv1.y)) * r,
                        ((edge1.z * uv2.y) - (edge2.z * uv1.y)) * r
                    );

                    var vdir = new float3(
                        ((edge1.x * uv2.x) - (edge2.x * uv1.x)) * r,
                        ((edge1.y * uv2.x) - (edge2.y * uv1.x)) * r,
                        ((edge1.z * uv2.x) - (edge2.z * uv1.x)) * r
                    );

                    tangentU[i0] += udir;
                    tangentU[i1] += udir;
                    tangentU[i2] += udir;

                    tangentV[i0] += vdir;
                    tangentV[i1] += vdir;
                    tangentV[i2] += vdir;
                }

                for (int i = 0; i < positions.Length; i++) 
                {
                    var n	= normals[i];
                    var t0	= tangentU[i];
                    var t1	= tangentV[i];

                    n = math.normalizesafe(n);
                    var t = t0 - (n * math.dot(n, t0));
                    t = math.normalizesafe(t);

                    var c = math.cross(n, t0);
                    float w = (math.dot(c, t1) < 0) ? 1.0f : -1.0f;
                    tangents[i] = new float4(t.x, t.y, t.z, w);
                }
            }

            static void Realloc<T>(ref NativeList<T> oldList, int newSize) where T : struct
            {
                if (!oldList.IsCreated)
                    return;
                
                oldList.Clear();
                oldList.Resize(newSize, NativeArrayOptions.ClearMemory);
            }

            public void Execute()
            {                
                int subMeshCountSize = (int)subMeshCounts.Length;

                bool success = false;
                int totalVertexCount = 0;
                int totalIndexCount = 0;
                
                var validMeshDescriptions = new NativeList<SubMeshCounts>(endIndex - startIndex, Allocator.Temp);
                for (int d = startIndex; d < endIndex; d++)
                { 
                    var meshDescription = subMeshCounts[d];
                    if (meshDescription.vertexCount < 3 ||
                        meshDescription.indexCount < 3)
                        throw new Exception($"{nameof(CSGTree)} called with a {nameof(GeneratedMeshDescription)} that isn't valid");

                    var meshIndex		= meshDescription.meshQueryIndex;
                    var subMeshIndex	= meshDescription.subMeshQueryIndex;
                    if (meshIndex    < 0) { throw new Exception("GetGeneratedMesh: MeshIndex cannot be negative"); }
                    if (subMeshIndex < 0) { throw new Exception("GetGeneratedMesh: SubMeshIndex cannot be negative"); }

            
                    if (subMeshIndex >= (int)subMeshCountSize) { throw new Exception("GetGeneratedMesh: SubMeshIndex is higher than the number of generated meshes"); }
                    if (meshIndex    >= (int)subMeshCountSize) { throw new Exception("GetGeneratedMesh: MeshIndex is higher than the number of generated meshes"); }

                    int foundIndex = -1;
                    for (int i = 0; i < subMeshCountSize; i++)
                    {
                        if (meshIndex    == subMeshCounts[i].meshQueryIndex &&
                            subMeshIndex == subMeshCounts[i].subMeshQueryIndex)
                        {
                            foundIndex = i;
                            break;
                        }
                    }
                    if (foundIndex < 0 || foundIndex >= subMeshCountSize) { throw new Exception("GetGeneratedMesh: Could not find mesh associated with MeshIndex/SubMeshIndex pair"); }
            
                    var subMeshCount = subMeshCounts[foundIndex];
                    if (subMeshCount.indexCount > meshDescription.indexCount) { throw new Exception($"GetGeneratedMesh: The destination indices array ({meshDescription.indexCount}) is smaller than the size of the source data ({(int)subMeshCount.indexCount})"); }
                    if (subMeshCount.vertexCount > meshDescription.vertexCount) { throw new Exception($"GetGeneratedMesh: The destination vertices array ({meshDescription.vertexCount}) is smaller than the size of the source data ({(int)subMeshCount.vertexCount})"); }
                    if (subMeshCount.indexCount == 0 || subMeshCount.vertexCount == 0) { throw new Exception("GetGeneratedMesh: Mesh is empty"); }

                    //var usedVertexChannels  = meshDescription.meshQuery.UsedVertexChannels;
                
                    var surfacesCount       = subMeshCount.surfacesCount;
                    if (surfacesCount == 0 ||
                        subMeshCount.vertexCount != meshDescription.vertexCount ||
                        subMeshCount.indexCount  != meshDescription.indexCount ||
                        subMeshCount.vertexCount == 0 ||
                        subMeshCount.indexCount == 0)
                        continue;

                    validMeshDescriptions.AddNoResize(subMeshCount);
                    totalVertexCount += subMeshCount.vertexCount;
                    totalIndexCount += subMeshCount.indexCount;
                    success = true;
                }

                var numberOfSubMeshes = validMeshDescriptions.Length;
                if (!success || numberOfSubMeshes == 0)
                {
                    Realloc(ref generatedSubMeshes,        0);
                    Realloc(ref generatedMeshTangents,     0);
                    Realloc(ref generatedMeshNormals,      0);
                    Realloc(ref generatedMeshUV0,          0);
                    Realloc(ref generatedMeshPositions,    0);
                    Realloc(ref generatedMeshIndices,      0);
                    Realloc(ref generatedMeshBrushIndices, 0 / 3);
                    return;
                }

                //Profiler.BeginSample("Alloc");
                Realloc(ref generatedSubMeshes,        numberOfSubMeshes);
                Realloc(ref generatedMeshTangents,     totalVertexCount);
                Realloc(ref generatedMeshNormals,      totalVertexCount);
                Realloc(ref generatedMeshUV0,          totalVertexCount);
                Realloc(ref generatedMeshPositions,    totalVertexCount);
                Realloc(ref generatedMeshIndices,      totalIndexCount);
                Realloc(ref generatedMeshBrushIndices, totalIndexCount / 3);
                //Profiler.EndSample();

                { 
                    int currentBaseVertex = 0;
                    int currentBaseIndex = 0;

                    for (int d = 0; d < validMeshDescriptions.Length; d++)
                    {
                        var subMeshCount        = validMeshDescriptions[d];
                        var vertexCount		    = subMeshCount.vertexCount;
                        var indexCount		    = subMeshCount.indexCount;
                        var surfacesOffset      = subMeshCount.surfacesOffset;
                        var surfacesCount       = subMeshCount.surfacesCount;

                        generatedSubMeshes[d] = new GeneratedSubMesh
                        { 
                            baseVertex          = currentBaseVertex,
                            baseIndex           = currentBaseIndex,
                            indexCount          = indexCount,
                            vertexCount         = vertexCount,
                            surfacesOffset      = surfacesOffset,
                            surfacesCount       = surfacesCount,
                        };

                        currentBaseVertex += vertexCount;
                        currentBaseIndex += indexCount;
                    }
                }


                // Would love to do this in parallel, since all slices are sequential, but yeah, can't.
                for (int index = 0; index < generatedSubMeshes.Length; index++)
                { 
                    var currentBaseIndex    = generatedSubMeshes[index].baseIndex;
                    var indexCount          = generatedSubMeshes[index].indexCount;
                    var currentBaseVertex   = generatedSubMeshes[index].baseVertex;
                    var vertexCount         = generatedSubMeshes[index].vertexCount;
            
                    var surfacesOffset      = generatedSubMeshes[index].surfacesOffset;
                    var surfacesCount       = generatedSubMeshes[index].surfacesCount;

                    var generatedMeshIndicesSlice       = generatedMeshIndices      .AsArray().Slice(currentBaseIndex, indexCount);
                    var generatedMeshBrushIndicesSlice  = generatedMeshBrushIndices .AsArray().Slice(currentBaseIndex / 3, indexCount / 3);
                    var generatedMeshPositionsSlice     = generatedMeshPositions    .AsArray().Slice(currentBaseVertex, vertexCount);
                    var generatedMeshTangentsSlice      = generatedMeshTangents     .AsArray().Slice(currentBaseVertex, vertexCount);
                    var generatedMeshNormalsSlice       = generatedMeshNormals      .AsArray().Slice(currentBaseVertex, vertexCount);
                    var generatedMeshUV0Slice           = generatedMeshUV0          .AsArray().Slice(currentBaseVertex, vertexCount);

                    // double snap_size = 1.0 / ants.SnapDistance();

                    { 
                        // copy all the vertices & indices to the sub-meshes for each material
                        for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                                surfaceIndex < lastSurfaceIndex;
                                ++surfaceIndex)
                        {
                            var subMeshSurface = subMeshSurfaces[surfaceIndex];
                            ref var sourceBuffer = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            if (sourceBuffer.indices.Length == 0 ||
                                sourceBuffer.vertices.Length == 0)
                                continue;

                            var brushNodeID = subMeshSurface.brushNodeID;

                            for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i += 3)
                            {
                                generatedMeshBrushIndicesSlice[brushIDIndexOffset] = brushNodeID; brushIDIndexOffset++;
                            }

                            for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i ++)
                            {
                                generatedMeshIndicesSlice[indexOffset] = (int)(sourceBuffer.indices[i] + vertexOffset); indexOffset++;
                            }

                            var sourceVertexCount = sourceBuffer.vertices.Length;

                            generatedMeshPositionsSlice.CopyFrom(vertexOffset, ref sourceBuffer.vertices, 0, sourceVertexCount);

                            generatedMeshUV0Slice.CopyFrom(vertexOffset, ref sourceBuffer.uv0, 0, sourceVertexCount);
                            generatedMeshNormalsSlice.CopyFrom(vertexOffset, ref sourceBuffer.normals, 0, sourceVertexCount);
                            vertexOffset += sourceVertexCount;
                        }
                    }

                    ComputeTangents(generatedMeshIndicesSlice,
                                    generatedMeshPositionsSlice,
                                    generatedMeshUV0Slice,
                                    generatedMeshNormalsSlice,
                                    generatedMeshTangentsSlice);
                }
            }
        }

        public static void GetMeshDescriptions(Int32                      treeNodeID,
                                               MeshQuery[]                meshQueries,
                                               VertexChannelFlags         vertexChannelMask,
                                               out List<GeneratedMeshDescription> descriptions,
                                               out List<GeneratedMeshContents>    meshContents)
        {
            descriptions = null;
            meshContents = null;
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


            combineSubMeshesJobHandle.Complete(); // <-- can't read from treeInfo.subMeshCounts otherwise

            var subMeshCounts = treeInfo.subMeshCounts;
            if (subMeshCounts.Length > 0)
            {
                // Sort all meshDescriptions so that meshes that can be merged are next to each other
                subMeshCounts.Sort(new SubMeshCountsComparer());

                var contents         = treeInfo.contents;
                if (contents.Count < subMeshCounts.Length)
                {
                    for (int i = contents.Count; i < subMeshCounts.Length; i++)
                        contents.Add(new GeneratedMeshContents());
                }

                JobHandle allCreateContentsJobHandle = default;

                int descriptionIndex = 0;
                var contentsIndex = 0;
                Profiler.BeginSample("Build Renderables");
                if (subMeshCounts[0].meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                {
                    var prevQuery = subMeshCounts[0].meshQuery;
                    var startIndex = 0;
                    for (; descriptionIndex < subMeshCounts.Length; descriptionIndex++)
                    {
                        var meshDescriptionIterator = subMeshCounts[descriptionIndex];
                        // Exit when layerParameterIndex is no longer LayerParameter1
                        if (meshDescriptionIterator.meshQuery.LayerParameterIndex != LayerParameterIndex.RenderMaterial)
                            break;

                        var currQuery = meshDescriptionIterator.meshQuery;
                        if (prevQuery == currQuery)
                            continue;

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

                        // Group by all subMeshCounts with same query
                        var generatedContents = contents[contentsIndex];
                        var createContentsJobHandle = 
                                GetGeneratedMeshes(treeInfo, startIndex, descriptionIndex,
                                                   ref generatedContents, combineSubMeshesJobHandle);
                        allCreateContentsJobHandle = JobHandle.CombineDependencies(allCreateContentsJobHandle, createContentsJobHandle);
                        contents[contentsIndex] = generatedContents;
                        contentsIndex++;

                        startIndex = descriptionIndex;
                        prevQuery = currQuery;
                    }

                    {
                        // Group by all subMeshCounts with same query
                        var generatedContents = contents[contentsIndex];
                        var createContentsJobHandle =
                                GetGeneratedMeshes(treeInfo, startIndex, descriptionIndex,
                                                   ref generatedContents, combineSubMeshesJobHandle);
                        allCreateContentsJobHandle = JobHandle.CombineDependencies(allCreateContentsJobHandle, createContentsJobHandle);
                        contents[contentsIndex] = generatedContents;
                        contentsIndex++;
                    }
                }
                Profiler.EndSample();
                

                Profiler.BeginSample("Build Colliders");
                if (descriptionIndex < subMeshCounts.Length &&
                    subMeshCounts[descriptionIndex].meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                {
                    Debug.Assert(subMeshCounts[subMeshCounts.Length - 1].meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial);

                    // Loop through all subMeshCounts with LayerParameter2, and create collider meshes from them
                    for (int i = 0; descriptionIndex < subMeshCounts.Length; descriptionIndex++, i++)
                    {
                        var meshDescription = subMeshCounts[descriptionIndex];

                        // Exit when layerParameterIndex is no longer LayerParameter2
                        if (meshDescription.meshQuery.LayerParameterIndex != LayerParameterIndex.PhysicsMaterial)
                            break;

                        var generatedContents = contents[contentsIndex];
                        var createContentsJobHandle = GetGeneratedMeshPositionOnly(treeInfo, ref meshDescription, 
                                                                ref generatedContents, combineSubMeshesJobHandle);
                        allCreateContentsJobHandle = JobHandle.CombineDependencies(allCreateContentsJobHandle, createContentsJobHandle);
                        contents[contentsIndex] = generatedContents;
                        contentsIndex++;
                    }
                }
                Profiler.EndSample();

                Profiler.BeginSample("Complete");
                allCreateContentsJobHandle.Complete();
                Profiler.EndSample();
            }


            if (treeInfo.subMeshCounts.Length == 0 ||
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
            meshContents = treeInfo.contents;
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
                subMeshCounts   = treeInfo.subMeshCounts
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
            [NoAlias, ReadOnly] public NativeArray<SectionData>     sections;

            // Read/Write (Sort)
            [NoAlias] public NativeArray<SubMeshSurface>            subMeshSurfaces;

            [NoAlias, WriteOnly] public NativeList<SubMeshCounts>   subMeshCounts;

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
                        meshQueryIndex           = meshIndex,
                        subMeshQueryIndex        = 0,
                        meshQuery           = meshQuery,
                        surfaceParameter   = querySurfaces[0].surfaceParameter,
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
                            (isPhysics && currentSubMesh.indexCount >= kMaxPhysicsIndexCount))
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