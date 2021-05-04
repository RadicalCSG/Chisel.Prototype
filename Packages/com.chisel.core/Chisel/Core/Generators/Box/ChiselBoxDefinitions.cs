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

        #region Generate
        [BurstCompile]
        public BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator)
        {
            if (!BrushMeshFactory.CreateBox(bounds.Min, bounds.Max,
                                            in surfaceDefinitionBlob,
                                            out var newBrushMesh,
                                            allocator))
                return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 6; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }
        #endregion

        #region Validation
        public void Validate()
        {
            var originalBox = bounds;
            bounds.Min = math.min(originalBox.Min, originalBox.Max);
            bounds.Max = math.max(originalBox.Min, originalBox.Max);
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages)
        {
            var size = Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
        #endregion

        #region Reset
        public void Reset() { this = DefaultValues; }
        #endregion
    }

    [Serializable]
    public class ChiselBoxDefinition : SerializedBrushGenerator<ChiselBox>
    {
        public const string kNodeTypeName = "Box";

        //[NamedItems("Top", "Bottom", "Right", "Left", "Back", "Front", fixedSize = 6)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        #region OnEdit
        public override void OnEdit(IChiselHandles handles)
        {
            handles.DoBoundsHandle(ref settings.bounds);
            handles.RenderBoxMeasurements(settings.bounds);
        }
        #endregion
    }
}