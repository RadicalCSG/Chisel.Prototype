using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

namespace Chisel.Core
{
    public static class NativeCollection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleConstruct<U>(bool runInParallel, out NativeStream dataStream, NativeList<U> forEachCountFromList, Allocator allocator, JobHandle dependsOn = default)
            where U : unmanaged
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
            where U : unmanaged
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleConstruct(runInParallel, out dataStream, forEachCountFromList, allocator, dependencies);
            writeDependencies.AddDependency(currentJobHandle);
            readDependencies.AddDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleEnsureCapacity<T, U>(bool runInParallel, ref NativeList<T> list, NativeList<U> forEachCountFromList, Allocator allocator, JobHandle dependsOn = default)
            where T : unmanaged
            where U : unmanaged
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
        public static JobHandle ScheduleSetCapacity<T, U>(ref NativeList<T> list, NativeList<U> forEachCountFromList, Allocator allocator, JobHandle dependsOn = default)
            where T : unmanaged
            where U : unmanaged
        {
            if (!list.IsCreated)
                list = new NativeList<T>(0, allocator);
            var jobData = new EnsureCapacityListForEachCountFromListJob<T, U> { list = list, forEachCountFromList = forEachCountFromList };
            return jobData.Schedule(dependsOn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleEnsureCapacity<T, U>(bool runInParallel, ref NativeList<T> list, NativeList<U> forEachCountFromList, ReadJobHandles readDependencies, WriteJobHandles writeDependencies, Allocator allocator)
            where T : unmanaged
            where U : unmanaged
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleEnsureCapacity(runInParallel, ref list, forEachCountFromList, allocator, dependencies);
            writeDependencies.AddDependency(currentJobHandle);
            readDependencies.AddDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleSetCapacity<T>(ref NativeList<T> list, NativeReference<int> capacity, Allocator allocator, JobHandle dependsOn = default)
           where T : unmanaged
        {
            if (!list.IsCreated)
                list = new NativeList<T>(0, allocator);
            var jobData = new EnsureCapacityListReferenceJob<T> { list = list, reference = capacity };
            return jobData.Schedule(dependsOn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleEnsureCapacity<T>(bool runInParallel, ref NativeList<T> list, NativeReference<int> capacity, Allocator allocator, JobHandle dependsOn = default)
            where T : unmanaged
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
            where T : unmanaged
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = ScheduleEnsureCapacity(runInParallel, ref list, capacity, allocator, dependencies);
            writeDependencies.AddDependency(currentJobHandle);
            readDependencies.AddDependency(currentJobHandle);
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


        public static JobHandle SafeDispose<T>(bool runInParallel, ref NativeArray<T> array, JobHandle dependencies)
            where T : unmanaged
        {
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            JobHandle currentHandle = default;
            if (runInParallel)
            {
                currentHandle = array.Dispose(dependencies);
            } else
                array.Dispose();
            array = default;
            return currentHandle;
        }

        public static JobHandle SafeDispose<T>(ref this NativeArray<T> array, JobHandle dependencies) where T : unmanaged { return SafeDispose(true, ref array, dependencies); }
        public static void SafeDispose<T>(ref this NativeArray<T> array) where T : unmanaged { SafeDispose(false, ref array, default); }


        public static JobHandle SafeDispose<T>(bool runInParallel, ref NativeList<T> list, JobHandle dependencies)
            where T : unmanaged
        {
            JobHandle currentHandle = default;
            if (runInParallel)
                currentHandle = list.Dispose(dependencies);
            else
                list.Dispose();
            list = default;
            return currentHandle;
        }

        public static JobHandle SafeDispose<T>(ref this NativeList<T> list, JobHandle dependencies) where T : unmanaged { return SafeDispose(true, ref list, dependencies); }
        public static void SafeDispose<T>(ref this NativeList<T> list) where T : unmanaged { SafeDispose(false, ref list, default); }

        public static JobHandle DisposeDeep<T>(ref this NativeArray<T> array, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep(true, ref array, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, ref NativeArray<T> array, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeArrayChildrenJob<T> { array = array };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
            {
                var result = array.Dispose(currentHandle);
                array = default;
                return result;
            }

            currentHandle.Complete();
            array.Dispose();
            array = default;
            return default;
        }

        public static JobHandle DisposeDeep<T>(bool runInParallel, ref NativeList<T> list, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeListChildrenJob<T> { list = list };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            return SafeDispose(runInParallel, ref list, currentHandle);
        }

        public static JobHandle DisposeDeep<T>(ref this NativeList<ChiselBlobAssetReference<T>> list, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep(true, ref list, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, ref NativeList<ChiselBlobAssetReference<T>> list, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeListChildrenBlobAssetReferenceJob<T> { list = list };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            return SafeDispose(runInParallel, ref list, currentHandle);
        }

        public static void DisposeDeep<T>(this NativeList<T> list) where T : unmanaged, IDisposable { DisposeDeep(false, ref list, default); }

        public static JobHandle DisposeDeep<T>(ref this NativeList<T> list, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep(true, ref list, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, ref NativeReference<T> reference, JobHandle dependencies)
            where T : unmanaged, IDisposable
        {
            var disposeListJob = new DisposeReferenceChildJob<T> { reference = reference };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
            {
                var result = reference.Dispose(currentHandle);
                reference = default;
                return result;
            }

            currentHandle.Complete();// TODO: get rid of this
            reference.Dispose();
            reference = default;
            return default;
        }

        public static JobHandle DisposeDeep<T>(ref this NativeReference<T> reference, JobHandle dependencies) where T : unmanaged, IDisposable { return DisposeDeep<T>(true, ref reference, dependencies); }

        public static JobHandle DisposeDeep<T>(bool runInParallel, ref NativeReference<ChiselBlobAssetReference<T>> reference, JobHandle dependencies) 
            where T : unmanaged
        {
            var disposeListJob = new DisposeReferenceChildBlobAssetReferenceJob<T> { reference = reference };
            JobExtensions.CheckDependencies(runInParallel, dependencies);
            var currentHandle = disposeListJob.Schedule(runInParallel, dependencies);
            if (runInParallel)
            {
                var result = reference.Dispose(currentHandle);
                reference = default;
                return result;
            }

            currentHandle.Complete();
            reference.Dispose();
            reference = default;
            return default;
        }

        public static JobHandle DisposeDeep<T>(ref NativeReference<ChiselBlobAssetReference<T>> reference, JobHandle dependencies) where T : unmanaged { return DisposeDeep(true, ref reference, dependencies); }

    }

    [BurstCompile(CompileSynchronously = true)]
    public struct EnsureCapacityListForEachCountFromListJob<T, U> : IJob
        where T : unmanaged
        where U : unmanaged
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
    public struct EnsureCapacityListReferenceJob<T> : IJob
        where T : unmanaged
    {
        // Read
        [NoAlias, ReadOnly] public NativeReference<int> reference;

        // Read/Write
        [NoAlias] public NativeList<T> list;

        public void Execute()
        {
            if (list.Capacity < reference.Value)
                list.Capacity = math.max(4, reference.Value);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct SafeDisposeListJob<T> : IJob
        where T : unmanaged
    {
        // Read / Write
        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public UnsafeList<T>* list;

        public void Execute()
        {
            if (list == null ||
                !list->IsCreated)
                return;

            try
            {
                UnsafeList<T>.Destroy(list);
            }
            catch
            {
                UnityEngine.Debug.LogError(typeof(T));
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct DisposeListChildrenJob<T> : IJob
        where T : unmanaged, IDisposable
    {
        // Read / Write
        [NoAlias] public NativeList<T> list;

        public void Execute()
        {
            if (!list.IsCreated)
                return;

            for (int i = 0; i < list.Length; i++)
            {
                list[i].Dispose();
                list[i] = default;
            }

            list.Clear();
        }
    }

    [BurstCompile]
    public struct DisposeArrayChildrenJob<T> : IJob
        where T : unmanaged, IDisposable
    {
        // Read / Write
        [NoAlias] public NativeArray<T> array;

        public void Execute()
        {
            if (!array.IsCreated)
                return;

            for (int i = 0; i < array.Length; i++)
            {
                array[i].Dispose();
                array[i] = default;
            }
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
                list[i] = default;
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
            reference.Value = default;
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
            reference.Value = default;
        }
    }
}
