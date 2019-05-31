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

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateTorusVertices(ChiselTorusDefinition definition, ref Vector3[] vertices)
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

        public static bool GenerateTorus(ref BrushMesh[] brushMeshes, ref ChiselTorusDefinition definition)
        {
            definition.Validate();
            Vector3[] vertices = null;
            if (!GenerateTorusVertices(definition, ref vertices))
            {
                brushMeshes = null;
                return false;
            }
            
            var tubeRadiusX		= (definition.tubeWidth  * 0.5f);
            var tubeRadiusY		= (definition.tubeHeight * 0.5f);
            var torusRadius		= (definition.outerDiameter * 0.5f) - tubeRadiusX;
        
    
            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;
            

            if (brushMeshes == null ||
                brushMeshes.Length != horzSegments)
            {
                brushMeshes = new BrushMesh[horzSegments];
                for (int i = 0; i < brushMeshes.Length; i++)
                    brushMeshes[i] = new BrushMesh();
            }


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
                
                var brushMesh = new BrushMesh();
                BrushMeshFactory.CreateExtrudedSubMesh(ref brushMesh, vertSegments, descriptionIndex, 0, 1, subMeshVertices, in definition.surfaceDefinition);
                if (!brushMesh.Validate())
                {
                    brushMeshes = null;
                    return false;
                }
                brushMeshes[h-1] = brushMesh;
            }

            return true;
        }
    }
}