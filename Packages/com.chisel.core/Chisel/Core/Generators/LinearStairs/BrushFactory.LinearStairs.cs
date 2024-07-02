using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        // TODO: Fix all overlapping brushes
        internal static bool GenerateLinearStairsSubMeshes(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, int subMeshOffset, in LineairStairsData description, in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator)
        {
            // TODO: properly assign all materials

            ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
            if (surfaceDefinition.surfaces.Length != (int)ChiselLinearStairs.SurfaceSides.TotalSides)
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
                            min.z += description.stepDepthOffset;
                        if (description.thickRiser)
                            min.z -= description.offsetZ;
                    }
                    min.y = max.y - description.stepHeight;
                    min.y -= description.treadHeight;
                    max.y -= description.treadHeight;
                    min.x += (description.leftSideType  != StairsSideType.None) ? description.sideWidth : 0;
                    max.x -= (description.rightSideType != StairsSideType.None) ? description.sideWidth : 0;

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

                        var vertices = new NativeArray<float3>(4, allocator);
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

                        var indices = new NativeArray<int>(6, allocator);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        using (indices)
                        {
                            using (vertices)
                            {
                                brushMeshes[subMeshOffset + i] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                            }
                        }
                        

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
                        max.x = description.bounds.Max.x + ((i == 0) ? description.leftTopNosingWidth  : description.leftNosingWidth );
                        if (i == 1)
                        {
                            min.z = max.z - (description.stepDepth + description.nosingDepth);
                        }
                        var vertices = new NativeArray<float3>(4, allocator);
                        vertices[0] = new float3(min.x, min.y, min.z); // 0
                        vertices[0] = new float3(min.x, min.y, max.z); // 1
                        vertices[0] = new float3(min.x, max.y, max.z); // 2
                        vertices[0] = new float3(min.x, max.y, min.z); // 3
                        var extrusion = new float3(max.x - min.x, 0, 0);

                        var indices = new NativeArray<int>(6, allocator);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        using (indices)
                        {
                            using (vertices)
                            {
                                brushMeshes[subMeshOffset + description.startTread + i] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                            }
                        }

                        min += stepOffset;
                        max += stepOffset;
                    }
                }

                if (description.leftSideDescription.enabled)
                {
                    var minX = description.bounds.Max.x - description.sideWidth;
                    var maxX = description.bounds.Max.x;

                    GenerateStairsSide(brushMeshes, subMeshOffset + description.startLeftSide, description.stepCount, minX, maxX, description.leftSideType, in description, ref surfaceDefinition, description.leftSideDescription, allocator);
                }

                if (description.rightSideDescription.enabled)
                {
                    var minX = description.bounds.Min.x;
                    var maxX = description.bounds.Min.x + description.sideWidth;

                    GenerateStairsSide(brushMeshes, subMeshOffset + description.startRightSide, description.stepCount, minX, maxX, description.rightSideType, in description, ref surfaceDefinition, description.rightSideDescription, allocator);
                }
            }
            return true;
        }

        static BlobAssetReference<BrushMeshBlob> CreateExtrudedSubMeshBlob([ReadOnly] NativeArray<float3> vertices, float3 extrusion, [ReadOnly] NativeArray<int> indices, ref NativeChiselSurfaceDefinition surfaceDefinition, Allocator allocator)
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                if (BrushMeshFactory.CreateExtrudedSubMesh(vertices, extrusion, indices,
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
                    root.localPlaneCount = polygons.Length;
                    // TODO: calculate corner planes
                    var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                    CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                    UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                    root.localBounds = CalculateBounds(in localVertices);
                    return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                } else
                    return BlobAssetReference<BrushMeshBlob>.Null;
            }
        }

        private static void GenerateStairsSide(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, int startIndex, int stepCount, float minX, float maxX, StairsSideType sideType, in LineairStairsData description, ref NativeChiselSurfaceDefinition surfaceDefinition, in LinearStairsSideData side, Allocator allocator)
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
                    var vertices = new NativeArray<float3>(4, allocator);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y1, z1); // 2
                    vertices[3] = new float3(min.x, y0, z1); // 3
                    var indices = new NativeArray<int>(6, allocator);
                    indices[0] = 0; 
                    indices[1] = 1; 
                    indices[2] = 2;
                    indices[3] = 0; 
                    indices[4] = 2; 
                    indices[5] = 3;
                    using (indices)
                    {
                        using (vertices)
                        {
                            brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                        }
                    }
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

                    var vertices = new NativeArray<float3>(4, allocator);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y2, z1); // 2
                    vertices[3] = new float3(min.x, y3, z1); // 3
                    var indices = new NativeArray<int>(6, allocator);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;
                    using (indices)
                    {
                        using (vertices)
                        {
                            brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                        }
                    }
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

                        var vertices = new NativeArray<float3>(5, allocator);
                        vertices[0] = new float3(min.x, y0, z2); // 0
                        vertices[1] = new float3(min.x, y2, z2); // 1
                        vertices[2] = new float3(min.x, y2, z1); // 2
                        vertices[3] = new float3(min.x, y3, z1); // 3
                        vertices[4] = new float3(min.x, y0, z0); // 4

                        var indices = new NativeArray<int>(9, allocator);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        indices[6] = 0;
                        indices[7] = 3;
                        indices[8] = 4; // TODO: fix this
                        using (indices)
                        {
                            using (vertices)
                            {
                                brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                            }
                        }
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
                            var vertices = new NativeArray<float3>(5, allocator);
                            vertices[0] = new float3(min.x, y0, z2); // 0
                            vertices[1] = new float3(min.x, y1, z2); // 1
                            vertices[2] = new float3(min.x, y5, z4); // 2
                            vertices[3] = new float3(min.x, y6, z4); // 3
                            vertices[4] = new float3(min.x, y0, z0); // 4

                            var indices = new NativeArray<int>(9, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            indices[6] = 0;
                            indices[7] = 3;
                            indices[8] = 4;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
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

                            var vertices = new NativeArray<float3>(4, allocator);
                            vertices[0] = new float3(min.x, y6, z4); // 0
                            vertices[1] = new float3(min.x, y7, z4); // 1
                            vertices[2] = new float3(min.x, y7, z1); // 2
                            vertices[3] = new float3(min.x, y3, z1); // 3

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[startIndex + 2] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
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

                        var vertices = new NativeArray<float3>(6, allocator);
                        vertices[0] = new float3(min.x, y0, z2); // 0
                        vertices[1] = new float3(min.x, y1, z2); // 1
                        vertices[2] = new float3(min.x, y2, z3); // 2
                        vertices[3] = new float3(min.x, y2, z1); // 3
                        vertices[4] = new float3(min.x, y3, z1); // 4
                        vertices[5] = new float3(min.x, y0, z0); // 5

                        var indices = new NativeArray<int>(9, allocator);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        indices[6] = 0;
                        indices[7] = 3;
                        indices[8] = 4;
                        using (indices)
                        {
                            using (vertices)
                            {
                                brushMeshes[startIndex + 1] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                            }
                        }
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
                    var vertices = new NativeArray<float3>(4, allocator);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y1, z1); // 2
                    vertices[3] = new float3(min.x, y0, z1); // 3

                    var indices = new NativeArray<int>(6, allocator);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;
                    using (indices)
                    {
                        using (vertices)
                        {
                            brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                        }
                    }
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

                    var vertices = new NativeArray<float3>(4, allocator);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y1, z0); // 1
                    vertices[2] = new float3(min.x, y1, z1); // 2
                    vertices[3] = new float3(min.x, y3, z1); // 3

                    var indices = new NativeArray<int>(6, allocator);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;
                    using (indices)
                    {
                        using (vertices)
                        {
                            brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                        }
                    }

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
                        var vertices = new NativeArray<float3>(3, allocator);
                        vertices[0] = new float3(min.x, y0, z0); // 0
                        vertices[1] = new float3(min.x, y1, z1); // 1
                        vertices[2] = new float3(min.x, y0, z1); // 2

                        var indices = new NativeArray<int>(6, allocator);
                        indices[0] = 0;
                        indices[1] = 1;
                        indices[2] = 2;
                        indices[3] = 0;
                        indices[4] = 2;
                        indices[5] = 3;
                        using (indices)
                        {
                            using (vertices)
                            {
                                brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                            }
                        }

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
                                var vertices = new NativeArray<float3>(4, allocator);
                                vertices[0] = new float3(min.x, y0, z3); // 0
                                vertices[1] = new float3(min.x, y2, z3); // 1
                                vertices[2] = new float3(min.x, y2, z1); // 2
                                vertices[3] = new float3(min.x, y0, z1); // 3

                                var indices = new NativeArray<int>(6, allocator);
                                indices[0] = 0;
                                indices[1] = 1;
                                indices[2] = 2;
                                indices[3] = 0;
                                indices[4] = 2;
                                indices[5] = 3;
                                using (indices)
                                {
                                    using (vertices)
                                    {
                                        brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                    }
                                }

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
                                var vertices = new NativeArray<float3>(5, allocator);
                                vertices[0] = new float3(min.x, y0, z3); // 0
                                vertices[1] = new float3(min.x, y3, z3); // 1
                                vertices[2] = new float3(min.x, y2, z2); // 2
                                vertices[3] = new float3(min.x, y2, z1); // 3
                                vertices[4] = new float3(min.x, y0, z1); // 4

                                var indices = new NativeArray<int>(6, allocator);
                                indices[0] = 0;
                                indices[1] = 1;
                                indices[2] = 2;
                                indices[3] = 0;
                                indices[4] = 2;
                                indices[5] = 3;
                                using (indices)
                                {
                                    using (vertices)
                                    {
                                        brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                    }
                                }

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
                                var vertices = new NativeArray<float3>(4, allocator);
                                vertices[0] = new float3(min.x, y0, z0); // 0
                                vertices[1] = new float3(min.x, y2, z2); // 1
                                vertices[2] = new float3(min.x, y2, z1); // 2
                                vertices[3] = new float3(min.x, y0, z1); // 3

                                var indices = new NativeArray<int>(6, allocator);
                                indices[0] = 0;
                                indices[1] = 1;
                                indices[2] = 2;
                                indices[3] = 0;
                                indices[4] = 2;
                                indices[5] = 3;
                                using (indices)
                                {
                                    using (vertices)
                                    {
                                        brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                    }
                                }

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
                            var vertices = new NativeArray<float3>(4, allocator);
                            vertices[0] = new float3(min.x, y0, z2); // 0
                            vertices[1] = new float3(min.x, y2, z2); // 1
                            vertices[2] = new float3(min.x, y1, z1); // 2
                            vertices[3] = new float3(min.x, y0, z1); // 3

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }

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
                            var vertices = new NativeArray<float3>(3, allocator);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y1, z1); // 1
                            vertices[2] = new float3(min.x, y0, z1); // 2

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[startIndex] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }

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
                            var vertices = new NativeArray<float3>(4, allocator);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y3, z0); // 1
                            vertices[2] = new float3(min.x, y3, z1); // 2
                            vertices[3] = new float3(min.x, y0, z1); // 3

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
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
                            var vertices = new NativeArray<float3>(5, allocator);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y2, z0); // 1
                            vertices[2] = new float3(min.x, y3, z2); // 2
                            vertices[3] = new float3(min.x, y3, z1); // 3
                            vertices[4] = new float3(min.x, y0, z1); // 4

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
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
                            var vertices = new NativeArray<float3>(4, allocator);
                            vertices[0] = new float3(min.x, y0, z0); // 0
                            vertices[1] = new float3(min.x, y2, z0); // 1
                            vertices[2] = new float3(min.x, y1, z1); // 2
                            vertices[3] = new float3(min.x, y0, z1); // 3

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[j] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
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
                            var vertices = new NativeArray<float3>(3, allocator);
                            vertices[0] = new float3(min.x, y1, z0); // 0
                            vertices[1] = new float3(min.x, y3, z0); // 1
                            vertices[2] = new float3(min.x, y3, z1); // 2

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[startIndex + stepCount] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
                        } else
                        {
                            // y1 z0 *
                            //       |\  
                            //       | \ 
                            //       |  \
                            //       |   * y2 z1 
                            //       |   |
                            // y3 z0 *---* y3 z1 
                            var vertices = new NativeArray<float3>(4, allocator);
                            vertices[0] = new float3(min.x, y1, z0); // 0
                            vertices[1] = new float3(min.x, y3, z0); // 1
                            vertices[2] = new float3(min.x, y3, z1); // 2
                            vertices[3] = new float3(min.x, y2, z1); // 3

                            var indices = new NativeArray<int>(6, allocator);
                            indices[0] = 0;
                            indices[1] = 1;
                            indices[2] = 2;
                            indices[3] = 0;
                            indices[4] = 2;
                            indices[5] = 3;
                            using (indices)
                            {
                                using (vertices)
                                {
                                    brushMeshes[startIndex + stepCount] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                                }
                            }
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
                    var vertices = new NativeArray<float3>(4, allocator);
                    vertices[0] = new float3(min.x, y0, z0); // 0
                    vertices[1] = new float3(min.x, y3, z0); // 1
                    vertices[2] = new float3(min.x, y3, z1); // 2
                    vertices[3] = new float3(min.x, y0, z1); // 3

                    var indices = new NativeArray<int>(6, allocator);
                    indices[0] = 0;
                    indices[1] = 1;
                    indices[2] = 2;
                    indices[3] = 0;
                    indices[4] = 2;
                    indices[5] = 3;
                    using (indices)
                    {
                        using (vertices)
                        {
                            brushMeshes[startIndex + stepCount - 1] = CreateExtrudedSubMeshBlob(vertices, extrusion, indices, ref surfaceDefinition, allocator);
                        }
                    }
                }
            }
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
                                        int stepCount, float sideDepth, AABB bounds, StairsRiserType riserType, float riserDepth, StairsSideType sideType)
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
    
        internal struct LineairStairsData
        {

            public const float kStepSmudgeValue = 0.0001f;

            public float treadHeight;

            public StairsRiserType riserType;
            public StairsSideType leftSideType;
            public StairsSideType rightSideType;

            public AABB bounds;

            public bool haveRiser;

            public bool haveLeftSideDown;
            public bool haveRightSideDown;
            public bool haveLeftSideUp;
            public bool haveRightSideUp;

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

            public LineairStairsData(AABB            bounds,
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
                this.haveLeftSideUp      = (this.leftSideType == StairsSideType.Up || this.leftSideType == StairsSideType.DownAndUp);
                this.haveRightSideDown   = this.riserType != StairsRiserType.FillDown && (this.rightSideType == StairsSideType.Down || this.rightSideType == StairsSideType.DownAndUp) && plateauHeight > 0;
                this.haveRightSideUp     = (this.rightSideType == StairsSideType.Up || this.rightSideType == StairsSideType.DownAndUp);
                
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
    }
}