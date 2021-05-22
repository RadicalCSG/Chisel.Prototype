using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    struct ChiselLayerParameterIndex
    {
        public int count;
        public int index;
    }

    // TODO: store this PER TREE
    struct ChiselLayerParameters
    {
        public UnsafeHashMap<int, ChiselLayerParameterIndex> uniqueParameters;
        public int uniqueParameterCount;


        public bool IsCreated
        {
            get
            {
                return uniqueParameters.IsCreated;
            }
        }

        internal void UnregisterParameter(int parameter)
        {
            if (parameter == 0)
                return;
            if (!uniqueParameters.TryGetValue(parameter, out var item))
                return;
            item.count--;
            if (item.count < 0)
                item.count = 0;
            uniqueParameters[parameter] = item;
            
            // TODO: have some way to remove parameters (swap with last index? would need to swap things outside of this class as well somehow)
        }

        internal bool RegisterParameter(int parameter)
        {
            if (parameter == 0)
                return false;
            if (!uniqueParameters.TryGetValue(parameter, out var item))
            {
                var index = uniqueParameterCount;
                uniqueParameterCount++;
                if (uniqueParameters.Capacity < uniqueParameterCount)
                    uniqueParameters.Capacity = Mathf.CeilToInt(uniqueParameterCount * 1.5f);
                uniqueParameters.Add(parameter, 
                    new ChiselLayerParameterIndex 
                    { 
                        count = 1, 
                        index = index 
                    });
                return true;
            } else
            {
                item.count++;
                uniqueParameters[parameter] = item;
                return false;
            }
        }

        internal void Initialize()
        {
            uniqueParameters = new UnsafeHashMap<int, ChiselLayerParameterIndex>(1000, Allocator.Persistent);
            uniqueParameterCount = 0;
        }

        internal void Dispose()
        {
            if (uniqueParameters.IsCreated) { uniqueParameters.Dispose(); uniqueParameters = default; }
            uniqueParameterCount = 0;
        }

        internal void Clear()
        {
            uniqueParameters.Clear();
            uniqueParameterCount = 0;
        }
    }
}
