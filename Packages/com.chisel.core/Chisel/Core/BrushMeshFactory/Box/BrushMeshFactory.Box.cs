using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateBox(ref ChiselBrushContainer brushContainer, ref ChiselBoxDefinition definition)
        {
            definition.Validate();

            var min = definition.min;
            var max = definition.max;
            if (!BoundsExtensions.IsValid(min, max))
                return false;

            brushContainer.EnsureSize(1);

            return CreateBox(ref brushContainer.brushMeshes[0], definition.min, definition.max, definition.surfaceDefinition);
        }

        static bool GenerateBox(ref BrushMesh brushMesh, ref ChiselBoxDefinition definition)
        {
            definition.Validate();

            var min = definition.min;
            var max = definition.max;
            if (!BoundsExtensions.IsValid(min, max))
            {
                brushMesh.Clear();
                return false;
            }

            return CreateBox(ref brushMesh, definition.min, definition.max, definition.surfaceDefinition);
        }

        public static bool CreateBox(ref BrushMesh brushMesh, UnityEngine.Vector3 min, UnityEngine.Vector3 max, in ChiselSurfaceDefinition surfaceDefinition)
        {
            if (surfaceDefinition == null)
                return false;

            var surfaces = surfaceDefinition.surfaces;
            if (surfaces == null)
                return false;

            if (surfaces.Length < 6)
                return false;

            if (!BoundsExtensions.IsValid(min, max))
                return false;

            if (min.x > max.x) { float x = min.x; min.x = max.x; max.x = x; }
            if (min.y > max.y) { float y = min.y; min.y = max.y; max.y = y; }
            if (min.z > max.z) { float z = min.z; min.z = max.z; max.z = z; }

            brushMesh.polygons  = CreateBoxPolygons(in surfaceDefinition);
            brushMesh.halfEdges = boxHalfEdges.ToArray();
            brushMesh.vertices  = BrushMeshFactory.CreateBoxVertices(min, max);
            brushMesh.UpdateHalfEdgePolygonIndices();
            brushMesh.CalculatePlanes();
            return true;
        }

        public static void CreateBoxVertices(UnityEngine.Vector3 min, UnityEngine.Vector3 max, ref Vector3[] vertices)
        {
            if (vertices == null ||
                vertices.Length != 8)
                vertices = new Vector3[8];

            vertices[0] = new Vector3( min.x, max.y, min.z); // 0
            vertices[1] = new Vector3( max.x, max.y, min.z); // 1
            vertices[2] = new Vector3( max.x, max.y, max.z); // 2
            vertices[3] = new Vector3( min.x, max.y, max.z); // 3

            vertices[4] = new Vector3( min.x, min.y, min.z); // 4  
            vertices[5] = new Vector3( max.x, min.y, min.z); // 5
            vertices[6] = new Vector3( max.x, min.y, max.z); // 6
            vertices[7] = new Vector3( min.x, min.y, max.z); // 7
        }

        // TODO: do not use this version unless we have no choice ..
        public static Vector3[] CreateBoxVertices(UnityEngine.Vector3 min, UnityEngine.Vector3 max)
        {
            Vector3[] vertices = null;
            CreateBoxVertices(min, max, ref vertices);
            return vertices;
        }

        public static BrushMesh CreateBox(UnityEngine.Vector3 min, UnityEngine.Vector3 max, in ChiselSurface surface)
        {
            if (!BoundsExtensions.IsValid(min, max))
                return null;

            if (min.x > max.x) { float x = min.x; min.x = max.x; max.x = x; }
            if (min.y > max.y) { float y = min.y; min.y = max.y; max.y = y; }
            if (min.z > max.z) { float z = min.z; min.z = max.z; max.z = z; }

            return new BrushMesh
            {
                polygons	= CreateBoxPolygons(in surface),
                halfEdges	= boxHalfEdges.ToArray(),
                vertices	= CreateBoxVertices(min, max)
            };
        }

        /// <summary>
        /// Creates a box <see cref="Chisel.Core.BrushMesh"/> with <paramref name="size"/> and optional <paramref name="material"/>
        /// </summary>
        /// <param name="size">The size of the box</param>
        /// <param name="material">The [UnityEngine.Material](https://docs.unity3d.com/ScriptReference/Material.html) that will be set to all surfaces of the box (optional)</param>
        /// <returns>A <see cref="Chisel.Core.BrushMesh"/> on success, null on failure</returns>
        public static BrushMesh CreateBox(UnityEngine.Vector3 size, in ChiselSurface surface)
        {
            var halfSize = size * 0.5f;
            return CreateBox(-halfSize, halfSize, in surface);
        }
    }
}