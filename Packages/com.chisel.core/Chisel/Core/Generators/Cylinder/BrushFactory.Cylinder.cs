﻿using System;
using System.Linq;
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
using Unity.Mathematics;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {

        [Serializable]
        public struct ChiselCircleDefinition
        {
            public ChiselCircleDefinition(float diameterX, float diameterZ, float height) { this.diameterX = diameterX; this.diameterZ = diameterZ; this.height = height; }
            public ChiselCircleDefinition(float diameter, float height) { this.diameterX = diameter; this.diameterZ = diameter; this.height = height; }

            public float diameterX;
            public float diameterZ;
            public float height;
        }

        public static bool GenerateCylinder(ref ChiselBrushContainer brushContainer, ref ChiselCylinderDefinition definition)
        {
            definition.Validate();

            var tempTop    = new ChiselCircleDefinition { diameterX = definition.topDiameterX, diameterZ = definition.topDiameterZ, height = definition.height + definition.bottomOffset };
            var tempBottom = new ChiselCircleDefinition { diameterX = definition.bottomDiameterX, diameterZ = definition.bottomDiameterZ, height = definition.bottomOffset };

            if (!definition.isEllipsoid)
            {
                tempTop   .diameterZ = tempTop.diameterX;
                tempBottom.diameterZ = tempBottom.diameterX;
            }

            brushContainer.EnsureSize(1);

            bool result = false;
            switch (definition.type)
            {
                case CylinderShapeType.Cylinder:       result = BrushMeshFactory.GenerateCylinder(ref brushContainer.brushMeshes[0], tempBottom, tempTop.height, definition.rotation, definition.sides, definition.fitToBounds, in definition.surfaceDefinition); break;
                case CylinderShapeType.ConicalFrustum: result = BrushMeshFactory.GenerateConicalFrustum(ref brushContainer.brushMeshes[0], tempBottom, tempTop, definition.rotation, definition.sides, definition.fitToBounds, in definition.surfaceDefinition); break;
                case CylinderShapeType.Cone:           result = BrushMeshFactory.GenerateCone(ref brushContainer.brushMeshes[0], tempBottom, tempTop.height, definition.rotation, definition.sides, definition.fitToBounds, in definition.surfaceDefinition); break;
            }
            return result;
        }

        public static bool GenerateCylinder(ref BrushMesh brushMesh, ref ChiselCylinderDefinition definition)
        {
            definition.Validate();
            
            var tempTop    = new ChiselCircleDefinition { diameterX = definition.topDiameterX, diameterZ = definition.topDiameterZ, height = definition.height + definition.bottomOffset };
            var tempBottom = new ChiselCircleDefinition { diameterX = definition.bottomDiameterX, diameterZ = definition.bottomDiameterZ, height = definition.bottomOffset };

            if (!definition.isEllipsoid)
            {
                tempTop   .diameterZ = tempTop.diameterX;
                tempBottom.diameterZ = tempBottom.diameterX;
            }
            
            bool result = false;
            switch (definition.type)
            {
                case CylinderShapeType.Cylinder:       result = BrushMeshFactory.GenerateCylinder(ref brushMesh, tempBottom, tempTop.height, definition.rotation, definition.sides, definition.fitToBounds, in definition.surfaceDefinition); break;
                case CylinderShapeType.ConicalFrustum: result = BrushMeshFactory.GenerateConicalFrustum(ref brushMesh, tempBottom, tempTop, definition.rotation, definition.sides, definition.fitToBounds, in definition.surfaceDefinition); break;
                case CylinderShapeType.Cone:           result = BrushMeshFactory.GenerateCone(ref brushMesh, tempBottom, tempTop.height, definition.rotation, definition.sides, definition.fitToBounds, in definition.surfaceDefinition); break;
            }
            if (!result)
                brushMesh.Clear();
            return result;
        }

        public static bool GenerateCylinder(ref BrushMesh brushMesh, ChiselCircleDefinition bottom, float topHeight, float rotation, int sides, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ChiselCircleDefinition  top;
            top.diameterX = bottom.diameterX;
            top.diameterZ = bottom.diameterZ;
            top.height = topHeight;
            return GenerateConicalFrustum(ref brushMesh, bottom, top, rotation, sides, fitToBounds, in surfaceDefinition);
        }

        public static bool GenerateCone(ref BrushMesh brushMesh, ChiselCircleDefinition bottom, float topHeight, float rotation, int sides, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ChiselCircleDefinition  top;
            top.diameterX = 0;
            top.diameterZ = 0;
            top.height = topHeight;
            return GenerateConicalFrustum(ref brushMesh, bottom, top, rotation, sides, fitToBounds, in surfaceDefinition);
        }

        public static bool GenerateConicalFrustum(ref BrushMesh brushMesh, ChiselCircleDefinition bottom, ChiselCircleDefinition top, float rotation, int segments, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            if (segments < 3 || (top.height - bottom.height) == 0 || (bottom.diameterX == 0 && top.diameterX == 0) || (bottom.diameterZ == 0 && top.diameterZ == 0))
            {
                brushMesh.Clear();
                return false;
            }

            if (surfaceDefinition == null ||
                surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length != segments + 2)
            {
                brushMesh.Clear();
                return false;
            }

            if (!GenerateConicalFrustumSubMesh(ref brushMesh, bottom, top, rotation, segments, fitToBounds, in surfaceDefinition))
            {
                brushMesh.Clear();
                return false;
            }
            
            return true;
        }

        public static bool GenerateCylinderSubMesh(ref BrushMesh brushMesh, float diameter, float bottomHeight, float topHeight, float rotation, int sides, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ChiselCircleDefinition  bottom;
            bottom.diameterX = diameter;
            bottom.diameterZ = diameter;
            bottom.height = bottomHeight;
            ChiselCircleDefinition  top;
            top.diameterX = diameter;
            top.diameterZ = diameter;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(ref brushMesh, bottom, top, rotation, sides, fitToBounds, in surfaceDefinition);
        }

        public static bool GenerateCylinderSubMesh(ref BrushMesh brushMesh, float diameterX, float diameterZ, float bottomHeight, float topHeight, float rotation, int sides, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ChiselCircleDefinition  bottom;
            bottom.diameterX = diameterX;
            bottom.diameterZ = diameterZ;
            bottom.height = bottomHeight;
            ChiselCircleDefinition  top;
            top.diameterX = diameterX;
            top.diameterZ = diameterZ;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(ref brushMesh, bottom, top, rotation, sides, fitToBounds, in surfaceDefinition);
        }

        public static bool GenerateCylinderSubMesh(ref BrushMesh brushMesh, ChiselCircleDefinition  bottom, float topHeight, float rotation, int sides, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ChiselCircleDefinition  top;
            top.diameterX = bottom.diameterX;
            top.diameterZ = bottom.diameterZ;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(ref brushMesh, bottom, top, rotation, sides, fitToBounds, in surfaceDefinition);
        }

        public static bool GenerateConeSubMesh(ref BrushMesh brushMesh, ChiselCircleDefinition  bottom, float topHeight, float rotation, int sides, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ChiselCircleDefinition  top;
            top.diameterX = 0;
            top.diameterZ = 0;
            top.height = topHeight;
            return GenerateConicalFrustumSubMesh(ref brushMesh, bottom, top, rotation, sides, fitToBounds, in surfaceDefinition);
        }
        
        // TODO: could probably figure out "inverse" from direction of topY compared to bottomY
        public static void GetConeFrustumVertices(ChiselCircleDefinition definition, float topHeight, float rotation, int segments, ref float3[] vertices, bool inverse = false, bool fitToBounds = false)
        {
            var rotate			= Quaternion.AngleAxis(rotation, Vector3.up);
            var bottomRadiusX	= (float3)(rotate * Vector3.right   * definition.diameterX * 0.5f);
            var bottomRadiusZ	= (float3)(rotate * Vector3.forward * definition.diameterZ * 0.5f);
            var topY			= (float3)(Vector3.up * topHeight);
            var bottomY			= (float3)(Vector3.up * definition.height);

            if (vertices == null ||
                vertices.Length != segments + 1)
                vertices = new float3[segments + 1];

            float angleOffset = ((segments & 1) == 1) ? 0.0f : ((360.0f / segments) * 0.5f);
            
            vertices[0] = topY;
            for (int v = 0; v < segments; v++)
            {
                var r = (((v * 360.0f) / (float)segments) + angleOffset) * Mathf.Deg2Rad;
                var s = Mathf.Sin(r);
                var c = Mathf.Cos(r);

                var bottomVertex = (bottomRadiusX * c) + (bottomRadiusZ * s);
                bottomVertex += bottomY;
                var vi = inverse ? (segments - v) : (v + 1);
                vertices[vi] = bottomVertex;
            }

            if (fitToBounds)
                FitXZ(vertices, 1, vertices.Length - 1, definition.diameterX, definition.diameterZ);
        }

        public static void FitXZ(float3[] vertices, int offset, int count, float diameterX, float diameterZ)
        {
            var expectedSize = new float2(diameterX, diameterZ);
            if (math.any(expectedSize == float2.zero))
                return;
            float2 min = vertices[offset].xz, max = vertices[offset].xz;
            var last = offset + count;
            for (int v = offset + 1; v < last; v++)
            {
                min = math.min(min, vertices[v].xz);
                max = math.max(max, vertices[v].xz);
            }

            var size = math.abs(max - min);
            if (math.any(size == float2.zero))
                return;
            var translation = (max + min) * 0.5f;
            var resize = expectedSize / size;
            for (int v = offset; v < last; v++)
            {
                vertices[v].xz -= translation;
                vertices[v].xz = vertices[v].xz * resize;
            }
        }

        public static void FitXZ(Vector3[] vertices, int offset, int count, float diameterX, float diameterZ)
        {
            float2 min = ((float3)vertices[offset]).xz, max = ((float3)vertices[offset]).xz;
            var last = offset + count;
            for (int v = offset + 1; v < last; v++)
            {
                var vert = (float3)vertices[v];
                min = math.min(min, vert.xz);
                max = math.min(max, vert.xz);
            }

            var expectedSize = new float2(diameterX, diameterZ);
            var size = math.abs(max - min);
            for (int v = offset; v < last; v++)
            {
                var vert = vertices[v];
                var newXZ = (((float3)vert).xz / size) * expectedSize;
                vertices[v] = new Vector3(newXZ.x, vert.y, newXZ.y);
            }
        }

        public static void GetCircleVertices(float radiusX, float radiusZ, float rotation, int segments, ref float3[] vertices, bool fitToBounds = false)
        {
            var rotate		= Quaternion.AngleAxis(rotation, Vector3.up);
            var circleAxisX = rotate * Vector3.right	* radiusX;
            var circleAxisZ = rotate * Vector3.forward	* radiusZ;

            if (vertices == null ||
                vertices.Length != segments)
                vertices = new float3[segments];

            for (int v = 0; v < segments; v++)
            {
                var r = ((v * 360.0f) / (float)segments) * Mathf.Deg2Rad;
                var s = Mathf.Sin(r);
                var c = Mathf.Cos(r);

                var bottomVertex = (circleAxisX * c) + (circleAxisZ * s);
                vertices[v] = bottomVertex;
            }

            if (fitToBounds)
                FitXZ(vertices, 0, vertices.Length, radiusX * 2.0f, radiusZ * 2.0f);
        }
        
        public static void GetConicalFrustumVertices(ChiselCircleDefinition  bottom, ChiselCircleDefinition  top, float rotation, int segments, ref float3[] vertices, bool fitToBounds = false)
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
                vertices = new float3[segments * 2];

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

            if (fitToBounds)
            {
                FitXZ(vertices, 0, segments, top.diameterX, top.diameterZ);
                FitXZ(vertices, segments, segments, bottom.diameterX, bottom.diameterZ);
            }
        }

        public static Vector3[] GetConicalFrustumVertices(ChiselCircleDefinition  bottom, ChiselCircleDefinition  top, float rotation, int segments, ref Vector3[] vertices, bool fitToBounds = false)
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

            if (fitToBounds)
            {
                FitXZ(vertices, 0, segments, top.diameterX, top.diameterZ);
                FitXZ(vertices, segments, segments, bottom.diameterX, bottom.diameterZ);
            }

            return vertices;
        }


        public static bool GenerateConicalFrustumSubMesh(ref BrushMesh brushMesh, ChiselCircleDefinition bottom, ChiselCircleDefinition  top, float rotation, int segments, bool fitToBounds, in ChiselSurfaceDefinition surfaceDefinition)
        {
            if (segments < 3 || (top.height - bottom.height) == 0 || (bottom.diameterX == 0 && top.diameterX == 0) || (bottom.diameterZ == 0 && top.diameterZ == 0))
            {
                brushMesh.Clear();
                return false;
            }
            
            if (surfaceDefinition == null ||
                surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length < segments + 2)
            {
                brushMesh.Clear();
                return false;
            }
            // TODO: handle situation where diameterX & diameterZ are 0 (only create one vertex)

            if (top.diameterX == 0)
            {
                GetConeFrustumVertices(bottom, top.height, rotation, segments, ref brushMesh.vertices, inverse: false, fitToBounds: fitToBounds);
            
                // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                CreateConeSubMesh(ref brushMesh, segments, null, brushMesh.vertices, in surfaceDefinition);
            } else
            if (bottom.diameterX == 0)
            {
                GetConeFrustumVertices(top, bottom.height, rotation, segments, ref brushMesh.vertices, inverse: true, fitToBounds: fitToBounds);
            
                // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                CreateConeSubMesh(ref brushMesh, segments, null, brushMesh.vertices, in surfaceDefinition);
            } else
            {
                if (top.height > bottom.height)
                { var temp = top; top = bottom; bottom = temp; }

                GetConicalFrustumVertices(bottom, top, rotation, segments, ref brushMesh.vertices, fitToBounds: fitToBounds);
            
                // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                CreateExtrudedSubMesh(ref brushMesh, segments, null, 0, 1, brushMesh.vertices, in surfaceDefinition);
            }

            return true;
        }

        static void CreateConeSubMesh(ref BrushMesh brushMesh, int segments, int[] segmentDescriptionIndices, float3[] vertices, in ChiselSurfaceDefinition surfaceDefinition)
        {
            ref var chiselSurface0 = ref surfaceDefinition.surfaces[0];

            var polygons = new BrushMesh.Polygon[segments + 1];
            polygons[0] = new BrushMesh.Polygon { surfaceID = 0, firstEdge = 0, edgeCount = segments, surface = chiselSurface0 };

            for (int s = 0, surfaceID = 1; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s + 2]);
                polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = segments + (s * 3), edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
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

            brushMesh.polygons	= polygons;
            brushMesh.halfEdges	= halfEdges;
            brushMesh.vertices  = vertices;
            if (!brushMesh.Validate(logErrors: true))
                brushMesh.Clear();
        }

    }
}