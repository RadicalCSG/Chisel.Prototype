using System;
using System.Linq;
using Chisel.Assets;
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

namespace Chisel.Components
{
    // TODO: clean up
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateCylinderAsset(CSGBrushMeshAsset brushMeshAsset, CSGCylinderDefinition definition)
        {
            definition.Validate();

            var tempTop		= definition.top;
            var tempBottom	= definition.bottom;

            if (!definition.isEllipsoid)
            {
                tempTop.diameterZ = tempTop.diameterX;
                tempBottom.diameterZ = tempBottom.diameterX;
            }

            switch (definition.type)
            {
                case CylinderShapeType.Cylinder:		return BrushMeshAssetFactory.GenerateCylinderAsset(brushMeshAsset, tempBottom, tempTop.height, definition.rotation, definition.sides, definition.surfaceAssets, definition.surfaceDescriptions); 
                case CylinderShapeType.ConicalFrustum:	return BrushMeshAssetFactory.GenerateConicalFrustumAsset(brushMeshAsset, tempBottom, tempTop, definition.rotation, definition.sides, definition.surfaceAssets, definition.surfaceDescriptions); 
                case CylinderShapeType.Cone:			return BrushMeshAssetFactory.GenerateConeAsset(brushMeshAsset, tempBottom, tempTop.height, definition.rotation, definition.sides, definition.surfaceAssets, definition.surfaceDescriptions); 
            }
            return false;
        }

        public static bool GenerateCylinderAsset(CSGBrushMeshAsset brushMeshAsset, CSGCircleDefinition bottom, float topHeight, float rotation, int sides, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CSGCircleDefinition top;
            top.diameterX = bottom.diameterX;
            top.diameterZ = bottom.diameterZ;
            top.height = topHeight;
            return GenerateConicalFrustumAsset(brushMeshAsset, bottom, top, rotation, sides, surfaceAssets, surfaceDescriptions);
        }

        public static bool GenerateConeAsset(CSGBrushMeshAsset brushMeshAsset, CSGCircleDefinition bottom, float topHeight, float rotation, int sides, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CSGCircleDefinition top;
            top.diameterX = 0;
            top.diameterZ = 0;
            top.height = topHeight;
            return GenerateConicalFrustumAsset(brushMeshAsset, bottom, top, rotation, sides, surfaceAssets, surfaceDescriptions);
        }

        public static bool GenerateCylinderSubMesh(CSGBrushSubMesh subMesh, float diameter, float bottomHeight, float topHeight, float rotation, int sides, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CSGCircleDefinition bottom;
            bottom.diameterX = diameter;
            bottom.diameterZ = diameter;
            bottom.height = bottomHeight;
            CSGCircleDefinition top;
            top.diameterX = diameter;
            top.diameterZ = diameter;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(subMesh, bottom, top, rotation, sides, surfaceAssets, surfaceDescriptions);
        }

        public static bool GenerateCylinderSubMesh(CSGBrushSubMesh subMesh, float diameterX, float diameterZ, float bottomHeight, float topHeight, float rotation, int sides, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CSGCircleDefinition bottom;
            bottom.diameterX = diameterX;
            bottom.diameterZ = diameterZ;
            bottom.height = bottomHeight;
            CSGCircleDefinition top;
            top.diameterX = diameterX;
            top.diameterZ = diameterZ;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(subMesh, bottom, top, rotation, sides, surfaceAssets, surfaceDescriptions);
        }

        public static bool GenerateCylinderSubMesh(CSGBrushSubMesh subMesh, CSGCircleDefinition bottom, float topHeight, float rotation, int sides, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CSGCircleDefinition top;
            top.diameterX = bottom.diameterX;
            top.diameterZ = bottom.diameterZ;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(subMesh, bottom, top, rotation, sides, surfaceAssets, surfaceDescriptions);
        }

        public static bool GenerateConeSubMesh(CSGBrushSubMesh subMesh, CSGCircleDefinition bottom, float topHeight, float rotation, int sides, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CSGCircleDefinition top;
            top.diameterX = 0;
            top.diameterZ = 0;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(subMesh, bottom, top, rotation, sides, surfaceAssets, surfaceDescriptions);
        }
        
