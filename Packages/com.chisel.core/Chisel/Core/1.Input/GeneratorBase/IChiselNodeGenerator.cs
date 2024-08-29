using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public interface IChiselNodeGenerator
    {
        int RequiredSurfaceCount { get; }
        void Reset();
        bool Validate();
        void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray);
        void OnEdit(IChiselHandles handles);
		void GetMessages(IChiselMessageHandler messages);
    }

    public interface IBrushGenerator
    {
		BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> internalSurfaceArrayBlob, Allocator allocator);
        void Reset();
        int RequiredSurfaceCount { get; }
        void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray);
        bool Validate();
		void GetMessages(IChiselMessageHandler messages);
    }

    public abstract class SerializedBrushGenerator<BrushGenerator> : IChiselNodeGenerator
        where BrushGenerator : unmanaged, IBrushGenerator
    {
        [HideFoldout] public BrushGenerator settings;

        public virtual int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public virtual BrushGenerator GetBrushGenerator() { return settings; }

        public virtual void Reset() { settings.Reset(); }

        public virtual void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray) 
        {
            settings.UpdateSurfaces(ref surfaceArray);
        }

        public virtual bool Validate()
        {
            return settings.Validate();
        }
        
        public virtual void GetMessages(IChiselMessageHandler messages)
        {
            settings.GetMessages(messages);
        }

        public abstract void OnEdit(IChiselHandles handles);
    }

    public struct GeneratedNode
    {
        public int                                parentIndex;    // -1 means root of generated node
        public CSGOperationType                   operation;
        public float4x4                           transformation;
        public BlobAssetReference<BrushMeshBlob>  brushMesh;      // Note: ignored if type is not Brush


        public static GeneratedNode GenerateBrush(BlobAssetReference<BrushMeshBlob> brushMesh, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return GenerateBrush(brushMesh, float4x4.identity, operation, parentIndex);
        }

        public static GeneratedNode GenerateBrush(BlobAssetReference<BrushMeshBlob> brushMesh, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return new GeneratedNode
            {
                parentIndex     = parentIndex,
                transformation  = transformation,
                operation       = operation,
                brushMesh       = brushMesh
            };
        }

        public static GeneratedNode GenerateBranch(CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return GenerateBranch(float4x4.identity, operation, parentIndex);
        }

        public static GeneratedNode GenerateBranch(float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return new GeneratedNode
            {
                parentIndex     = parentIndex,
                transformation  = transformation,
                operation       = operation,
                brushMesh       = BlobAssetReference<BrushMeshBlob>.Null
            };
        }
    }

    public interface IBranchGenerator
    {
        int PrepareAndCountRequiredBrushMeshes();
        bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> internalSurfaceArrayBlob, NativeList<GeneratedNode> nodes, Allocator allocator);
        void Dispose();

        void Reset();
        int RequiredSurfaceCount { get; }
        void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray);
        bool Validate();
        void GetWarningMessages(IChiselMessageHandler messages);
    }

    public abstract class SerializedBranchGenerator<BranchGenerator> : IChiselNodeGenerator
        where BranchGenerator : unmanaged, IBranchGenerator
    {
        [HideFoldout] public BranchGenerator settings;

        public virtual int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public virtual BranchGenerator GetBranchGenerator() { return settings; }

        public virtual void Reset()     { settings.Reset(); }
        public virtual bool Validate()  { return settings.Validate(); }

        public virtual void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray)
        {
            settings.UpdateSurfaces(ref surfaceArray);
        }

        public virtual void GetMessages(IChiselMessageHandler messages)
        {
            settings.GetWarningMessages(messages);
        }

        public abstract void OnEdit(IChiselHandles handles);
    }

}
