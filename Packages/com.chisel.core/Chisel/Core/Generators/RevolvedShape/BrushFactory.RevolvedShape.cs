using System;
using System.Linq;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnitySceneExtensions;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Split2DPolygonAlongOriginXAxis(NativeList<SegmentVertex> polygonVerticesList, NativeList<int> polygonVerticesSegments, int defaultSegment = 0)
        {
            const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;
            for (int r = polygonVerticesSegments.Length - 1; r >= 0; r--)
            {
                var start   = r == 0 ? 0 : polygonVerticesSegments[r - 1];
                var end     =              polygonVerticesSegments[r    ];

                var positiveSide = 0;
                var negativeSide = 0;
                for (int i = start; i < end; i++)
                {
                    var x = polygonVerticesList[i].position.x;
                    if (x < -kEpsilon) { negativeSide++; if (positiveSide > 0) break; }
                    if (x >  kEpsilon) { positiveSide++; if (negativeSide > 0) break; }
                }
                if (negativeSide == 0 ||
                    positiveSide == 0)
                    return;

                using (var polygon = new NativeList<SegmentVertex>(Allocator.Temp))
                {
                    for (int a = end - 1, b = start; b < end; a = b, b++)
                    {
                        var point_a = polygonVerticesList[a];
                        var point_b = polygonVerticesList[b];

                        var x_a = point_a.position.x;
                        var y_a = point_a.position.y;

                        var x_b = point_b.position.x;
                        var y_b = point_b.position.y;

                        if (!(x_a <= kEpsilon && x_b <= kEpsilon) &&
                            !(x_a >= -kEpsilon && x_b >= -kEpsilon))
                        {

                            // *   .
                            //  \  .
                            //   \ .
                            //    \.
                            //     *
                            //     .\
                            //     . \
                            //     .  \
                            //     .   *

                            if (x_b < x_a) { var t = x_a; x_a = x_b; x_b = t; t = y_a; y_a = y_b; y_b = t; }

                            var x_s = (x_a - x_b);
                            var y_s = (y_a - y_b);

                            var intersection = new float2(0, y_b - (y_s * (x_b / x_s)));
                            polygon.Add(new SegmentVertex { position = intersection, segmentIndex = defaultSegment });
                        }
                        polygon.Add(point_b);
                    }

                    polygonVerticesList.RemoveRangeWithBeginEnd(start, end);
                    polygonVerticesSegments.RemoveAt(r);
                    var delta = end - start;
                    for (; r < polygonVerticesSegments.Length; r++)
                        polygonVerticesSegments[r] -= delta;

                    // TODO: set this to 0 after the topology can handle this situation (zero area polygon) 
                    const float kCenter = 0.0f;

                    // positive side polygon
                    for (int i = 0; i < polygon.Length; i++)
                    {
                        var v = polygon[i];
                        var p = v.position;
                        if (p.x < -kEpsilon)
                            continue;
                        if (p.x > kEpsilon)
                            polygonVerticesList.Add(v); 
                        else
                            polygonVerticesList.Add(new SegmentVertex { position = new float2(kCenter, p.y), segmentIndex = defaultSegment });
                    }
                    polygonVerticesSegments.Add(polygonVerticesList.Length);


                    // negative side polygon (reversed)
                    for (int i = polygon.Length - 1; i >= 0; i--)
                    {
                        var v = polygon[i];
                        var p = v.position;
                        if (p.x > kEpsilon)
                            continue;
                        if (p.x < -kEpsilon)
                            polygonVerticesList.Add(v); 
                        else
                            polygonVerticesList.Add(new SegmentVertex { position = new float2(-kCenter, p.y), segmentIndex = defaultSegment });
                    }
                    polygonVerticesSegments.Add(polygonVerticesList.Length);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<float4x4> GetCircleMatrices(int segments, float3 axis, Allocator allocator)
        {
            return GetCircleMatrices(segments, axis, 360, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<float4x4> GetCircleMatrices(int segments, float3 axis, float totalAngle, Allocator allocator)
        {
            var radiansPerSegment = math.radians(totalAngle / segments);

            segments++;

            var matrices = new NativeList<float4x4>(segments, allocator);
            matrices.ResizeUninitialized(segments);
            for (int s = 0; s < segments; s++)
            {
                var hRadians = (s * radiansPerSegment);
                var rotation = quaternion.AxisAngle(axis, hRadians);
                matrices[s] = float4x4.TRS(float3.zero, rotation, new float3(1));
            }
            return matrices;
        }

        public static bool GenerateRevolvedShape(ref ChiselBrushContainer brushContainer, ref ChiselRevolvedShapeDefinition definition)
        { 
            definition.Validate();
        
            
            var shapeVertices		= new List<SegmentVertex>();
            BrushMeshFactory.GetPathVertices(definition.shape, definition.curveSegments, shapeVertices);

            SegmentVertex[][] polygonVerticesArray;

            if (!Decomposition.ConvexPartition(shapeVertices, 
                                           out polygonVerticesArray))
                return false;
            
            // TODO: splitting it before we do the composition would be better

            var polygonVerticesList		= polygonVerticesArray.ToList();
            for (int i = polygonVerticesList.Count - 1; i >= 0; i--)
            {
                Split2DPolygonAlongOriginXAxis(polygonVerticesList, i);
            }

            var brushMeshesList			= new List<BrushMesh>();
            var horzSegments			= definition.revolveSegments;//horizontalSegments;
            var horzDegreePerSegment	= definition.totalAngle / horzSegments;


            // TODO: make this work when intersecting rotation axis
            //			1. split polygons along rotation axis
            //			2. if edge lies on rotation axis, make sure we don't create infinitely thin quad
            //					collapse this quad, or prevent this from happening
            // TODO: share this code with torus generator
            for (int p = 0; p < polygonVerticesList.Count; p++)
            {
                var polygonVertices		= polygonVerticesList[p];
                
                var vertSegments		= polygonVertices.Length;
                var descriptionIndex	= new int[2 + vertSegments];
            
                descriptionIndex[0] = 0;
                descriptionIndex[1] = 1;
            
                for (int v = 0; v < vertSegments; v++)
                {
                    descriptionIndex[v + 2] = 2;
                }
                
                var horzOffset		= definition.startAngle;
                for (int h = 1, pr = 0; h < horzSegments + 1; pr = h, h++)
                {
                    var hDegree0 = math.radians((pr * horzDegreePerSegment) + horzOffset);
                    var hDegree1 = math.radians((h  * horzDegreePerSegment) + horzOffset);
                    var rotation0 = quaternion.AxisAngle(Vector3.forward, hDegree0);
                    var rotation1 = quaternion.AxisAngle(Vector3.forward, hDegree1);
                    var subMeshVertices = new Vector3[vertSegments * 2];
                    for (int v = 0; v < vertSegments; v++)
                    {
                        var polygonVertex = polygonVertices[v].position;
                        subMeshVertices[v               ] = math.mul(rotation0, new Vector3(polygonVertex.x, 0, polygonVertex.y));
                        subMeshVertices[v + vertSegments] = math.mul(rotation1, new Vector3(polygonVertex.x, 0, polygonVertex.y));
                    }

                    var brushMesh = new BrushMesh();
                    if (!BrushMeshFactory.CreateExtrudedSubMesh(ref brushMesh, vertSegments, descriptionIndex, 0, 1, subMeshVertices, in definition.surfaceDefinition))
                        continue;

                    if (!brushMesh.Validate())
                        return false;
                    brushMeshesList.Add(brushMesh);
                }
            }

            brushContainer.CopyFrom(brushMeshesList);
            return true;
        }

        static void Split2DPolygonAlongOriginXAxis(List<SegmentVertex[]> polygons, int index, int defaultSegment = 0)
        {
            const float kEpsilon = 0.0001f;
            var positiveSide = 0;
            var negativeSide = 0;
            var polygonArray = polygons[index];
            for (int i = 0; i < polygonArray.Length; i++)
            {
                var x = polygonArray[i].position.x;
                if (x < -kEpsilon) { negativeSide++; if (positiveSide > 0) break; }
                if (x >  kEpsilon) { positiveSide++; if (negativeSide > 0) break; }
            }
            if (negativeSide == 0 || 
                positiveSide == 0)
                return;

            
            var polygon = polygons[index].ToList();
            for (int j = polygon.Count - 1,i = 0; i < polygon.Count; j = i, i++)
            {
                var point_i = polygon[i].position;
                var point_j = polygon[j].position;
                var x_j = point_j.x;
                var y_j = point_j.y;

                var x_i = point_i.x;
                var y_i = point_i.y;

                if (x_j <= kEpsilon && x_i <= kEpsilon)
                    continue;
                
                if (x_j >= -kEpsilon && x_i >= -kEpsilon)
                    continue;
                
                // *   .
                //  \  .
                //   \ .
                //    \.
                //     *
                //     .\
                //     . \
                //     .  \
                //     .   *

                if (x_i < x_j) { var t = x_j; x_j = x_i; x_i = t; t = y_j; y_j = y_i; y_i = t; }

                var x_s = (x_j - x_i);
                var y_s = (y_j - y_i);
                    
                var intersection = new float2(0, y_i - (y_s * (x_i / x_s)));
                polygon.Insert(i, new SegmentVertex { position = intersection, segmentIndex = defaultSegment });
                //j = (i + (polygon.Count - 1)) % polygon.Count;
            }

            // TODO: set this to 0 after the topology can handle this situation (zero area polygon) 
            const float kCenter = 0.0f;

            var positivePolygon = new List<SegmentVertex>();
            var negativePolygon = new List<SegmentVertex>();

            // positive side polygon
            for (int i = 0; i < polygon.Count; i++)
            {
                var v = polygon[i];
                var p = v.position;
                if (p.x < -kEpsilon)
                    continue;
                if (p.x > kEpsilon)
                    positivePolygon.Add(v);
                else
                    positivePolygon.Add(new SegmentVertex { position = new float2(kCenter, p.y), segmentIndex = defaultSegment });
            }
            
            // negative side polygon (reversed)
            for (int i = polygon.Count - 1; i >= 0; i--)
            {
                var v = polygon[i];
                var p = v.position;
                if (p.x > kEpsilon)
                    continue;
                if (p.x < -kEpsilon)
                    negativePolygon.Add(v);
                else
                    negativePolygon.Add(new SegmentVertex { position = new float2(-kCenter, p.y), segmentIndex = defaultSegment });
            }

            polygons[index] = positivePolygon.ToArray();
            polygons.Insert(index, negativePolygon.ToArray());
        }
    }
}