using System;
using System.Collections.Generic;
using Unity.Jobs;
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
            JobExtensions.CheckDependencies(runInParallel, dependsOn);
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
        public static JobHandle ScheduleEnsureCapacity<T, U>(bool runInParallel, ref NativeList<T> list, NativeList<U> forEachCountFromList, Allocator allocator, JobHandle dependsOn = default)
            where T : struct
            where U : struct
        {
            JobExtensions.CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return ScheduleSetCapacity(ref list, forEachCountFromList, allocator, dependsOn);

            dependsOn.Complete();
            if (list.Capacity < forEachCountFromList.Length)
                list.Capacity = forEachCountFromList.Length;
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static JobHandle ScheduleSetCapacity<T, U>(ref NativeList<T> list, NativeList<U> forEachCountFromList, Allocator allocator, JobHandle dependsOn = default)
            where T : struct
            where U : struct
        {
            if (!list.IsCreated)
                list = new NativeList<T>(0, allocator);
            var jobData = new EnsureCapacityListForEachCountFromListJob<T, U> { list = list, forEachCountFromList = forEachCountFromList };
            return jobData.Schedule(dependsOn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleEnsureCapacity<T, U>(bool runInParallel, ref NativeList<T> list, NativeList<U> forEachCountFromList, ReadJobHandles readDependencies, WriteJobHandles writeDependencies, Allocator allocator)
            where T : struct
            where U : struct
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleEnsureCapacity(runInParallel, ref list, forEachCountFromList, allocator, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static JobHandle ScheduleSetCapacity<T>(ref NativeList<T> list, NativeReference<int> capacity, Allocator allocator, JobHandle dependsOn = default)
           where T : struct
        {
            if (!list.IsCreated)
                list = new NativeList<T>(0, allocator);
            var jobData = new EnsureCapacityListReferenceJob<T> { list = list, reference = capacity };
            return jobData.Schedule(dependsOn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleEnsureCapacity<T>(bool runInParallel, ref NativeList<T> list, NativeReference<int> capacity, Allocator allocator, JobHandle dependsOn = default)
            where T : struct
        {
            JobExtensions.CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return ScheduleSetCapacity(ref list, capacity, allocator, dependsOn);

            dependsOn.Complete();
            if (list.Capacity < capacity.Value)
                list.Capacity = capacity.Value;
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleEnsureCapacity<T>(bool runInParallel, ref NativeList<T> list, NativeReference<int> capacity, ReadJobHandles readDependencies, WriteJobHandles writeDependencies, Allocator allocator)
            where T : struct
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleEnsureCapacity(runInParallel, ref list, capacity, allocator, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleDispose(bool runInParallel, ref NativeStream dataStream, JobHandle dependsOn = default)
        {
            JobExtensions.CheckDependencies(runInParallel, dependsOn);
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

        public static JobHandle DisposeDeep<T>(bool runInParallel, NativeList<T> list, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeListChildrenJob<T> { list = list };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
                return list.Dispose(currentHandle);

            currentHandle.Complete();
            list.Dispose();
            return default;
        }

        public static JobHandle DisposeDeep<T>(this NativeList<ChiselBlobAssetReference<T>> list, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep(true, list, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, NativeList<ChiselBlobAssetReference<T>> list, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeListChildrenBlobAssetReferenceJob<T> { list = list };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
                return list.Dispose(currentHandle);

            currentHandle.Complete();
            list.Dispose();
            return default;
        }

        public static JobHandle DisposeDeep<T>(this NativeList<T> list, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep(true, list, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, NativeReference<T> reference, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeReferenceChildJob<T> { reference = reference };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
                return reference.Dispose(currentHandle);

            currentHandle.Complete();
            reference.Dispose();
            return default;
        }

        public static JobHandle DisposeDeep<T>(this NativeReference<T> reference, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep<T>(true, reference, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, NativeReference<ChiselBlobAssetReference<T>> reference, JobHandle dependencies) 
            where T : unmanaged
        {
            var disposeListJob = new DisposeReferenceChildBlobAssetReferenceJob<T> { reference = reference };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
                return reference.Dispose(currentHandle);

            currentHandle.Complete();
            reference.Dispose();
            return default;
        }

        public static JobHandle DisposeDeep<T>(NativeReference<ChiselBlobAssetReference<T>> reference, JobHandle dependencies) where T : unmanaged { return DisposeDeep(true, reference, dependencies); }

    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct EnsureCapacityListForEachCountFromListJob<T, U> : IJob
        where T : struct
        where U : struct
    {
        // Read
        [NoAlias,ReadOnly] public NativeList<U> forEachCountFromList;

        // Read/Write
        [NoAlias] public NativeList<T> list;

        public void Execute()
        {
            if (list.Capacity < forEachCountFromList.Length)
                list.Capacity = forEachCountFromList.Length;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct EnsureCapacityListReferenceJob<T> : IJob
        where T : struct
    {
        // Read
        [NoAlias, ReadOnly] public NativeReference<int> reference;

        // Read/Write
        [NoAlias] public NativeList<T> list;

        public void Execute()
        {
            if (list.Capacity < reference.Value)
                list.Capacity = reference.Value;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct DisposeListChildrenJob<T> : IJob
        where T : unmanaged, IDisposable
    {
        // Read / Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<T> list;

        public void Execute()
        {
            if (!list.IsCreated)
                return;
            for (int i = 0; i < list.Length; i++)
                list[i].Dispose();
            list.Clear();
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    public struct DisposeListChildrenBlobAssetReferenceJob<T> : IJob
        where T : unmanaged, IDisposable
    {
        // Read / Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<T>> list;

        public void Execute()
        {
            if (!list.IsCreated)
                return;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].IsCreated)
                    list[i].Dispose();
            }
            list.Clear();
        }
    }    

    [BurstCompile(CompileSynchronously = true)]
    public struct DisposeReferenceChildJob<T> : IJob
        where T : unmanaged, IDisposable
    {
        // Read / Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeReference<T> reference;

        public void Execute()
        {
            if (!reference.IsCreated)
                return;
            reference.Value.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct DisposeReferenceChildBlobAssetReferenceJob<T> : IJob
        where T : unmanaged
    {
        // Read / Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeReference<ChiselBlobAssetReference<T>> reference;

        public void Execute()
        {
            if (!reference.IsCreated)
                return;
            if (!reference.Value.IsCreated)
                return;
            reference.Value.Dispose();
        }
    }
}
