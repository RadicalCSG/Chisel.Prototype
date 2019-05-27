using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
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

        public static BrushMesh CreateBox(UnityEngine.Vector3 min, UnityEngine.Vector3 max, ChiselBrushMaterial brushMaterial, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            if (!BoundsExtensions.IsValid(min, max))
                return null;

            if (min.x > max.x) { float x = min.x; min.x = max.x; max.x = x; }
            if (min.y > max.y) { float y = min.y; min.y = max.y; max.y = y; }
            if (min.z > max.z) { float z = min.z; min.z = max.z; max.z = z; }

            return new BrushMesh
            {
                polygons	= CreateBoxPolygons(brushMaterial, surfaceFlags),
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
        public static BrushMesh CreateBox(UnityEngine.Vector3 size, ChiselBrushMaterial brushMaterial, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            var halfSize = size * 0.5f;
            return CreateBox(-halfSize, halfSize, brushMaterial, surfaceFlags);
        }

        static BrushMesh.Polygon[] CreateBoxPolygons(ChiselBrushMaterial brushMaterial, SurfaceFlags surfaceFlags)
        {
            return new[]
            {
                // left/right
                new BrushMesh.Polygon{ surfaceID = 0, firstEdge =  0, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = brushMaterial },
                new BrushMesh.Polygon{ surfaceID = 1, firstEdge =  4, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = brushMaterial },
                
                // front/back
                new BrushMesh.Polygon{ surfaceID = 2, firstEdge =  8, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = brushMaterial },
                new BrushMesh.Polygon{ surfaceID = 3, firstEdge = 12, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = brushMaterial },
                
                // top/down
                new BrushMesh.Polygon{ surfaceID = 4, firstEdge = 16, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = brushMaterial },
                new BrushMesh.Polygon{ surfaceID = 5, firstEdge = 20, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = brushMaterial }
            };
        }
        

        static readonly BrushMesh.HalfEdge[] boxHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 0 },	//  0
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 3 },	//  1
            new BrushMesh.HalfEdge{ twinIndex = 20, vertexIndex = 2 },	//  2
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 1 },	//  3
                
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 4 },	//  4
            new BrushMesh.HalfEdge{ twinIndex = 19, vertexIndex = 5 },	//  5
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 6 },	//  6
            new BrushMesh.HalfEdge{ twinIndex = 22, vertexIndex = 7 },	//  7
                    
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 0 },	//  8
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 4 },	//  9
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 7 },	// 10
            new BrushMesh.HalfEdge{ twinIndex = 21, vertexIndex = 3 },	// 11
                    
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex = 18, vertexIndex = 1 },	// 12
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 2 },	// 13
            new BrushMesh.HalfEdge{ twinIndex = 23, vertexIndex = 6 },	// 14
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 5 },	// 15
                     
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 0 },	// 16
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 1 },	// 17
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 5 },	// 18
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 4 },	// 19
                    
            // polygon 5
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 3 },	// 20
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 7 },	// 21
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 6 },	// 22
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 2 }	// 23
        };        
    }
}