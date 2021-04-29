using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;

namespace Chisel.Core
{
    public struct ChiselBoxGenerator : IChiselBrushTypeGenerator<MinMaxAABB>
    {
        [BurstCompile(CompileSynchronously = true)]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>                                          settings;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>>   surfaceDefinitions;
            [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>                  brushMeshes;

            public void Execute(int index)
            {
                brushMeshes[index] = GenerateMesh(settings[index], surfaceDefinitions[index], Allocator.Persistent);
            }
        }

        public JobHandle Schedule(NativeList<MinMaxAABB> settings, NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
        {
            var job = new CreateBrushesJob
            {
                settings            = settings.AsArray(),
                surfaceDefinitions  = surfaceDefinitions.AsArray(),
                brushMeshes         = brushMeshes.AsArray()
            };
            return job.Schedule(settings, 8);
        }

        [BurstCompile(CompileSynchronously = true)]
        public static BlobAssetReference<BrushMeshBlob> GenerateMesh(MinMaxAABB bounds, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator)
        {
            if (!BrushMeshFactory.CreateBox(bounds.Min, bounds.Max,
                                            in surfaceDefinitionBlob,
                                            out var newBrushMesh,
                                            allocator))
                return default;
            return newBrushMesh;
        }
    }


    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBoxDefinition : IChiselBrushGenerator<ChiselBoxGenerator, MinMaxAABB>
    {
        public const string kNodeTypeName = "Box";

        public static readonly MinMaxAABB kDefaultBounds = new MinMaxAABB { Min = new float3(-0.5f), Max = new float3(0.5f) };

        public MinMaxAABB               bounds;

        #region Properties
        public float3   Min     { get { return bounds.Min;    } set { bounds.Min    = value; } }
        public float3   Max	    { get { return bounds.Max;    } set { bounds.Max    = value; } }
        public float3   Size    
        { 
            get { return bounds.Max - bounds.Min; } 
            set 
            {
                var newSize  = math.abs(value);
                var halfSize = newSize * 0.5f;
                var center   = this.Center;
                bounds.Min = center - halfSize;
                bounds.Max = center + halfSize;
            } 
        }

        public float3   Center  
        { 
            get { return (bounds.Max + bounds.Min) * 0.5f; } 
            set 
            { 
                var newSize  = math.abs(Size);
                var halfSize = newSize * 0.5f;
                bounds.Min = value - halfSize;
                bounds.Max = value + halfSize;
            } 
        }
        #endregion

        //[NamedItems("Top", "Bottom", "Right", "Left", "Back", "Front", fixedSize = 6)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            bounds = kDefaultBounds;
        }

        public int RequiredSurfaceCount { get { return 6; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            var originalBox = bounds;
            
            bounds.Min = math.min(originalBox.Min, originalBox.Max);
            bounds.Max = math.max(originalBox.Min, originalBox.Max);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct CreateBoxJob : IJob
        {
            public MinMaxAABB                                           bounds;
            [NoAlias, ReadOnly]
            public BlobAssetReference<NativeChiselSurfaceDefinition>    surfaceDefinitionBlob;
            
            [NoAlias]
            public NativeReference<BlobAssetReference<BrushMeshBlob>>   brushMesh;

            public void Execute()
            {
                if (!BrushMeshFactory.CreateBox(bounds.Min, bounds.Max,
                                                in surfaceDefinitionBlob,
                                                out var newBrushMesh,
                                                Allocator.Persistent))
                    brushMesh.Value = default;
                else
                    brushMesh.Value = newBrushMesh;
            }
        }

        public MinMaxAABB GenerateSettings()
        {
            return bounds;
        }

        [BurstCompile(CompileSynchronously = true)]
        public JobHandle Generate(NativeReference<BlobAssetReference<BrushMeshBlob>> brushMeshRef, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob)
        {
            var createBoxJob = new CreateBoxJob
            {
                bounds                  = bounds,
                surfaceDefinitionBlob   = surfaceDefinitionBlob,
                brushMesh               = brushMeshRef
            };
            return createBoxJob.Schedule();
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoBoundsHandle(ref bounds);
            handles.RenderBoxMeasurements(bounds);
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        public bool HasValidState()
        {
            var size = this.Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                return false;
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
            var size = this.Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
    }
}