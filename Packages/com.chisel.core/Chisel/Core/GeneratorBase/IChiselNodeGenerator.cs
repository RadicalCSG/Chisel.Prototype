using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    public interface IChiselNodeGenerator
    {
        int RequiredSurfaceCount { get; }
        void Reset();
        void Validate();
        void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition);
        void OnEdit(IChiselHandles handles);
        void GetWarningMessages(IChiselMessageHandler messages);
    }

    public interface IBrushGenerator
    {
        ChiselBlobAssetReference<BrushMeshBlob> GenerateMesh(ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator);
        void Reset();
        int RequiredSurfaceCount { get; }
        void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition);
        void Validate();
        void GetWarningMessages(IChiselMessageHandler messages);
    }

    public abstract class SerializedBrushGenerator<BrushGenerator> : IChiselNodeGenerator
        where BrushGenerator : unmanaged, IBrushGenerator
    {
        [HideFoldout] public BrushGenerator settings;

        public virtual int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public virtual BrushGenerator GetBrushGenerator() { return settings; }

        public virtual void Reset() { settings.Reset(); }

        public virtual void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) 
        {
            settings.UpdateSurfaces(ref surfaceDefinition);
        }

        public virtual void Validate()
        {
            settings.Validate();
        }
        
        public virtual void GetWarningMessages(IChiselMessageHandler messages)
        {
            settings.GetWarningMessages(messages);
        }

        public abstract void OnEdit(IChiselHandles handles);
    }

    public struct GeneratedNode
    {
        public int                                  parentIndex;    // -1 means root of generated node
        public CSGOperationType                     operation;
        public float4x4                             transformation;
        public ChiselBlobAssetReference<BrushMeshBlob>    brushMesh;      // Note: ignored if type is not Brush


        public static GeneratedNode GenerateBrush(ChiselBlobAssetReference<BrushMeshBlob> brushMesh, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return GenerateBrush(brushMesh, float4x4.identity, operation, parentIndex);
        }

        public static GeneratedNode GenerateBrush(ChiselBlobAssetReference<BrushMeshBlob> brushMesh, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
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
                brushMesh       = ChiselBlobAssetReference<BrushMeshBlob>.Null
            };
        }
    }

    public interface IBranchGenerator
    {
        int PrepareAndCountRequiredBrushMeshes();
        bool GenerateNodes(ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, Allocator allocator);
        void Dispose();

        void Reset();
        int RequiredSurfaceCount { get; }
        void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition);
        void Validate();
        void GetWarningMessages(IChiselMessageHandler messages);
    }

    public abstract class SerializedBranchGenerator<BranchGenerator> : IChiselNodeGenerator
        where BranchGenerator : unmanaged, IBranchGenerator
    {
        [HideFoldout] public BranchGenerator settings;

        public virtual int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public virtual BranchGenerator GetBranchGenerator() { return settings; }

        public virtual void Reset()     { settings.Reset(); }
        public virtual void Validate()  { settings.Validate(); }

        public virtual void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            settings.UpdateSurfaces(ref surfaceDefinition);
        }

        public virtual void GetWarningMessages(IChiselMessageHandler messages)
        {
            settings.GetWarningMessages(messages);
        }

        public abstract void OnEdit(IChiselHandles handles);
    }

}