        // TODO: could probably figure out "inverse" from direction of topY compared to bottomY
        public static Vector3[] GetConeFrustumVertices(CSGCircleDefinition definition, float topHeight, float rotation, int segments, ref Vector3[] vertices, bool inverse = false)
        {
            var rotate			= Quaternion.AngleAxis(rotation, Vector3.up);
            var bottomAxisX		= rotate * Vector3.right   * definition.diameterX * 0.5f;
            var bottomAxisZ		= rotate * Vector3.forward * definition.diameterZ * 0.5f;
            var topY			= Vector3.up * topHeight;
            var bottomY			= Vector3.up * definition.height;

            if (vertices == null ||
                vertices.Length != segments + 1)
                vertices = new Vector3[segments + 1];

            float angleOffset = ((segments & 1) == 1) ? 0.0f : ((360.0f / segments) * 0.5f);
            
            vertices[0] = topY;
            for (int v = 0; v < segments; v++)
            {
                var r = (((v * 360.0f) / (float)segments) + angleOffset) * Mathf.Deg2Rad;
                var s = Mathf.Sin(r);
                var c = Mathf.Cos(r);

                var bottomVertex = (bottomAxisX * c) + (bottomAxisZ * s);
                bottomVertex += bottomY;
                var vi = inverse ? (segments - v) : (v + 1);
                vertices[vi] = bottomVertex;
            }

            return vertices;
        }


        public static Vector3[] GetConicalFrustumVertices(CSGCircleDefinition bottom, CSGCircleDefinition top, float rotation, int segments, ref Vector3[] vertices)
        {
            if (top.height > bottom.height) { var temp = top; top = bottom; bottom = temp; }

            var rotate		= Quaternion.AngleAxis(rotation, Vector3.up);
            var topAxisX	= rotate * Vector3.right	* top.diameterX * 0.5f;
            var topAxisZ	= rotate * Vector3.forward	* top.diameterZ * 0.5f;
            var bottomAxisX = rotate * Vector3.right	* bottom.diameterX * 0.5f;
            var bottomAxisZ = rotate * Vector3.forward	* bottom.diameterZ * 0.5f;
            var topY		= Vector3.up * top.height;
            var bottomY		= Vector3.up * bottom.height;

            // TODO: handle situation where diameterX & diameterZ are 0 (only create one vertex)

            if (vertices == null ||
                vertices.Length != segments * 2)
                vertices = new Vector3[segments * 2];

            float angleOffset = ((segments & 1) == 1) ? 0.0f : ((360.0f / segments) * 0.5f);

            for (int v = 0; v < segments; v++)
            {
                var r = (((v * 360.0f) / (float)segments) + angleOffset) * Mathf.Deg2Rad;
                var s = Mathf.Sin(r);
                var c = Mathf.Cos(r);

                var topVertex = (topAxisX * c) + (topAxisZ * s);
                var bottomVertex = (bottomAxisX * c) + (bottomAxisZ * s);
                topVertex += topY;
                bottomVertex += bottomY;
                vertices[v] = topVertex;
                vertices[v + segments] = bottomVertex;
            }

            return vertices;
        }

        public static bool GenerateConicalFrustumAsset(CSGBrushMeshAsset brushMeshAsset, CSGCircleDefinition bottom, CSGCircleDefinition top, float rotation, int segments, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            if (segments < 3 || (top.height - bottom.height) == 0 || (bottom.diameterX == 0 && top.diameterX == 0) || (bottom.diameterZ == 0 && top.diameterZ == 0))
            {
                brushMeshAsset.Clear();
                return false;
            }

            if (surfaceAssets.Length != 3 ||
                surfaceDescriptions.Length != segments + 2)
            {
                brushMeshAsset.Clear();
                return false;
            }

            var subMesh = new CSGBrushSubMesh();
            if (!GenerateConicalFrustumSubMesh(subMesh, bottom, top, rotation, segments, surfaceAssets, surfaceDescriptions))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SubMeshes = new CSGBrushSubMesh[] { subMesh };
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }

