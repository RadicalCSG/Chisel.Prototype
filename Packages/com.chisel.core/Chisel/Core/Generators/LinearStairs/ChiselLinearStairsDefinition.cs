using System;
using System.Linq;
using System.Collections.Generic;
using Bounds  = UnityEngine.Bounds;
using Mathf   = UnityEngine.Mathf;
using Vector3 = UnityEngine.Vector3;
using UnitySceneExtensions;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    [Serializable]
    public enum StairsRiserType : byte
    {
        None,
        ThinRiser,
        ThickRiser,
//		Pyramid,
        Smooth,
        FillDown
    }

    [Serializable]
    public enum StairsSideType : byte
    {
        None,
        // TODO: better names
        Down,
        Up,
        DownAndUp
    }

    // https://www.archdaily.com/892647/how-to-make-calculations-for-staircase-designs
    // https://inspectapedia.com/Stairs/2024s.jpg
    // https://landarchbim.com/2014/11/18/stair-nosing-treads-and-stringers/
    // https://en.wikipedia.org/wiki/Stairs
    [Serializable]
    public struct ChiselLinearStairsDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Linear Stairs";

        public enum SurfaceSides : byte
        {
            Top,
            Bottom,
            Left,
            Right,
            Front,
            Back,
            Tread,
            Step,

            TotalSides
        }

        const float kStepSmudgeValue = 0.0001f;

        public const float	kMinStepHeight			= 0.01f;
        public const float	kMinStepDepth			= 0.01f;
        public const float  kMinRiserDepth          = 0.01f;
        public const float  kMinSideWidth			= 0.01f;
        public const float	kMinWidth				= 0.0001f;

        public const float	kDefaultStepHeight		= 0.20f;
        public const float	kDefaultStepDepth		= 0.20f;
        public const float	kDefaultTreadHeight     = 0.02f;
        public const float	kDefaultNosingDepth     = 0.02f; 
        public const float	kDefaultNosingWidth     = 0.01f;

        public const float	kDefaultWidth			= 1;
        public const float	kDefaultHeight			= 1;
        public const float	kDefaultDepth			= 1;

        public const float	kDefaultPlateauHeight	= 0;

        public const float  kDefaultRiserDepth      = 0.05f;
        public const float  kDefaultSideDepth		= 0.125f;
        public const float  kDefaultSideWidth		= 0.125f;
        public const float  kDefaultSideHeight      = 0.5f;

        // TODO: add all spiral stairs improvements to linear stairs

        public Bounds bounds;

        [DistanceValue] public float	stepHeight;
        [DistanceValue] public float	stepDepth;

        [DistanceValue] public float	treadHeight;

        [DistanceValue] public float	nosingDepth;
        [DistanceValue] public float	nosingWidth;

        [DistanceValue] public float    plateauHeight;

        public StairsRiserType          riserType;
        [DistanceValue] public float	riserDepth;

        public StairsSideType           leftSide;
        public StairsSideType           rightSide;
        
        [DistanceValue] public float	sideWidth;
        [DistanceValue] public float	sideHeight;
        [DistanceValue] public float	sideDepth;
        
        [NamedItems("Top", "Bottom", "Left", "Right", "Front", "Back", "Tread", "Step", overflow = "Side {0}", fixedSize = 8)]
        public ChiselSurfaceDefinition  surfaceDefinition;

        public bool     HasVolume
        {
            get
            {
                return bounds.size.x != 0 &&
                       bounds.size.y != 0 &&
                       bounds.size.z != 0;
            }
        }
        
        public float	width  { get { return bounds.size.x; } set { var size = bounds.size; size.x = value; bounds.size = size; } }
        public float	height { get { return bounds.size.y; } set { var size = bounds.size; size.y = value; bounds.size = size; } }
        public float	depth  { get { return bounds.size.z; } set { var size = bounds.size; size.z = value; bounds.size = size; } }
        
        public Vector3  boundsMin { get { return new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z)); } }
        public Vector3  boundsMax { get { return new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z)); } }
        
        public float	absWidth  { get { return Mathf.Abs(bounds.size.x); } }
        public float	absHeight { get { return Mathf.Abs(bounds.size.y); } }
        public float	absDepth  { get { return Mathf.Abs(bounds.size.z); } }

        public int StepCount
        {
            get
            {
                return Mathf.Max(1,
                          Mathf.FloorToInt((absHeight - plateauHeight + kStepSmudgeValue) / stepHeight));
            }
        }

        public float StepDepthOffset
        {
            get { return Mathf.Max(0, absDepth - (StepCount * stepDepth)); }
        }

        public void Reset()
        {
            // TODO: set defaults using attributes?
            stepHeight		= kDefaultStepHeight;
            stepDepth		= kDefaultStepDepth;
            treadHeight		= kDefaultTreadHeight;
            nosingDepth		= kDefaultNosingDepth;
            nosingWidth		= kDefaultNosingWidth;

            width			= kDefaultWidth;
            height			= kDefaultHeight;
            depth			= kDefaultDepth;

            plateauHeight	= kDefaultPlateauHeight;

            riserType		= StairsRiserType.ThinRiser;
            leftSide		= StairsSideType.None;
            rightSide		= StairsSideType.None;
            riserDepth		= kDefaultRiserDepth;
            sideDepth		= kDefaultSideDepth;
            sideWidth		= kDefaultSideWidth;
            sideHeight		= kDefaultSideHeight;

            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            if (surfaceDefinition.EnsureSize((int)SurfaceSides.TotalSides))
            {
                var defaultRenderMaterial  = ChiselMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial = ChiselMaterialManager.DefaultPhysicsMaterial;

                surfaceDefinition.surfaces[(int)SurfaceSides.Top    ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultFloorMaterial, defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Bottom ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultFloorMaterial, defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Left   ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Right  ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Front  ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Back   ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Tread  ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultTreadMaterial, defaultPhysicsMaterial);
                surfaceDefinition.surfaces[(int)SurfaceSides.Step   ].brushMaterial = ChiselBrushMaterial.CreateInstance(ChiselMaterialManager.DefaultStepMaterial,  defaultPhysicsMaterial);

                for (int i = 0; i < surfaceDefinition.surfaces.Length; i++)
                {
                    if (surfaceDefinition.surfaces[i].brushMaterial == null)
                        surfaceDefinition.surfaces[i].brushMaterial = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
                }
            }


            stepHeight		= Mathf.Max(kMinStepHeight, stepHeight);
            stepDepth		= Mathf.Clamp(stepDepth, kMinStepDepth, absDepth);
            treadHeight		= Mathf.Max(0, treadHeight);
            nosingDepth		= Mathf.Max(0, nosingDepth);
            nosingWidth		= Mathf.Max(0, nosingWidth);

            width			= Mathf.Max(kMinWidth, absWidth) * (width < 0 ? -1 : 1);
            depth			= Mathf.Max(stepDepth, absDepth) * (depth < 0 ? -1 : 1);

            riserDepth		= Mathf.Max(kMinRiserDepth, riserDepth);
            sideDepth		= Mathf.Max(0, sideDepth);
            sideWidth		= Mathf.Max(kMinSideWidth, sideWidth);
            sideHeight		= Mathf.Max(0, sideHeight);

            var realHeight       = Mathf.Max(stepHeight, absHeight);
            var maxPlateauHeight = realHeight - stepHeight;

            plateauHeight		= Mathf.Clamp(plateauHeight, 0, maxPlateauHeight);

            var totalSteps      = Mathf.Max(1, Mathf.FloorToInt((realHeight - plateauHeight + kStepSmudgeValue) / stepHeight));
            var totalStepHeight = totalSteps * stepHeight;

            plateauHeight		= Mathf.Max(0, realHeight - totalStepHeight);
            stepDepth			= Mathf.Clamp(stepDepth, kMinStepDepth, absDepth / totalSteps);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateLinearStairs(ref brushContainer, ref this);
        }


        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        public void OnEdit(IChiselHandles handles)
        {
            var newDefinition = this;

            {
                var stepDepthOffset = this.StepDepthOffset;
                var stepHeight      = this.stepHeight;
                var stepCount       = this.StepCount;
                var bounds          = this.bounds;

                var steps		    = handles.moveSnappingSteps;
                steps.y			    = stepHeight;

                if (handles.DoBoundsHandle(ref bounds, snappingSteps: steps))
                    newDefinition.bounds = bounds;

                var min			= new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z));
                var max			= new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z));

                var size        = (max - min);

                var heightStart = bounds.max.y + (bounds.size.y < 0 ? size.y : 0);

                var edgeHeight  = heightStart - stepHeight * stepCount;
                var pHeight0	= new Vector3(min.x, edgeHeight, max.z);
                var pHeight1	= new Vector3(max.x, edgeHeight, max.z);

                var depthStart = bounds.min.z - (bounds.size.z < 0 ? size.z : 0);

                var pDepth0		= new Vector3(min.x, max.y, depthStart + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, depthStart + stepDepthOffset);

                if (handles.DoTurnHandle(ref bounds))
                    newDefinition.bounds = bounds;

                if (handles.DoEdgeHandle1D(out edgeHeight, Axis.Y, pHeight0, pHeight1, snappingStep: stepHeight))
                {
                    var totalStepHeight = Mathf.Clamp((heightStart - edgeHeight), size.y % stepHeight, size.y);
                    const float kSmudgeValue = 0.0001f;
                    var oldStepCount = newDefinition.StepCount;
                    var newStepCount = Mathf.Max(1, Mathf.FloorToInt((Mathf.Abs(totalStepHeight) + kSmudgeValue) / stepHeight));

                    newDefinition.stepDepth     = (oldStepCount * newDefinition.stepDepth) / newStepCount;
                    newDefinition.plateauHeight = size.y - (stepHeight * newStepCount);
                }

                if (handles.DoEdgeHandle1D(out stepDepthOffset, Axis.Z, pDepth0, pDepth1, snappingStep: ChiselLinearStairsDefinition.kMinStepDepth))
                {
                    stepDepthOffset -= depthStart;
                    stepDepthOffset = Mathf.Clamp(stepDepthOffset, 0, this.absDepth - ChiselLinearStairsDefinition.kMinStepDepth);
                    newDefinition.stepDepth = ((this.absDepth - stepDepthOffset) / this.StepCount);
                }

                float heightOffset;
                var prevModified = handles.modified;
                {
                    var direction = Vector3.Cross(Vector3.forward, pHeight0 - pDepth0).normalized;
                    handles.DoEdgeHandle1DOffset(out var height0vec, Axis.Y, pHeight0, pDepth0, direction, snappingStep: stepHeight);
                    handles.DoEdgeHandle1DOffset(out var height1vec, Axis.Y, pHeight1, pDepth1, direction, snappingStep: stepHeight);
                    var height0 = Vector3.Dot(direction, height0vec);
                    var height1 = Vector3.Dot(direction, height1vec);
                    if (Mathf.Abs(height0) > Mathf.Abs(height1)) heightOffset = height0; else heightOffset = height1;
                }
                if (prevModified != handles.modified)
                {
                    newDefinition.plateauHeight += heightOffset;
                }
            }
            if (handles.modified)
                this = newDefinition;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}