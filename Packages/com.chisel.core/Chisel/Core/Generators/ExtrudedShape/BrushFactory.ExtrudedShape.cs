using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        static bool GetExtrudedVertices(UnsafeList<SegmentVertex> shapeVertices, Range range, Matrix4x4 matrix0, Matrix4x4 matrix1, in ChiselBlobBuilder builder, ref BrushMeshBlob root, out ChiselBlobBuilderArray<float3> localVertices, out NativeArray<int> segmentIndices, Allocator allocator)
        {
            const int pathSegments = 2;
            var rangeLength = range.Length;
            var vertexCount = rangeLength * pathSegments;

            localVertices = default;
            segmentIndices = default;
            if (range.Length <= 0)
            {
                Debug.LogError($"range.Length <= 0");
                return false;
            }

            if (range.start < 0)
            {
                Debug.LogError($"range.start < 0");
                return false;
            }

            if (range.end > shapeVertices.Length)
            {
                Debug.LogError($"range.end {range.end} > shapeVertices.length {shapeVertices.Length}");
                return false;
            }


            localVertices = builder.Allocate(ref root.localVertices, vertexCount);
            segmentIndices = new NativeArray<int>(vertexCount, allocator);

            for (int s = range.start, v = 0; s < range.end; s++, v++)
            {
                Debug.Assert(s < shapeVertices.Length);
                var srcPoint   = shapeVertices[s].position;
                var srcSegment = shapeVertices[s].segmentIndex;
                var srcPoint3  = new float3(srcPoint.x, srcPoint.y, 0);
                localVertices[              v] = matrix0.MultiplyPoint(srcPoint3);
                localVertices[rangeLength + v] = matrix1.MultiplyPoint(srcPoint3);
                segmentIndices[              v] = srcSegment + 2;
                segmentIndices[rangeLength + v] = srcSegment + 2;
                Debug.Assert(rangeLength + v < vertexCount);
            }
            return true;
        }

        [BurstCompile]
        public static unsafe bool GenerateExtrudedShape(NativeList<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshes, 
                                                        in UnsafeList<SegmentVertex>    polygonVerticesArray, 
                                                        in UnsafeList<int>              polygonVerticesSegments,
                                                        in UnsafeList<float4x4>         pathMatrices,
                                                        in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                        Allocator allocator)
        {
            // TODO: make each extruded quad split into two triangles when it's not a perfect plane,
            //			split it to make sure it's convex

            // TODO: make it possible to smooth (parts) of the shape

            // TODO: make materials work well
            // TODO: make it possible to 'draw' shapes on any surface

            // TODO: make path work as a spline, with subdivisions
            // TODO:	make this work well with twisted rotations
            // TODO: make shape/path subdivisions be configurable / automatic

            if (!brushMeshes.IsCreated)
            {
                Debug.Log($"brushMeshes.IsCreated {brushMeshes.IsCreated}");
                return false;
            }

            int brushMeshIndex = 0;

            //Profiler.BeginSample("CreateExtrudedSubMeshes");
            for (int p = 0; p < polygonVerticesSegments.Length; p++)
            {
                var range = new Range
                { 
                    start = p == 0 ? 0 : polygonVerticesSegments[p - 1],
                    end   =              polygonVerticesSegments[p    ]
                };

                for (int s = 1; s < pathMatrices.Length; s++)
                {
                    var matrix0 = pathMatrices[s - 1];
                    var matrix1 = pathMatrices[s];

                    var polygonVertex4      = new float4(polygonVerticesArray[range.start].position, 0, 1);
                    var distanceToBottom    = math.mul(math.inverse(matrix0), math.mul(matrix1, polygonVertex4)).z;
                     
                    if (distanceToBottom < 0) { var m = matrix0; matrix0 = matrix1; matrix1 = m; }

                    if (brushMeshIndex >= brushMeshes.Length)
                    {
                        Debug.Log($"{brushMeshIndex} >= {brushMeshes.Length}");
                        return false;
                    }
                    brushMeshes[brushMeshIndex] = ChiselBlobAssetReference<BrushMeshBlob>.Null;

                    using var builder = new ChiselBlobBuilder(Allocator.Temp);
                    ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                    
                    if (!GetExtrudedVertices(polygonVerticesArray, range, matrix0, matrix1, in builder, ref root, out var localVertices, out var segmentIndices, Allocator.Temp))
                        continue;

                    using (segmentIndices)
                    {
                        CreateExtrudedSubMesh(range.Length, (int*)segmentIndices.GetUnsafePtr(), segmentIndices.Length, 0, 1, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out var polygons, out var halfEdges);

                        if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                            return false;

                        var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                        root.localPlaneCount = polygons.Length;
                        // TODO: calculate corner planes
                        var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                        CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                        UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                        root.localBounds = CalculateBounds(in localVertices);
                        brushMeshes[brushMeshIndex] = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                        brushMeshIndex++;
                    }
                }
            }
            //Profiler.EndSample();
            return true;
        }

        static Vector2 PointOnBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            return (1 - t) * (1 - t) * (1 - t) * p0 + 3 * t * (1 - t) * (1 - t) * p1 + 3 * t * t * (1 - t) * p2 + t * t * t * p3;
        }

        public static void GetPathVertices(Curve2D shape, int shapeCurveSegments, List<SegmentVertex> shapeVertices)
        {
            var points = shape.controlPoints;
            var length = points.Length;

            for (int i = 0; i < length; i++)
            {
                var index1 = i;
                var index2 = (i + 1) % length;
                var p1 = points[index1];
                var p2 = points[index2];
                var v1 = p1.position;
                var v2 = p2.position;

                if (shapeCurveSegments == 0 ||
                    (points[index1].constraint2 == ControlPointConstraint.Straight &&
                     points[index2].constraint1 == ControlPointConstraint.Straight))
                {
                    shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                    continue;
                }

                float2 v0, v3;

                if (p1.constraint2 != ControlPointConstraint.Straight)
                    v0 = v1 - p1.tangent2;
                else
                    v0 = v1;
                if (p2.constraint1 != ControlPointConstraint.Straight)
                    v3 = v2 - p2.tangent1;
                else
                    v3 = v2;

                shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                for (int n = 1; n < shapeCurveSegments; n++)
                {
                    shapeVertices.Add(new SegmentVertex { position = PointOnBezier(v1, v0, v3, v2, n / (float)shapeCurveSegments), segmentIndex = i });
                }
            }
        }
    }
}