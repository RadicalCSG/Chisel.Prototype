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
    public unsafe struct ConstructListJob<T> : IJob
        where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public UnsafeList* List;

        [ReadOnly]
        public NativeReference<int> size;

        public void Execute()
        {
            if (List->Capacity < size.Value)
                List->SetCapacity<T>(size.Value);
        }
    }

    public static partial class NativeConstruct
    {
        public unsafe static JobHandle ScheduleSetCapacity<T>(ref NativeList<T> list, NativeReference<int> capacity, JobHandle dependency, Allocator allocator)
           where T : struct
        {
            if (!list.IsCreated)
                list = new NativeList<T>(0, allocator);
            var jobData = new ConstructListJob<T> { List = list.GetUnsafeList(), size = capacity };
            return jobData.Schedule(dependency);
        }
    }
}
