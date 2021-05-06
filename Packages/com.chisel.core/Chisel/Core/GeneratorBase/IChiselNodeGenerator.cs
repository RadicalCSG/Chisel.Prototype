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
        void GetWarningMessages(IChiselMessageHandler messages);
    }

    public interface IBrushGenerator
    {
        BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator);
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

    public interface IBranchGenerator
    {
        int PrepareAndCountRequiredBrushMeshes();
        bool GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator);
        void Dispose();

        // TODO: Fix Temporary workaround, make it possible to setup hierarchy from within jobs
        void FixupOperations(CSGTreeBranch branch);


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
