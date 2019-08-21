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

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateLinearStairs(ref ChiselBrushContainer brushContainer, ref ChiselLinearStairsDefinition definition)
        {
            definition.Validate();

            if (!definition.HasVolume)
            {
                brushContainer.Reset();
                return false;
            }


            int requiredSubMeshCount = BrushMeshFactory.GetLinearStairsSubMeshCount(definition, definition.leftSide, definition.rightSide);
            if (requiredSubMeshCount == 0)
            {
                brushContainer.Reset();
                return false;
            }
            
            int subMeshOffset = 0;

            brushContainer.EnsureSize(requiredSubMeshCount);

            return GenerateLinearStairsSubMeshes(ref brushContainer, definition, definition.leftSide, definition.rightSide, subMeshOffset);
        }

        struct LinearStairsSideData
        {
            public bool enabled;
            public int  subMeshCount;
            public LinearStairsSideData(ChiselLinearStairsDefinition definition, int stepCount, float sideDepth, Vector3 boundsMin, Vector3 boundsMax, StairsRiserType riserType, float riserDepth, StairsSideType sideDefinition, StairsSideType sideType)
            {
                enabled = sideType != StairsSideType.None;

                if (!enabled)
                {
                    subMeshCount = 0;
                    return;
                }

                if (sideType == StairsSideType.Down)
                {
                    subMeshCount = stepCount;

                    var aspect = definition.stepHeight / definition.stepDepth;
                    if (riserType == StairsRiserType.ThinRiser &&
                        riserDepth > definition.stepDepth && 
                        definition.sideDepth < (definition.plateauHeight - definition.treadHeight) / aspect &&
                        riserDepth < definition.bounds.size.z)
                    {
                        subMeshCount++;
                    }
                } else
                if (sideType == StairsSideType.Up)
                {
                    if (definition.StepDepthOffset > 0)
                        subMeshCount = stepCount + 1;
                    else
                        subMeshCount = stepCount;
                } else
                if (sideType == StairsSideType.DownAndUp)
                {
                    if (definition.StepDepthOffset > sideDepth)
                        subMeshCount = 2;
                    else
                        subMeshCount = 1;

                    if ((boundsMax.y - definition.treadHeight) >= boundsMin.y)
                    {
                        var aspect = definition.stepHeight / definition.stepDepth;
                        if ((definition.sideDepth * aspect) < (definition.plateauHeight - definition.treadHeight))
                            subMeshCount ++;
                    }
                } else
                    subMeshCount = 0;
            }
        }

        private static void GenerateStairsSide(ref ChiselBrushContainer brushContainer, int startIndex, int stepCount, float minX, float maxX, StairsSideType sideType, ChiselLinearStairsDefinition definition, in LineairStairsData description, in LinearStairsSideData side)
        {
            var min = new Vector3(minX, description.boundsMax.y - definition.treadHeight - definition.stepHeight, description.boundsMin.z + definition.StepDepthOffset);
            var max = new Vector3(maxX, description.boundsMax.y - definition.treadHeight                        , description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);

            var maxZ = description.boundsMax.z - description.riserDepth;

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
                    var z0 = description.boundsMin.z;
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
                    var y2 = description.boundsMin.y;
                    var y4 = min.y - (definition.stepHeight * (stepCount - 1));
                    var y3 = y4 + definition.sideHeight;
                    var z0 = min.z;
                    var z1 = max.z + (definition.stepDepth * (stepCount - 1));
                    var z2 = z0 - description.sideDepth;
                    var z3 = z1 - description.sideDepth - ((y2- y4) / aspect);

                    if (z2 < description.boundsMin.z)
                    {
                        y1 -= (description.boundsMin.z - z2) * aspect;
                        z2 = description.boundsMin.z;
                    }
                    if (y1 < description.boundsMin.y)
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
                            var y7 = description.boundsMin.y;
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
                if ((min.z - description.boundsMin.z) > 0)
                {
                    // z0 y0 *----* z1 y0 
                    //       |    |
                    // z0 y1 *----* z1 y2 
                    var y0 = max.y + definition.sideHeight;
                    var y1 = max.y;
                    var y2 = min.y;
                    var z0 = description.boundsMin.z;
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
                        if (y1 < description.boundsMin.y)
                        {
                            var y2 = description.boundsMin.y;
                            var z2 = max.z - ((description.boundsMin.y - y1) / aspect);
                            if (z2 < description.boundsMin.z)
                            {
                                var z3 = description.boundsMin.z;
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
                            if (z0 < description.boundsMin.z)
                            {
                                var z3 = description.boundsMin.z;
                                var y3 = max.y - ((description.boundsMin.z - z0) * aspect);
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
                        if (z0 < description.boundsMin.z)
                        {
                            var z2 = description.boundsMin.z;
                            var y2 = max.y - ((description.boundsMin.z - z0) * aspect);
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

                        if (y2 < description.boundsMin.y)
                        {
                            var y3 = description.boundsMin.y;
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
                        if (y1 < description.boundsMin.y)
                        {
                            var z2 = max.z - ((description.boundsMin.y - y1) / aspect);
                            var y3 = description.boundsMin.y;
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
                        description.riserDepth < definition.bounds.size.z)
                    {
                        var z0 = description.boundsMax.z - description.riserDepth;
                        var z1 = min.z;
                        var y1 = max.y - (description.sideDepth * aspect) + ((min.z - z0) * aspect);
                        var y2 = max.y - (description.sideDepth * aspect);
                        var y3 = description.boundsMin.y;
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

                    var y3 = description.boundsMin.y;
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

        struct LineairStairsData
        {
            public float treadHeight;

            public StairsRiserType riserType;
            public StairsSideType leftSideType;
            public StairsSideType rightSideType;

            public Vector3 boundsMin;
            public Vector3 boundsMax;

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

            public LinearStairsSideData leftSideDescription;
            public LinearStairsSideData rightSideDescription;

            public LineairStairsData(ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition)
            {
                // TODO: implement smooth riser-type

                const float kEpsilon = 0.001f;

                treadHeight     = (definition.treadHeight < kEpsilon) ? 0 : definition.treadHeight;
                riserType       = (treadHeight == 0 && definition.riserType == StairsRiserType.ThinRiser) ? StairsRiserType.ThickRiser : definition.riserType;
                leftSideType    = (riserType == StairsRiserType.None && definition.leftSide  == StairsSideType.Up) ? StairsSideType.DownAndUp : leftSideDefinition;
                rightSideType   = (riserType == StairsRiserType.None && definition.rightSide == StairsSideType.Up) ? StairsSideType.DownAndUp : rightSideDefinition;
                if (definition.sideHeight <= 0)
                {
                    switch (leftSideType)
                    {
                        case StairsSideType.Up: leftSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp: leftSideType = StairsSideType.Down; break;
                    }
                    switch (rightSideType)
                    {
                        case StairsSideType.Up: rightSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp: rightSideType = StairsSideType.Down; break;
                    }
                }
                if (riserType == StairsRiserType.FillDown)
                {
                    switch (leftSideType)
                    {
                        case StairsSideType.Down:       leftSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  leftSideType = StairsSideType.Up; break;
                    }
                    switch (rightSideType)
                    {
                        case StairsSideType.Down:       rightSideType = StairsSideType.None; break;
                        case StairsSideType.DownAndUp:  rightSideType = StairsSideType.Up; break;
                    }
                } else
                if (riserType == StairsRiserType.Smooth)
                {
                    switch (leftSideType)
                    {
                        case StairsSideType.Up:   leftSideType = StairsSideType.DownAndUp; break;
                        case StairsSideType.None: leftSideType = StairsSideType.Down; break;
                    }
                    switch (rightSideType)
                    {
                        case StairsSideType.Up:   rightSideType = StairsSideType.DownAndUp; break;
                        case StairsSideType.None: rightSideType = StairsSideType.Down; break;
                    }
                }
                boundsMin = definition.bounds.min;
                boundsMax = definition.bounds.max;

                if (boundsMin.y > boundsMax.y) { var t = boundsMin.y; boundsMin.y = boundsMax.y; boundsMax.y = t; }
                if (boundsMin.x > boundsMax.x) { var t = boundsMin.x; boundsMin.x = boundsMax.x; boundsMax.x = t; }
                if (boundsMin.z > boundsMax.z) { var t = boundsMin.z; boundsMin.z = boundsMax.z; boundsMax.z = t; }

                haveRiser           = riserType != StairsRiserType.None;

                haveLeftSideDown    = riserType != StairsRiserType.FillDown && (leftSideType == StairsSideType.Down || leftSideType == StairsSideType.DownAndUp) && definition.plateauHeight > 0;
                var haveLeftSideUp  = (leftSideType == StairsSideType.Up || leftSideType == StairsSideType.DownAndUp);
                haveRightSideDown   = riserType != StairsRiserType.FillDown && (rightSideType == StairsSideType.Down || rightSideType == StairsSideType.DownAndUp) && definition.plateauHeight > 0;
                var haveRightSideUp = (rightSideType == StairsSideType.Up || rightSideType == StairsSideType.DownAndUp);

                sideWidth           = definition.sideWidth;
                var sideHeight      = definition.sideHeight;
                thickRiser          = riserType == StairsRiserType.ThickRiser || riserType == StairsRiserType.Smooth;
                riserDepth          = (haveRiser && !thickRiser) ? definition.riserDepth : 0;

                sideDepth           = riserDepth + Mathf.Max(definition.sideDepth, thickRiser ? definition.stepDepth : 0);
                
                stepCount           = definition.StepCount;
                offsetZ             = (definition.StepDepthOffset < kEpsilon) ? 0 : definition.StepDepthOffset;
                offsetY             = definition.plateauHeight;
                nosingDepth         = definition.nosingDepth;

                haveTread           = (treadHeight >= kEpsilon);
                haveTopSide         = (sideHeight > kEpsilon);

                leftNosingWidth     = haveLeftSideUp  ? -sideWidth : definition.nosingWidth;
                rightNosingWidth    = haveRightSideUp ? -sideWidth : definition.nosingWidth;
                leftTopNosingWidth  = (haveLeftSideUp  && (!haveTopSide)) ? definition.nosingWidth : leftNosingWidth;
                rightTopNosingWidth = (haveRightSideUp && (!haveTopSide)) ? definition.nosingWidth : rightNosingWidth;



                leftSideDescription     = new LinearStairsSideData(definition, stepCount, sideDepth, boundsMin, boundsMax, riserType, riserDepth, leftSideDefinition, leftSideType);
                rightSideDescription    = new LinearStairsSideData(definition, stepCount, sideDepth, boundsMin, boundsMax, riserType, riserDepth, rightSideDefinition, rightSideType);

                subMeshCount        = 0; if (haveRiser) subMeshCount = stepCount;
                startTread          = subMeshCount; if (haveTread) subMeshCount += stepCount;

                startLeftSide       = subMeshCount; if (leftSideDescription.enabled)    subMeshCount += leftSideDescription.subMeshCount;
                startRightSide      = subMeshCount; if (rightSideDescription.enabled)   subMeshCount += rightSideDescription.subMeshCount;
            }
        }

        // TODO: Fix all overlapping brushes
        public static bool GenerateLinearStairsSubMeshes(ref ChiselBrushContainer brushContainer, ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition, int subMeshOffset = 0)
        {
            // TODO: properly assign all materials

            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
                return false;

            brushContainer.Clear();

            var description = new LineairStairsData(definition, leftSideDefinition, rightSideDefinition);

            var stepOffset = new Vector3(0, -definition.stepHeight, definition.stepDepth);
            if (description.stepCount > 0)
            {
                if (description.haveRiser)
                {
                    var min = description.boundsMin;
                    var max = description.boundsMax;
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

                        var minZ = Mathf.Max(description.boundsMin.z, min.z);
                        var maxZ = Mathf.Min(description.boundsMax.z, max.z);

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
                    var min = new Vector3(description.boundsMin.x + description.sideWidth, description.boundsMax.y - definition.treadHeight, description.boundsMin.z);
                    var max = new Vector3(description.boundsMax.x - description.sideWidth, description.boundsMax.y, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth + description.nosingDepth);
                    for (int i = 0; i < description.stepCount; i++)
                    {
                        min.x = description.boundsMin.x - ((i == 0) ? description.rightTopNosingWidth : description.rightNosingWidth);
                        max.x = description.boundsMax.x + ((i == 0) ? description.leftTopNosingWidth : description.leftNosingWidth);
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
                    var minX = description.boundsMax.x - description.sideWidth;
                    var maxX = description.boundsMax.x;

                    GenerateStairsSide(ref brushContainer, subMeshOffset + description.startLeftSide, description.stepCount, minX, maxX, description.leftSideType, definition, description, description.leftSideDescription);
                }

                if (description.rightSideDescription.enabled)
                {
                    var minX = description.boundsMin.x;
                    var maxX = description.boundsMin.x + description.sideWidth;

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

            var description = new LineairStairsData(definition, leftSideDefinition, rightSideDefinition);
            return description.subMeshCount;
        }
    }
}