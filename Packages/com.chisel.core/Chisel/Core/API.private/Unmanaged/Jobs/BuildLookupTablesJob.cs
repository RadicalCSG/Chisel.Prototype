﻿using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile]
    unsafe struct BuildLookupTablesJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> brushes;
        [NoAlias, ReadOnly] public int brushCount;

        // Read/Write
        [NoAlias] public NativeList<int>           nodeIDValueToNodeOrderArray;

        // Write
        [NoAlias, WriteOnly] public NativeReference<int>        nodeIDValueToNodeOrderOffsetRef;
        [NoAlias, WriteOnly] public NativeList<IndexOrder>      allTreeBrushIndexOrders;

        public void Execute()
        {
            var nodeIDValueMin = int.MaxValue;
            var nodeIDValueMax = 0;
            if (brushCount > 0)
            {
                Debug.Assert(brushCount == brushes.Length);
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    var brushCompactNodeID      = brushes[nodeOrder];
                    var brushCompactNodeIDValue = brushCompactNodeID.value;
                    nodeIDValueMin = math.min(nodeIDValueMin, brushCompactNodeIDValue);
                    nodeIDValueMax = math.max(nodeIDValueMax, brushCompactNodeIDValue);
                }
            } else
                nodeIDValueMin = 0;

            var nodeIDValueToNodeOrderOffset = nodeIDValueMin;
            nodeIDValueToNodeOrderOffsetRef.Value = nodeIDValueToNodeOrderOffset;
            var desiredLength = (nodeIDValueMax + 1) - nodeIDValueMin;
                    
            nodeIDValueToNodeOrderArray.Clear();
            nodeIDValueToNodeOrderArray.Resize(desiredLength, NativeArrayOptions.ClearMemory);
            for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
            {
                var brushCompactNodeID      = brushes[nodeOrder];
                var brushCompactNodeIDValue = brushCompactNodeID.value;
                nodeIDValueToNodeOrderArray[brushCompactNodeIDValue - nodeIDValueToNodeOrderOffset] = nodeOrder;
                    
                // We need the index into the tree to ensure deterministic ordering
                var brushIndexOrder = new IndexOrder { compactNodeID = brushCompactNodeID, nodeOrder = nodeOrder };
                allTreeBrushIndexOrders[nodeOrder] = brushIndexOrder;
            }        
        }
    }

    [BurstCompile]
    unsafe struct UpdateBrushIDValuesJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> brushes;
        [NoAlias, ReadOnly] public int brushCount;

        // Read/Write
        [NoAlias] public NativeList<CompactNodeID> brushIDValues;

        public void Execute()
        {
            if (brushIDValues.Length != brushCount)
            {
                brushIDValues.Resize(brushCount, NativeArrayOptions.ClearMemory);
            }

            for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
            {
                var brushCompactNodeID = brushes[nodeOrder];
                brushIDValues[nodeOrder] = brushCompactNodeID;
            }        
        }
    }
}
