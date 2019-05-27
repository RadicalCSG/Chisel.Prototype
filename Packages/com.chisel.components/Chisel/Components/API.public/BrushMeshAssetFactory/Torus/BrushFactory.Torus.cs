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
        public static bool GenerateTorusVertices(CSGTorusDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            //var surfaces		= definition.brushMaterials;
            //var descriptions	= definition.surfaceDescriptions;
            var tubeRadiusX		= (definition.tubeWidth  * 0.5f);
            var tubeRadiusY		= (definition.tubeHeight * 0.5f);
            var torusRadius		= (definition.outerDiameter * 0.5f) - tubeRadiusX;
        
    
            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;

            var horzDegreePerSegment	= (definition.totalAngle / horzSegments);
            var vertDegreePerSegment	= (360.0f / vertSegments) * Mathf.Deg2Rad;
            
            var circleVertices	= new Vector2[vertSegments];

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var tubeAngleOffset	= ((((vertSegments & 1) == 1) ? 0.0f : ((360.0f / vertSegments) * 0.5f)) + definition.tubeRotation) * Mathf.Deg2Rad;
            for (int v = 0; v < vertSegments; v++)
            {
                var vRad = tubeAngleOffset + (v * vertDegreePerSegment);
                circleVertices[v] = new Vector2((Mathf.Cos(vRad) * tubeRadiusX) - torusRadius, 
                                                (Mathf.Sin(vRad) * tubeRadiusY));
                min.x = Mathf.Min(min.x, circleVertices[v].x);
                min.y = Mathf.Min(min.y, circleVertices[v].y);
                max.x = Mathf.Max(max.x, circleVertices[v].x);
                max.y = Mathf.Max(max.y, circleVertices[v].y);
            }

            if (definition.fitCircle)
            {
                var center = (max + min) * 0.5f;
                var size   = (max - min) * 0.5f;
                size.x = tubeRadiusX / size.x;
                size.y = tubeRadiusY / size.y;
                for (int v = 0; v < vertSegments; v++)
                {
                    circleVertices[v].x = (circleVertices[v].x - center.x) * size.x;
                    circleVertices[v].y = (circleVertices[v].y - center.y) * size.y;
                    circleVertices[v].x -= torusRadius;
                }
            }

            if (definition.totalAngle != 360)
                horzSegments++;
            
            var horzOffset	= definition.startAngle;
            var vertexCount = vertSegments * horzSegments;
            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];
            for (int h = 0, v = 0; h < horzSegments; h++)
            {
                var hDegree1 = (h * horzDegreePerSegment) + horzOffset;
                var rotation1 = Quaternion.AngleAxis(hDegree1, Vector3.up);
                for (int i = 0; i < vertSegments; i++, v++)
                {
                    vertices[v] = rotation1 * circleVertices[i];
                }
            }
            return true;
        }

        public static bool GenerateTorusAsset(CSGBrushMeshAsset brushMeshAsset, CSGTorusDefinition definition)
        {
            Vector3[] vertices = null;
            if (!GenerateTorusVertices(definition, ref vertices))
            {
                brushMeshAsset.Clear();
                return false;
            }

            definition.Validate();
            var brushMaterials	= definition.brushMaterials;
            var descriptions	= definition.surfaceDescriptions;
            var tubeRadiusX		= (definition.tubeWidth  * 0.5f);
            var tubeRadiusY		= (definition.tubeHeight * 0.5f);
            var torusRadius		= (definition.outerDiameter * 0.5f) - tubeRadiusX;
        
    
            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;

            var horzDegreePerSegment	= (definition.totalAngle / horzSegments);
            var vertDegreePerSegment	= (360.0f / vertSegments) * Mathf.Deg2Rad;
            var descriptionIndex		= new int[2 + vertSegments];
            
            descriptionIndex[0] = 0;
            descriptionIndex[1] = 1;
            
            var circleVertices	= new Vector2[vertSegments];

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var tubeAngleOffset	= ((((vertSegments & 1) == 1) ? 0.0f : ((360.0f / vertSegments) * 0.5f)) + definition.tubeRotation) * Mathf.Deg2Rad;
            for (int v = 0; v < vertSegments; v++)
            {
                var vRad = tubeAngleOffset + (v * vertDegreePerSegment);
                circleVertices[v] = new Vector2((Mathf.Cos(vRad) * tubeRadiusX) - torusRadius, 
                                                (Mathf.Sin(vRad) * tubeRadiusY));
                min.x = Mathf.Min(min.x, circleVertices[v].x);
                min.y = Mathf.Min(min.y, circleVertices[v].y);
                max.x = Mathf.Max(max.x, circleVertices[v].x);
                max.y = Mathf.Max(max.y, circleVertices[v].y);
                descriptionIndex[v + 2] = 2;
            }

            if (definition.fitCircle)
            {
                var center = (max + min) * 0.5f;
                var size   = (max - min) * 0.5f;
                size.x = tubeRadiusX / size.x;
                size.y = tubeRadiusY / size.y;
                for (int v = 0; v < vertSegments; v++)
                {
                    circleVertices[v].x = (circleVertices[v].x - center.x) * size.x;
                    circleVertices[v].y = (circleVertices[v].y - center.y) * size.y;
                    circleVertices[v].x -= torusRadius;
                }
            }

            var subMeshes	= new CSGBrushMeshAsset.CSGBrushSubMesh[horzSegments];
            var horzOffset	= definition.startAngle;
            for (int h = 1, p = 0; h < horzSegments + 1; p = h, h++)
            {
                var hDegree0 = (p * horzDegreePerSegment) + horzOffset;
                var hDegree1 = (h * horzDegreePerSegment) + horzOffset;
                var rotation0 = Quaternion.AngleAxis(hDegree0, Vector3.up);
                var rotation1 = Quaternion.AngleAxis(hDegree1, Vector3.up);
                var subMeshVertices	= new Vector3[vertSegments * 2];
                for (int v = 0; v < vertSegments; v++)
                {
                    subMeshVertices[v + vertSegments] = rotation0 * circleVertices[v];
                    subMeshVertices[v] = rotation1 * circleVertices[v];
                }
                
                var subMesh = new CSGBrushMeshAsset.CSGBrushSubMesh();
                CreateExtrudedSubMesh(ref subMesh.brushMesh, vertSegments, descriptionIndex, descriptionIndex, 0, 1, subMeshVertices, brushMaterials, descriptions);
                if (!subMesh.brushMesh.Validate())
                {
                    brushMeshAsset.Clear();
                    return false;
                }
                subMeshes[h-1] = subMesh;
            }
            
            brushMeshAsset.SubMeshes = subMeshes;

            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }
    }
}