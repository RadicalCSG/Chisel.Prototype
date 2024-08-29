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
        public readonly static ChiselBox DefaultValues = new()
        {
            bounds = new MinMaxAABB { Min = float3.zero, Max = float3.zero }
        };

        public MinMaxAABB bounds;


        #region Properties
        public float3 Min { readonly get { return bounds.Min; } set { bounds.SetMin(value); } }
        public float3 Max { readonly get { return bounds.Max; } set { bounds.SetMax(value); } }
        public float3 Size
        {
			readonly get { return bounds.Max - bounds.Min; }
            set { bounds.SetExtents(math.abs(value) * 0.5f); }
        }

        public float3 Center
        {
			readonly get { return (bounds.Max + bounds.Min) * 0.5f; }
            set { bounds.SetCenter(value); }
        }
        #endregion

        #region Generate
        public BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator)
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
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion

        #region Validation
        public bool Validate()
        {
            var originalBox = bounds;
            var min = math.min(originalBox.Min, originalBox.Max);
            var max = math.max(originalBox.Min, originalBox.Max);
            bounds = new MinMaxAABB { Min = min, Max = max };
            return true;
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

		[BurstDiscard]
        public void GetMessages(IChiselMessageHandler messages)
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