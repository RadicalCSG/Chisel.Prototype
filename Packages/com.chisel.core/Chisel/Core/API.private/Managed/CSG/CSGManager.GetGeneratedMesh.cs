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
            
            
            static void ComputeTriangleTangentBasis(float3 vertices0, float3 vertices1, float3 vertices2, 
                                                    float2 uvs0, float2 uvs1, float2 uvs2, 
                                                    out double3 tangent0,
                                                    out double3 binormal0,
                                                    out double3 tangent1,
                                                    out double3 binormal1,
                                                    out double3 tangent2,
                                                    out double3 binormal2)
            {
                double3 p = new double3(vertices1.x - vertices0.x, vertices1.y - vertices0.y, vertices1.z - vertices0.z );
                double3 q = new double3(vertices2.x - vertices0.x, vertices2.y - vertices0.y, vertices2.z - vertices0.z );

                double2 s = new double2(uvs1.x - uvs0.x, uvs2.x - uvs0.x);
                double2 t = new double2(uvs1.y - uvs0.y, uvs2.y - uvs0.y);

                var tangent  = double3.zero;
                var binormal = double3.zero;

                double div      = s[0] * t[1] - s[1] * t[0];
                double areaMult = math.abs(div);

                if (areaMult >= 1e-8)
                {
                    double r = 1.0 / div;

                    s[0] *= r;  t[0] *= r;
                    s[1] *= r;  t[1] *= r;


                    tangent.x  = (t[1] * p[0] - t[0] * q[0]);
                    tangent.y  = (t[1] * p[1] - t[0] * q[1]);
                    tangent.z  = (t[1] * p[2] - t[0] * q[2]);

                    binormal.x  = (s[0] * q[0] - s[1] * p[0]);
                    binormal.y  = (s[0] * q[1] - s[1] * p[1]);
                    binormal.z  = (s[0] * q[2] - s[1] * p[2]);

                    // weight by area

                    tangent    = math.normalize(tangent);
                    tangent.x *= areaMult;
                    tangent.y *= areaMult;
                    tangent.z *= areaMult;

                    binormal    = math.normalize(binormal);
                    binormal.x *= areaMult;
                    binormal.y *= areaMult;
                    binormal.z *= areaMult;
                }


                {
                    double3 edge1 = vertices2 - vertices0;
                    double3 edge2 = vertices1 - vertices0;

                    // weight by angle

                    double angle = math.dot(math.normalize(edge1), math.normalize(edge2));
                    double w     = math.acos(math.clamp(angle, -1.0, 1.0));

                    tangent0  = w * tangent;
                    binormal0 = w * binormal;
                }

                {
                    double3 edge1 = vertices0 - vertices1;
                    double3 edge2 = vertices2 - vertices1;

                    // weight by angle

                    double angle = math.dot(math.normalize(edge1), math.normalize(edge2));
                    double w = math.acos(math.clamp(angle, -1.0, 1.0));

                    tangent1 = w * tangent;
                    binormal1 = w * binormal;
                }

                {
                    double3 edge1 = vertices1 - vertices2;
                    double3 edge2 = vertices0 - vertices2;

                    // weight by angle

                    double angle = math.dot(math.normalize(edge1), math.normalize(edge2));
                    double w = math.acos(math.clamp(angle, -1.0, 1.0));

                    tangent2 = w * tangent;
                    binormal2 = w * binormal;
                }

            }
            
            static float4 OrthogonalizeTangent(double3 tangent, double3 binormal, float3 normalf)
            {
                double3 normal = new double3( normalf.x, normalf.y, normalf.z );

                double NdotT = math.dot(normal, tangent);
                double3 newTangent = new double3(tangent.x - NdotT * normal.x, tangent.y - NdotT * normal.y, tangent.z - NdotT * normal.z);

                double magT = math.length(newTangent);
                newTangent /= magT;

                double NdotB = math.dot(normal, binormal);
                double TdotB = math.dot(newTangent, binormal) * magT;

                double3 newBinormal = new double3
                (
                    binormal.x - NdotB * normal.x - TdotB * newTangent.x,
                    binormal.y - NdotB * normal.y - TdotB * newTangent.y,
                    binormal.z - NdotB * normal.z - TdotB * newTangent.z
                );

                double magB = math.length(newBinormal);
                newBinormal /= magB;


                float3 tangentf = new float3((float)newTangent.x, (float)newTangent.y, (float)newTangent.z);
                float3 binormalf = new float3((float)newBinormal.x, (float)newBinormal.y, (float)newBinormal.z);


                const double kNormalizeEpsilon = 1e-6;

                if (magT <= kNormalizeEpsilon || magB <= kNormalizeEpsilon)
                {
                    // Create tangent basis from scratch - we can safely use float3 here - no computations ;-)

                    var dpXN = math.abs(math.dot(new float3(1, 0, 0), normalf));
                    var dpYN = math.abs(math.dot(new float3(0, 1, 0), normalf));
                    var dpZN = math.abs(math.dot(new float3(0, 0, 1), normalf));

                    float3 axis1, axis2;
                    if (dpXN <= dpYN && dpXN <= dpZN)
                    {
                        axis1 = new float3(1,0,0);
                        if (dpYN <= dpZN)
                            axis2 = new float3(0, 1, 0);
                        else
                            axis2 = new float3(0, 0, 1);
                    }
                    else if (dpYN <= dpXN && dpYN <= dpZN)
                    {
                        axis1 = new float3(0, 1, 0);
                        if (dpXN <= dpZN)
                            axis2 = new float3(1, 0, 0);
                        else
                            axis2 = new float3(0, 0, 1);
                    }
                    else
                    {
                        axis1 = new float3(0, 0, 1);
                        if (dpXN <= dpYN)
                            axis2 = new float3(1, 0, 0);
                        else
                            axis2 = new float3(0, 1, 0);
                    }


                    tangentf = axis1 - math.dot(normalf, axis1) * normalf;
                    binormalf = axis2 - math.dot(normalf, axis2) * normalf - math.dot(tangentf, axis2) * math.normalizesafe(tangentf);

                    tangentf = math.normalizesafe(tangentf);
                    binormalf = math.normalizesafe(binormalf);
                }

                float dp = math.dot(math.cross(normalf, tangentf), binormalf);
                return new float4(tangentf.x, tangentf.y, tangentf.z, (dp > 0) ? 1 : -1);
            }

            static void ComputeTangents(NativeSlice<int>        indices,
                                        NativeSlice<float3>	    positions,
                                        NativeSlice<float2>	    uvs,
                                        NativeSlice<float3>	    normals,
                                        NativeSlice<float4>	    tangents) 
            {

                var triTangents     = new NativeArray<double3>(positions.Length, Allocator.Temp);
                var triBinormals    = new NativeArray<double3>(positions.Length, Allocator.Temp);

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var index0 = indices[i + 0];
                    var index1 = indices[i + 1];
                    var index2 = indices[i + 2];

                    var vertices0 = positions[index0];
                    var vertices1 = positions[index1];
                    var vertices2 = positions[index2];
                    var uvs0 = uvs[index0];
                    var uvs1 = uvs[index1];
                    var uvs2 = uvs[index2];

                    var p = new double3(vertices1.x - vertices0.x, vertices1.y - vertices0.y, vertices1.z - vertices0.z );
                    var q = new double3(vertices2.x - vertices0.x, vertices2.y - vertices0.y, vertices2.z - vertices0.z );
                    var s = new double2(uvs1.x - uvs0.x, uvs2.x - uvs0.x);
                    var t = new double2(uvs1.y - uvs0.y, uvs2.y - uvs0.y);

                    var scale       = s.x * t.y - s.y * t.x;
                    var absScale    = math.abs(scale);
                    p *= scale; q *= scale;

                    var tangent  = math.normalize(t.y * p - t.x * q) * absScale;
                    var binormal = math.normalize(s.x * q - s.y * p) * absScale;

                    var edge20 = math.normalize(vertices2 - vertices0);
                    var edge01 = math.normalize(vertices0 - vertices1);
                    var edge12 = math.normalize(vertices1 - vertices2);

                    var angle0 = math.dot(edge20, -edge01);
                    var angle1 = math.dot(edge01, -edge12);
                    var angle2 = math.dot(edge12, -edge20);
                    var weight0 = math.acos(math.clamp(angle0, -1.0, 1.0));
                    var weight1 = math.acos(math.clamp(angle1, -1.0, 1.0));
                    var weight2 = math.acos(math.clamp(angle2, -1.0, 1.0));

                    triTangents[index0] = weight0 * tangent;
                    triTangents[index1] = weight1 * tangent;
                    triTangents[index2] = weight2 * tangent;

                    triBinormals[index0] = weight0 * binormal;
                    triBinormals[index1] = weight1 * binormal;
                    triBinormals[index2] = weight2 * binormal;
                }

                for (int v = 0; v < positions.Length; ++v)
                {
                    var originalTangent  = triTangents[v];
                    var originalBinormal = triBinormals[v];
                    var normal           = (double3)normals[v];

                    var dotTangent = math.dot(normal, originalTangent);
                    var newTangent = new double3(originalTangent.x - dotTangent * normal.x, 
                                                 originalTangent.y - dotTangent * normal.y, 
                                                 originalTangent.z - dotTangent * normal.z);
                    var tangentMagnitude = math.length(newTangent);
                    newTangent /= tangentMagnitude;

                    var dotBinormal = math.dot(normal, originalBinormal);
                    dotTangent      = math.dot(newTangent, originalBinormal) * tangentMagnitude;
                    var newBinormal = new double3(originalBinormal.x - dotBinormal * normal.x - dotTangent * newTangent.x,
                                                  originalBinormal.y - dotBinormal * normal.y - dotTangent * newTangent.y,
                                                  originalBinormal.z - dotBinormal * normal.z - dotTangent * newTangent.z);
                    var binormalMagnitude = math.length(newBinormal);
                    newBinormal /= binormalMagnitude;

                    const double kNormalizeEpsilon = 1e-6;
                    if (tangentMagnitude <= kNormalizeEpsilon || binormalMagnitude <= kNormalizeEpsilon)
                    {
                        var dpXN = math.abs(math.dot(new double3(1, 0, 0), normal));
                        var dpYN = math.abs(math.dot(new double3(0, 1, 0), normal));
                        var dpZN = math.abs(math.dot(new double3(0, 0, 1), normal));

                        double3 axis1, axis2;
                        if (dpXN <= dpYN && dpXN <= dpZN)
                        {
                            axis1 = new double3(1,0,0);
                            axis2 = (dpYN <= dpZN) ? new double3(0, 1, 0) : new double3(0, 0, 1);
                        }
                        else if (dpYN <= dpXN && dpYN <= dpZN)
                        {
                            axis1 = new double3(0, 1, 0);
                            axis2 = (dpXN <= dpZN) ? new double3(1, 0, 0) : new double3(0, 0, 1);
                        }
                        else
                        {
                            axis1 = new double3(0, 0, 1);
                            axis2 = (dpXN <= dpYN) ? new double3(1, 0, 0) : new double3(0, 1, 0);
                        }

                        newTangent  = axis1 - math.dot(normal, axis1) * normal;
                        newBinormal = axis2 - math.dot(normal, axis2) * normal - math.dot(newTangent, axis2) * math.normalizesafe(newTangent);

                        newTangent  = math.normalizesafe(newTangent);
                        newBinormal = math.normalizesafe(newBinormal);
                    }

                    var dp = math.dot(math.cross(normal, newTangent), newBinormal);
                    tangents[v] = new float4((float3)newTangent.xyz, (dp > 0) ? 1 : -1);
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
            if (subMeshCounts.IsCreated &&
                subMeshCounts.Length > 0)
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