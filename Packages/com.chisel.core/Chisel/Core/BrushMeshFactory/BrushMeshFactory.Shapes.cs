using System;
using System.Linq;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        //
        //
        //            
        //
        //  0          3
        //  *---------*
        //  | \____  / \
        //  \      \/   \
        //   *--__ /_\___\ 
        //  1 \_  /  \___\\
        //       *---------*
        //      2           4
        //

        public static BrushMesh.Polygon[] CreateSquarePyramidAssetPolygons(ChiselBrushMaterial[] surfaces, SurfaceDescription[] surfaceDescriptions)
        {
            return new[]
            {
                new BrushMesh.Polygon { surfaceID = 0, firstEdge =  0, edgeCount = 4, description = surfaceDescriptions[0], brushMaterial = surfaces[0] },
                new BrushMesh.Polygon { surfaceID = 1, firstEdge =  4, edgeCount = 3, description = surfaceDescriptions[1], brushMaterial = surfaces[1] },
                new BrushMesh.Polygon { surfaceID = 2, firstEdge =  7, edgeCount = 3, description = surfaceDescriptions[2], brushMaterial = surfaces[2] },
                new BrushMesh.Polygon { surfaceID = 3, firstEdge = 10, edgeCount = 3, description = surfaceDescriptions[3], brushMaterial = surfaces[3] },
                new BrushMesh.Polygon { surfaceID = 3, firstEdge = 13, edgeCount = 3, description = surfaceDescriptions[3], brushMaterial = surfaces[3] }
            };
        }
        
        static readonly BrushMesh.HalfEdge[] squarePyramidHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 0 },	// 0  (0-3)
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 1 },	// 1  (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 2 },	// 2  (2-1)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 3 },	// 3  (3-2)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 3 },	// 4  (3-4)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 2 },	// 5  (2-3)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 4 },	// 6  (4-2)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 0 },	// 7  (0-4)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 3 },	// 8  (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 4 },	// 9  (4-3)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 1 },	// 10 (1-4)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 0 },	// 11 (0-1)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 4 },	// 12 (4-0)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 2 },	// 13 (2-4)
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 1 },	// 14 (1-2)
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 4 },	// 15 (4-1)
        };

        static readonly BrushMesh.HalfEdge[] invertedSquarePyramidHalfEdges = squarePyramidHalfEdges;

        //
        //
        //            
        //
        //  1          3
        //  *---------*
        //   \\____  / \
        //    \    \/   \
        //     \   / \___\ 
        //      \ /      \\
        //       *---------*
        //      2           0
        //

        public static BrushMesh.Polygon[] CreateTriangularPyramidAssetPolygons(ChiselBrushMaterial[] surfaces, SurfaceDescription[] surfaceDescriptions)
        {
            return new[]
            {
                new BrushMesh.Polygon { surfaceID = 0, firstEdge =  0, edgeCount = 3, description = surfaceDescriptions[0], brushMaterial = surfaces[0] },
                new BrushMesh.Polygon { surfaceID = 1, firstEdge =  3, edgeCount = 3, description = surfaceDescriptions[1], brushMaterial = surfaces[1] },
                new BrushMesh.Polygon { surfaceID = 2, firstEdge =  6, edgeCount = 3, description = surfaceDescriptions[2], brushMaterial = surfaces[2] },
                new BrushMesh.Polygon { surfaceID = 3, firstEdge =  9, edgeCount = 3, description = surfaceDescriptions[3], brushMaterial = surfaces[3] }
            };
        }
        
        static readonly BrushMesh.HalfEdge[] triangularPyramidHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 3 },	// 0  (3-1)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 2 },	// 1  (2-3)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 1 },	// 2  (1-2)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 0 },	// 3  (0-3)
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 2 },	// 4  (2-0)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 3 },	// 5  (3-2)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 0 },	// 6  (0-2)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 1 },	// 7  (1-0)
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 2 },	// 8  (2-1)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 0 },	// 9  (0-1)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 3 },	// 10 (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 1 },	// 11 (1-3)
        };

        static readonly BrushMesh.HalfEdge[] invertedTriangularPyramidHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 1 },	// 0  (1-3)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 2 },	// 1  (2-1)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 3 },	// 2  (3-2)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 3 },	// 3  (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 2 },	// 4  (2-3)
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 0 },	// 5  (0-2)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 2 },	// 6  (2-0)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 1 },	// 7  (1-2)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 0 },	// 8  (0-1)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 1 },	// 9  (1-0)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 3 },	// 10 (3-1)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 0 },	// 11 (0-3)
        };

        public static BrushMesh.Polygon[] CreateWedgeAssetPolygons(ChiselBrushMaterial[] surfaces, SurfaceDescription[] surfaceDescriptions)
        {
            return new[]
            {
                new BrushMesh.Polygon { surfaceID = 0, firstEdge =  0, edgeCount = 3, description = surfaceDescriptions[0], brushMaterial = surfaces[0] },
                new BrushMesh.Polygon { surfaceID = 1, firstEdge =  3, edgeCount = 3, description = surfaceDescriptions[1], brushMaterial = surfaces[1] },
                new BrushMesh.Polygon { surfaceID = 2, firstEdge =  6, edgeCount = 4, description = surfaceDescriptions[2], brushMaterial = surfaces[2] },
                new BrushMesh.Polygon { surfaceID = 3, firstEdge = 10, edgeCount = 4, description = surfaceDescriptions[3], brushMaterial = surfaces[3] },
                new BrushMesh.Polygon { surfaceID = 4, firstEdge = 14, edgeCount = 4, description = surfaceDescriptions[4], brushMaterial = surfaces[4] }
            };
        }

        static readonly BrushMesh.HalfEdge[] wedgeHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 0 },	// 0  (0-2)
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 1 },	// 1  (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 2 },	// 2  (2-1)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 4 },	// 3  (4-5)
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 3 },	// 4  (3-4)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 5 },	// 5  (5-3)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 2 },	// 6  (2-0)
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 5 },	// 7  (5-2)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 3 },	// 8  (3-5)
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 0 },	// 9  (0-3)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 0 },	// 10 (0-1)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 3 },	// 11 (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 4 },	// 12 (4-3)
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 1 },	// 13 (1-4)
            
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 1 },	// 14 (1-2)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 4 },	// 15 (4-1)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 5 },	// 16 (5-4)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 2 }	// 17 (2-5)
        };

        static readonly BrushMesh.HalfEdge[] invertedWedgeHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 2 },	// 0  (2-0)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 1 },	// 1  (1-2)
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 0 },	// 2  (0-1)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 5 },	// 3  (5-4)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 3 },	// 4  (3-5)
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 4 },	// 5  (4-3)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 0 },	// 6  (0-2)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 3 },	// 7  (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 5 },	// 8  (5-3)
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 2 },	// 9  (2-5)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 1 },	// 10 (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 4 },	// 11 (4-1)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 3 },	// 12 (3-4)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 0 },	// 13 (0-3)
            
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 2 },	// 14 (2-1)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 5 },	// 15 (5-2)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 4 },	// 16 (4-5)
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 1 }	// 17 (1-4)
        };

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
        
        static readonly BrushMesh.HalfEdge[] invertedBoxHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 19, vertexIndex = 1 },	//  0 (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 2 },	//  1 (2-1)
            new BrushMesh.HalfEdge{ twinIndex = 20, vertexIndex = 3 },	//  2 (3-2)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 0 },	//  3 (0-3)
                
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 7 },	//  4 (7-4)
            new BrushMesh.HalfEdge{ twinIndex = 22, vertexIndex = 6 },	//  5 (6-7)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 5 },	//  6 (5-6)
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 4 },	//  7 (4-5)
                    
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 3 },	//  8 (3-0)
            new BrushMesh.HalfEdge{ twinIndex = 23, vertexIndex = 7 },	//  9 (7-3)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 4 },	// 10 (4-7)
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 0 },	// 11 (0-4)
                    
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex = 18, vertexIndex = 5 },	// 12 (5-1)
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 6 },	// 13 (6-5)
            new BrushMesh.HalfEdge{ twinIndex = 21, vertexIndex = 2 },	// 14 (2-6)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 1 },	// 15 (1-2)
                     
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 4 },	// 16 (4-0)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 5 },	// 17 (5-4)
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 1 },	// 18 (1-5)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 0 },	// 19 (0-1)
                    
            // polygon 5
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 2 },	// 20 (2-3)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 6 },	// 21 (6-2)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 7 },	// 22 (7-6)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 3 }	// 23 (3-7)
        };

        public static BrushMesh.Polygon[] CreateBoxAssetPolygons(ChiselBrushMaterial[] surfaces, SurfaceFlags surfaceFlags)
        {
            return new[]
            {
                // left/right
                new BrushMesh.Polygon { surfaceID = 0, firstEdge =  0, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = surfaces[0] },
                new BrushMesh.Polygon { surfaceID = 1, firstEdge =  4, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = surfaces[1] },
                 
                // front/back
                new BrushMesh.Polygon { surfaceID = 2, firstEdge =  8, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = surfaces[2] },
                new BrushMesh.Polygon { surfaceID = 3, firstEdge = 12, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = surfaces[3] },
                
                // top/down
                new BrushMesh.Polygon { surfaceID = 4, firstEdge = 16, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = surfaces[4] },
                new BrushMesh.Polygon { surfaceID = 5, firstEdge = 20, edgeCount = 4, description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }, brushMaterial = surfaces[5] }
            };
        }

        public static BrushMesh.Polygon[] CreateBoxAssetPolygons(ChiselBrushMaterial[] surfaces, SurfaceDescription[] surfaceDescriptions)
        {
            return new[]
            {
                // left/right
                new BrushMesh.Polygon { surfaceID = 0, firstEdge =  0, edgeCount = 4, description = surfaceDescriptions[0], brushMaterial = surfaces[0] },
                new BrushMesh.Polygon { surfaceID = 1, firstEdge =  4, edgeCount = 4, description = surfaceDescriptions[1], brushMaterial = surfaces[1] },
                
                // front/back
                new BrushMesh.Polygon { surfaceID = 2, firstEdge =  8, edgeCount = 4, description = surfaceDescriptions[2], brushMaterial = surfaces[2] },
                new BrushMesh.Polygon { surfaceID = 3, firstEdge = 12, edgeCount = 4, description = surfaceDescriptions[3], brushMaterial = surfaces[3] },
                
                // top/down
                new BrushMesh.Polygon { surfaceID = 4, firstEdge = 16, edgeCount = 4, description = surfaceDescriptions[4], brushMaterial = surfaces[4] },
                new BrushMesh.Polygon { surfaceID = 5, firstEdge = 20, edgeCount = 4, description = surfaceDescriptions[5], brushMaterial = surfaces[5] }
            };
        }


    }
}