using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct MergeTouchingBrushVerticesJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushes;

        // Read/Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>        treeSpaceVerticesArray;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices hashedVertices;

        public void Execute(int b)
        {
            var brushIndexOrder = treeBrushIndexOrders[b];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;

            var treeSpaceVerticesBlob = treeSpaceVerticesArray[brushIndexOrder.nodeOrder];
            if (treeSpaceVerticesBlob == BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null)
                return;
            var brushIntersectionsBlob = brushesTouchedByBrushes[brushNodeOrder];
            if (brushIntersectionsBlob == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;
            ref var vertices  = ref treeSpaceVerticesBlob.Value.treeSpaceVertices;

            if (!hashedVertices.IsCreated)
            {
                hashedVertices = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
            } else
            {
                if (hashedVertices.Capacity < vertices.Length)
                {
                    hashedVertices.Dispose();
                    hashedVertices = new HashedVertices(vertices.Length, Allocator.Temp);
                } else
                    hashedVertices.Clear();
            }
            hashedVertices.AddUniqueVertices(ref vertices);

            // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
            ref var brushIntersections = ref brushIntersectionsBlob.Value.brushIntersections;
            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                if (intersectingNodeOrder < brushNodeOrder)
                    continue;

                // In order, goes through the previous brushes in the tree, 
                // and snaps any vertex that is almost the same in the next brush, with that vertex
                ref var intersectingVertices = ref treeSpaceVerticesArray[intersectingNodeOrder].Value.treeSpaceVertices;
                hashedVertices.ReplaceIfExists(ref intersectingVertices);
            }


            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = hashedVertices.GetUniqueVertex(vertices[i]);
            }

            //treeSpaceVerticesLookup.TryAdd(brushNodeIndex, treeSpaceVerticesBlob);
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    struct MergeTouchingBrushVertices2Job : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushes;

        // Read/Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>        treeSpaceVerticesArray;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices hashedVertices;

        public void Execute(int b)
        {
            var brushIndexOrder = treeBrushIndexOrders[b];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;

            var treeSpaceVerticesBlob = treeSpaceVerticesArray[brushIndexOrder.nodeOrder];
            if (treeSpaceVerticesBlob == BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null)
                return;
            var brushIntersectionsBlob = brushesTouchedByBrushes[brushNodeOrder];
            if (brushIntersectionsBlob == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;
            ref var vertices  = ref treeSpaceVerticesBlob.Value.treeSpaceVertices;

            if (!hashedVertices.IsCreated)
            {
                hashedVertices = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
            } else
            {
                if (hashedVertices.Capacity < vertices.Length)
                {
                    hashedVertices.Dispose();
                    hashedVertices = new HashedVertices(vertices.Length, Allocator.Temp);
                } else
                    hashedVertices.Clear();
            }
            hashedVertices.AddUniqueVertices(ref vertices);

            // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
            ref var brushIntersections = ref brushIntersectionsBlob.Value.brushIntersections;
            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                if (intersectingNodeOrder < brushNodeOrder)
                    continue;

                // In order, goes through the previous brushes in the tree, 
                // and snaps any vertex that is almost the same in the next brush, with that vertex
                ref var intersectingVertices = ref treeSpaceVerticesArray[intersectingNodeOrder].Value.treeSpaceVertices;
                hashedVertices.ReplaceIfExists(ref intersectingVertices);
            }


            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = hashedVertices.GetUniqueVertex(vertices[i]);
            }

            //treeSpaceVerticesLookup.TryAdd(brushNodeIndex, treeSpaceVerticesBlob);
        }
    }
}
