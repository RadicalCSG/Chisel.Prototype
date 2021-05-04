using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Entities;
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
        bool HasValidState();

        // TODO: make these messages show in hierarchy tooltips
        void OnMessages(IChiselMessages messages);
    }

    public interface IBrushGenerator
    {
        BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator);
    }

    public interface ISerializedBrushGenerator<BrushGenerator> : IChiselNodeGenerator
        where BrushGenerator : unmanaged, IBrushGenerator
    {
        BrushGenerator GetBrushGenerator();
    }

    public interface IBranchGenerator
    {
        int PrepareAndCountRequiredBrushMeshes();
        bool GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator);
        void Dispose();

        // Temporary workaround
        void FixupOperations(CSGTreeBranch branch);
    }

    public interface ISerializedBranchGenerator<BranchGenerator> : IChiselNodeGenerator
        where BranchGenerator  : unmanaged, IBranchGenerator
    {
        BranchGenerator GetBranchGenerator();
    }
}
