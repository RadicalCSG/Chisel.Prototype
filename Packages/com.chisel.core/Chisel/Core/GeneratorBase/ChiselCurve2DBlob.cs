using System;
using Debug = UnityEngine.Debug;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Profiling;
using UnitySceneExtensions;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    // TODO: put somewhere else
    public struct SegmentVertex
    {
        public float2   position;
        public int      segmentIndex;
    }

    public struct ChiselCurve2DBlob
    {
        public struct Point
        {
            public float2 position;
            public float2 tangent1;
            public float2 tangent2;
            public ControlPointConstraint constraint1;
            public ControlPointConstraint constraint2;
        }

        public bool closed;
        public BlobArray<Point> controlPoints;

        static Point Convert(CurveControlPoint2D srcPoint)
        {
            return new Point
            {
                position = srcPoint.position,
                tangent1 = srcPoint.tangent1,
                tangent2 = srcPoint.tangent2,
                constraint1 = srcPoint.constraint1,
                constraint2 = srcPoint.constraint2
            };
        }

        [BurstCompile]
        static float2 PointOnBezier(float2 p0, float2 p1, float2 p2, float2 p3, float t)
        {
            return (1 - t) * (1 - t) * (1 - t) * p0 + 3 * t * (1 - t) * (1 - t) * p1 + 3 * t * t * (1 - t) * p2 + t * t * t * p3;
        }

        [BurstCompile]
        public void GetPathVertices(int shapeCurveSegments, NativeList<SegmentVertex> shapeVertices)
        {
            var length = controlPoints.Length;

            for (int i = 0; i < length; i++)
            {
                var index1 = i;
                var index2 = (i + 1) % length;
                var p1 = controlPoints[index1];
                var p2 = controlPoints[index2];
                var v1 = p1.position;
                var v2 = p2.position;

                if (shapeCurveSegments == 0 ||
                    (controlPoints[index1].constraint2 == ControlPointConstraint.Straight &&
                     controlPoints[index2].constraint1 == ControlPointConstraint.Straight))
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
        
        [BurstCompile]
        public void GetPathVertices(int shapeCurveSegments, out UnsafeList<SegmentVertex> shapeVertices, Allocator allocator)
        {
            var length = controlPoints.Length;

            shapeVertices = new UnsafeList<SegmentVertex>(length * (1 + math.max(1, shapeCurveSegments)), allocator);
            for (int i = 0; i < length; i++)
            {
                var index1 = i;
                var index2 = (i + 1) % length;
                var p1 = controlPoints[index1];
                var p2 = controlPoints[index2];
                var v1 = p1.position;
                var v2 = p2.position;

                if (shapeCurveSegments == 0 ||
                    (controlPoints[index1].constraint2 == ControlPointConstraint.Straight &&
                     controlPoints[index2].constraint1 == ControlPointConstraint.Straight))
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

        [BurstCompile]
        static float CalculateOrientation(NativeList<SegmentVertex> vertices, Range range)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var direction = 0.0f;
            var prevVertex = vertices[range.end - 1].position;
            for (int n = range.start; n < range.end; n++)
            {
                var currVertex = vertices[n].position;
                direction += (prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y);
                prevVertex = currVertex;
            }
            return direction;
        }

        [BurstCompile]
        static float CalculateOrientation(UnsafeList<SegmentVertex> vertices, Range range)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var direction = 0.0f;
            var prevVertex = vertices[range.end - 1].position;
            for (int n = range.start; n < range.end; n++)
            {
                var currVertex = vertices[n].position;
                direction += (prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y);
                prevVertex = currVertex;
            }
            return direction;
        }

        [BurstCompile]
        public bool ConvexPartition(int curveSegments, out NativeList<SegmentVertex> polygonVerticesArray, out NativeList<int> polygonVerticesSegments, Allocator allocator)
        {
            using (var shapeVertices = new NativeList<SegmentVertex>(Allocator.Temp))
            {
                GetPathVertices(curveSegments, shapeVertices);

                polygonVerticesArray = new NativeList<SegmentVertex>(allocator);
                polygonVerticesSegments = new NativeList<int>(allocator);

                //Profiler.BeginSample("ConvexPartition");
                if (shapeVertices.Length == 3)
                { 
                    polygonVerticesArray.ResizeUninitialized(3);
                    polygonVerticesArray[0] = shapeVertices[0];
                    polygonVerticesArray[1] = shapeVertices[1];
                    polygonVerticesArray[2] = shapeVertices[2];

                    polygonVerticesSegments.ResizeUninitialized(1);
                    polygonVerticesSegments[0] = polygonVerticesArray.Length;
                } else
                {
                    if (!External.BayazitDecomposerBursted.ConvexPartition(shapeVertices,
                                                                           polygonVerticesArray,
                                                                           polygonVerticesSegments))
                    {
                        polygonVerticesArray.Dispose();
                        polygonVerticesSegments.Dispose();
                        polygonVerticesArray    = default;
                        polygonVerticesSegments = default;
                        return false;
                    }

                    for (int i = 0; i < polygonVerticesSegments.Length; i++)
                    {
                        var range = new Range
                        {
                            start   = i == 0 ? 0 : polygonVerticesSegments[i - 1],
                            end     =              polygonVerticesSegments[i    ]
                        };

                        if (CalculateOrientation(polygonVerticesArray, range) < 0)
                            External.BayazitDecomposerBursted.Reverse(polygonVerticesArray, range);
                    }
                }
                //Profiler.EndSample();

                //Debug.Assert(polygonVerticesArray.Length == 0 || polygonVerticesArray.Length == polygonVerticesSegments[polygonVerticesSegments.Length - 1]);
                return true;
            }
        }

        [BurstCompile]
        public bool ConvexPartition(int curveSegments, out UnsafeList<SegmentVertex> polygonVerticesArray, out UnsafeList<int> polygonVerticesSegments, Allocator allocator)
        {
            using (var shapeVertices = new NativeList<SegmentVertex>(Allocator.Temp))
            {
                GetPathVertices(curveSegments, shapeVertices);

                //Profiler.BeginSample("ConvexPartition");
                if (shapeVertices.Length == 3)
                {
                    polygonVerticesArray = new UnsafeList<SegmentVertex>(3, allocator);
                    polygonVerticesSegments = new UnsafeList<int>(1, allocator);

                    polygonVerticesArray.Resize(3, NativeArrayOptions.UninitializedMemory);
                    polygonVerticesArray[0] = shapeVertices[0];
                    polygonVerticesArray[1] = shapeVertices[1];
                    polygonVerticesArray[2] = shapeVertices[2];

                    polygonVerticesSegments.Resize(1, NativeArrayOptions.UninitializedMemory);
                    polygonVerticesSegments[0] = polygonVerticesArray.Length;
                } else
                {
                    polygonVerticesArray    = new UnsafeList<SegmentVertex>(shapeVertices.Length * math.max(1, shapeVertices.Length / 2), allocator);
                    polygonVerticesSegments = new UnsafeList<int>(shapeVertices.Length, allocator);
                    if (!External.BayazitDecomposerBursted.ConvexPartition(shapeVertices,
                                                                           ref polygonVerticesArray,
                                                                           ref polygonVerticesSegments))
                    {
                        polygonVerticesArray.Dispose();
                        polygonVerticesSegments.Dispose();
                        polygonVerticesArray    = default;
                        polygonVerticesSegments = default;
                        return false;
                    }

                    for (int i = 0; i < polygonVerticesSegments.Length; i++)
                    {
                        var range = new Range
                        {
                            start   = i == 0 ? 0 : polygonVerticesSegments[i - 1],
                            end     =              polygonVerticesSegments[i    ]
                        };

                        if (CalculateOrientation(polygonVerticesArray, range) < 0)
                            External.BayazitDecomposerBursted.Reverse(polygonVerticesArray, range);
                    }
                }
                //Profiler.EndSample();

                //Debug.Assert(polygonVerticesArray.Length == 0 || polygonVerticesArray.Length == polygonVerticesSegments[polygonVerticesSegments.Length - 1]);
                return true;
            }
        }

        public static BlobAssetReference<ChiselCurve2DBlob> Convert(Curve2D curve, Allocator allocator)
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ChiselCurve2DBlob>();
                root.closed = curve.closed;
                var srcControlPoints = curve.controlPoints;
                var dstControlPoints = builder.Allocate(ref root.controlPoints, srcControlPoints.Length);
                // TODO: just use fixed-array + memcpy
                for (int i = 0; i < srcControlPoints.Length; i++)
                    dstControlPoints[i] = Convert(srcControlPoints[i]);
                return builder.CreateBlobAssetReference<ChiselCurve2DBlob>(allocator);
            }
        }
    }
}