﻿using System;
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
using Unity.Mathematics;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateCapsule(ref ChiselBrushContainer brushContainer, ref ChiselCapsuleDefinition definition)
        {
            definition.Validate();
            Vector3[] vertices = null;
            if (!BrushMeshFactory.GenerateCapsuleVertices(ref definition, ref vertices))
                return false;

            // TODO: share this with GenerateCapsuleVertices
            var bottomCap		= !definition.haveRoundedBottom;
            var topCap			= !definition.haveRoundedTop;
            var sides			= definition.sides;
            var segments		= definition.segments;
            var bottomVertex	= definition.bottomVertex;
            var topVertex		= definition.topVertex;

            brushContainer.EnsureSize(1);

            return BrushMeshFactory.GenerateSegmentedSubMesh(ref brushContainer.brushMeshes[0], 
                                                             sides, segments, 
                                                             vertices, 
                                                             topCap, bottomCap,  
                                                             topVertex, bottomVertex, 
                                                             definition.surfaceDefinition);
        }

        public static bool GenerateCapsule(ref BrushMesh brushMesh, ref ChiselCapsuleDefinition definition)
        {
            Vector3[] vertices = null;
            if (!BrushMeshFactory.GenerateCapsuleVertices(ref definition, ref vertices))
            {
                brushMesh.Clear();
                return false;
            }

            // TODO: share this with GenerateCapsuleVertices
            var bottomCap		= !definition.haveRoundedBottom;
            var topCap			= !definition.haveRoundedTop;
            var sides			= definition.sides;
            var segments		= definition.segments;
            var bottomVertex	= definition.bottomVertex;
            var topVertex		= definition.topVertex;
            
            if (!BrushMeshFactory.GenerateSegmentedSubMesh(ref brushMesh, 
                                                           sides, segments, 
                                                           vertices, 
                                                           topCap, bottomCap,  
                                                           topVertex, bottomVertex, 
                                                           definition.surfaceDefinition))
            {
                brushMesh.Clear();
                return false;
            }
            return true;
        }

        // possible situations:
        //	capsule with top AND bottom set to >0 height
        //	capsule with top OR bottom set to 0 height
        //	capsule with both top AND bottom set to 0 height
        //	capsule with height equal to top and bottom height
        public static bool GenerateCapsuleVertices(ref ChiselCapsuleDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            var haveTopHemisphere		= definition.haveRoundedTop;
            var haveBottomHemisphere	= definition.haveRoundedBottom;
            var haveMiddleCylinder		= definition.haveCylinder;

            if (!haveBottomHemisphere && !haveTopHemisphere && !haveMiddleCylinder)
                return false;
            
            var radiusX				= definition.diameterX * 0.5f;
            var radiusZ				= definition.diameterZ * 0.5f;
            var topHeight			= haveTopHemisphere    ? definition.topHeight    : 0;
            var bottomHeight		= haveBottomHemisphere ? definition.bottomHeight : 0;
            var totalHeight			= definition.height;
            var cylinderHeight		= definition.cylinderHeight;
            
            var sides				= definition.sides;
            
            var extraVertices		= definition.extraVertexCount;

            var bottomRings			= definition.bottomRingCount;
            var topRings			= definition.topRingCount;
            var ringCount			= definition.ringCount;
            var vertexCount			= definition.vertexCount;

            var bottomVertex		= definition.bottomVertex;
            var topVertex			= definition.topVertex;

            var topOffset			= definition.topOffset    + definition.offsetY;
            var bottomOffset		= definition.bottomOffset + definition.offsetY;
            
            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];

            if (haveBottomHemisphere) vertices[bottomVertex] = new Vector3(0, 1, 0) * (bottomOffset - bottomHeight); // bottom
            if (haveTopHemisphere   ) vertices[topVertex   ] = new Vector3(0, 1, 0) * (topOffset    + topHeight   ); // top

            var degreePerSegment	= (360.0f / sides) * Mathf.Deg2Rad;
            var angleOffset			= definition.rotation + (((sides & 1) == 1) ? 0.0f : 0.5f * degreePerSegment);

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