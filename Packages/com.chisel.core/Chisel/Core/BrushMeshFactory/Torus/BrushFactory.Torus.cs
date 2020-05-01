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

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateTorus(ref ChiselBrushContainer brushContainer, ref ChiselTorusDefinition definition)
        {
            definition.Validate();
            Vector3[] vertices = null;
            if (!GenerateTorusVertices(definition, ref vertices))
                return false;
            
            var tubeRadiusX		= (definition.tubeWidth  * 0.5f);
            var tubeRadiusY		= (definition.tubeHeight * 0.5f);
            var torusRadius		= (definition.outerDiameter * 0.5f) - tubeRadiusX;
        
    
            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;

            brushContainer.EnsureSize(horzSegments);


            var horzDegreePerSegment	= (definition.totalAngle / horzSegments);
            var vertDegreePerSegment	= math.radians(360.0f / vertSegments);
            var descriptionIndex		= new int[2 + vertSegments];
            
            descriptionIndex[0] = 0;
            descriptionIndex[1] = 1;
            
            var circleVertices	= new Vector3[vertSegments];

            var min = new float2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new float2(float.NegativeInfinity, float.NegativeInfinity);
            var tubeAngleOffset	= math.radians((((vertSegments & 1) == 1) ? 0.0f : ((360.0f / vertSegments) * 0.5f)) + definition.tubeRotation);
            for (int v = 0; v < vertSegments; v++)
            {
                var vRad = tubeAngleOffset + (v * vertDegreePerSegment);
                circleVertices[v] = new Vector3((math.cos(vRad) * tubeRadiusX) - torusRadius, 
                                               (math.sin(vRad) * tubeRadiusY), 0);
                min.x = math.min(min.x, circleVertices[v].x);
                min.y = math.min(min.y, circleVertices[v].y);
                max.x = math.max(max.x, circleVertices[v].x);
                max.y = math.max(max.y, circleVertices[v].y);
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
                var rotation0 = quaternion.AxisAngle(new Vector3(0,1,0), hDegree0);
                var rotation1 = quaternion.AxisAngle(new Vector3(0, 1, 0), hDegree1);
                var subMeshVertices	= new Vector3[vertSegments * 2];
                for (int v = 0; v < vertSegments; v++)
                {
                    subMeshVertices[v + vertSegments] = math.mul(rotation0, circleVertices[v]);
                    subMeshVertices[v] = math.mul(rotation1, circleVertices[v]);
                }
                
                var brushMesh = new BrushMesh();
                BrushMeshFactory.CreateExtrudedSubMesh(ref brushMesh, vertSegments, descriptionIndex, 0, 1, subMeshVertices, in definition.surfaceDefinition);
                if (!brushMesh.Validate())
                    return false;

                brushContainer.brushMeshes[h-1] = brushMesh;
            }
            return true;
        }

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
            var vertDegreePerSegment	= math.radians(360.0f / vertSegments);
            
            var circleVertices	= new Vector3[vertSegments];

            var min = new float2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new float2(float.NegativeInfinity, float.NegativeInfinity);
            var tubeAngleOffset	= math.radians((((vertSegments & 1) == 1) ? 0.0f : ((360.0f / vertSegments) * 0.5f)) + definition.tubeRotation);
            for (int v = 0; v < vertSegments; v++)
            {
                var vRad = tubeAngleOffset + (v * vertDegreePerSegment);
                circleVertices[v] = new Vector3((math.cos(vRad) * tubeRadiusX) - torusRadius, 
                                               (math.sin(vRad) * tubeRadiusY), 0);
                min.x = math.min(min.x, circleVertices[v].x);
                min.y = math.min(min.y, circleVertices[v].y);
                max.x = math.max(max.x, circleVertices[v].x);
                max.y = math.max(max.y, circleVertices[v].y);
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
                var rotation1 = quaternion.AxisAngle(new Vector3(0, 1, 0), hDegree1);
                for (int i = 0; i < vertSegments; i++, v++)
                {
                    vertices[v] = math.mul(rotation1, circleVertices[i]);
                }
            }
            return true;
        }
    }
}