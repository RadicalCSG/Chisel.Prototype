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

            int requiredSubMeshCount = BrushMeshFactory.GetLinearStairsSubMeshCount(definition, definition.leftSide, definition.rightSide);
            if (requiredSubMeshCount == 0)
                return false;
            
            int subMeshOffset = 0;

            brushContainer.EnsureSize(requiredSubMeshCount);

            return GenerateLinearStairsSubMeshes(ref brushContainer, definition, definition.leftSide, definition.rightSide, subMeshOffset);
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
            public bool haveLeftSideUp;
            public bool haveRightSideDown;
            public bool haveRightSideUp;

            public float sideWidth;
            public float sideHeight;
            public float leftSideDepth;
            public float rightSideDepth;
            public bool thickRiser;
            public float riserDepth;

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
            public int startLeftSideDown;
            public int startRightSideDown;
            public int startLeftSideUp;
            public int startLeftTopSideUp;
            public int startRightSideUp;
            public int startRightTopSideUp;
            public int startLeftBottom;
            public int startRightBottom;

            public LineairStairsData(ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition)
            {
                // TODO: implement smooth riser-type

                const float kEpsilon = 0.001f;

                treadHeight     = (definition.treadHeight < kEpsilon) ? 0 : definition.treadHeight;
                riserType       = (treadHeight == 0 && definition.riserType == StairsRiserType.ThinRiser) ? StairsRiserType.ThickRiser : definition.riserType;
                leftSideType    = (riserType == StairsRiserType.None && definition.leftSide  == StairsSideType.Up) ? StairsSideType.DownAndUp : leftSideDefinition;
                rightSideType   = (riserType == StairsRiserType.None && definition.rightSide == StairsSideType.Up) ? StairsSideType.DownAndUp : rightSideDefinition;
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
                haveLeftSideDown    = riserType != StairsRiserType.FillDown && 
                                      (leftSideType == StairsSideType.Down || leftSideType == StairsSideType.DownAndUp) &&
                                        definition.plateauHeight > 0;
                haveLeftSideUp      = (leftSideType == StairsSideType.Up || leftSideType == StairsSideType.DownAndUp);
                haveRightSideDown   = riserType != StairsRiserType.FillDown && (rightSideType == StairsSideType.Down || rightSideType == StairsSideType.DownAndUp) &&
                                        definition.plateauHeight > 0;
                haveRightSideUp     = (rightSideType == StairsSideType.Up || rightSideType == StairsSideType.DownAndUp);

                sideWidth           = definition.sideWidth;
                sideHeight          = definition.sideHeight;
                leftSideDepth       = (haveLeftSideDown ) ? definition.sideDepth : 0;
                rightSideDepth      = (haveRightSideDown) ? definition.sideDepth : 0;
                thickRiser          = riserType == StairsRiserType.ThickRiser || riserType == StairsRiserType.Smooth;
                riserDepth          = (haveRiser && !thickRiser) ? definition.riserDepth : 0;

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

                subMeshCount        = 0; if (haveRiser) subMeshCount = stepCount;
                startTread          = subMeshCount; if (haveTread) subMeshCount += stepCount;
                startLeftSideDown   = subMeshCount; if (haveLeftSideDown ) subMeshCount += stepCount;
                startRightSideDown  = subMeshCount; if (haveRightSideDown) subMeshCount += stepCount;
                startLeftSideUp     = subMeshCount; if (haveLeftSideUp   ) subMeshCount += (stepCount - 1);
                startLeftTopSideUp  = subMeshCount; if (haveLeftSideUp && haveTopSide) subMeshCount += 1;
                startRightSideUp    = subMeshCount; if (haveRightSideUp  ) subMeshCount += (stepCount - 1);
                startRightTopSideUp = subMeshCount; if (haveRightSideUp && haveTopSide) subMeshCount += 1;
                startLeftBottom     = subMeshCount; if (haveLeftSideDown ) subMeshCount += 1;
                startRightBottom    = subMeshCount; if (haveRightSideDown) subMeshCount += 1;
            }
        }

        // TODO: Fix all overlapping brushes
        public static bool GenerateLinearStairsSubMeshes(ref ChiselBrushContainer brushContainer, ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition, int subMeshOffset = 0)
        {
            // TODO: properly assign all materials

            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
                return false;


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
                    max.x -= description.haveLeftSideDown ? description.sideWidth : 0;
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

                        Vector3[] vertices;
                        if (i == 0 || description.riserType != StairsRiserType.Smooth)
                        {
                            vertices = new[] {
                                                new Vector3( min.x, min.y, min.z),	// 0
                                                new Vector3( min.x, min.y, max.z),	// 1
                                                new Vector3( min.x, max.y, max.z),  // 2
                                                new Vector3( min.x, max.y, min.z),	// 3
                                            };
                        } else
                        {
                            vertices = new[] {
                                                new Vector3( min.x, min.y, min.z),	// 0
                                                new Vector3( min.x, min.y, max.z),	// 1
                                                new Vector3( min.x, max.y, max.z),  // 2
                                                new Vector3( min.x, max.y, min.z - definition.stepDepth),	// 3
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
                if (description.haveLeftSideDown)
                {
                    var min         = new Vector3(description.boundsMax.x - description.sideWidth, description.boundsMax.y - definition.stepHeight - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset);
                    var max         = new Vector3(description.boundsMax.x, description.boundsMax.y - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);

                    var extrusion   = new Vector3(description.sideWidth, 0, 0);
                    var extraDepth  = (description.thickRiser ? definition.stepDepth : description.riserDepth) + description.leftSideDepth;
                    var maxDepth    = description.boundsMin.z;

                    GenerateBottomRamp(ref brushContainer, subMeshOffset + description.startLeftSideDown, description.stepCount, min, max, extrusion, description.riserType, definition.stepDepth /*- riserDepth*/, extraDepth, maxDepth, definition, definition.surfaceDefinition);
                }
                if (description.haveRightSideDown)
                {
                    var min         = new Vector3(description.boundsMin.x, description.boundsMax.y - definition.stepHeight - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset);
                    var max         = new Vector3(description.boundsMin.x + description.sideWidth, description.boundsMax.y - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);

                    var extrusion   = new Vector3(description.sideWidth, 0, 0); 
                    var extraDepth  = (description.thickRiser ? definition.stepDepth : description.riserDepth) + description.rightSideDepth;
                    var maxDepth    = description.boundsMin.z;

                    GenerateBottomRamp(ref brushContainer, subMeshOffset + description.startRightSideDown, description.stepCount, min, max, extrusion, description.riserType, definition.stepDepth /*- riserDepth*/, extraDepth, maxDepth, definition, definition.surfaceDefinition);
                }
                if (description.haveLeftSideUp)
                {
                    var min         = new Vector3(description.boundsMax.x - description.sideWidth, description.boundsMax.y - definition.treadHeight - definition.stepHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);
                    var max         = new Vector3(description.boundsMax.x, description.boundsMax.y - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth + definition.stepDepth);
                    var extrusion   = new Vector3(description.sideWidth, 0, 0);
                    var extraDepth  = (description.thickRiser ? definition.stepDepth : description.riserDepth) + description.leftSideDepth;
                    var maxDepth    = description.boundsMin.z;

                    GenerateTopRamp(ref brushContainer, subMeshOffset + description.startLeftSideUp, description.stepCount - 1, min, max, extrusion, description.sideHeight, extraDepth, maxDepth, description.riserType, definition, definition.surfaceDefinition, description.haveLeftSideDown);

                    if (description.haveTopSide)
                    {
                        var vertices = new[] {
                                            new Vector3( min.x, max.y + description.sideHeight + definition.treadHeight, min.z),		// 0
                                            new Vector3( min.x, max.y + description.sideHeight + definition.treadHeight, description.boundsMin.z),	// 1
                                            new Vector3( min.x, max.y                                      , description.boundsMin.z),  // 2
                                            new Vector3( min.x, max.y                                      , min.z),		// 3
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + description.startLeftTopSideUp], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                }
                if (description.haveRightSideUp)
                {
                    var min         = new Vector3(description.boundsMin.x, description.boundsMax.y - definition.treadHeight - definition.stepHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);
                    var max         = new Vector3(description.boundsMin.x + description.sideWidth, description.boundsMax.y - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth + definition.stepDepth);
                    var extrusion   = new Vector3(description.sideWidth, 0, 0);
                    var extraDepth  = (description.thickRiser ? definition.stepDepth : description.riserDepth) + description.rightSideDepth;
                    var maxDepth    = description.boundsMin.z;

                    GenerateTopRamp(ref brushContainer, subMeshOffset + description.startRightSideUp, description.stepCount - 1, min, max, extrusion, description.sideHeight, extraDepth, maxDepth, description.riserType, definition, definition.surfaceDefinition, description.haveRightSideDown);

                    if (description.haveTopSide)
                    {
                        var vertices = new[] {
                                            new Vector3( min.x, max.y + description.sideHeight + definition.treadHeight, min.z),		// 0
                                            new Vector3( min.x, max.y + description.sideHeight + definition.treadHeight, description.boundsMin.z),  // 1
                                            new Vector3( min.x, max.y                                      , description.boundsMin.z),  // 2
                                            new Vector3( min.x, max.y                                      , min.z),		// 3
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + description.startRightTopSideUp], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                }
                if (description.haveLeftSideDown)
                {
                    var min         = new Vector3(description.boundsMax.x - description.sideWidth, description.boundsMax.y - definition.treadHeight - definition.stepHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);
                    var max         = new Vector3(description.boundsMax.x, description.boundsMax.y - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth + definition.stepDepth);
                    var extrusion   = new Vector3(description.sideWidth, 0, 0);
                    var extraDepth  = (description.thickRiser ? definition.stepDepth : description.riserDepth) + description.leftSideDepth;
                    
                    //if (!haveLeftSideDown)
                    {
                        var plateauHeight = definition.plateauHeight;
                        Vector3[] vertices;
                        if (description.riserType == StairsRiserType.FillDown)
                        {
                            vertices = new[] {
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMax.z),	// 0
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMin.z), // 1
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMin.z), // 2
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMax.z),	// 3
                                        };
                        } else
                        {
                            vertices = new[] {
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMax.z),		// 0
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMax.z - extraDepth),  // 1
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMax.z - extraDepth),  // 2
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMax.z),		// 3
                                        };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + description.startLeftBottom], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this 
                                        definition.surfaceDefinition);
                    }
                }
                if (description.haveRightSideDown)
                {
                    var min         = new Vector3(description.boundsMin.x, description.boundsMax.y - definition.treadHeight - definition.stepHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth);
                    var max         = new Vector3(description.boundsMin.x + description.sideWidth, description.boundsMax.y - definition.treadHeight, description.boundsMin.z + definition.StepDepthOffset + definition.stepDepth + definition.stepDepth);
                    var extrusion   = new Vector3(description.sideWidth, 0, 0);
                    var extraDepth  = (description.thickRiser ? definition.stepDepth : description.riserDepth) + description.rightSideDepth;
                    
                    //if (!haveRightSideDown)
                    {
                        var plateauHeight = definition.plateauHeight;
                        Vector3[] vertices;
                        if (description.riserType == StairsRiserType.FillDown)
                        {
                            vertices = new[] {
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMax.z),		// 0
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMin.z),  // 1
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMin.z),  // 2
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMax.z),		// 3
                                        };
                        } else
                        {
                            vertices = new[] {
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMax.z),		// 0
                                            new Vector3( min.x, description.boundsMin.y + plateauHeight, description.boundsMax.z - extraDepth),  // 1
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMax.z - extraDepth),  // 2
                                            new Vector3( min.x, description.boundsMin.y                , description.boundsMax.z),		// 3
                                        };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + description.startRightBottom], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                }
            }
            return true;
        }

        // TODO: remove all stairs specific parameters
        static void GenerateBottomRamp(ref ChiselBrushContainer brushContainer, int startIndex, int stepCount, Vector3 min, Vector3 max, Vector3 extrusion, StairsRiserType riserType, float riserDepth, float extraDepth, float maxDepth, ChiselLinearStairsDefinition definition, in ChiselSurfaceDefinition surfaceDefinition)
        {
            for (int i = 0, j = startIndex; i < stepCount; i++, j++)
            {
                Vector3[] vertices;
                var z0 = Mathf.Max(maxDepth, min.z - extraDepth);
                var z1 = Mathf.Max(maxDepth, max.z - extraDepth);
                var z2 = Mathf.Max(maxDepth, min.z + riserDepth);
                if (z2 < z1)
                {
                    var t = z1; z1 = z2; z2 = t;
                    z1 = Mathf.Max(maxDepth, min.z - extraDepth + definition.stepDepth);
                    z2 = Mathf.Max(maxDepth, max.z);
                }
                if (i != stepCount - 1)
                {
                    vertices = new[] {
                                        new Vector3( min.x, max.y, z2), // 0
                                        new Vector3( min.x, max.y, z0), // 1
                                        new Vector3( min.x, min.y, z1), // 2
                                        new Vector3( min.x, min.y, z2), // 3
                                    };
                } else
                {
                    vertices = new[] {  
                                        new Vector3( min.x, max.y, z2), // 0
                                        new Vector3( min.x, max.y, z0), // 1
                                        new Vector3( min.x, min.y + definition.treadHeight, z1), // 2
                                        new Vector3( min.x, min.y + definition.treadHeight, z2), // 3  
                                    };
                }

                BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j], vertices, extrusion,
                                new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                surfaceDefinition);

                min.z += definition.stepDepth;
                max.z += definition.stepDepth;
                min.y -= definition.stepHeight;
                max.y -= definition.stepHeight;
            }
        }

        // TODO: remove all stairs specific parameters
        static void GenerateTopRamp(ref ChiselBrushContainer brushContainer, int startIndex, int stepCount, Vector3 min, Vector3 max, Vector3 extrusion, float sideHeight, float extraDepth, float maxDepth, StairsRiserType riserType, ChiselLinearStairsDefinition definition, in ChiselSurfaceDefinition surfaceDefinition, bool allLargeSteps)
        {
            //var diffY			= (max.y - min.y);
            //var diffZ			= (max.z - min.z);
            //var aspect		= diffY / diffZ;
            var diagonalHeight	= sideHeight + definition.treadHeight;//Mathf.Max(sideHeight + definition.treadHeight, (aspect * (riserDepth)));// + definition.nosingDepth)) + definition.treadHeight;

            if (!allLargeSteps)
            {
                for (int i = 0, j = startIndex; i < stepCount; i++, j++)
                {
                    var topY        = max.y + diagonalHeight;
                    var bottomY     = min.y;
                    var middleY     = (bottomY + diagonalHeight);// - (lastStep ? (riserDepth * aspect) : 0);
                    var rightZ      = Mathf.Max(maxDepth, max.z);// + (lastStep ? riserDepth : 0);
                    var leftZ       = Mathf.Max(maxDepth, min.z);
                    
                    //            topY leftZ
                    //          0    4 
                    //           *--*    
                    //           |   \
                    //           |    \
                    //  lefterZ  |     \ 3
                    //           |      * 
                    //           |      |   rightZ
                    //           *------*
                    //          1        2
                    //            bottomY
                    var lefterZ = (i == 0 || (riserType == StairsRiserType.FillDown)) ? maxDepth : Mathf.Max(maxDepth, leftZ - extraDepth);
                    var vertices = new[] {
                                        new Vector3( min.x, topY,    lefterZ),   // 0
                                        new Vector3( min.x, bottomY, lefterZ),   // 1
                                        new Vector3( min.x, bottomY, rightZ),  // 2
                                        new Vector3( min.x, middleY, rightZ),  // 3
                                        new Vector3( min.x, topY,    leftZ),   // 4
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j + 0], vertices, extrusion, 
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    surfaceDefinition);

                    min.z += definition.stepDepth;
                    max.z += definition.stepDepth;
                    min.y -= definition.stepHeight;
                    max.y -= definition.stepHeight;
                }
            } else
            { 
                int i = 0, j = startIndex;
                for (; i < stepCount - 1; i++, j++)
                {
                    var topY		= max.y + diagonalHeight;
                    var bottomY		= min.y;
                    var middleY		= (bottomY + diagonalHeight);// - (lastStep ? (riserDepth * aspect) : 0);
                    var rightZ		= Mathf.Max(maxDepth, max.z);// + (lastStep ? riserDepth : 0);
                    var leftZ		= Mathf.Max(maxDepth, min.z);

                    //            topY leftZ
                    //          0    3 
                    //           *--*    
                    //           |   \
                    //           |    \
                    //  lefterZ  |     \  
                    //           *------*
                    //          1        2
                    //            middleY
                    var lefterZ = (i == 0 || (riserType == StairsRiserType.FillDown)) ? maxDepth : Mathf.Max(maxDepth, leftZ - extraDepth);
                    var vertices = new[] {
                                        new Vector3( min.x, topY,    lefterZ),   // 0
                                        new Vector3( min.x, middleY, lefterZ),   // 1
                                        new Vector3( min.x, middleY, rightZ),  // 2
                                        new Vector3( min.x, topY,    leftZ),   // 3
                                    }; 

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j + 0], vertices, extrusion, 
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    surfaceDefinition);
                        
                    min.z += definition.stepDepth;
                    max.z += definition.stepDepth;
                    min.y -= definition.stepHeight;
                    max.y -= definition.stepHeight;
                }
                i = stepCount - 1;
                j = startIndex + i;
                { 
                    var topY		= max.y + diagonalHeight;
                    var bottomY		= min.y;
                    var middleY		= (bottomY + diagonalHeight);// - (lastStep ? (riserDepth * aspect) : 0);
                    var rightZ		= Mathf.Max(maxDepth, max.z);// + (lastStep ? riserDepth : 0);
                    var leftZ		= Mathf.Max(maxDepth, min.z);

                    //            topY leftZ
                    //          0    4 
                    //           *--*    
                    //           |   \
                    //           |    \
                    //  lefterZ  |     \ 3
                    //           |      * 
                    //           |      |   rightZ
                    //           *------*
                    //          1        2
                    //            bottomY
                    var lefterZ = (i == 0 || (riserType == StairsRiserType.FillDown)) ? maxDepth : Mathf.Max(maxDepth, leftZ - extraDepth);
                    var vertices = new[] {
                                        new Vector3( min.x, topY,    lefterZ),   // 0
                                        new Vector3( min.x, bottomY, lefterZ),   // 1
                                        new Vector3( min.x, bottomY, rightZ),  // 2
                                        new Vector3( min.x, middleY, rightZ),  // 3
                                        new Vector3( min.x, topY,    leftZ),   // 4
                                    };

                    BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j + 0], vertices, extrusion, 
                                    new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                    surfaceDefinition);
                        
                    min.z += definition.stepDepth;
                    max.z += definition.stepDepth;
                    min.y -= definition.stepHeight;
                    max.y -= definition.stepHeight;
                }
            }
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