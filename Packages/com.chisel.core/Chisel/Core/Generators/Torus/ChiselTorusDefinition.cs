using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using Vector3 = UnityEngine.Vector3;
using AOT;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    [Serializable]
    public struct TorusSettings
    {
        public const float kMinTubeDiameter = 0.1f;

        public float    outerDiameter;
        public float    InnerDiameter { get { return math.max(0, outerDiameter - (tubeWidth * 2)); } set { tubeWidth = math.max(kMinTubeDiameter, (outerDiameter - InnerDiameter) * 0.5f); } }
        public float    tubeWidth;
        public float    tubeHeight;
        public float    tubeRotation;
        public float    startAngle;
        public float    totalAngle;
        public int      verticalSegments;
        public int      horizontalSegments;

        [MarshalAs(UnmanagedType.U1)]
        public bool     fitCircle;

    }

    public struct ChiselTorusGenerator : IChiselBranchTypeGenerator<TorusSettings>
    {
        [BurstCompile()]
        unsafe struct PrepareAndCountBrushesJob : IJobParallelForDefer
        {
            [NoAlias] public NativeArray<TorusSettings>     settings;
            [NoAlias, WriteOnly] public NativeArray<int>    brushCounts;

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
            [NoAlias, ReadOnly] public NativeArray<int>                     brushCounts;
            [NoAlias, WriteOnly] public NativeArray<Range>                  ranges;
            [NoAlias] public NativeList<BlobAssetReference<BrushMeshBlob>>  brushMeshes;

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
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range>                                         ranges;
            [NoAlias] public NativeArray<TorusSettings>                                 settings;
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
                            for (int i = range.start, m = 0; i < range.end; i++, m++)
                            {
                                brushMeshes[i] = generatedBrushMeshes[m];
                            }
                        }
                    }
                }
                finally
                {
                    var setting = settings[index];
                    Dispose(ref setting);
                    settings[index] = setting;
                }
            }
        }

        [BurstDiscard]
        public JobHandle Schedule(NativeList<TorusSettings> settings, NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions, NativeList<Range> ranges, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
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
                brushCounts = brushCounts,
                ranges      = ranges.AsArray(),
                brushMeshes = brushMeshes
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

        public static void Dispose(ref TorusSettings settings)
        {
        }

        public int PrepareAndCountRequiredBrushMeshes(ref TorusSettings settings)
        {
            return settings.horizontalSegments;
        }

        public static int PrepareAndCountRequiredBrushMeshes_(ref TorusSettings settings)
        {
            return settings.horizontalSegments;
        }

        [BurstCompile()]
        public static bool GenerateMesh(TorusSettings settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            using (var vertices = BrushMeshFactory.GenerateTorusVertices(settings.outerDiameter,
                                                                         settings.tubeWidth,
                                                                         settings.tubeHeight,
                                                                         settings.tubeRotation,
                                                                         settings.startAngle,
                                                                         settings.totalAngle,
                                                                         settings.verticalSegments,
                                                                         settings.horizontalSegments,
                                                                         settings.fitCircle,
                                                                         Allocator.Temp))
            {
                if (!BrushMeshFactory.GenerateTorus(brushMeshes,
                                                    in vertices,
                                                    settings.verticalSegments,
                                                    settings.horizontalSegments,
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
        }

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch, TorusSettings settings) { }
    }

    [Serializable]
    public struct ChiselTorusDefinition : IChiselBranchGenerator<ChiselTorusGenerator, TorusSettings>
    {
        public const string kNodeTypeName = "Torus";

        public const int kDefaultHorizontalSegments = 8;
        public const int kDefaultVerticalSegments = 8;

        // TODO: add scale the tube in y-direction (use transform instead?)
        // TODO: add start/total angle of tube

        [HideFoldout] public TorusSettings settings;


        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            // TODO: create constants
            settings.tubeWidth = 0.5f;
            settings.tubeHeight = 0.5f;
            settings.outerDiameter = 1.0f;
            settings.tubeRotation = 0;
            settings.startAngle = 0.0f;
            settings.totalAngle = 360.0f;
            settings.horizontalSegments = kDefaultHorizontalSegments;
            settings.verticalSegments = kDefaultVerticalSegments;

            settings.fitCircle = true;
        }

        public int RequiredSurfaceCount { get { return 6; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            settings.tubeWidth			= math.max(settings.tubeWidth,  TorusSettings.kMinTubeDiameter);
            settings.tubeHeight			= math.max(settings.tubeHeight, TorusSettings.kMinTubeDiameter);
            settings.outerDiameter		= math.max(settings.outerDiameter, settings.tubeWidth * 2);

            settings.horizontalSegments	= math.max(settings.horizontalSegments, 3);
            settings.verticalSegments	= math.max(settings.verticalSegments, 3);

            settings.totalAngle			= math.clamp(settings.totalAngle, 1, 360); // TODO: constants
        }

        public TorusSettings GenerateSettings()
        {
            return settings;
        }

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselTorusDefinition definition, float3[] vertices, LineMode lineMode)
        {
            var horzSegments	= definition.settings.horizontalSegments;
            var vertSegments	= definition.settings.verticalSegments;
            
            if (definition.settings.totalAngle != 360)
                horzSegments++;
            
            var prevColor		= renderer.color;
            prevColor.a *= 0.8f;
            var color			= prevColor;
            color.a *= 0.6f;

            renderer.color = color;
            for (int i = 0, j = 0; i < horzSegments; i++, j += vertSegments)
                renderer.DrawLineLoop(vertices, j, vertSegments, lineMode: lineMode, thickness: kVertLineThickness);

            for (int k = 0; k < vertSegments; k++)
            {
                for (int i = 0, j = 0; i < horzSegments - 1; i++, j += vertSegments)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + vertSegments], lineMode: lineMode, thickness: kHorzLineThickness);
            }
            if (definition.settings.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    renderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            renderer.color = prevColor;
        }

        public void OnEdit(IChiselHandles handles)
        {
            var normal			= Vector3.up;

            float3[] vertices = null;
            if (BrushMeshFactory.GenerateTorusVertices(this, ref vertices))
            {
                var baseColor = handles.color;
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);
                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }

            var outerRadius = settings.outerDiameter * 0.5f;
            var innerRadius = settings.InnerDiameter * 0.5f;
            var topPoint	= normal * (settings.tubeHeight * 0.5f);
            var bottomPoint	= normal * (-settings.tubeHeight * 0.5f);

            handles.DoRadiusHandle(ref outerRadius, normal, float3.zero);
            handles.DoRadiusHandle(ref innerRadius, normal, float3.zero);
            handles.DoDirectionHandle(ref bottomPoint, -normal);
            handles.DoDirectionHandle(ref topPoint, normal);
            if (handles.modified)
            {
                settings.outerDiameter	= outerRadius * 2.0f;
                settings.InnerDiameter	= innerRadius * 2.0f;
                settings.tubeHeight		= (topPoint.y - bottomPoint.y);
                // TODO: handle sizing down
            }
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