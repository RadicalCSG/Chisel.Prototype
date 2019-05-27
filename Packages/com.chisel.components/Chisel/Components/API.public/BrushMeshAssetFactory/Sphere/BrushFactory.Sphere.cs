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
using Chisel.Core;

namespace Chisel.Components
{
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateSphereAsset(ChiselGeneratedBrushes brushMeshAsset, CSGSphereDefinition definition)
        {
            var subMeshes = new[] { new ChiselGeneratedBrushes.ChiselGeneratedBrush() };
            if (!GenerateSphereSubMesh(subMeshes[0], definition))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SubMeshes = subMeshes;
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }

        public static bool GenerateSphereSubMesh(ChiselGeneratedBrushes.ChiselGeneratedBrush subMesh, CSGSphereDefinition definition)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateSphereSubMesh(subMesh, definition.diameterXYZ, definition.offsetY, definition.generateFromCenter, transform, definition.horizontalSegments, definition.verticalSegments, definition.brushMaterials, definition.surfaceDescriptions);
        }

        public static bool GenerateSphereVertices(CSGSphereDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            BrushMeshFactory.CreateSphereVertices(definition.diameterXYZ, definition.offsetY, definition.generateFromCenter, definition.horizontalSegments, definition.verticalSegments, ref vertices);
            return true;
        }

        public static bool GenerateSphereSubMesh(ChiselGeneratedBrushes.ChiselGeneratedBrush subMesh, Vector3 diameterXYZ, float offsetY, bool generateFromCenter, Matrix4x4 transform, int horzSegments, int vertSegments, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            if (diameterXYZ.x == 0 ||
                diameterXYZ.y == 0 ||
                diameterXYZ.z == 0)
            {
                subMesh.brushMesh.Clear();
                return false;
            }

            var brushMesh = BrushMeshFactory.CreateSphere(diameterXYZ, offsetY, generateFromCenter, horzSegments, vertSegments);

            ref var dstBrushMesh = ref subMesh.brushMesh;

            dstBrushMesh.halfEdges = brushMesh.halfEdges;
            dstBrushMesh.vertices = brushMesh.vertices;
            dstBrushMesh.polygons = new BrushMesh.Polygon[brushMesh.polygons.Length];

            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                dstBrushMesh.polygons[i] = new BrushMesh.Polygon
                {
                    surfaceID       = i,
                    edgeCount       = brushMesh.polygons[i].edgeCount,
                    firstEdge       = brushMesh.polygons[i].firstEdge,
                    brushMaterial   = i < brushMaterials.Length ? brushMaterials[i] : brushMaterials[0],
                    description     = i < surfaceDescriptions.Length ? surfaceDescriptions[i] : surfaceDescriptions[0],
                };
            }

            return true;
        }
    }
}