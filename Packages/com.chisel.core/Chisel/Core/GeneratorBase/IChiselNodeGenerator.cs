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
    // TODO: move somewhere else
    public struct Range
    {
        public int start;
        public int end;
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return end - start; }
        }

        public int Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return start + ((end - start) / 2); }
        }
    }



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

    public interface IChiselBrushTypeGenerator<Settings>
        where Settings : struct
    {
        JobHandle Schedule(NativeList<Settings> settings, NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes);
    }


    public interface IChiselBrushGenerator<Generator, Settings> : IChiselNodeGenerator
        where Settings : struct
        where Generator : IChiselBrushTypeGenerator<Settings>
    {
        Settings GenerateSettings();
        JobHandle Generate(NativeReference<BlobAssetReference<BrushMeshBlob>> brushMeshRef, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob);
    }

    public interface IChiselBranchTypeGenerator<Settings>
        where Settings : struct
    {
        // Temporary workaround
        void FixupOperations(CSGTreeBranch branch);
    }

    public interface IChiselBranchGenerator : IChiselNodeGenerator
    {
        JobHandle Generate(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob);
        
        // Temporary workaround
        void FixupOperations(CSGTreeBranch branch);
    }

    public static class TypeGeneratorExtensions
    {
        public static void Assign<Settings>(this IChiselBrushTypeGenerator<Settings> @this, NativeList<CSGTreeNode> nodes, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
            where Settings : struct
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var brushMesh = brushMeshes[i];
                var brush = (CSGTreeBrush)nodes[i];
                if (!brush.Valid)
                    continue;

                if (!brushMesh.IsCreated)
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                else
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
            }
        }
        
        static void ClearBrushes(CSGTreeBranch branch)
        {
            for (int i = branch.Count - 1; i >= 0; i--)
                branch[i].Destroy();
            branch.Clear();
        }

        static unsafe void BuildBrushes(CSGTreeBranch branch, int desiredBrushCount)
        {
            if (branch.Count < desiredBrushCount)
            {
                var newBrushCount = desiredBrushCount - branch.Count;
                var newRange = new NativeArray<CSGTreeNode>(newBrushCount, Allocator.Temp);
                try
                {
                    var userID = branch.UserID;
                    for (int i = 0; i < newBrushCount; i++)
                        newRange[i] = CSGTreeBrush.Create(userID: userID, operation: CSGOperationType.Additive);
                    branch.AddRange((CSGTreeNode*)newRange.GetUnsafePtr(), newBrushCount);
                }
                finally { newRange.Dispose(); }
            } else
            {
                for (int i = branch.Count - 1; i >= desiredBrushCount; i--)
                {
                    var oldBrush = branch[i];
                    branch.RemoveAt(i);
                    oldBrush.Destroy();
                }
            }
        }

        public static void Assign<Settings>(this IChiselBranchTypeGenerator<Settings> @this, NativeList<CSGTreeNode> nodes, NativeList<Range> ranges, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
            where Settings : struct
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var range = ranges[i];
                var branch = (CSGTreeBranch)nodes[i];
                if (range.Length == 0)
                {
                    ClearBrushes(branch);
                    continue;
                }

                if (branch.Count != range.Length)
                    BuildBrushes(branch, range.Length);

                for (int b = 0, m = range.start; b < range.Length; i++, m++)
                {
                    var brush = (CSGTreeBrush)branch[b];
                    brush.LocalTransformation = float4x4.identity;
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshes[m]) };
                }

                @this.FixupOperations(branch);
            }
        }
    }
}
