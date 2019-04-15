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
        public static bool GenerateStadiumVertices(CSGStadiumDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            
            var topSides		= definition.topSides;
            var bottomSides		= definition.bottomSides;
            var sides			= definition.sides;

            var length			= definition.length;
            var topLength		= definition.topLength;
            var bottomLength	= definition.bottomLength;
            var diameter		= definition.diameter;
            var radius			= diameter * 0.5f;

            if (vertices == null ||
                vertices.Length != sides * 2)
                vertices		= new Vector3[sides * 2];
            
            var firstTopSide	= definition.firstTopSide;
            var lastTopSide		= definition.lastTopSide;
            var firstBottomSide = definition.firstBottomSide;
            var lastBottomSide  = definition.lastBottomSide;

            var haveCenter		= definition.haveCenter;
            
            int vertexIndex = 0;
            if (!definition.haveRoundedTop)
            {
                vertices[vertexIndex] = new Vector3(-radius, 0, length * -0.5f); vertexIndex++;
                vertices[vertexIndex] = new Vector3( radius, 0, length * -0.5f); vertexIndex++;
            } else
            {
                var degreeOffset		= -180.0f * Mathf.Deg2Rad;
                var degreePerSegment	= (180.0f / topSides) * Mathf.Deg2Rad;
                var center				= new Vector3(0, 0, (length * -0.5f) + topLength);
                for (int s = 0; s <= topSides; s++)
                {
                    var hRad = (s * degreePerSegment) + degreeOffset;

                    var x = center.x + (Mathf.Cos(hRad) * radius);
                    var y = center.y;
                    var z = center.z + (Mathf.Sin(hRad) * topLength);

                    vertices[vertexIndex] = new Vector3(x, y, z);
                    vertexIndex++;
                }
            }

            if (!haveCenter)
                vertexIndex--;

            //vertexIndex = definition.firstBottomSide;
            if (!definition.haveRoundedBottom)
            {
                vertices[vertexIndex] = new Vector3( radius, 0, length * 0.5f); vertexIndex++;
                vertices[vertexIndex] = new Vector3(-radius, 0, length * 0.5f); vertexIndex++;
            } else
            {
                var degreeOffset		= 0.0f * Mathf.Deg2Rad;
                var degreePerSegment	= (180.0f / bottomSides) * Mathf.Deg2Rad;
                var center				= new Vector3(0, 0, (length * 0.5f) - bottomLength);
                for (int s = 0; s <= bottomSides; s++)
                {
                    var hRad = (s * degreePerSegment) + degreeOffset;

                    var x = center.x + (Mathf.Cos(hRad) * radius);
                    var y = center.y;
                    var z = center.z + (Mathf.Sin(hRad) * bottomLength);

                    vertices[vertexIndex] = new Vector3(x, y, z);
                    vertexIndex++;
                }
            }
            
            var extrusion = Vector3.up *  definition.height;
            for (int s = 0; s < sides; s++)
                vertices[s + sides] = vertices[s] + extrusion;
            return true;
        }

        public static bool GenerateStadiumAsset(CSGBrushMeshAsset brushMeshAsset, CSGStadiumDefinition definition)
        {
            Vector3[] vertices = null;
            if (!GenerateStadiumVertices(definition, ref vertices))
            {
                brushMeshAsset.Clear();
                return false;
            }

            var subMeshes		= new[] { new CSGBrushSubMesh() };
            var surfaceIndices	= new int[vertices.Length + 2];
            CreateExtrudedSubMesh(subMeshes[0], definition.sides, surfaceIndices, surfaceIndices, 0, 1, vertices, definition.surfaceAssets, definition.surfaceDescriptions);

            brushMeshAsset.SubMeshes = subMeshes;
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }
    }
}