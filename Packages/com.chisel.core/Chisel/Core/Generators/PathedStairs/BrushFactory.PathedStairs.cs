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
using Unity.Mathematics;
using UnitySceneExtensions;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static int CountPathedStairBrushes(in NativeList<SegmentVertex> shapeVertices,
                                                  bool              closedLoop,
                                                  
                                                  MinMaxAABB        bounds,

                                                  float	            stepHeight,
                                                  float	            stepDepth,

                                                  float	            treadHeight,

                                                  float	            nosingDepth,

                                                  float             plateauHeight,

                                                  StairsRiserType   riserType,
                                                  float	            riserDepth,

                                                  StairsSideType    leftSide,
                                                  StairsSideType    rightSide,
        
                                                  float	            sideWidth,
                                                  float	            sideHeight,
                                                  float	            sideDepth)
        {
            var totalSubMeshCount = 0;
            for (int i = 0; i < shapeVertices.Length; i++)
            {
                if (i == 0 && !closedLoop)
                    continue;

                var segmentLeftSide  = (!closedLoop && i ==                        1) ? leftSide : StairsSideType.None;
                var segmentRightSide = (!closedLoop && i == shapeVertices.Length - 1) ? rightSide : StairsSideType.None;
                
                var description = new LineairStairsData(bounds, stepHeight, stepDepth, treadHeight,
                                                        nosingDepth, nosingWidth: 0, plateauHeight,
                                                        riserType, riserDepth,
                                                        segmentLeftSide, segmentRightSide,
                                                        sideWidth, sideHeight, sideDepth);
                totalSubMeshCount += description.subMeshCount;
            }
            return totalSubMeshCount;
        }

        // TODO: kind of broken, needs fixing
        public static bool GeneratePathedStairs(NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes,
                                                in NativeList<SegmentVertex> shapeVertices,
                                                bool                closedLoop,
                                                MinMaxAABB          bounds,

                                                float	            stepHeight,
                                                float	            stepDepth,

                                                float	            treadHeight,

                                                float	            nosingDepth,
                                                
                                                float               plateauHeight,

                                                StairsRiserType     riserType,
                                                float	            riserDepth,

                                                StairsSideType      leftSide,
                                                StairsSideType      rightSide,
        
                                                float	            sideWidth,
                                                float	            sideHeight,
                                                float	            sideDepth,
                                                in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                Allocator allocator)
        {
            var absDepth    = math.abs(bounds.Max.z - bounds.Min.z);
            var absHeight   = math.abs(bounds.Max.y - bounds.Min.y);
            var width       = (bounds.Max.x - bounds.Min.x);
            var centerX     = (bounds.Max.x + bounds.Min.x) * 0.5f;

            var halfDepth	= absDepth  * 0.5f;
            var halfHeight	= absHeight * 0.5f;

            int subMeshIndex = 0;
            var count = shapeVertices.Length;
            for (int vi0 = (count + count - 3) % count, vi1 = (count + count - 2) % count, vi2 = (count + count - 1) % count, vi3 = 0; vi3 < count; vi0 = vi1, vi1 = vi2, vi2 = vi3, vi3++)
            {
                if (vi2 == 0 && !closedLoop)
                    continue;

                // TODO: optimize this, we're probably redoing a lot of stuff for every iteration
                var v0 = shapeVertices[vi0].position;
                var v1 = shapeVertices[vi1].position;
                var v2 = shapeVertices[vi2].position;
                var v3 = shapeVertices[vi3].position;

                var m0 = (v0 + v1) * 0.5f;
                var m1 = (v1 + v2) * 0.5f;
                var m2 = (v2 + v3) * 0.5f;

                var d0 = (v1 - v0);
                var d1 = (v2 - v1);
                var d2 = (v3 - v2);

                var maxWidth0 = math.length(d0);
                var maxWidth1 = math.length(d1);
                var maxWidth2 = math.length(d2);
                var halfWidth1 = d1 * 0.5f;

                d0 /= maxWidth0;
                d1 /= maxWidth1;
                d2 /= maxWidth2;

                var depthVector = new float3(d1.y, 0, -d1.x);
                var lineCenter  = new float3(m1.x, halfHeight, m1.y) - (depthVector * halfDepth);

                var depthVector0 = new float2(d0.y, -d0.x) * absDepth;
                var depthVector1 = new float2(d1.y, -d1.x) * absDepth;
                var depthVector2 = new float2(d2.y, -d2.x) * absDepth;

                m0 -= depthVector0;
                m1 -= depthVector1;
                m2 -= depthVector2;

                float2 output;
                var leftShear	= Intersect(m1, d1, m0, d0, out output) ?  math.dot(d1, (output - (m1 - halfWidth1))) : 0;
                var rightShear	= Intersect(m1, d1, m2, d2, out output) ? -math.dot(d1, (output - (m1 + halfWidth1))) : 0;

                var transform = float4x4.TRS(lineCenter, // move to center of line
                                              quaternion.LookRotationSafe(depthVector, Vector3.up),	// rotate to align with line
                                              new float3(1));

                // set the width to the width of the line
                var newWidth = math.abs(maxWidth1);
                var halfWidth = newWidth * 0.5f;
                var min = bounds.Min;
                var max = bounds.Max;
                min.x = centerX - halfWidth;
                max.x = centerX + halfWidth;
                bounds.Min = min;
                bounds.Max = max;

                var segmentLeftSide  = (!closedLoop && vi2 ==                        1) ? leftSide  : StairsSideType.None;
                var segmentRightSide = (!closedLoop && vi2 == shapeVertices.Length - 1) ? rightSide : StairsSideType.None;

                var description = new LineairStairsData(bounds,
                                                        stepHeight, stepDepth,
                                                        treadHeight,
                                                        nosingDepth, nosingWidth: 0,
                                                        plateauHeight,
                                                        riserType, riserDepth,
                                                        segmentLeftSide, segmentRightSide,
                                                        sideWidth, sideHeight, sideDepth);
                var subMeshCount = description.subMeshCount;
                if (subMeshCount == 0)
                    continue;

                if (!GenerateLinearStairsSubMeshes(brushMeshes, subMeshIndex, in description, in surfaceDefinitionBlob))
                    return false;

                for (int m = 0; m < subMeshCount; m++)
                {
                    ref var localVertices = ref brushMeshes[subMeshIndex + m].Value.localVertices;
                    for (int v = 0; v < localVertices.Length; v++)
                    {
                        // TODO: is it possible to put all of this in a single matrix?
                        // lerp the stairs to go from less wide to wider depending on the depth of the vertex
                        var depthFactor = 1.0f - ((localVertices[v].z / absDepth) + 0.5f);
                        var wideFactor  = (localVertices[v].x / halfWidth) + 0.5f;
                        var scale		= (localVertices[v].x / halfWidth);

                        // lerp the stairs width depending on if it's on the left or right side of the stairs
                        localVertices[v].x = math.lerp( scale * (halfWidth - (rightShear * depthFactor)),
                                                        scale * (halfWidth - (leftShear  * depthFactor)),
                                                        wideFactor);
                        localVertices[v] = math.mul(transform, new float4(localVertices[v], 1)).xyz;
                    }
                }

                subMeshIndex += subMeshCount;
            }
            return true;
        }


        // TODO: move somewhere else
        static bool Intersect(float2 p1, float2 d1,
                              float2 p2, float2 d2, out float2 intersection)
        {
            const float kEpsilon = 0.0001f;

            var f = d1.y * d2.x - d1.x * d2.y;
            // check if the rays are parallel
            if (f >= -kEpsilon && f <= kEpsilon)
            {
                intersection = float2.zero;
                return false;
            }

            var c0 = p1 - p2;
            var t = (d2.y * c0.x - d2.x * c0.y) / f;
            intersection = p1 + (t * d1);
            return true;
        }

    }
}