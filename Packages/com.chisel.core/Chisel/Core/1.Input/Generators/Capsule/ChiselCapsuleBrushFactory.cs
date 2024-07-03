using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateCapsule(in ChiselCapsule                                   settings,
                                           in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition,
                                           out BlobAssetReference<BrushMeshBlob>                brushMesh,
                                           Allocator                                            allocator)
        {
            brushMesh = BlobAssetReference<BrushMeshBlob>.Null;
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                if (!GenerateCapsuleVertices(in settings, in builder, ref root, out var localVertices))
                    return false;

                var bottomCap		= !settings.HaveRoundedBottom;
                var topCap			= !settings.HaveRoundedTop;
                var sides			= settings.sides;
                var segments		= settings.Segments;
                var bottomVertex	= settings.BottomVertex;
                var topVertex		= settings.TopVertex;

                if (!GenerateSegmentedSubMesh(sides, segments,
                                              topCap, bottomCap,
                                              topVertex, bottomVertex,
                                              in localVertices, 
                                              ref surfaceDefinition.Value,
                                              in builder, ref root,
                                              out var polygons,
                                              out var halfEdges))
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

        public static bool GenerateCapsuleVertices(in ChiselCapsule     settings,
                                                   in BlobBuilder       builder, 
                                                   ref BrushMeshBlob    root,
                                                   out BlobBuilderArray<float3> localVertices)
        {
            var haveTopHemisphere		= settings.HaveRoundedTop;
            var haveBottomHemisphere	= settings.HaveRoundedBottom;
            var haveMiddleCylinder		= settings.HaveCylinder;

            localVertices = default;
            if (!haveBottomHemisphere && !haveTopHemisphere && !haveMiddleCylinder)
                return false;
            
            var radiusX				= settings.diameterX * 0.5f;
            var radiusZ				= settings.diameterZ * 0.5f;
            var topHeight			= haveTopHemisphere    ? settings.topHeight    : 0;
            var bottomHeight		= haveBottomHemisphere ? settings.bottomHeight : 0;

            var sides				= settings.sides;
            
            var extraVertices		= settings.ExtraVertexCount;

            var bottomRings			= settings.BottomRingCount;
            var topRings			= settings.TopRingCount;
            var ringCount			= settings.RingCount;
            var vertexCount			= settings.VertexCount;

            var bottomVertex		= settings.BottomVertex;
            var topVertex			= settings.TopVertex;

            var topOffset			= settings.TopOffset    + settings.offsetY;
            var bottomOffset		= settings.BottomOffset + settings.offsetY;

            localVertices = builder.Allocate(ref root.localVertices, vertexCount);
            if (haveBottomHemisphere) localVertices[bottomVertex] = new Vector3(0, 1, 0) * (bottomOffset - bottomHeight); // bottom
            if (haveTopHemisphere   ) localVertices[topVertex   ] = new Vector3(0, 1, 0) * (topOffset    + topHeight   ); // top

            var degreePerSegment	= (360.0f / sides) * Mathf.Deg2Rad;
            var angleOffset			= settings.rotation + (((sides & 1) == 1) ? 0.0f : 0.5f * degreePerSegment);

            var topVertexOffset		= extraVertices + ((topRings - 1) * sides);
            var bottomVertexOffset	= extraVertices + ((ringCount - bottomRings) * sides);
            var unitCircleOffset	= topVertexOffset;
            var vertexIndex			= unitCircleOffset;
            {
                for (int h = sides - 1; h >= 0; h--, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    localVertices[vertexIndex] = new Vector3(math.cos(hRad) * radiusX,  
                                                             0.0f, 
                                                             math.sin(hRad) * radiusZ);
                }
            }
            for (int v = 1; v < topRings; v++)
            {
                vertexIndex			= topVertexOffset - (v * sides);
                var segmentFactor	= ((v - (topRings * 0.5f)) / topRings) + 0.5f;	// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);							// [0 .. 90]
                var segmentHeight	= topOffset + 
                                        (math.sin(segmentDegree * Mathf.Deg2Rad) * 
                                            topHeight);
                var segmentRadius	= math.cos(segmentDegree * Mathf.Deg2Rad);		// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    localVertices[vertexIndex].x = localVertices[h + unitCircleOffset].x * segmentRadius;
                    localVertices[vertexIndex].y = segmentHeight;
                    localVertices[vertexIndex].z = localVertices[h + unitCircleOffset].z * segmentRadius; 
                }
            }
            vertexIndex = bottomVertexOffset;
            {
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    localVertices[vertexIndex] = new Vector3(localVertices[h + unitCircleOffset].x,  
                                                             bottomOffset,
                                                             localVertices[h + unitCircleOffset].z);
                }
            }
            for (int v = 1; v < bottomRings; v++)
            {
                var segmentFactor	= ((v - (bottomRings * 0.5f)) / bottomRings) + 0.5f;	// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);									// [0 .. 90]
                var segmentHeight	= bottomOffset - bottomHeight + 
                                        ((1-math.sin(segmentDegree * Mathf.Deg2Rad)) * 
                                            bottomHeight);
                var segmentRadius	= math.cos(segmentDegree * Mathf.Deg2Rad);				// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    localVertices[vertexIndex].x = localVertices[h + unitCircleOffset].x * segmentRadius;
                    localVertices[vertexIndex].y = segmentHeight;
                    localVertices[vertexIndex].z = localVertices[h + unitCircleOffset].z * segmentRadius; 
                }
            }
            {
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    localVertices[h + unitCircleOffset].y = topOffset;
                }
            }
            return true;
        }

        // possible situations:
        //	capsule with top AND bottom set to >0 height
        //	capsule with top OR bottom set to 0 height
        //	capsule with both top AND bottom set to 0 height
        //	capsule with height equal to top and bottom height
        public static bool GenerateCapsuleVertices(ref ChiselCapsule settings, ref Vector3[] vertices)
        {
            var haveTopHemisphere		= settings.HaveRoundedTop;
            var haveBottomHemisphere	= settings.HaveRoundedBottom;
            var haveMiddleCylinder		= settings.HaveCylinder;

            if (!haveBottomHemisphere && !haveTopHemisphere && !haveMiddleCylinder)
                return false;
            
            var radiusX				= settings.diameterX * 0.5f;
            var radiusZ				= settings.diameterZ * 0.5f;
            var topHeight			= haveTopHemisphere    ? settings.topHeight    : 0;
            var bottomHeight		= haveBottomHemisphere ? settings.bottomHeight : 0;
            var totalHeight			= settings.height;
            var cylinderHeight		= settings.CylinderHeight;
            
            var sides				= settings.sides;
            
            var extraVertices		= settings.ExtraVertexCount;

            var bottomRings			= settings.BottomRingCount;
            var topRings			= settings.TopRingCount;
            var ringCount			= settings.RingCount;
            var vertexCount			= settings.VertexCount;

            var bottomVertex		= settings.BottomVertex;
            var topVertex			= settings.TopVertex;

            var topOffset			= settings.TopOffset    + settings.offsetY;
            var bottomOffset		= settings.BottomOffset + settings.offsetY;
            
            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];

            if (haveBottomHemisphere) vertices[bottomVertex] = new Vector3(0, 1, 0) * (bottomOffset - bottomHeight); // bottom
            if (haveTopHemisphere   ) vertices[topVertex   ] = new Vector3(0, 1, 0) * (topOffset    + topHeight   ); // top

            var degreePerSegment	= (360.0f / sides) * Mathf.Deg2Rad;
            var angleOffset			= settings.rotation + (((sides & 1) == 1) ? 0.0f : 0.5f * degreePerSegment);

            var topVertexOffset		= extraVertices + ((topRings - 1) * sides);
            var bottomVertexOffset	= extraVertices + ((ringCount - bottomRings) * sides);
            var unitCircleOffset	= topVertexOffset;
            var vertexIndex			= unitCircleOffset;
            {
                for (int h = sides - 1; h >= 0; h--, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = new Vector3(math.cos(hRad) * radiusX,  
                                                        0.0f, 
                                                        math.sin(hRad) * radiusZ);
                }
            }
            for (int v = 1; v < topRings; v++)
            {
                vertexIndex			= topVertexOffset - (v * sides);
                var segmentFactor	= ((v - (topRings * 0.5f)) / topRings) + 0.5f;	// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);							// [0 .. 90]
                var segmentHeight	= topOffset + 
                                        (math.sin(segmentDegree * Mathf.Deg2Rad) * 
                                            topHeight);
                var segmentRadius	= math.cos(segmentDegree * Mathf.Deg2Rad);		// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + unitCircleOffset].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + unitCircleOffset].z * segmentRadius; 
                }
            }
            vertexIndex = bottomVertexOffset;
            {
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    vertices[vertexIndex] = new Vector3(vertices[h + unitCircleOffset].x,  
                                                        bottomOffset, 
                                                        vertices[h + unitCircleOffset].z);
                }
            }
            for (int v = 1; v < bottomRings; v++)
            {
                var segmentFactor	= ((v - (bottomRings * 0.5f)) / bottomRings) + 0.5f;	// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);									// [0 .. 90]
                var segmentHeight	= bottomOffset - bottomHeight + 
                                        ((1-math.sin(segmentDegree * Mathf.Deg2Rad)) * 
                                            bottomHeight);
                var segmentRadius	= math.cos(segmentDegree * Mathf.Deg2Rad);				// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + unitCircleOffset].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + unitCircleOffset].z * segmentRadius; 
                }
            }
            {
                for (int h = 0; h < sides; h++, vertexIndex++)
                {
                    vertices[h + unitCircleOffset].y = topOffset;
                }
            }
            return true;
        }
    }
}