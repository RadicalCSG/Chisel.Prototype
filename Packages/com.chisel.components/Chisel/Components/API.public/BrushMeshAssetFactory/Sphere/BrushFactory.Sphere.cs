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
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateSphereAsset(CSGBrushMeshAsset brushMeshAsset, CSGSphereDefinition definition)
        {
            var subMesh = new CSGBrushSubMesh();
            if (!GenerateSphereSubMesh(subMesh, definition))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SubMeshes = new[] { subMesh };
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }

        public static bool GenerateSphereSubMesh(CSGBrushSubMesh subMesh, CSGSphereDefinition definition)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateSphereSubMesh(subMesh, definition.diameterXYZ, definition.generateFromCenter, transform, definition.horizontalSegments, definition.verticalSegments, definition.surfaceAssets, definition.surfaceDescriptions);
        }

        public static bool GenerateSphereVertices(CSGSphereDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            BrushMeshFactory.CreateSphereVertices(definition.diameterXYZ, definition.generateFromCenter, definition.horizontalSegments, definition.verticalSegments, ref vertices);
            return true;
        }

        public static bool GenerateSphereSubMesh(CSGBrushSubMesh subMesh, Vector3 diameterXYZ, bool generateFromCenter, Matrix4x4 transform, int horzSegments, int vertSegments, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            if (diameterXYZ.x == 0 ||
                diameterXYZ.y == 0 ||
                diameterXYZ.z == 0)
            {
                subMesh.Clear();
                return false;
            }

            var brushMesh = BrushMeshFactory.CreateSphere(diameterXYZ, generateFromCenter, horzSegments, vertSegments);

            subMesh.HalfEdges = brushMesh.halfEdges;
            subMesh.Vertices = brushMesh.vertices;
            subMesh.Polygons = new CSGBrushSubMesh.Polygon[brushMesh.polygons.Length];

            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                subMesh.Polygons[i] = new CSGBrushSubMesh.Polygon
                {
                    surfaceID = i,
                    edgeCount = brushMesh.polygons[i].edgeCount,
                    firstEdge = brushMesh.polygons[i].firstEdge,
                    surfaceAsset = i < surfaceAssets.Length ? surfaceAssets[i] : surfaceAssets[0],
                    description = i < surfaceDescriptions.Length ? surfaceDescriptions[i] : surfaceDescriptions[0],
                };
            }

            return true;
        }
    }
}