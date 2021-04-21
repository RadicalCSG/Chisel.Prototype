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
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        [BurstCompile]
        static float CalculateOrientation(NativeList<SegmentVertex> vertices, int start, int end)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var direction = 0.0f;
            var prevVertex = vertices[end - 1].position;
            for (int n = start; n < end; n++)
            {
                var currVertex = vertices[n].position;
                direction += (prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y);
                prevVertex = currVertex;
            }
            return direction;
        }

        static bool GetExtrudedVertices(NativeList<SegmentVertex> shapeVertices, Range range, Matrix4x4 matrix0, Matrix4x4 matrix1, in BlobBuilder builder, ref BrushMeshBlob root, out BlobBuilderArray<float3> localVertices, out NativeArray<int> segmentIndices, Allocator allocator)
        {
            const int pathSegments = 2;
            var rangeLength = range.Length;
            var vertexCount = rangeLength * pathSegments;

            localVertices = builder.Allocate(ref root.localVertices, vertexCount);
            segmentIndices = new NativeArray<int>(vertexCount, allocator);

            for (int s = range.start, v = 0; s < range.end; s++, v++)
            {
                var srcPoint    = shapeVertices[s].position;
                var srcSegment  = shapeVertices[s].segmentIndex;
                var srcPoint3   = new float3(srcPoint.x, srcPoint.y, 0);
                localVertices[              v] = matrix0.MultiplyPoint(srcPoint3);
                localVertices[rangeLength + v] = matrix1.MultiplyPoint(srcPoint3);
                segmentIndices[              v] = srcSegment;
                segmentIndices[rangeLength + v] = srcSegment;
            }
            return true;
        }

        [BurstCompile]
        public static unsafe bool GenerateExtrudedShape(NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes, 
                                                        in NativeList<SegmentVertex> polygonVerticesArray, 
                                                        in NativeList<int> polygonVerticesSegments,
                                                        in NativeList<float4x4> pathMatrices,
                                                        in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
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


            int brushMeshIndex = 0;

            brushMeshesList.Clear();
            Profiler.BeginSample("CreateExtrudedSubMeshes");
            for (int p = 0; p < polygonVerticesSegments.Length; p++)
            {
                var range = new Range
                { 
                    start = p == 0 ? 0 : polygonVerticesSegments[p - 1],
                    end   =              polygonVerticesSegments[p    ]
                };

                for (int s = 0; s < pathMatrices.Length - 1; s++)
                {
                    var matrix0 = pathMatrices[s];
                    var matrix1 = pathMatrices[s + 1];

                    // TODO: this doesn't work if top and bottom polygons intersect
                    //			=> need to split into two brushes then, invert one of the two brushes
                    var polygonVertex4      = new float4(polygonVerticesArray[range.start].position, 0, 1);
                    var distanceToBottom    = math.mul(math.inverse(matrix0), math.mul(matrix1, polygonVertex4)).z;
                    if (distanceToBottom < 0) { var m = matrix0; matrix0 = matrix1; matrix1 = m; }

                    brushMeshes[brushMeshIndex] = BlobAssetReference<BrushMeshBlob>.Null;

                    using (var builder = new BlobBuilder(Allocator.Temp))
                    {
                        ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                        BlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges;
                        BlobBuilderArray<BrushMeshBlob.Polygon> polygons;

                        if (!GetExtrudedVertices(polygonVerticesArray, range, matrix0, matrix1, in builder, ref root, out var localVertices, out var segmentIndices, Allocator.Temp))
                            continue;
                        try
                        {
                            CreateExtrudedSubMesh(range.Length, (int*)segmentIndices.GetUnsafePtr(), 0, 1, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out polygons, out halfEdges);
                        }
                        finally
                        {
                            segmentIndices.Dispose();
                        }

                        if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                            return false;

                        var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                        var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                        CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                        UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                        root.localBounds = CalculateBounds(in localVertices);
                        brushMeshes[brushMeshIndex] = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                        brushMeshIndex++;
                    }
                }
            }
            Profiler.EndSample();
            return true;
        }

        static List<SegmentVertex> shapeVertices = new List<SegmentVertex>();
        public static bool ConvexPartition(Curve2D shape, int curveSegments, out List<SegmentVertex> polygonVerticesArray, out List<int> polygonVerticesSegments)
        {
            shapeVertices.Clear();
            GetPathVertices(shape, curveSegments, shapeVertices);

            Profiler.BeginSample("ConvexPartition");
            if (shapeVertices.Count == 3)
            {
                polygonVerticesArray = new List<SegmentVertex>
                {
                    shapeVertices[0],
                    shapeVertices[1],
                    shapeVertices[2]
                };
                polygonVerticesSegments = new List<int> { 3 };
            } else
            {
                polygonVerticesArray = new List<SegmentVertex>();
                polygonVerticesSegments = new List<int>();
                if (!External.BayazitDecomposer.ConvexPartition(shapeVertices,
                                                                polygonVerticesArray,
                                                                polygonVerticesSegments))
                    return false;
            }
            Profiler.EndSample();
            return true;
        }

        static float CalculateOrientation(List<SegmentVertex> vertices, int start, int end)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var direction = 0.0f;
            var prevVertex = vertices[end - 1].position;
            for (int n = start; n < end; n++)
            {
                var currVertex = vertices[n].position;
                direction += (prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y);
                prevVertex = currVertex;
            }
            return direction;
        }

        static List<BrushMesh>      brushMeshesList = new List<BrushMesh>();
        public static bool GenerateExtrudedShape(ref ChiselBrushContainer brushContainer, ref ChiselSurfaceDefinition surfaceDefinition, ref ChiselExtrudedShapeDefinition definition)
        {
            definition.Validate(ref surfaceDefinition);

            ref readonly var shape               = ref definition.shape;
            int              curveSegments       = definition.curveSegments;

            if (!ConvexPartition(shape, curveSegments, out var polygonVerticesArray, out var polygonVerticesSegments))
                return false;

            ref readonly var path                = ref definition.path;
            path.UpgradeIfNecessary();

            // TODO: make each extruded quad split into two triangles when it's not a perfect plane,
            //			split it to make sure it's convex

            // TODO: make it possible to smooth (parts) of the shape

            // TODO: make materials work well
            // TODO: make it possible to 'draw' shapes on any surface

            // TODO: make path work as a spline, with subdivisions
            // TODO:	make this work well with twisted rotations
            // TODO: make shape/path subdivisions be configurable / automatic


            var originalBrushMeshes = brushContainer.brushMeshes;
            int brushMeshIndex = 0;
            int brushMeshCount = (originalBrushMeshes == null) ? 0 : originalBrushMeshes.Length;

            brushMeshesList.Clear();
            Profiler.BeginSample("CreateExtrudedSubMeshes");
            for (int p = 0; p < polygonVerticesSegments.Count; p++)
            {
                var range           = new Range
                { 
                    start           = p == 0 ? 0 : polygonVerticesSegments[p - 1],
                    end             = polygonVerticesSegments[p]
                };
                
                if (CalculateOrientation(polygonVerticesArray, range.start, range.end) < 0)
                {
                    External.BayazitDecomposer.Reverse(polygonVerticesArray, range);
                }

                for (int s = 0; s < path.segments.Length - 1; s++)
                {
                    var pathPointA = path.segments[s];
                    var pathPointB = path.segments[s + 1];
                    int subSegments = 1;
                    var offsetQuaternion = pathPointB.rotation * Quaternion.Inverse(pathPointA.rotation);
                    var offsetEuler = offsetQuaternion.eulerAngles;
                    if (offsetEuler.x > 180) offsetEuler.x = 360 - offsetEuler.x;
                    if (offsetEuler.y > 180) offsetEuler.y = 360 - offsetEuler.y;
                    if (offsetEuler.z > 180) offsetEuler.z = 360 - offsetEuler.z;
                    var maxAngle = math.max(math.max(offsetEuler.x, offsetEuler.y), offsetEuler.z);
                    if (maxAngle != 0)
                        subSegments = math.max(1, (int)math.ceil(maxAngle / 5));

                    if ((pathPointA.scale.x / pathPointA.scale.y) != (pathPointB.scale.x / pathPointB.scale.y) &&
                        (subSegments & 1) == 1)
                        subSegments += 1;

                    for (int n = 0; n < subSegments; n++)
                    {
                        var matrix0 = ChiselPathPoint.Lerp(ref path.segments[s], ref path.segments[s + 1], n / (float)subSegments);
                        var matrix1 = ChiselPathPoint.Lerp(ref path.segments[s], ref path.segments[s + 1], (n + 1) / (float)subSegments);

                        // TODO: this doesn't work if top and bottom polygons intersect
                        //			=> need to split into two brushes then, invert one of the two brushes
                        var polygonVertex = polygonVerticesArray[range.start].position;
                        var invertDot = math.dot(matrix0.MultiplyVector(new Vector3(0,0,1)).normalized, (matrix1.MultiplyPoint(new Vector3(polygonVertex.x, polygonVertex.y, 0)) - matrix0.MultiplyPoint(new Vector3(polygonVertex.x, polygonVertex.y, 0))).normalized);

                        if (invertDot == 0.0f)
                            continue;

                        if (invertDot < 0) { var m = matrix0; matrix0 = matrix1; matrix1 = m; }

                        int[] segmentIndices;
                        float3[] vertices;
                        BrushMesh brushMesh;
                        if (brushMeshIndex >= brushMeshCount)
                        {
                            vertices = null;
                            if (!GetExtrudedVertices(polygonVerticesArray, range, matrix0, matrix1, ref vertices, out segmentIndices))
                                continue;
                            brushMesh = new BrushMesh();
                        } else
                        {
                            brushMesh = originalBrushMeshes[brushMeshIndex];
                            vertices = brushMesh.vertices;
                            if (!GetExtrudedVertices(polygonVerticesArray, range, matrix0, matrix1, ref vertices, out segmentIndices))
                                continue;
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushMesh, range.Length, segmentIndices, 0, 1, vertices, surfaceDefinition);
                        brushMeshesList.Add(brushMesh);
                        brushMeshIndex++;
                    }
                }
            }
            Profiler.EndSample();

            brushContainer.CopyFrom(brushMeshesList);
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

        static bool GetExtrudedVertices(List<SegmentVertex> shapeVertices, Range range, Matrix4x4 matrix0, Matrix4x4 matrix1, ref float3[] vertices, out int[] segmentIndices)
        {
            const int pathSegments = 2;
            var rangeLength = range.Length;
            var vertexCount = rangeLength * pathSegments;
            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new float3[vertexCount];
            segmentIndices = new int[vertexCount];

            for (int s = range.start, v = 0; s < range.end; s++, v++)
            {
                var srcPoint    = shapeVertices[s].position;
                var srcSegment  = shapeVertices[s].segmentIndex;
                var srcPoint3   = new float3(srcPoint.x, srcPoint.y, 0);
                vertices[              v] = matrix0.MultiplyPoint(srcPoint3);
                vertices[rangeLength + v] = matrix1.MultiplyPoint(srcPoint3);
                segmentIndices[              v] = srcSegment;
                segmentIndices[rangeLength + v] = srcSegment;
            }
            return true;
        }
    }
}