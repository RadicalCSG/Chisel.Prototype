using System;
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
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateLinearStairs(NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes, 
                                                MinMaxAABB      bounds,

                                                float           stepHeight,
                                                float           stepDepth,

                                                float           treadHeight,

                                                float           nosingDepth,
                                                float           nosingWidth,

                                                float           plateauHeight,

                                                StairsRiserType riserType,
                                                float           riserDepth,

                                                StairsSideType  leftSide,
                                                StairsSideType  rightSide,

                                                float           sideWidth,
                                                float           sideHeight,
                                                float           sideDepth, 
                                                in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob)
        {
            if (surfaceDefinitionBlob.Value.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
                return false;

            var description = new LineairStairsData(bounds,
                                                    stepHeight, stepDepth,
                                                    treadHeight,
                                                    nosingDepth, nosingWidth,
                                                    plateauHeight,
                                                    riserType, riserDepth,
                                                    leftSide, rightSide,
                                                    sideWidth, sideHeight, sideDepth);
            int requiredSubMeshCount = description.subMeshCount;
            if (requiredSubMeshCount == 0)
                return false;

            int subMeshOffset = 0;

            return GenerateLinearStairsSubMeshes(brushMeshes, subMeshOffset, in description, in surfaceDefinitionBlob);
        }
        
        // TODO: Fix all overlapping brushes
        internal static unsafe bool GenerateLinearStairsSubMeshes(NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes, int subMeshOffset, in LineairStairsData description, in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob)
        {
            // TODO: properly assign all materials

            ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
            if (surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
                return false;

            var stepOffset = new float3(0, -description.stepHeight, description.stepDepth);
            if (description.stepCount > 0)
            {
                if (description.haveRiser)
                {
                    var min = description.bounds.Min;
                    var max = description.bounds.Max;
                    max.z = min.z + description.stepDepthOffset + description.stepDepth;
                    if (description.riserType != StairsRiserType.FillDown)
                    {
                        if (description.riserType == StairsRiserType.ThinRiser)
                            min.z = max.z - description.riserDepth;
                        else
                            min.z = min.z + description.stepDepthOffset;
                        if (description.thickRiser)
                            min.z -= description.offsetZ;
                    }
                    min.y = max.y - description.stepHeight;
                    min.y -= description.treadHeight;
                    max.y -= description.treadHeight;
                    min.x += description.haveRightSideDown ? description.sideWidth : 0;
                    max.x -= description.haveLeftSideDown  ? description.sideWidth : 0;
                    
                    var extrusion = new float3(max.x - min.x, 0, 0);
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        if (i == 1 &&
                            description.thickRiser)
                        {
                            min.z += description.offsetZ;
                        }
                        if (i == description.stepCount - 1)
                        {
                            min.y += description.treadHeight - description.offsetY;
                        }

                        var minZ = math.max(description.bounds.Min.z, min.z);
                        var maxZ = math.min(description.bounds.Max.z, max.z);

                        float3* vertices = stackalloc float3[4];
                        if (i == 0 || description.riserType != StairsRiserType.Smooth)
                        {
                            vertices[0] = new float3(min.x, min.y, minZ);	// 0
                            vertices[1] = new float3(min.x, min.y, maxZ);	// 1
                            vertices[2] = new float3(min.x, max.y, maxZ);   // 2
                            vertices[3] = new float3(min.x, max.y, minZ);	// 3
                        } else
                        {
                            vertices[0] = new float3(min.x, min.y, minZ);	// 0
                            vertices[1] = new float3(min.x, min.y, maxZ);	// 1
                            vertices[2] = new float3(min.x, max.y, maxZ);   // 2
                            vertices[3] = new float3(min.x, max.y, minZ - description.stepDepth);	// 3
                        }

                        var indices = stackalloc[] { 0, 1, 2, 3, 3, 3 }; // TODO: fix this
                        brushMeshes[subMeshOffset + i] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                        

                        if (description.riserType != StairsRiserType.FillDown)
                            min.z += description.stepDepth;
                        max.z += description.stepDepth;
                        min.y -= description.stepHeight;
                        max.y -= description.stepHeight;
                    }
                }
                if (description.haveTread)
                {
                    var min = new float3(description.bounds.Min.x + description.sideWidth, description.bounds.Max.y - description.treadHeight, description.bounds.Min.z);
                    var max = new float3(description.bounds.Max.x - description.sideWidth, description.bounds.Max.y, description.bounds.Min.z + description.stepDepthOffset + description.stepDepth + description.nosingDepth);
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        min.x = description.bounds.Min.x - ((i == 0) ? description.rightTopNosingWidth : description.rightNosingWidth);
                        max.x = description.bounds.Max.x + ((i == 0) ? description.leftTopNosingWidth : description.leftNosingWidth);
                        if (i == 1)
                        {
                            min.z = max.z - (description.stepDepth + description.nosingDepth);
                        }
                        var vertices = stackalloc[] {
                                                new float3( min.x, min.y, min.z), // 0
                                                new float3( min.x, min.y, max.z), // 1
                                                new float3( min.x, max.y, max.z), // 2
                                                new float3( min.x, max.y, min.z), // 3
                                            };
                        var extrusion = new float3(max.x - min.x, 0, 0);
                        

                        var indices = stackalloc[] { 0, 1, 2, 2, 2, 2 }; // TODO: fix this
                        brushMeshes[subMeshOffset + description.startTread + i] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);

                        min += stepOffset;
                        max += stepOffset;
                    }
                }

                if (description.leftSideDescription.enabled)
                {
                    var minX = description.bounds.Max.x - description.sideWidth;
                    var maxX = description.bounds.Max.x;

                    GenerateStairsSide(brushMeshes, subMeshOffset + description.startLeftSide, description.stepCount, minX, maxX, description.leftSideType, in description, ref surfaceDefinition, description.leftSideDescription);
                }

                if (description.rightSideDescription.enabled)
                {
                    var minX = description.bounds.Min.x;
                    var maxX = description.bounds.Min.x + description.sideWidth;

                    GenerateStairsSide(brushMeshes, subMeshOffset + description.startRightSide, description.stepCount, minX, maxX, description.rightSideType, in description, ref surfaceDefinition, description.rightSideDescription);
                }
            }
            return true;
        }

        static unsafe BlobAssetReference<BrushMeshBlob> CreateExtrudedSubMeshBlob(float3* vertices, int vertexCount, float3 extrusion, int* indices, ref NativeChiselSurfaceDefinition surfaceDefinition)
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                if (BrushMeshFactory.CreateExtrudedSubMesh(vertices, vertexCount, extrusion, indices,
                                                           ref surfaceDefinition,
                                                           in builder, ref root,
                                                           out var polygons,
                                                           out var halfEdges,
                                                           out var localVertices))
                {
                    // TODO: eventually remove when it's more battle tested
                    if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                        return BlobAssetReference<BrushMeshBlob>.Null;

                    var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                    var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                    CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                    UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                    root.localBounds = CalculateBounds(in localVertices);
                    return builder.CreateBlobAssetReference<BrushMeshBlob>(Allocator.Persistent);
                } else
                    return BlobAssetReference<BrushMeshBlob>.Null;
            }
        }

        private static unsafe void GenerateStairsSide(NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes, int startIndex, int stepCount, float minX, float maxX, StairsSideType sideType, in LineairStairsData description, ref NativeChiselSurfaceDefinition surfaceDefinition, in LinearStairsSideData side)
        {
            var min = new float3(minX, description.bounds.Max.y - description.treadHeight - description.stepHeight, description.bounds.Min.z + description.stepDepthOffset);
            var max = new float3(maxX, description.bounds.Max.y - description.treadHeight                         , description.bounds.Min.z + description.stepDepthOffset + description.stepDepth);

            var maxZ = description.bounds.Max.z - description.riserDepth;

            var aspect = description.stepHeight / description.stepDepth;

            if (sideType == StairsSideType.DownAndUp)
            {
                var extrusion = new float3(max.x - min.x, 0, 0);
                // Top "wall"
                if (description.stepDepthOffset > description.sideDepth)
                {
                    // z0 y0 *----* z1 y0 
                    //       |    |
                    // z0 y1 *----* z1 y2 
                    var y0 = max.y + description.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var z0 = description.bounds.Min.z;
                    var z1 = min.z - description.sideDepth;
                    var vertices = stackalloc[] {
                                        new float3( min.x, y0, z0), // 0
                                        new float3( min.x, y1, z0), // 1
                                        new float3( min.x, y1, z1), // 2
                                        new float3( min.x, y0, z1), // 3
                                    };

                    var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                    brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                } else
                    startIndex--;

                // "wall" on stair steps
                if (description.sideDepth <= 0)
                {
                    var y1 = max.y;
                    var y0 = y1 + description.sideHeight;
                    var y2 = min.y - (description.stepHeight * (stepCount - 1));
                    var y3 = y2 + description.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (description.stepDepth * (stepCount - 1));

                    // z0 y0 *
                    //       |\
                    //       | \
                    //       |  \
                    //       |   \
                    //       |    \
                    //       |     \
                    // z0 y1 *      * z1 y3 
                    //        \     |
                    //         \    |
                    //          \   |
                    //           \  |
                    //            \ |
                    //             \|
                    //              * z1 y2 

                    var vertices = stackalloc[] {
                                        new float3( min.x, y0, z0), // 0
                                        new float3( min.x, y1, z0), // 1
                                        new float3( min.x, y2, z1), // 2
                                        new float3( min.x, y3, z1), // 3
                                    };

                    var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                    brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                } else
                {
                    var y1 = max.y;
                    var y0 = y1 + description.sideHeight;
                    var y2 = description.bounds.Min.y;
                    var y4 = min.y - (description.stepHeight * (stepCount - 1));
                    var y3 = y4 + description.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (description.stepDepth * (stepCount - 1));
                    var z2 = z0 - description.sideDepth;
                    var z3 = z1 - description.sideDepth - ((y2- y4) / aspect);

                    if (z2 < description.bounds.Min.z)
                    {
                        y1 -= (description.bounds.Min.z - z2) * aspect;
                        z2 = description.bounds.Min.z;
                    }
                    if (y1 < description.bounds.Min.y)
                    {
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        //       |      \
                        //       |       \
                        //       |        \
                        //       |         \
                        //       |          * y3 z1 
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        // y2 z2 *----------* y2 z1 

                        var vertices = stackalloc[] {
                                            new float3( min.x, y0, z2), // 0
                                            new float3( min.x, y2, z2), // 1
                                            new float3( min.x, y2, z1), // 2
                                            new float3( min.x, y3, z1), // 3
                                            new float3( min.x, y0, z0), // 4
                                        };

                        var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3, 3, 3 }; // TODO: fix this
                        brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, 5, extrusion, indices, ref surfaceDefinition);
                    } else
                    if (z3 > maxZ)
                    {
                        var z4 = maxZ;
                        var y5 = y2 + ((z3 - z4) * aspect);
                        var y6 = y3 + ((z1 - z4) * aspect);
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        // y1 z2 *      * y6 z4 
                        //        \     | 
                        //         \    |
                        //          \   |
                        //           \  |
                        //            \ |
                        //             \|
                        //              * y5 z4 

                        { 
                            var vertices = stackalloc[] {
                                                new float3( min.x, y0, z2), // 0
                                                new float3( min.x, y1, z2), // 1
                                                new float3( min.x, y5, z4), // 2
                                                new float3( min.x, y6, z4), // 3
                                                new float3( min.x, y0, z0), // 4
                                            };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, 5, extrusion, indices, ref surfaceDefinition);
                        }

                        if ((description.sideDepth * aspect) < (description.plateauHeight - description.treadHeight))
                        {
                            var y7 = description.bounds.Min.y;
                            // y6 z4 *
                            //       |\
                            //       | \
                            //       |  \
                            //       |   \
                            //       |    * y3 z1 
                            //       |    |
                            // y7 z4 *----* y7 z1 

                            var vertices = stackalloc[] {
                                        new float3( min.x, y6, z4), // 0
                                        new float3( min.x, y7, z4), // 1
                                        new float3( min.x, y7, z1), // 2
                                        new float3( min.x, y3, z1), // 3
                                    };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[startIndex + 2] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                        }
                    } else
                    {
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        //       |      \
                        //       |       \
                        //       |        \
                        //       |         \
                        // y1 z2 *          * y3 z1 
                        //        \         |
                        //         \        |
                        //          \       |
                        //           \      |
                        //            \     |
                        //             \    |
                        //        y2 z3 *---* y2 z1 

                        var vertices = stackalloc[] {
                                            new float3( min.x, y0, z2), // 0
                                            new float3( min.x, y1, z2), // 1
                                            new float3( min.x, y2, z3), // 2
                                            new float3( min.x, y2, z1), // 3
                                            new float3( min.x, y3, z1), // 4
                                            new float3( min.x, y0, z0), // 5
                                        };

                        var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3, 3, 3 }; // TODO: fix this
                        brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, 6, extrusion, indices, ref surfaceDefinition);
                    }
                }
            } else
            if (sideType == StairsSideType.Up)
            {
                var extrusion = new float3(max.x - min.x, 0, 0);
                // Top "wall"
                if ((min.z - description.bounds.Min.z) > 0)
                {
                    // z0 y0 *----* z1 y0 
                    //       |    |
                    // z0 y1 *----* z1 y2 
                    var y0 = max.y + description.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var z0 = description.bounds.Min.z;
                    var z1 = min.z;
                    var vertices = stackalloc[] {
                                        new float3( min.x, y0, z0), // 0
                                        new float3( min.x, y1, z0), // 1
                                        new float3( min.x, y1, z1), // 2
                                        new float3( min.x, y0, z1), // 3
                                    };

                    var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                    brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                } else
                    startIndex--;
                // "wall" on stair steps
                for (int i = 0, j = startIndex + 1; i < stepCount; i++, j++)
                {
                    var y0 = max.y + description.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var y3 = min.y + description.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z;

                    // z0 y0 *
                    //       |\
                    //       | \
                    //       |  \
                    //       |   \
                    //       |    * z1 y3 
                    //       |    |
                    // z0 y1 *----* z1 y2 

                    var vertices = stackalloc[] {
                                        new float3( min.x, y0, z0), // 0
                                        new float3( min.x, y1, z0), // 1
                                        new float3( min.x, y1, z1), // 2
                                        new float3( min.x, y3, z1), // 3
                                    };

                    var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                    brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);

                    min.z += description.stepDepth;
                    max.z += description.stepDepth;
                    min.y -= description.stepHeight;
                    max.y -= description.stepHeight;
                }
            } else
            if (sideType == StairsSideType.Down)
            {
                var extrusion = new float3(max.x - min.x, 0, 0);
                if (description.sideDepth == 0)
                {
                    for (int i = 0, j = startIndex; i < stepCount - 1; i++, j++)
                    {
                        var y0 = max.y;
                        var y1 = min.y;
                        var z0 = min.z;
                        var z1 = max.z;

                        // z0 y0 *------* z1 y0 
                        //        \     |
                        //         \    |
                        //          \   |
                        //           \  |
                        //            \ |
                        //             \|
                        //              * z1 y1 
                        var vertices = stackalloc[] {
                                        new float3( min.x, y0, z0), // 0
                                        new float3( min.x, y1, z1), // 1
                                        new float3( min.x, y0, z1), // 2
                                    };

                        var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                        brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, 3, extrusion, indices, ref surfaceDefinition);

                        min.z += description.stepDepth;
                        max.z += description.stepDepth;
                        min.y -= description.stepHeight;
                        max.y -= description.stepHeight;
                    }
                } else
                {
                    {
                        var y0 = max.y;
                        var y1 = min.y - (description.sideDepth * aspect);
                        var z0 = min.z - description.sideDepth;
                        var z1 = max.z;
                        if (y1 < description.bounds.Min.y)
                        {
                            var y2 = description.bounds.Min.y;
                            var z2 = max.z - ((description.bounds.Min.y - y1) / aspect);
                            if (z2 < description.bounds.Min.z)
                            {
                                var z3 = description.bounds.Min.z;
                                // z3 y0 *---------* z1 y0 
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                // z3 y2 *---------* z1 y2 
                                var vertices = stackalloc[] {
                                                new float3( min.x, y0, z3), // 0
                                                new float3( min.x, y2, z3), // 1
                                                new float3( min.x, y2, z1), // 2
                                                new float3( min.x, y0, z1), // 3
                                            };

                                var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                                brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);

                                min.z += description.stepDepth;
                                max.z += description.stepDepth;
                                min.y -= description.stepHeight;
                                max.y -= description.stepHeight;
                            } else
                            if (z0 < description.bounds.Min.z)
                            {
                                var z3 = description.bounds.Min.z;
                                var y3 = max.y - ((description.bounds.Min.z - z0) * aspect);
                                // z3 y0 *---------* z1 y0 
                                //       |         |
                                //       |         |
                                //       |         |
                                // z3 y3 *         |
                                //        \        |
                                //         \       |
                                //          \      |
                                //           \     |
                                //            \    |
                                //             \   |
                                //        y2 z2 *--* z1 y2
                                var vertices = stackalloc[] {
                                                new float3( min.x, y0, z3), // 0
                                                new float3( min.x, y3, z3), // 1
                                                new float3( min.x, y2, z2), // 2
                                                new float3( min.x, y2, z1), // 3
                                                new float3( min.x, y0, z1), // 4
                                            };

                                var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                                brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 5, extrusion, indices, ref surfaceDefinition);

                                min.z += description.stepDepth;
                                max.z += description.stepDepth;
                                min.y -= description.stepHeight;
                                max.y -= description.stepHeight;
                            } else
                            {
                                // z0 y0 *---------* z1 y0 
                                //        \        |
                                //         \       |
                                //          \      |
                                //           \     |
                                //            \    |
                                //             \   |
                                //        z2 y2 *--* z1 y2 
                                var vertices = stackalloc[] {
                                                new float3( min.x, y0, z0), // 0
                                                new float3( min.x, y2, z2), // 1
                                                new float3( min.x, y2, z1), // 2
                                                new float3( min.x, y0, z1), // 3
                                            };

                                var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                                brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);

                                min.z += description.stepDepth;
                                max.z += description.stepDepth;
                                min.y -= description.stepHeight;
                                max.y -= description.stepHeight;
                            }
                        } else
                        if (z0 < description.bounds.Min.z)
                        {
                            var z2 = description.bounds.Min.z;
                            var y2 = max.y - ((description.bounds.Min.z - z0) * aspect);
                            // z2 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z2 y2 *      |
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            var vertices = stackalloc[] {
                                            new float3( min.x, y0, z2), // 0
                                            new float3( min.x, y2, z2), // 1
                                            new float3( min.x, y1, z1), // 2
                                            new float3( min.x, y0, z1), // 3
                                        };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);

                            min.z += description.stepDepth;
                            max.z += description.stepDepth;
                            min.y -= description.stepHeight;
                            max.y -= description.stepHeight;
                        } else
                        {
                            // z0 y0 *------* z1 y0 
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            var vertices = stackalloc[] {
                                            new float3( min.x, y0, z0), // 0
                                            new float3( min.x, y1, z1), // 1
                                            new float3( min.x, y0, z1), // 2
                                        };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, 3, extrusion, indices, ref surfaceDefinition);

                            min.z += description.stepDepth;
                            max.z += description.stepDepth;
                            min.y -= description.stepHeight;
                            max.y -= description.stepHeight;
                        }
                    }
                    for (int i = 1, j = startIndex + 1; i < stepCount - 1; i++, j++)
                    {
                        var y0 = max.y;
                        var y1 = min.y - (description.sideDepth * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var z0 = min.z;
                        var z1 = max.z;

                        if (y2 < description.bounds.Min.y)
                        {
                            var y3 = description.bounds.Min.y;
                            // z0 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z0 y3 *------* z1 y3 
                            var vertices = stackalloc[] {
                                            new float3( min.x, y0, z0), // 0
                                            new float3( min.x, y3, z0), // 1
                                            new float3( min.x, y3, z1), // 2
                                            new float3( min.x, y0, z1), // 3
                                        };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                        } else
                        if (y1 < description.bounds.Min.y)
                        {
                            var z2 = max.z - ((description.bounds.Min.y - y1) / aspect);
                            var y3 = description.bounds.Min.y;
                            // z0 y0 *---------* z1 y0 
                            //       |         |
                            //       |         |
                            //       |         |
                            // z0 y2 *         |
                            //        \        |
                            //         \       |
                            //          \      |
                            //           \     |
                            //            \    |
                            //             \   |
                            //        y3 z2 *--* z1 y3
                            var vertices = stackalloc[] {
                                            new float3( min.x, y0, z0), // 0
                                            new float3( min.x, y2, z0), // 1
                                            new float3( min.x, y3, z2), // 2
                                            new float3( min.x, y3, z1), // 3
                                            new float3( min.x, y0, z1), // 4
                                        };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, 5, extrusion, indices, ref surfaceDefinition);
                        } else
                        {
                            // z0 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z0 y2 *      |
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            var vertices = stackalloc[] {
                                            new float3( min.x, y0, z0), // 0
                                            new float3( min.x, y2, z0), // 1
                                            new float3( min.x, y1, z1), // 2
                                            new float3( min.x, y0, z1), // 3
                                        };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                        }

                        min.z += description.stepDepth;
                        max.z += description.stepDepth;
                        min.y -= description.stepHeight;
                        max.y -= description.stepHeight;
                    }
                    if (description.riserType == StairsRiserType.ThinRiser &&
                        description.riserDepth > description.stepDepth &&
                        description.sideDepth < (description.plateauHeight - description.treadHeight) / aspect &&
                        description.riserDepth < description.absDepth)
                    {
                        var z0 = description.bounds.Max.z - description.riserDepth;
                        var z1 = min.z;
                        var y1 = max.y - (description.sideDepth * aspect) + ((min.z - z0) * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var y3 = description.bounds.Min.y;
                        if (y2 <= y3)
                        {
                            if (y2 < y3)
                            {
                                z1 -= (y3 - y2) / aspect;
                            }
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            // y3 z0 *---* y3 z1 
                            var vertices = stackalloc[] {
                                                new float3( min.x, y1, z0), // 0
                                                new float3( min.x, y3, z0), // 1
                                                new float3( min.x, y3, z1), // 2
                                            };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[startIndex + stepCount] = CreateExtrudedSubMeshBlob(vertices, 3, extrusion, indices, ref surfaceDefinition);
                        } else
                        {
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            //       |   * y2 z1 
                            //       |   |
                            // y3 z0 *---* y3 z1 
                            var vertices = stackalloc[] {
                                                new float3( min.x, y1, z0), // 0
                                                new float3( min.x, y3, z0), // 1
                                                new float3( min.x, y3, z1), // 2
                                                new float3( min.x, y2, z1), // 3
                                            };

                            var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                            brushMeshes[startIndex + stepCount] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                        }
                    }
                }
                {
                    var y0 = max.y;
                    var z0 = min.z;
                    var z1 = max.z;

                    var y3 = description.bounds.Min.y;
                    // z0 y0 *------* z1 y0 
                    //       |      |
                    //       |      |
                    //       |      |
                    // z0 y3 *------* z1 y3 
                    var vertices = stackalloc[] {
                                        new float3( min.x, y0, z0), // 0
                                        new float3( min.x, y3, z0), // 1
                                        new float3( min.x, y3, z1), // 2
                                        new float3( min.x, y0, z1), // 3
                                    };

                    var indices = stackalloc[] { 0, 1, 2, 3, 3, 3, 3 }; // TODO: fix this
                    brushMeshes[startIndex + stepCount - 1] = CreateExtrudedSubMeshBlob(vertices, 4, extrusion, indices, ref surfaceDefinition);
                }
            }
        }


        public static bool GenerateLinearStairs(ref ChiselBrushContainer brushContainer, ref ChiselLinearStairsDefinition definition)
        {
            definition.Validate();

            if (!definition.HasVolume)
            {
                brushContainer.Reset();
                return false;
            }

            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
            {
                brushContainer.Reset();
                return false;
            }

            var minMaxAABB = new MinMaxAABB { Min = definition.bounds.min, Max = definition.bounds.max };
            var description = new LineairStairsData(minMaxAABB,
                                                    definition.stepHeight, definition.stepDepth,
                                                    definition.treadHeight,
                                                    definition.nosingDepth, definition.nosingWidth,
                                                    definition.plateauHeight,
                                                    definition.riserType, definition.riserDepth,
                                                    definition.leftSide, definition.rightSide,
                                                    definition.sideWidth, definition.sideHeight, definition.sideDepth);
            int requiredSubMeshCount = description.subMeshCount;
            if (requiredSubMeshCount == 0)
            {
                brushContainer.Reset();
                return false;
            }
            
            int subMeshOffset = 0;

            brushContainer.EnsureSize(requiredSubMeshCount);

            return GenerateLinearStairsSubMeshes(ref brushContainer, ref description, definition, definition.leftSide, definition.rightSide, subMeshOffset);
        }

        internal struct LinearStairsSideData
        {
            public bool enabled;        // TODO: just check if subMeshCount == 0
            public int  subMeshCount;

            // TODO: just turn into method that returns subMeshCount
            public LinearStairsSideData(float stepHeight,
                                        float stepDepth,
                                        float plateauHeight,
                                        float treadHeight,
                                        float absDepth,
                                        float stepDepthOffset,
                                        int stepCount, float sideDepth, MinMaxAABB bounds, StairsRiserType riserType, float riserDepth, StairsSideType sideType)
            {
                this.enabled = sideType != StairsSideType.None;

                if (!enabled)
                {
                    this.subMeshCount = 0;
                    return;
                }

                if (sideType == StairsSideType.Down)
                {
                    this.subMeshCount = stepCount;

                    var aspect = stepHeight / stepDepth;
                    if (riserType == StairsRiserType.ThinRiser &&
                        riserDepth > stepDepth && 
                        sideDepth < (plateauHeight - treadHeight) / aspect &&
                        riserDepth < absDepth)
                    {
                        this.subMeshCount++;
                    }
                } else
                if (sideType == StairsSideType.Up)
                {
                    if (stepDepthOffset > 0)
                        this.subMeshCount = stepCount + 1;
                    else
                        this.subMeshCount = stepCount;
                } else
                if (sideType == StairsSideType.DownAndUp)
                {
                    if (stepDepthOffset > sideDepth)
                        this.subMeshCount = 2;
                    else
                        this.subMeshCount = 1;

                    if ((bounds.Max.y - treadHeight) >= bounds.Min.y)
                    {
                        var aspect = stepHeight / stepDepth;
                        if ((sideDepth * aspect) < (plateauHeight - treadHeight))
                            this.subMeshCount ++;
                    }
                } else
                    this.subMeshCount = 0;
            }
        }

        private static void GenerateStairsSide(ref ChiselBrushContainer brushContainer, int startIndex, int stepCount, float minX, float maxX, StairsSideType sideType, ChiselLinearStairsDefinition definition, in LineairStairsData description, in LinearStairsSideData side)
        {
            var min = new Vector3(minX, description.bounds.Max.y - definition.treadHeight - definition.stepHeight, description.bounds.Min.z + definition.StepDepthOffset);
            var max = new Vector3(maxX, description.bounds.Max.y - definition.treadHeight                        , description.bounds.Min.z + definition.StepDepthOffset + definition.stepDepth);

            var maxZ = description.bounds.Max.z - description.riserDepth;

            var aspect = definition.stepHeight / definition.stepDepth;

            if (sideType == StairsSideType.DownAndUp)
            {
                var extrusion = new Vector3(max.x - min.x, 0, 0);
                // Top "wall"
                if (definition.StepDepthOffset > description.sideDepth)
                {
                    // z0 y0 *----* z1 y0 
                    //       |    |
                    // z0 y1 *----* z1 y2 
                    var y0 = max.y + definition.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var z0 = description.bounds.Min.z;
                    var z1 = min.z - description.sideDepth;
                    var vertices = new[] {
                                        new Vector3( min.x, y0, z0), // 0
                                        new Vector3( min.x, y1, z0), // 1
                                        new Vector3( min.x, y1, z1), // 2
                                        new Vector3( min.x, y0, z1), // 3
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    definition.surfaceDefinition);
                } else
                    startIndex--;

                // "wall" on stair steps
                if (description.sideDepth <= 0)
                {
                    var y1 = max.y;
                    var y0 = y1 + definition.sideHeight;
                    var y2 = min.y - (definition.stepHeight * (stepCount - 1));
                    var y3 = y2 + definition.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (definition.stepDepth * (stepCount - 1));

                    // z0 y0 *
                    //       |\
                    //       | \
                    //       |  \
                    //       |   \
                    //       |    \
                    //       |     \
                    // z0 y1 *      * z1 y3 
                    //        \     |
                    //         \    |
                    //          \   |
                    //           \  |
                    //            \ |
                    //             \|
                    //              * z1 y2 

                    var vertices = new[] {
                                        new Vector3( min.x, y0, z0), // 0
                                        new Vector3( min.x, y1, z0), // 1
                                        new Vector3( min.x, y2, z1), // 2
                                        new Vector3( min.x, y3, z1), // 3
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + 1], vertices, extrusion,
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    definition.surfaceDefinition);
                } else
                {
                    var y1 = max.y;
                    var y0 = y1 + definition.sideHeight;
                    var y2 = description.bounds.Min.y;
                    var y4 = min.y - (definition.stepHeight * (stepCount - 1));
                    var y3 = y4 + definition.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (definition.stepDepth * (stepCount - 1));
                    var z2 = z0 - description.sideDepth;
                    var z3 = z1 - description.sideDepth - ((y2- y4) / aspect);

                    if (z2 < description.bounds.Min.z)
                    {
                        y1 -= (description.bounds.Min.z - z2) * aspect;
                        z2 = description.bounds.Min.z;
                    }
                    if (y1 < description.bounds.Min.y)
                    {
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        //       |      \
                        //       |       \
                        //       |        \
                        //       |         \
                        //       |          * y3 z1 
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        //       |          |
                        // y2 z2 *----------* y2 z1 

                        var vertices = new[] {
                                            new Vector3( min.x, y0, z2), // 0
                                            new Vector3( min.x, y2, z2), // 1
                                            new Vector3( min.x, y2, z1), // 2
                                            new Vector3( min.x, y3, z1), // 3
                                            new Vector3( min.x, y0, z0), // 4
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + 1], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    } else
                    if (z3 > maxZ)
                    {
                        var z4 = maxZ;
                        var y5 = y2 + ((z3 - z4) * aspect);
                        var y6 = y3 + ((z1 - z4) * aspect);
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        // y1 z2 *      * y6 z4 
                        //        \     | 
                        //         \    |
                        //          \   |
                        //           \  |
                        //            \ |
                        //             \|
                        //              * y5 z4 

                        var vertices = new[] {
                                            new Vector3( min.x, y0, z2), // 0
                                            new Vector3( min.x, y1, z2), // 1
                                            new Vector3( min.x, y5, z4), // 3
                                            new Vector3( min.x, y6, z4), // 4
                                            new Vector3( min.x, y0, z0), // 5
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + 1], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                        if ((definition.sideDepth * aspect) < (definition.plateauHeight - definition.treadHeight))
                        {
                            var y7 = description.bounds.Min.y;
                            // y6 z4 *
                            //       |\
                            //       | \
                            //       |  \
                            //       |   \
                            //       |    * y3 z1 
                            //       |    |
                            // y7 z4 *----* y7 z1 

                            vertices = new[] {
                                        new Vector3( min.x, y6, z4), // 0
                                        new Vector3( min.x, y7, z4), // 1
                                        new Vector3( min.x, y7, z1), // 2
                                        new Vector3( min.x, y3, z1), // 3
                                    };

                            BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + 2], vertices, extrusion,
                                            new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                            definition.surfaceDefinition);
                        }
                    } else
                    {
                        // y0 z2 *---* y0 z0 
                        //       |    \
                        //       |     \
                        //       |      \
                        //       |       \
                        //       |        \
                        //       |         \
                        // y1 z2 *          * y3 z1 
                        //        \         |
                        //         \        |
                        //          \       |
                        //           \      |
                        //            \     |
                        //             \    |
                        //        y2 z3 *---* y2 z1 

                        var vertices = new[] {
                                            new Vector3( min.x, y0, z2), // 0
                                            new Vector3( min.x, y1, z2), // 1
                                            new Vector3( min.x, y2, z3), // 2
                                            new Vector3( min.x, y2, z1), // 3
                                            new Vector3( min.x, y3, z1), // 4
                                            new Vector3( min.x, y0, z0), // 5
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + 1], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                }
            } else
            if (sideType == StairsSideType.Up)
            {
                var extrusion = new Vector3(max.x - min.x, 0, 0);
                // Top "wall"
                if ((min.z - description.bounds.Min.z) > 0)
                {
                    // z0 y0 *----* z1 y0 
                    //       |    |
                    // z0 y1 *----* z1 y2 
                    var y0 = max.y + definition.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var z0 = description.bounds.Min.z;
                    var z1 = min.z;
                    var vertices = new[] {
                                        new Vector3( min.x, y0, z0), // 0
                                        new Vector3( min.x, y1, z0), // 1
                                        new Vector3( min.x, y1, z1), // 2
                                        new Vector3( min.x, y0, z1), // 3
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    definition.surfaceDefinition);
                } else
                    startIndex--;
                // "wall" on stair steps
                for (int i = 0, j = startIndex + 1; i < stepCount; i++, j++)
                {
                    var y0 = max.y + definition.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var y3 = min.y + definition.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z;

                    // z0 y0 *
                    //       |\
                    //       | \
                    //       |  \
                    //       |   \
                    //       |    * z1 y3 
                    //       |    |
                    // z0 y1 *----* z1 y2 

                    var vertices = new[] {
                                        new Vector3( min.x, y0, z0), // 0
                                        new Vector3( min.x, y1, z0), // 1
                                        new Vector3( min.x, y1, z1), // 2
                                        new Vector3( min.x, y3, z1), // 3
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j], vertices, extrusion,
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    definition.surfaceDefinition);

                    min.z += definition.stepDepth;
                    max.z += definition.stepDepth;
                    min.y -= definition.stepHeight;
                    max.y -= definition.stepHeight;
                }
            } else
            if (sideType == StairsSideType.Down)
            {
                Vector3[] vertices;
                var extrusion = new Vector3(max.x - min.x, 0, 0);
                if (description.sideDepth == 0)
                {
                    for (int i = 0, j = startIndex; i < stepCount - 1; i++, j++)
                    {
                        var y0 = max.y;
                        var y1 = min.y;
                        var z0 = min.z;
                        var z1 = max.z;

                        // z0 y0 *------* z1 y0 
                        //        \     |
                        //         \    |
                        //          \   |
                        //           \  |
                        //            \ |
                        //             \|
                        //              * z1 y1 
                        vertices = new[] {
                                        new Vector3( min.x, y0, z0), // 0
                                        new Vector3( min.x, y1, z1), // 1
                                        new Vector3( min.x, y0, z1), // 2
                                    };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);

                        min.z += definition.stepDepth;
                        max.z += definition.stepDepth;
                        min.y -= definition.stepHeight;
                        max.y -= definition.stepHeight;
                    }
                } else
                {
                    {
                        var y0 = max.y;
                        var y1 = min.y - (description.sideDepth * aspect);
                        var z0 = min.z - description.sideDepth;
                        var z1 = max.z;
                        if (y1 < description.bounds.Min.y)
                        {
                            var y2 = description.bounds.Min.y;
                            var z2 = max.z - ((description.bounds.Min.y - y1) / aspect);
                            if (z2 < description.bounds.Min.z)
                            {
                                var z3 = description.bounds.Min.z;
                                // z3 y0 *---------* z1 y0 
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                //       |         |
                                // z3 y2 *---------* z1 y2 
                                vertices = new[] {
                                                new Vector3( min.x, y0, z3), // 0
                                                new Vector3( min.x, y2, z3), // 1
                                                new Vector3( min.x, y2, z1), // 2
                                                new Vector3( min.x, y0, z1), // 3
                                            };

                                BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                                new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                                definition.surfaceDefinition);

                                min.z += definition.stepDepth;
                                max.z += definition.stepDepth;
                                min.y -= definition.stepHeight;
                                max.y -= definition.stepHeight;
                            } else
                            if (z0 < description.bounds.Min.z)
                            {
                                var z3 = description.bounds.Min.z;
                                var y3 = max.y - ((description.bounds.Min.z - z0) * aspect);
                                // z3 y0 *---------* z1 y0 
                                //       |         |
                                //       |         |
                                //       |         |
                                // z3 y3 *         |
                                //        \        |
                                //         \       |
                                //          \      |
                                //           \     |
                                //            \    |
                                //             \   |
                                //        y2 z2 *--* z1 y2
                                vertices = new[] {
                                                new Vector3( min.x, y0, z3), // 0
                                                new Vector3( min.x, y3, z3), // 1
                                                new Vector3( min.x, y2, z2), // 2
                                                new Vector3( min.x, y2, z1), // 2
                                                new Vector3( min.x, y0, z1), // 3
                                            };

                                BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                                new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                                definition.surfaceDefinition);

                                min.z += definition.stepDepth;
                                max.z += definition.stepDepth;
                                min.y -= definition.stepHeight;
                                max.y -= definition.stepHeight;
                            } else
                            {
                                // z0 y0 *---------* z1 y0 
                                //        \        |
                                //         \       |
                                //          \      |
                                //           \     |
                                //            \    |
                                //             \   |
                                //        z2 y2 *--* z1 y2 
                                vertices = new[] {
                                                new Vector3( min.x, y0, z0), // 0
                                                new Vector3( min.x, y2, z2), // 1
                                                new Vector3( min.x, y2, z1), // 2
                                                new Vector3( min.x, y0, z1), // 3
                                            };

                                BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                                new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                                definition.surfaceDefinition);

                                min.z += definition.stepDepth;
                                max.z += definition.stepDepth;
                                min.y -= definition.stepHeight;
                                max.y -= definition.stepHeight;
                            }
                        } else
                        if (z0 < description.bounds.Min.z)
                        {
                            var z2 = description.bounds.Min.z;
                            var y2 = max.y - ((description.bounds.Min.z - z0) * aspect);
                            // z2 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z2 y2 *      |
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            vertices = new[] {
                                            new Vector3( min.x, y0, z2), // 0
                                            new Vector3( min.x, y2, z2), // 1
                                            new Vector3( min.x, y1, z1), // 2
                                            new Vector3( min.x, y0, z1), // 3
                                        };

                            BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                            new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                            definition.surfaceDefinition);

                            min.z += definition.stepDepth;
                            max.z += definition.stepDepth;
                            min.y -= definition.stepHeight;
                            max.y -= definition.stepHeight;
                        } else
                        {
                            // z0 y0 *------* z1 y0 
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            vertices = new[] {
                                            new Vector3( min.x, y0, z0), // 0
                                            new Vector3( min.x, y1, z1), // 1
                                            new Vector3( min.x, y0, z1), // 2
                                        };

                            BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex], vertices, extrusion,
                                            new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                            definition.surfaceDefinition);

                            min.z += definition.stepDepth;
                            max.z += definition.stepDepth;
                            min.y -= definition.stepHeight;
                            max.y -= definition.stepHeight;
                        }
                    }
                    for (int i = 1, j = startIndex + 1; i < stepCount - 1; i++, j++)
                    {
                        var y0 = max.y;
                        var y1 = min.y - (description.sideDepth * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var z0 = min.z;
                        var z1 = max.z;

                        if (y2 < description.bounds.Min.y)
                        {
                            var y3 = description.bounds.Min.y;
                            // z0 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z0 y3 *------* z1 y3 
                            vertices = new[] {
                                            new Vector3( min.x, y0, z0), // 0
                                            new Vector3( min.x, y3, z0), // 1
                                            new Vector3( min.x, y3, z1), // 2
                                            new Vector3( min.x, y0, z1), // 3
                                        };
                        } else
                        if (y1 < description.bounds.Min.y)
                        {
                            var z2 = max.z - ((description.bounds.Min.y - y1) / aspect);
                            var y3 = description.bounds.Min.y;
                            // z0 y0 *---------* z1 y0 
                            //       |         |
                            //       |         |
                            //       |         |
                            // z0 y2 *         |
                            //        \        |
                            //         \       |
                            //          \      |
                            //           \     |
                            //            \    |
                            //             \   |
                            //        y3 z2 *--* z1 y3
                            vertices = new[] {
                                            new Vector3( min.x, y0, z0), // 0
                                            new Vector3( min.x, y2, z0), // 1
                                            new Vector3( min.x, y3, z2), // 2
                                            new Vector3( min.x, y3, z1), // 2
                                            new Vector3( min.x, y0, z1), // 3
                                        };
                        } else
                        {
                            // z0 y0 *------* z1 y0 
                            //       |      |
                            //       |      |
                            //       |      |
                            // z0 y2 *      |
                            //        \     |
                            //         \    |
                            //          \   |
                            //           \  |
                            //            \ |
                            //             \|
                            //              * z1 y1 
                            vertices = new[] {
                                            new Vector3( min.x, y0, z0), // 0
                                            new Vector3( min.x, y2, z0), // 1
                                            new Vector3( min.x, y1, z1), // 2
                                            new Vector3( min.x, y0, z1), // 3
                                        };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);

                        min.z += definition.stepDepth;
                        max.z += definition.stepDepth;
                        min.y -= definition.stepHeight;
                        max.y -= definition.stepHeight;
                    }
                    if (description.riserType == StairsRiserType.ThinRiser &&
                        description.riserDepth > definition.stepDepth &&
                        definition.sideDepth < (definition.plateauHeight - definition.treadHeight) / aspect &&
                        description.riserDepth < definition.absDepth)
                    {
                        var z0 = description.bounds.Max.z - description.riserDepth;
                        var z1 = min.z;
                        var y1 = max.y - (description.sideDepth * aspect) + ((min.z - z0) * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var y3 = description.bounds.Min.y;
                        if (y2 <= y3)
                        {
                            if (y2 < y3)
                            {
                                z1 -= (y3 - y2) / aspect;
                            }
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            // y3 z0 *---* y3 z1 
                            vertices = new[] {
                                                new Vector3( min.x, y1, z0), // 0
                                                new Vector3( min.x, y3, z0), // 1
                                                new Vector3( min.x, y3, z1), // 2
                                            };

                            BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + stepCount], vertices, extrusion,
                                            new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                            definition.surfaceDefinition);
                        } else
                        {
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            //       |   * y2 z1 
                            //       |   |
                            // y3 z0 *---* y3 z1 
                            vertices = new[] {
                                                new Vector3( min.x, y1, z0), // 0
                                                new Vector3( min.x, y3, z0), // 1
                                                new Vector3( min.x, y3, z1), // 2
                                                new Vector3( min.x, y2, z1), // 3
                                            };

                            BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + stepCount], vertices, extrusion,
                                            new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                            definition.surfaceDefinition);
                        }
                    }
                }
                {
                    var y0 = max.y;
                    var z0 = min.z;
                    var z1 = max.z;

                    var y3 = description.bounds.Min.y;
                    // z0 y0 *------* z1 y0 
                    //       |      |
                    //       |      |
                    //       |      |
                    // z0 y3 *------* z1 y3 
                    vertices = new[] {
                                        new Vector3( min.x, y0, z0), // 0
                                        new Vector3( min.x, y3, z0), // 1
                                        new Vector3( min.x, y3, z1), // 2
                                        new Vector3( min.x, y0, z1), // 3
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[startIndex + stepCount - 1], vertices, extrusion,
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    definition.surfaceDefinition);
                }
            }
        }

        internal struct LineairStairsData
        {

            public const float kStepSmudgeValue = 0.0001f;

            public float treadHeight;

            public StairsRiserType riserType;
            public StairsSideType leftSideType;
            public StairsSideType rightSideType;

            public MinMaxAABB bounds;

            public bool haveRiser;

            public bool haveLeftSideDown;
            public bool haveRightSideDown;

            public float sideWidth;
            public bool thickRiser;
            public float riserDepth;
            public float sideDepth;

            public int stepCount;
            public float offsetZ;
            public float offsetY;
            public float nosingDepth;

            public bool haveTread;
            public bool haveTopSide;

            public float leftNosingWidth;
            public float rightNosingWidth;
            public float leftTopNosingWidth;
            public float rightTopNosingWidth;

            public int subMeshCount;

            public int startTread;
            public int startLeftSide;
            public int startRightSide;

            public float stepDepthOffset;
            public float stepHeight;
            public float stepDepth;
            public float sideHeight;
            public float plateauHeight;
            public float absDepth;

            public LinearStairsSideData leftSideDescription;
            public LinearStairsSideData rightSideDescription;

            public LineairStairsData(MinMaxAABB      bounds,
                                     float           stepHeight,
                                     float           stepDepth,
                                     float           treadHeight,
                                     float           nosingDepth,
                                     float           nosingWidth,
                                     float           plateauHeight,
                                     StairsRiserType riserType,
                                     float           riserDepth,
                                     StairsSideType  leftSide,
                                     StairsSideType  rightSide,
                                     float           sideWidth,
                                     float           sideHeight,
                                     float           sideDepth)
            {
                // TODO: implement smooth riser-type

                const float kEpsilon = 0.001f;

                this.treadHeight     = (treadHeight < kEpsilon) ? 0 : treadHeight;
                this.riserType       = (this.treadHeight == 0 && riserType == StairsRiserType.ThinRiser) ? StairsRiserType.ThickRiser : riserType;
                this.leftSideType    = (this.riserType == StairsRiserType.None && leftSide  == StairsSideType.Up) ? StairsSideType.DownAndUp : leftSide;
                this.rightSideType   = (this.riserType == StairsRiserType.None && rightSide == StairsSideType.Up) ? StairsSideType.DownAndUp : rightSide;
                this.sideHeight      = sideHeight;
                this.plateauHeight   = plateauHeight;

                if (sideHeight <= 0)
                {
                    switch (this.leftSideType)
                    {
                        case StairsSideType.Up:         this.leftSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.leftSideType = StairsSideType.Down; break;
                    }
                    switch (this.rightSideType)
                    {
                        case StairsSideType.Up:         this.rightSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.rightSideType = StairsSideType.Down; break;
                    }
                }
                if (this.riserType == StairsRiserType.FillDown)
                {
                    switch (this.leftSideType)
                    {
                        case StairsSideType.Down:       this.leftSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.leftSideType = StairsSideType.Up; break;
                    }
                    switch (this.rightSideType)
                    {
                        case StairsSideType.Down:       this.rightSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  this.rightSideType = StairsSideType.Up; break;
                    }
                } else
                if (riserType == StairsRiserType.Smooth)
                {
                    switch (this.leftSideType)
                    {
                        case StairsSideType.Up:   this.leftSideType = StairsSideType.DownAndUp; break;
                        case StairsSideType.None: this.leftSideType = StairsSideType.Down; break;
                    }
                    switch (this.rightSideType)
                    {
                        case StairsSideType.Up:   this.rightSideType = StairsSideType.DownAndUp; break;
                        case StairsSideType.None: this.rightSideType = StairsSideType.Down; break;
                    }
                }
                this.bounds              = bounds;

                this.haveRiser           = this.riserType != StairsRiserType.None;

                this.haveLeftSideDown    = this.riserType != StairsRiserType.FillDown && (this.leftSideType == StairsSideType.Down || this.leftSideType == StairsSideType.DownAndUp) && plateauHeight > 0;
                var haveLeftSideUp       = (this.leftSideType == StairsSideType.Up || this.leftSideType == StairsSideType.DownAndUp);
                this.haveRightSideDown   = this.riserType != StairsRiserType.FillDown && (this.rightSideType == StairsSideType.Down || this.rightSideType == StairsSideType.DownAndUp) && plateauHeight > 0;
                var haveRightSideUp      = (this.rightSideType == StairsSideType.Up || this.rightSideType == StairsSideType.DownAndUp);

                this.sideWidth           = sideWidth;
                this.thickRiser          = this.riserType == StairsRiserType.ThickRiser || this.riserType == StairsRiserType.Smooth;
                this.riserDepth          = (this.haveRiser && !this.thickRiser) ? riserDepth : 0;

                this.sideDepth           = this.riserDepth + math.max(sideDepth, this.thickRiser ? stepDepth : 0);
                
                var boundsSize           = bounds.Max - bounds.Min;
                var absWidth             = Mathf.Abs(boundsSize.x); 
                var absHeight            = Mathf.Abs(boundsSize.y);
                this.absDepth            = Mathf.Abs(boundsSize.z);

                this.stepCount           = Mathf.Max(1, Mathf.FloorToInt((absHeight - plateauHeight + kStepSmudgeValue) / stepHeight));
                this.stepDepthOffset     = Mathf.Max(0, this.absDepth - (this.stepCount * stepDepth));
                this.offsetZ             = (this.stepDepthOffset < kEpsilon) ? 0 : this.stepDepthOffset;
                this.offsetY             = plateauHeight;
                this.nosingDepth         = nosingDepth;
                this.stepHeight          = stepHeight;
                this.stepDepth           = stepDepth;

                this.haveTread           = (this.treadHeight >= kEpsilon);
                this.haveTopSide         = (sideHeight > kEpsilon);

                this.leftNosingWidth     = haveLeftSideUp  ? -this.sideWidth : nosingWidth;
                this.rightNosingWidth    = haveRightSideUp ? -this.sideWidth : nosingWidth;
                this.leftTopNosingWidth  = (haveLeftSideUp  && (!this.haveTopSide)) ? nosingWidth : this.leftNosingWidth;
                this.rightTopNosingWidth = (haveRightSideUp && (!this.haveTopSide)) ? nosingWidth : this.rightNosingWidth;



                this.leftSideDescription     = new LinearStairsSideData(stepHeight, stepDepth, plateauHeight, 
                                                                        this.treadHeight, this.absDepth, this.stepDepthOffset, 
                                                                        this.stepCount, this.sideDepth, this.bounds, this.riserType, this.riserDepth, this.leftSideType);
                this.rightSideDescription    = new LinearStairsSideData(stepHeight, stepDepth, plateauHeight,
                                                                        this.treadHeight, this.absDepth, this.stepDepthOffset, 
                                                                        this.stepCount, this.sideDepth, this.bounds, this.riserType, this.riserDepth, this.rightSideType);

                this.subMeshCount        = 0; if (haveRiser) this.subMeshCount = this.stepCount;
                this.startTread          = this.subMeshCount; if (haveTread) this.subMeshCount += this.stepCount;

                this.startLeftSide       = this.subMeshCount; if (leftSideDescription.enabled)  this.subMeshCount += leftSideDescription.subMeshCount;
                this.startRightSide      = this.subMeshCount; if (rightSideDescription.enabled) this.subMeshCount += rightSideDescription.subMeshCount;
            }
        }

        // TODO: Fix all overlapping brushes
        internal static bool GenerateLinearStairsSubMeshes(ref ChiselBrushContainer brushContainer, ref LineairStairsData description, ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition, int subMeshOffset = 0)
        {
            // TODO: properly assign all materials

            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
                return false;

            brushContainer.Clear();

            var stepOffset = new Vector3(0, -definition.stepHeight, definition.stepDepth);
            if (description.stepCount > 0)
            {
                if (description.haveRiser)
                {
                    var min = description.bounds.Min;
                    var max = description.bounds.Max;
                    max.z = min.z + definition.StepDepthOffset + definition.stepDepth;
                    if (description.riserType != StairsRiserType.FillDown)
                    {
                        if (description.riserType == StairsRiserType.ThinRiser)
                            min.z = max.z - description.riserDepth;
                        else
                            min.z = min.z + definition.StepDepthOffset;
                        if (description.thickRiser)
                            min.z -= description.offsetZ;
                    }
                    min.y = max.y - definition.stepHeight;
                    min.y -= description.treadHeight;
                    max.y -= description.treadHeight;
                    min.x += description.haveRightSideDown ? description.sideWidth : 0;
                    max.x -= description.haveLeftSideDown  ? description.sideWidth : 0;
                    
                    var extrusion = new Vector3(max.x - min.x, 0, 0);
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        if (i == 1 &&
                            description.thickRiser)
                        {
                            min.z += description.offsetZ;
                        }
                        if (i == description.stepCount - 1)
                        {
                            min.y += description.treadHeight - description.offsetY;
                        }

                        var minZ = math.max(description.bounds.Min.z, min.z);
                        var maxZ = math.min(description.bounds.Max.z, max.z);

                        Vector3[] vertices;
                        if (i == 0 || description.riserType != StairsRiserType.Smooth)
                        {
                            vertices = new[] {
                                                new Vector3( min.x, min.y, minZ),	// 0
                                                new Vector3( min.x, min.y, maxZ),	// 1
                                                new Vector3( min.x, max.y, maxZ),  // 2
                                                new Vector3( min.x, max.y, minZ),	// 3
                                            };
                        } else
                        {
                            vertices = new[] {
                                                new Vector3( min.x, min.y, minZ),	// 0
                                                new Vector3( min.x, min.y, maxZ),	// 1
                                                new Vector3( min.x, max.y, maxZ),  // 2
                                                new Vector3( min.x, max.y, minZ - definition.stepDepth),	// 3
                                            };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + i], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);

                        if (description.riserType != StairsRiserType.FillDown)
                            min.z += definition.stepDepth;
                        max.z += definition.stepDepth;
                        min.y -= definition.stepHeight;
                        max.y -= definition.stepHeight;
                    }
                }
                if (description.haveTread)
                {
                    var min = new Vector3(description.bounds.Min.x + description.sideWidth, description.bounds.Max.y - definition.treadHeight, description.bounds.Min.z);
                    var max = new Vector3(description.bounds.Max.x - description.sideWidth, description.bounds.Max.y, description.bounds.Min.z + definition.StepDepthOffset + definition.stepDepth + description.nosingDepth);
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        min.x = description.bounds.Min.x - ((i == 0) ? description.rightTopNosingWidth : description.rightNosingWidth);
                        max.x = description.bounds.Max.x + ((i == 0) ? description.leftTopNosingWidth : description.leftNosingWidth);
                        if (i == 1)
                        {
                            min.z = max.z - (definition.stepDepth + description.nosingDepth);
                        }
                        var vertices = new[] {
                                                new Vector3( min.x, min.y, min.z),	// 0
                                                new Vector3( min.x, min.y, max.z),	// 1
                                                new Vector3( min.x, max.y, max.z),  // 2
                                                new Vector3( min.x, max.y, min.z),	// 3
                                            };
                        var extrusion = new Vector3(max.x - min.x, 0, 0);
                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + description.startTread + i], vertices, extrusion,
                                        new int[] { 0, 1, 2, 2, 2, 2 }, // TODO: fix this
                                        definition.surfaceDefinition);
                        min += stepOffset;
                        max += stepOffset;
                    }
                }

                if (description.leftSideDescription.enabled)
                {
                    var minX = description.bounds.Max.x - description.sideWidth;
                    var maxX = description.bounds.Max.x;

                    GenerateStairsSide(ref brushContainer, subMeshOffset + description.startLeftSide, description.stepCount, minX, maxX, description.leftSideType, definition, description, description.leftSideDescription);
                }

                if (description.rightSideDescription.enabled)
                {
                    var minX = description.bounds.Min.x;
                    var maxX = description.bounds.Min.x + description.sideWidth;

                    GenerateStairsSide(ref brushContainer, subMeshOffset + description.startRightSide, description.stepCount, minX, maxX, description.rightSideType, definition, description, description.rightSideDescription);
                }
            }
            return true;
        }
 

        public static int GetLinearStairsSubMeshCount(ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition)
        {
            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
            {
                return 0;
            }

            var minMaxAABB = new MinMaxAABB { Min = definition.bounds.min, Max = definition.bounds.max };
            var description = new LineairStairsData(minMaxAABB,
                                                    definition.stepHeight, definition.stepDepth,
                                                    definition.treadHeight,
                                                    definition.nosingDepth, definition.nosingWidth,
                                                    definition.plateauHeight,
                                                    definition.riserType, definition.riserDepth,
                                                    leftSideDefinition, rightSideDefinition,
                                                    definition.sideWidth, definition.sideHeight, definition.sideDepth);
            return description.subMeshCount;
        }
    }
}