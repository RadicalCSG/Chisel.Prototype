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
using Chisel.Assets;
using Chisel.Core;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        static void SplitPolygon(List<Vector2[]> polygons, int index)
        {
            const float kEpsilon = 0.0001f;
            var positiveSide = 0;
            var negativeSide = 0;
            var polygonArray = polygons[index];
            for (int i = 0; i < polygonArray.Length; i++)
            {
                var x = polygonArray[i].x;
                if (x < -kEpsilon) { negativeSide++; if (positiveSide > 0) break; }
                if (x >  kEpsilon) { positiveSide++; if (negativeSide > 0) break; }
            }
            if (negativeSide == 0 || 
                positiveSide == 0)
                return;

            
            var polygon = polygons[index].ToList();
            for (int j = polygon.Count - 1,i = 0; i < polygon.Count; j = i, i++)
            {
                var x_j = polygon[j].x;
                var y_j = polygon[j].y;

                var x_i = polygon[i].x;
                var y_i = polygon[i].y;

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
                    
                var intersection = new Vector2(0, y_i - (y_s * (x_i / x_s)));
                polygon.Insert(i, intersection);
                j = (i + (polygon.Count - 1)) % polygon.Count;
            }

            // TODO: set this to 0 after the topology can handle this situation (zero area polygon) 
            const float kCenter = 0.0f;

            var positivePolygon = new List<Vector2>();
            var negativePolygon = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                if      (polygon[i].x < -kEpsilon) { negativePolygon.Add(p);  }
                else if (polygon[i].x >  kEpsilon) { positivePolygon.Add(p);  }
                else
                {
                    negativePolygon.Add(new Vector2(-kCenter, p.y));
                    positivePolygon.Add(new Vector2( kCenter, p.y));
                }
            }
            positivePolygon.Reverse();
            polygons[index] = negativePolygon.ToArray();
            polygons.Insert(index, positivePolygon.ToArray());
        }

        public static bool GenerateRevolvedShapeAsset(CSGBrushMeshAsset brushMeshAsset, CSGRevolvedShapeDefinition definition)
        {
            definition.Validate();
            var surfaces		= definition.brushMaterials;
            var descriptions	= definition.surfaceDescriptions;
        
            
            var shapeVertices		= new List<Vector2>();
            var shapeSegmentIndices = new List<int>();
            GetPathVertices(definition.shape, definition.curveSegments, shapeVertices, shapeSegmentIndices);
            
            Vector2[][] polygonVerticesArray;
            int[][] polygonIndicesArray;

            if (!Decomposition.ConvexPartition(shapeVertices, shapeSegmentIndices,
                                           out polygonVerticesArray,
                                           out polygonIndicesArray))
            {
                brushMeshAsset.Clear();
                return false;
            }
            
            // TODO: splitting it before we do the composition would be better
            var polygonVerticesList		= polygonVerticesArray.ToList();
            for (int i = polygonVerticesList.Count - 1; i >= 0; i--)
            {
                SplitPolygon(polygonVerticesList, i);
            }

            var subMeshes				= new List<CSGBrushMeshAsset.CSGBrushSubMesh>();
    
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
//				var segmentIndices		= polygonIndicesArray[p];
                var shapeSegments		= polygonVertices.Length;
                
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
                    var hDegree0 = (pr * horzDegreePerSegment) + horzOffset;
                    var hDegree1 = (h * horzDegreePerSegment) + horzOffset;
                    var rotation0 = Quaternion.AngleAxis(hDegree0, Vector3.forward);
                    var rotation1 = Quaternion.AngleAxis(hDegree1, Vector3.forward);
                    var subMeshVertices = new Vector3[vertSegments * 2];
                    for (int v = 0; v < vertSegments; v++)
                    {
                        subMeshVertices[v + vertSegments] = rotation0 * new Vector3(polygonVertices[v].x, 0, polygonVertices[v].y);
                        subMeshVertices[v               ] = rotation1 * new Vector3(polygonVertices[v].x, 0, polygonVertices[v].y);
                    }

                    var subMesh = new CSGBrushMeshAsset.CSGBrushSubMesh();
                    if (!CreateExtrudedSubMesh(ref subMesh.brushMesh, vertSegments, descriptionIndex, descriptionIndex, 0, 1, subMeshVertices, surfaces, descriptions))
                        continue;

                    if (!subMesh.brushMesh.Validate())
                    {
                        brushMeshAsset.Clear();
                        return false;
                    }
                    subMeshes.Add(subMesh);
                }
            }
            
            brushMeshAsset.SubMeshes = subMeshes.ToArray();

            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }
    }
}