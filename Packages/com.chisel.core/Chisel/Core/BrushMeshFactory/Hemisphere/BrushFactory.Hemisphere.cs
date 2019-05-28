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
        public static bool GenerateHemisphere(ref BrushMesh brushMesh, ref CSGHemisphereDefinition definition)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateHemisphereSubMesh(ref brushMesh, definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, definition.brushMaterials, definition.surfaceDescriptions);
        }

        public static bool GenerateHemisphereVertices(ref CSGHemisphereDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateHemisphereVertices(definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, ref vertices);
        }

        public static bool GenerateHemisphereVertices(Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, ref Vector3[] vertices)
        {
            var bottomCap		= true;
            var topCap			= false;
            var extraVertices	= ((!bottomCap) ? 1 : 0) + ((!topCap) ? 1 : 0);

            var rings			= (vertSegments) + (topCap ? 1 : 0);
            var vertexCount		= (horzSegments * rings) + extraVertices;

            var topVertex		= 0;
            var bottomVertex	= (!topCap) ? 1 : 0;
            var radius			= new Vector3(diameterXYZ.x * 0.5f, 
                                              diameterXYZ.y, 
                                              diameterXYZ.z * 0.5f);

            if (vertices == null || 
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];
            if (!topCap   ) vertices[topVertex   ] = transform.MultiplyPoint(Vector3.up * radius.y);  // top
            if (!bottomCap) vertices[bottomVertex] = transform.MultiplyPoint(Vector3.zero);					 // bottom
            var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : 0.5f * degreePerSegment;
            var vertexIndex			= extraVertices;
            {
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(Mathf.Cos(hRad) * radius.x,  
                                                                                0.0f, 
                                                                                Mathf.Sin(hRad) * radius.z));
                }
            }
            for (int v = 1; v < rings; v++)
            {
                var segmentFactor	= ((v - (rings / 2.0f)) / rings) + 0.5f;			// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);								// [0 .. 90]
                var segmentHeight	= Mathf.Sin(segmentDegree * Mathf.Deg2Rad) * radius.y;
                var segmentRadius	= Mathf.Cos(segmentDegree * Mathf.Deg2Rad);		// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + extraVertices].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + extraVertices].z * segmentRadius; 
                }
            }
            return true;
        }

        public static bool GenerateHemisphereSubMesh(ref BrushMesh brushMesh, Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            if (diameterXYZ.x == 0 ||
                diameterXYZ.y == 0 ||
                diameterXYZ.z == 0)
            {
                brushMesh.Clear();
                return false;
            }

            var bottomCap		= true;
            var topCap			= false;
            var extraVertices	= ((!bottomCap) ? 1 : 0) + ((!topCap) ? 1 : 0);

            var rings			= (vertSegments) + (topCap ? 1 : 0);
            var vertexCount		= (horzSegments * rings) + extraVertices;

            var topVertex		= 0;
            var bottomVertex	= (!topCap) ? 1 : 0;
            var radius			= new Vector3(diameterXYZ.x * 0.5f, 
                                              diameterXYZ.y, 
                                              diameterXYZ.z * 0.5f);

            var heightY = radius.y;
            float topY, bottomY;
            if (heightY < 0)
            {
                topY = 0;
                bottomY = heightY;
            } else
            {
                topY = heightY;
                bottomY = 0;
            }

            var vertices = new Vector3[vertexCount];
            if (!topCap   ) vertices[topVertex   ] = transform.MultiplyPoint(Vector3.up * topY);    // top
            if (!bottomCap) vertices[bottomVertex] = transform.MultiplyPoint(Vector3.up * bottomY); // bottom
            var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : 0.5f * degreePerSegment;
            var vertexIndex			= extraVertices;
            if (heightY < 0)
            {
                for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(Mathf.Cos(hRad) * radius.x,
                                                                                0.0f,
                                                                                Mathf.Sin(hRad) * radius.z));
                }
            } else
            {
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(Mathf.Cos(hRad) * radius.x,  
                                                                                0.0f, 
                                                                                Mathf.Sin(hRad) * radius.z));
                }
            }
            for (int v = 1; v < rings; v++)
            {
                var segmentFactor	= ((v - (rings / 2.0f)) / rings) + 0.5f;			// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);								// [0 .. 90]
                var segmentHeight	= Mathf.Sin(segmentDegree * Mathf.Deg2Rad) * heightY;
                var segmentRadius	= Mathf.Cos(segmentDegree * Mathf.Deg2Rad);		// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + extraVertices].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + extraVertices].z * segmentRadius; 
                }
            }

            return BrushMeshFactory.GenerateSegmentedSubMesh(ref brushMesh, horzSegments, vertSegments, vertices, bottomCap, topCap, bottomVertex, topVertex, brushMaterials, surfaceDescriptions);
        }
    }
}