        public static bool GenerateConicalFrustumSubMesh(CSGBrushSubMesh subMesh, CSGCircleDefinition bottom, CSGCircleDefinition top, float rotation, int segments, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            if (segments < 3 || (top.height - bottom.height) == 0 || (bottom.diameterX == 0 && top.diameterX == 0) || (bottom.diameterZ == 0 && top.diameterZ == 0))
            {
                subMesh.Clear();
                return false;
            }
            
            if (surfaceAssets.Length < 3 ||
                surfaceDescriptions.Length < segments + 2)
            {
                subMesh.Clear();
                return false;
            }
            // TODO: handle situation where diameterX & diameterZ are 0 (only create one vertex)

            if (top.diameterX == 0)
            {
                var vertices = new Vector3[segments + 1];
                GetConeFrustumVertices(bottom, top.height, rotation, segments, ref vertices, inverse: false);
            
                // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                CreateConeSubMesh(subMesh, segments, null, vertices, surfaceAssets, surfaceDescriptions);
            } else
            if (bottom.diameterX == 0)
            {
                var vertices = new Vector3[segments + 1];
                GetConeFrustumVertices(top, bottom.height, rotation, segments, ref vertices, inverse: true);
            
                // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                CreateConeSubMesh(subMesh, segments, null, vertices, surfaceAssets, surfaceDescriptions);
            } else
            {

                if (top.height > bottom.height)
                { var temp = top; top = bottom; bottom = temp; }

                var vertices = new Vector3[segments * 2];
                GetConicalFrustumVertices(bottom, top, rotation, segments, ref vertices);
            
                // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                CreateExtrudedSubMesh(subMesh, segments, null, 0, 1, vertices, surfaceAssets, surfaceDescriptions);
            }

            return true;
        }

        static void CreateConeSubMesh(CSGBrushSubMesh subMesh, int segments, int[] segmentDescriptionIndices, Vector3[] vertices, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {
            CreateConeSubMesh(subMesh, segments, segmentDescriptionIndices, null, vertices, surfaceAssets, surfaceDescriptions);
        }

        static void CreateConeSubMesh(CSGBrushSubMesh subMesh, int segments, int[] segmentDescriptionIndices, int[] segmentAssetIndices, Vector3[] vertices, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
        {			
            var surfaceDescription0 = surfaceDescriptions[0];
            var surfaceAsset0		= surfaceAssets[0];

            var polygons = new CSGBrushSubMesh.Polygon[segments + 1];
            polygons[0] = new CSGBrushSubMesh.Polygon { surfaceID = 0, firstEdge = 0, edgeCount = segments, description = surfaceDescription0, surfaceAsset = surfaceAsset0 };

            for (int s = 0, surfaceID = 1; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s + 2]);
                var assetIndex       = (segmentAssetIndices       == null) ?     2 : (segmentAssetIndices      [s + 2]);
                polygons[surfaceID] = new CSGBrushSubMesh.Polygon { surfaceID = surfaceID, firstEdge = segments + (s * 3), edgeCount = 3, description = surfaceDescriptions[descriptionIndex], surfaceAsset = surfaceAssets[assetIndex] };
                polygons[surfaceID].description.smoothingGroup = (uint)0;
                surfaceID++;
            }

            var halfEdges = new BrushMesh.HalfEdge[4 * segments];
            for (int n = 0, e = segments - 1; n < segments; e=n, n++)
            {
                var p = (n + 1) % segments; // TODO: optimize away
                //			 	    v0	 
                //	                 *             
                //                ^ /^ \
                //               / /  \ \
                //			    / / e2 \ \   
                //             / /      \ \  
                //	       t2 / /e3 q2 e2\ \ t3
                //           / /          \ \  
                //			/ v     e1     \ v
                //	 ------>   ----------->   ----->
                //        v2 *              * v1
                //	 <-----    <----------    <-----  
                //			        e0    

                var v0 = 0;
                var v1 = n + 1;
                var v2 = e + 1;
                
                var e0 = n;
                var e1 = ((n * 3) + segments) + 0;
                var e2 = ((n * 3) + segments) + 1;
                var e3 = ((n * 3) + segments) + 2;

                var t3 = ((e * 3) + segments) + 2;
                var t2 = ((p * 3) + segments) + 1;

                // bottom polygon
                halfEdges[e0] = new BrushMesh.HalfEdge { vertexIndex = v1, twinIndex = e1 };

                // triangle
                halfEdges[e1] = new BrushMesh.HalfEdge { vertexIndex = v2, twinIndex = e0 };
                halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = v0, twinIndex = t3 };
                halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = v1, twinIndex = t2 };
            }

            subMesh.Polygons	= polygons;
            subMesh.HalfEdges	= halfEdges;
            subMesh.Vertices	= vertices;
            subMesh.CreateOrUpdateBrushMesh();
        }
    }
}