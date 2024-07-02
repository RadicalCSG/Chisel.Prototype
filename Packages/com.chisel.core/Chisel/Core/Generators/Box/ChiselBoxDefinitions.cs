using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

namespace Chisel.Core
{
    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBox : IBrushGenerator
    {
        public readonly static ChiselBox DefaultValues = new ChiselBox
        {
            bounds = new AABB { Center = float3.zero, Extents = new float3(0.5f) }
        };

        public AABB bounds;


        #region Properties
        public float3 Min { get { return bounds.Min; } set { bounds = MathExtensions.CreateAABB(min: Min, max: Max); } }
        public float3 Max { get { return bounds.Max; } set { bounds = MathExtensions.CreateAABB(min: Min, max: Max); } }
        public float3 Size
        {
            get { return bounds.Max - bounds.Min; }
            set { bounds.Extents = math.abs(value) * 0.5f; }
        }

        public float3 Center
        {
            get { return (bounds.Max + bounds.Min) * 0.5f; }
            set { bounds.Center = value; }
        }
        #endregion

        #region Generate
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
            var min = math.min(originalBox.Min, originalBox.Max);
            var max = math.max(originalBox.Min, originalBox.Max);
            originalBox = MathExtensions.CreateAABB(min: min, max: max);
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