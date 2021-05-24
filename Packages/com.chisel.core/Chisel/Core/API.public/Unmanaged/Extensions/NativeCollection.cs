using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Chisel.Core
{
    public static class NativeCollection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleConstruct<U>(bool runInParallel, out NativeStream dataStream, NativeList<U> forEachCountFromList, Allocator allocator, JobHandle dependsOn = default)
            where U : struct
        {
            if (runInParallel)
                return NativeStream.ScheduleConstruct(out dataStream, forEachCountFromList, dependsOn, allocator);

            dependsOn.Complete();
            dataStream = new NativeStream(forEachCountFromList.Length, allocator);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleConstruct<U>(bool runInParallel, out NativeStream dataStream, NativeList<U> forEachCountFromList, ReadJobHandles readDependencies, WriteJobHandles writeDependencies, Allocator allocator)
            where U:struct
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleConstruct(runInParallel, out dataStream, forEachCountFromList, allocator, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);
            return currentJobHandle;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static JobHandle ScheduleSetCapacity<T>(ref NativeList<T> list, NativeReference<int> capacity, Allocator allocator, JobHandle dependsOn = default)
           where T : struct
        {
            if (!list.IsCreated)
                list = new NativeList<T>(0, allocator);
            var jobData = new ConstructListJob<T> { List = list.GetUnsafeList(), size = capacity };
            return jobData.Schedule(dependsOn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleSetCapacity<T>(bool runInParallel, ref NativeList<T> list, NativeReference<int> capacity, Allocator allocator, JobHandle dependsOn = default)
            where T : struct
        {
            if (runInParallel)
                return ScheduleSetCapacity(ref list, capacity, allocator, dependsOn);

            dependsOn.Complete();
            if (list.Capacity < capacity.Value)
                list.Capacity = capacity.Value;
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleSetCapacity<T>(bool runInParallel, ref NativeList<T> list, NativeReference<int> capacity, ReadJobHandles readDependencies, WriteJobHandles writeDependencies, Allocator allocator)
            where T : struct
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleSetCapacity(runInParallel, ref list, capacity, allocator, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleDispose(bool runInParallel, ref NativeStream dataStream, JobHandle dependsOn = default)
        {
            if (runInParallel)
            {
                var result = dataStream.Dispose(dependsOn);
                dataStream = default;
                return result;
            }

            dependsOn.Complete();
            dataStream.Dispose();
            dataStream = default;
            return default;
        }
    }

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
}
