using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Entities;

namespace Chisel.Core
{
    public interface IChiselGenerator
    {
        int RequiredSurfaceCount { get; }
        void Reset();
        void Validate();
        void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition);
        void OnEdit(IChiselHandles handles);
        void OnMessages(IChiselMessages messages);
        JobHandle Generate(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, ref CSGTreeNode node, int userID, CSGOperationType operation);
    }
    
    public static class IChiselGeneratorExtensions
    {

        internal static void ClearBrushes(this IChiselGenerator generator, CSGTreeBranch branch)
        {
            for (int i = branch.Count - 1; i >= 0; i--)
                branch[i].Destroy();
            branch.Clear();
        }

        internal static unsafe void BuildBrushes(this IChiselGenerator generator, CSGTreeBranch branch, int desiredBrushCount)
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
    }
}
