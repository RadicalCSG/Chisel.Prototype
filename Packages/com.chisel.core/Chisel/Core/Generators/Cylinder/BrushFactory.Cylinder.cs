using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        [Serializable]
        public struct ChiselCircleDefinition
        {
            public float diameterX;
            public float diameterZ;
            public float height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GenerateCylinderSubMesh(float    diameter,
                                                   float    topHeight, 
                                                   float    bottomHeight, 
                                                   float    rotation, 
                                                   int      sides, 
                                                   bool     fitToBounds, 
                                                   in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                   out ChiselBlobAssetReference<BrushMeshBlob> brushMesh,
                                                   Allocator allocator)
        {
            return GenerateConicalFrustumSubMesh(new float2(diameter, diameter), topHeight, 
                                                 new float2(diameter, diameter), bottomHeight, 
                                                 rotation, sides, fitToBounds, 
                                                 in surfaceDefinitionBlob, out brushMesh, allocator);
        }

        public static unsafe bool GenerateConicalFrustumSubMesh(float2 topDiameter,    float topHeight,
                                                                float2 bottomDiameter, float bottomHeight, 
                                                                float                  rotation, 
                                                                int                    segments, 
                                                                bool                   fitToBounds, 
                                                                in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                                out ChiselBlobAssetReference<BrushMeshBlob>                brushMesh,
                                                                Allocator                                            allocator)
        {
            brushMesh = ChiselBlobAssetReference<BrushMeshBlob>.Null;
            if (topHeight > bottomHeight) 
            {
                { var temp = topHeight; topHeight = bottomHeight; bottomHeight = temp; }
                { var temp = topDiameter; topDiameter = bottomDiameter; bottomDiameter = temp; }
            }
            if (segments < 3 || (topHeight - bottomHeight) == 0 || (bottomDiameter.x == 0 && topDiameter.x == 0) || (bottomDiameter.y == 0 && topDiameter.y == 0))
                return false;

            ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
            if (surfaceDefinition.surfaces.Length < segments + 2)
                return false;

            using (var builder = new ChiselBlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                ChiselBlobBuilderArray<float3>                    localVertices;
                ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge>    halfEdges;
                ChiselBlobBuilderArray<BrushMeshBlob.Polygon>     polygons;

                // TODO: handle situation where ellipsoid is a line

                if (topDiameter.x == 0)
                {
                    GetConeFrustumVertices(topHeight, 
                                           bottomDiameter, bottomHeight, 
                                           rotation, segments, 
                                           in builder, ref root, out localVertices, inverse: false, fitToBounds: fitToBounds);

                    // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                    CreateConeSubMesh(segments, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out polygons, out halfEdges);
                } else
                if (bottomDiameter.x == 0)
                {
                    GetConeFrustumVertices(bottomHeight, 
                                           topDiameter, topHeight, 
                                           rotation, segments, 
                                           in builder, ref root, out localVertices, inverse: true, fitToBounds: fitToBounds);

                    // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                    CreateConeSubMesh(segments, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out polygons, out halfEdges);
                } else
                {
                    GetConicalFrustumVertices(topDiameter,    topHeight,
                                              bottomDiameter, bottomHeight, 
                                              rotation, segments, 
                                              in builder, ref root, out localVertices, fitToBounds: fitToBounds);

                    // TODO: the polygon/half-edge part would be the same for any extruded shape and should be re-used
                    CreateExtrudedSubMesh(segments, null, 0, 0, 1, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out polygons, out halfEdges);
                }

                if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                    return false;
                
                var localPlanes             = builder.Allocate(ref root.localPlanes, polygons.Length);
                root.localPlaneCount = polygons.Length;
                // TODO: calculate corner planes
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                root.localBounds = CalculateBounds(in localVertices);
                brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                return true;
            }
        }
        
        // TODO: could probably figure out "inverse" from direction of topY compared to bottomY
        public static void GetConeFrustumVertices(float topHeight, float2 bottomDiameter, float bottomHeight, float rotation, int segments, 
                                                  in ChiselBlobBuilder builder, ref BrushMeshBlob root, out ChiselBlobBuilderArray<float3> localVertices, 
                                                  bool inverse = false, bool fitToBounds = false)
        {
            var rotate			= quaternion.AxisAngle(new float3(0, 1, 0), math.radians(rotation));
            var bottomRadiusX	= math.mul(rotate, new float3(bottomDiameter.x * 0.5f, 0, 0));
            var bottomRadiusZ	= math.mul(rotate, new float3(0, 0, bottomDiameter.y * 0.5f));
            var topY			= new float3(0, topHeight, 0);
            var bottomY			= new float3(0, bottomHeight, 0);

            localVertices = builder.Allocate(ref root.localVertices, segments + 1);
            localVertices[0] = topY;

            const float doublePI = (math.PI * 2);
            var angleStep     = doublePI / segments;
            var angleOffset = ((segments & 1) == 1) ? 0.0f : angleStep * 0.5f;
            var angle = angleOffset;
            for (int v = 0; v < segments; v++, angle += angleStep)
            {
                var s = math.sin(angle);
                var c = math.cos(angle);

                var bottomVertex = (bottomRadiusX * c) + (bottomRadiusZ * s);
                bottomVertex += bottomY;

                var vi = inverse ? (segments - v) : (v + 1);
                localVertices[vi] = bottomVertex;
            }

            if (fitToBounds)
                FitXZ(ref localVertices, 1, localVertices.Length - 1, bottomDiameter);
        }
        
        public static void GetConicalFrustumVertices(float2 topDiameter, float topHeight,
                                                     float2 bottomDiameter, float bottomHeight, 
                                                     float rotation, int segments, 
                                                     in ChiselBlobBuilder builder, ref BrushMeshBlob root, out ChiselBlobBuilderArray<float3> localVertices, 
                                                     bool fitToBounds = false)
        {
            if (topHeight > bottomHeight) 
            {
                { var temp = topHeight;   topHeight   = bottomHeight;   bottomHeight   = temp; }
                { var temp = topDiameter; topDiameter = bottomDiameter; bottomDiameter = temp; }
            }
            
            var rotate		= quaternion.AxisAngle(new float3(0, 1, 0), math.radians(rotation));
            var topAxisX	= math.mul(rotate, new float3(topDiameter.x * 0.5f, 0, 0));
            var topAxisZ	= math.mul(rotate, new float3(0, 0, topDiameter.y * 0.5f));
            var bottomAxisX = math.mul(rotate, new float3(bottomDiameter.x * 0.5f, 0, 0));
            var bottomAxisZ = math.mul(rotate, new float3(0, 0, bottomDiameter.y * 0.5f));
            var topY		= new float3(0, topHeight, 0);
            var bottomY		= new float3(0, bottomHeight, 0);

            // TODO: handle situation where diameterX & diameterZ are 0 (only create one vertex)

            localVertices = builder.Allocate(ref root.localVertices, segments * 2);
            
            const float doublePI = (math.PI * 2);
            var angleStep   = doublePI / segments;
            var angleOffset = ((segments & 1) == 1) ? 0.0f : angleStep * 0.5f;
            var angle       = angleOffset;
            for (int v = 0; v < segments; v++, angle += angleStep)
            {
                var s = Mathf.Sin(angle);
                var c = Mathf.Cos(angle);

                var topVertex = (topAxisX * c) + (topAxisZ * s);
                var bottomVertex = (bottomAxisX * c) + (bottomAxisZ * s);
                topVertex += topY;
                bottomVertex += bottomY;
                localVertices[v] = topVertex;
                localVertices[v + segments] = bottomVertex;
            }

            if (fitToBounds)
            {
                FitXZ(ref localVertices, 0, segments, topDiameter);
                FitXZ(ref localVertices, segments, segments, bottomDiameter);
            }
        }

        static void CreateConeSubMesh(int segments, 
                                      in ChiselBlobBuilderArray<float3>                           localVertices, 
                                      in ChiselBlobAssetReference<NativeChiselSurfaceDefinition>  surfaceDefinitionBlob, 
                                      in ChiselBlobBuilder builder, ref BrushMeshBlob root,
                                      out ChiselBlobBuilderArray<BrushMeshBlob.Polygon>           polygons,
                                      out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge>          halfEdges)
        {
            ref var surfaceDefinition   = ref surfaceDefinitionBlob.Value;
            const int descriptionIndex0 = 0;
            ref var chiselSurface0      = ref surfaceDefinition.surfaces[descriptionIndex0];

            polygons = builder.Allocate(ref root.polygons, segments + 1);
            polygons[0] = new BrushMeshBlob.Polygon { firstEdge = 0, edgeCount = segments, descriptionIndex = descriptionIndex0, surface = chiselSurface0 };

            for (int s = 0, surfaceID = 1; s < segments; s++)
            {
                var descriptionIndex = s + 2;
                polygons[surfaceID] = new BrushMeshBlob.Polygon 
                { 
                    firstEdge           = segments + (s * 3), 
                    edgeCount           = 3, 
                    descriptionIndex    = descriptionIndex,
                    surface             = surfaceDefinition.surfaces[descriptionIndex] 
                };
                surfaceID++;
            }

            halfEdges = builder.Allocate(ref root.halfEdges, 4 * segments);
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
                halfEdges[e0] = new BrushMeshBlob.HalfEdge { vertexIndex = v1, twinIndex = e1 };

                // triangle
                halfEdges[e1] = new BrushMeshBlob.HalfEdge { vertexIndex = v2, twinIndex = e0 };
                halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = v0, twinIndex = t3 };
                halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = v1, twinIndex = t2 };
            }
        }

        public static void FitXZ(float3[] vertices, int offset, int count, float2 expectedSize)
        {
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
                FitXZ(vertices, 0, vertices.Length, new float2(radiusX, radiusZ) * 2.0f);
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

    }
}