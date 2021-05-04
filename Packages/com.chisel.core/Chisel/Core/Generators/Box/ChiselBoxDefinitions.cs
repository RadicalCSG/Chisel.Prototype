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
    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBox : IBrushGenerator
    {
        public readonly static ChiselBox DefaultValues = new ChiselBox
        {
            bounds = new MinMaxAABB { Min = new float3(-0.5f), Max = new float3(0.5f) }
        };

        public MinMaxAABB bounds;

        #region Properties
        public float3 Min { get { return bounds.Min; } set { bounds.Min = value; } }
        public float3 Max { get { return bounds.Max; } set { bounds.Max = value; } }
        public float3 Size
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

        public float3 Center
        {
            get { return (bounds.Max + bounds.Min) * 0.5f; }
            set
            {
                var newSize = math.abs(Size);
                var halfSize = newSize * 0.5f;
                bounds.Min = value - halfSize;
                bounds.Max = value + halfSize;
            }
        }
        #endregion


        [BurstCompile(CompileSynchronously = true)]
        public BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator)
        {
            if (!BrushMeshFactory.CreateBox(bounds.Min, bounds.Max,
                                            in surfaceDefinitionBlob,
                                            out var newBrushMesh,
                                            allocator))
                return default;
            return newBrushMesh;
        }

        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 6; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        [BurstDiscard]
        public void Validate()
        {
            var originalBox = bounds;
            bounds.Min = math.min(originalBox.Min, originalBox.Max);
            bounds.Max = math.max(originalBox.Min, originalBox.Max);
        }
    }

    [Serializable]
    public struct ChiselBoxDefinition : ISerializedBrushGenerator<ChiselBox>
    {
        public const string kNodeTypeName = "Box";

        [HideFoldout] public ChiselBox settings;

        //[NamedItems("Top", "Bottom", "Right", "Left", "Back", "Front", fixedSize = 6)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset() { settings = ChiselBox.DefaultValues; }
        public int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) => settings.UpdateSurfaces(ref surfaceDefinition);
        public void Validate() => settings.Validate(); 

        public ChiselBox GetBrushGenerator() { return settings; }


        public void OnEdit(IChiselHandles handles)
        {
            handles.DoBoundsHandle(ref settings.bounds);
            handles.RenderBoxMeasurements(settings.bounds);
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        public bool HasValidState()
        {
            var size = settings.Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                return false;
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
            var size = settings.Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
    }
}