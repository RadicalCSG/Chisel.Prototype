using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using Bounds  = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;

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

    [Serializable]
    public struct LinearStairsSettings
    {
        public const float kStepSmudgeValue = BrushMeshFactory.LineairStairsData.kStepSmudgeValue;

        // TODO: add all spiral stairs improvements to linear stairs

        public MinMaxAABB bounds;

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
        
        //[NamedItems("Top", "Bottom", "Left", "Right", "Front", "Back", "Tread", "Step", overflow = "Side {0}", fixedSize = 8)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        
        public float	Width  { get { return BoundsSize.x; } set { var size = BoundsSize; size.x = value; BoundsSize = size; } }
        public float	Height { get { return BoundsSize.y; } set { var size = BoundsSize; size.y = value; BoundsSize = size; } }
        public float	Depth  { get { return BoundsSize.z; } set { var size = BoundsSize; size.z = value; BoundsSize = size; } }

        public float3 Center
        {
            get { return (bounds.Max + bounds.Min) * 0.5f; }
            set
            {
                var newSize = math.abs(BoundsSize);
                var halfSize = newSize * 0.5f;
                bounds.Min = value - halfSize;
                bounds.Max = value + halfSize;
            }
        }

        public float3 BoundsSize
        {
            get { return bounds.Max - bounds.Min; }
            set
            {
                var newSize = math.abs(value);
                var halfSize = newSize * 0.5f;
                var center = this.Center;
                bounds.Min = center - halfSize;
                bounds.Max = center + halfSize;
            }
        }
        
        public float3   BoundsMin   { get { return math.min(bounds.Min, bounds.Max); } }
        public float3   BoundsMax   { get { return math.max(bounds.Min, bounds.Max); } }
        
        public float	AbsWidth  { get { return math.abs(BoundsSize.x); } }
        public float	AbsHeight { get { return math.abs(BoundsSize.y); } }
        public float	AbsDepth  { get { return math.abs(BoundsSize.z); } }

        public int StepCount
        {
            get
            {
                return math.max(1,
                            (int)math.floor((AbsHeight - plateauHeight + kStepSmudgeValue) / stepHeight));
            }
        }

        public float StepDepthOffset
        {
            get { return math.max(0, AbsDepth - (StepCount * stepDepth)); }
        }
    }

    public struct ChiselLinearStairsGenerator : IChiselBranchTypeGenerator<LinearStairsSettings>
    {
        [BurstCompile()]
        unsafe struct PrepareAndCountBrushesJob : IJobParallelForDefer
        {
            [NoAlias] public NativeArray<LinearStairsSettings>  settings;
            [NoAlias, WriteOnly] public NativeArray<int>        brushCounts;

            public void Execute(int index)
            {
                var setting = settings[index];
                brushCounts[index] = PrepareAndCountRequiredBrushMeshes_(ref setting);
                settings[index] = setting;
            }
        }

        [BurstCompile()]
        unsafe struct AllocateBrushesJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int> brushCounts;
            [NoAlias, WriteOnly] public NativeArray<Range> ranges;
            [NoAlias] public NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes;

            public void Execute()
            {
                var totalRequiredBrushCount = 0;
                for (int i = 0; i < brushCounts.Length; i++)
                {
                    var length = brushCounts[i];
                    var start = totalRequiredBrushCount;
                    var end = start + length;
                    ranges[i] = new Range { start = start, end = end };
                    totalRequiredBrushCount += length;
                }
                brushMeshes.Resize(totalRequiredBrushCount, NativeArrayOptions.ClearMemory);
            }
        }

        [BurstCompile()]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<LinearStairsSettings>                settings;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range> ranges;
            [NativeDisableParallelForRestriction]
            [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>  brushMeshes;

            public void Execute(int index)
            {
                try
                {
                    var range = ranges[index];
                    var requiredSubMeshCount = range.Length;
                    if (requiredSubMeshCount != 0)
                    {
                        using (var generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp))
                        {
                            generatedBrushMeshes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory);
                            if (!GenerateMesh(settings[index], surfaceDefinitions[index], generatedBrushMeshes, Allocator.Persistent))
                            {
                                ranges[index] = new Range { start = 0, end = 0 };
                                return;
                            }
                            
                            Debug.Assert(requiredSubMeshCount == generatedBrushMeshes.Length);
                            if (requiredSubMeshCount != generatedBrushMeshes.Length)
                                throw new InvalidOperationException();
                            for (int i = range.start, m=0; i < range.end; i++,m++)
                            {
                                brushMeshes[i] = generatedBrushMeshes[m];
                            }
                        }
                    }
                }
                finally
                {
                    Dispose(settings[index]);
                }
            }
        }

        [BurstDiscard]
        public JobHandle Schedule(NativeList<LinearStairsSettings> settings, NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions, NativeList<Range> ranges, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
        {
            var brushCounts = new NativeArray<int>(settings.Length, Allocator.TempJob);
            var countBrushesJob = new PrepareAndCountBrushesJob
            {
                settings            = settings.AsArray(),
                brushCounts         = brushCounts
            };
            var brushCountJobHandle = countBrushesJob.Schedule(settings, 8);
            var allocateBrushesJob = new AllocateBrushesJob
            {
                brushCounts         = brushCounts,
                ranges              = ranges.AsArray(),
                brushMeshes         = brushMeshes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(brushCountJobHandle);
            var createJob = new CreateBrushesJob
            {
                settings            = settings.AsArray(),
                ranges              = ranges.AsArray(),
                brushMeshes         = brushMeshes.AsDeferredJobArray(),
                surfaceDefinitions  = surfaceDefinitions.AsArray()
            };
            var createJobHandle = createJob.Schedule(settings, 8, allocateBrushesJobHandle);
            return brushCounts.Dispose(createJobHandle);
        }

        public static void Dispose(LinearStairsSettings settings)
        {
        }

        [BurstCompile()]
        public int PrepareAndCountRequiredBrushMeshes(ref LinearStairsSettings settings)
        {
            var size = settings.BoundsSize;
            if (math.any(size == 0))
                return 0;

            var description = new BrushMeshFactory.LineairStairsData(settings.bounds,
                                                                        settings.stepHeight, settings.stepDepth,
                                                                        settings.treadHeight,
                                                                        settings.nosingDepth, settings.nosingWidth,
                                                                        settings.plateauHeight,
                                                                        settings.riserType, settings.riserDepth,
                                                                        settings.leftSide, settings.rightSide,
                                                                        settings.sideWidth, settings.sideHeight, settings.sideDepth);
            return description.subMeshCount;
        }

        [BurstCompile()]
        public static int PrepareAndCountRequiredBrushMeshes_(ref LinearStairsSettings settings)
        {
            var size = settings.BoundsSize;
            if (math.any(size == 0))
                return 0;

            var description = new BrushMeshFactory.LineairStairsData(settings.bounds,
                                                                        settings.stepHeight, settings.stepDepth,
                                                                        settings.treadHeight,
                                                                        settings.nosingDepth, settings.nosingWidth,
                                                                        settings.plateauHeight,
                                                                        settings.riserType, settings.riserDepth,
                                                                        settings.leftSide, settings.rightSide,
                                                                        settings.sideWidth, settings.sideHeight, settings.sideDepth);
            return description.subMeshCount;
        }

        [BurstCompile()]
        public static bool GenerateMesh(LinearStairsSettings settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            var description = new BrushMeshFactory.LineairStairsData(settings.bounds,
                                                                        settings.stepHeight, settings.stepDepth,
                                                                        settings.treadHeight,
                                                                        settings.nosingDepth, settings.nosingWidth,
                                                                        settings.plateauHeight,
                                                                        settings.riserType, settings.riserDepth,
                                                                        settings.leftSide, settings.rightSide,
                                                                        settings.sideWidth, settings.sideHeight, settings.sideDepth);
            const int subMeshOffset = 0;
            if (!BrushMeshFactory.GenerateLinearStairsSubMeshes(brushMeshes, 
                                                                subMeshOffset, 
                                                                in description, 
                                                                in surfaceDefinitionBlob, 
                                                                Allocator.Persistent))
            {
                for (int i = 0; i < brushMeshes.Length; i++)
                {
                    if (brushMeshes[i].IsCreated)
                        brushMeshes[i].Dispose();
                }
                return false;
            }
            return true;
        }

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch, LinearStairsSettings settings) { }
    }

    // https://www.archdaily.com/892647/how-to-make-calculations-for-staircase-designs
    // https://inspectapedia.com/Stairs/2024s.jpg
    // https://landarchbim.com/2014/11/18/stair-nosing-treads-and-stringers/
    // https://en.wikipedia.org/wiki/Stairs
    [Serializable]
    public struct ChiselLinearStairsDefinition : IChiselBranchGenerator<ChiselLinearStairsGenerator, LinearStairsSettings>
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

        [HideFoldout] public LinearStairsSettings settings;

        //[NamedItems("Top", "Bottom", "Left", "Right", "Front", "Back", "Tread", "Step", overflow = "Side {0}", fixedSize = 8)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        

        public void Reset()
        {
            // TODO: set defaults using attributes?
            settings.stepHeight		= kDefaultStepHeight;
            settings.stepDepth		= kDefaultStepDepth;
            settings.treadHeight	= kDefaultTreadHeight;
            settings.nosingDepth	= kDefaultNosingDepth;
            settings.nosingWidth	= kDefaultNosingWidth;

            settings.Width			= kDefaultWidth;
            settings.Height			= kDefaultHeight;
            settings.Depth			= kDefaultDepth;

            settings.plateauHeight	= kDefaultPlateauHeight;

            settings.riserType		= StairsRiserType.ThinRiser;
            settings.leftSide		= StairsSideType.None;
            settings.rightSide		= StairsSideType.None;
            settings.riserDepth		= kDefaultRiserDepth;
            settings.sideDepth		= kDefaultSideDepth;
            settings.sideWidth		= kDefaultSideWidth;
            settings.sideHeight		= kDefaultSideHeight;
        }

        public int RequiredSurfaceCount { get { return (int)SurfaceSides.TotalSides; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition)
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

        public void Validate()
        {
            settings.stepHeight		= math.max(kMinStepHeight, settings.stepHeight);
            settings.stepDepth		= math.clamp(settings.stepDepth, kMinStepDepth, settings.AbsDepth);
            settings.treadHeight	= math.max(0, settings.treadHeight);
            settings.nosingDepth	= math.max(0, settings.nosingDepth);
            settings.nosingWidth	= math.max(0, settings.nosingWidth);

            settings.Width			= math.max(kMinWidth, settings.AbsWidth) * (settings.Width < 0 ? -1 : 1);
            settings.Depth			= math.max(settings.stepDepth, settings.AbsDepth) * (settings.Depth < 0 ? -1 : 1);

            settings.riserDepth		= math.max(kMinRiserDepth, settings.riserDepth);
            settings.sideDepth		= math.max(0, settings.sideDepth);
            settings.sideWidth		= math.max(kMinSideWidth, settings.sideWidth);
            settings.sideHeight		= math.max(0, settings.sideHeight);

            var realHeight          = math.max(settings.stepHeight, settings.AbsHeight);
            var maxPlateauHeight    = realHeight - settings.stepHeight;

            settings.plateauHeight	= math.clamp(settings.plateauHeight, 0, maxPlateauHeight);

            var totalSteps          = math.max(1, (int)math.floor((realHeight - settings.plateauHeight + LinearStairsSettings.kStepSmudgeValue) / settings.stepHeight));
            var totalStepHeight     = totalSteps * settings.stepHeight;

            settings.plateauHeight	= math.max(0, realHeight - totalStepHeight);
            settings.stepDepth		= math.clamp(settings.stepDepth, kMinStepDepth, settings.AbsDepth / totalSteps);
        }

        public LinearStairsSettings GenerateSettings()
        {
            return settings;
        }

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        public void OnEdit(IChiselHandles handles)
        {
            var newDefinition = this;

            {
                var stepDepthOffset = settings.StepDepthOffset;
                var stepHeight      = settings.stepHeight;
                var stepCount       = settings.StepCount;
                var bounds          = settings.bounds;

                var steps		    = handles.moveSnappingSteps;
                steps.y			    = stepHeight;

                if (handles.DoBoundsHandle(ref bounds, snappingSteps: steps))
                    newDefinition.settings.bounds = bounds;

                var min			= math.min(bounds.Min, bounds.Max);
                var max			= math.min(bounds.Min, bounds.Max);

                var size        = (max - min);

                var heightStart = bounds.Max.y + (size.y < 0 ? size.y : 0);

                var edgeHeight  = heightStart - stepHeight * stepCount;
                var pHeight0	= new Vector3(min.x, edgeHeight, max.z);
                var pHeight1	= new Vector3(max.x, edgeHeight, max.z);

                var depthStart = bounds.Min.z - (size.z < 0 ? size.z : 0);

                var pDepth0		= new Vector3(min.x, max.y, depthStart + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, depthStart + stepDepthOffset);

                if (handles.DoTurnHandle(ref bounds))
                    newDefinition.settings.bounds = bounds;

                if (handles.DoEdgeHandle1D(out edgeHeight, Axis.Y, pHeight0, pHeight1, snappingStep: stepHeight))
                {
                    var totalStepHeight = math.clamp((heightStart - edgeHeight), size.y % stepHeight, size.y);
                    const float kSmudgeValue = 0.0001f;
                    var oldStepCount = newDefinition.settings.StepCount;
                    var newStepCount = math.max(1, (int)math.floor((math.abs(totalStepHeight) + kSmudgeValue) / stepHeight));

                    newDefinition.settings.stepDepth     = (oldStepCount * newDefinition.settings.stepDepth) / newStepCount;
                    newDefinition.settings.plateauHeight = size.y - (stepHeight * newStepCount);
                }

                if (handles.DoEdgeHandle1D(out stepDepthOffset, Axis.Z, pDepth0, pDepth1, snappingStep: ChiselLinearStairsDefinition.kMinStepDepth))
                {
                    stepDepthOffset -= depthStart;
                    stepDepthOffset = math.clamp(stepDepthOffset, 0, settings.AbsDepth - ChiselLinearStairsDefinition.kMinStepDepth);
                    newDefinition.settings.stepDepth = ((settings.AbsDepth - stepDepthOffset) / settings.StepCount);
                }

                float heightOffset;
                var prevModified = handles.modified;
                {
                    var direction = Vector3.Cross(Vector3.forward, pHeight0 - pDepth0).normalized;
                    handles.DoEdgeHandle1DOffset(out var height0vec, Axis.Y, pHeight0, pDepth0, direction, snappingStep: stepHeight);
                    handles.DoEdgeHandle1DOffset(out var height1vec, Axis.Y, pHeight1, pDepth1, direction, snappingStep: stepHeight);
                    var height0 = Vector3.Dot(direction, height0vec);
                    var height1 = Vector3.Dot(direction, height1vec);
                    if (math.abs(height0) > math.abs(height1)) heightOffset = height0; else heightOffset = height1;
                }
                if (prevModified != handles.modified)
                {
                    newDefinition.settings.plateauHeight += heightOffset;
                }
            }
            if (handles.modified)
                this = newDefinition;
        }
        #endregion

        public bool HasValidState()
        {
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}