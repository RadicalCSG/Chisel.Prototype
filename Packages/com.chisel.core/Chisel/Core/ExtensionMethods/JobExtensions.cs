using System.Runtime.CompilerServices;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    static class JobExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<T, U>(this T jobData, NativeList<U> list)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            for (int index = 0; index < list.Length; index++)
                jobData.Execute(index); 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, U>(this T jobData, bool runInParallel, NativeList<U> list, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return jobData.Schedule(list, innerloopBatchCount, dependsOn);
            
            dependsOn.Complete();
            jobData.Run(list);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelFor
        {
            CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return jobData.Schedule(arrayLength, innerloopBatchCount, dependsOn);
            
            dependsOn.Complete();
            jobData.Run(arrayLength);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, JobHandle dependsOn = default)
            where T : struct, IJob
        {
            CheckDependencies(runInParallel, dependsOn);
            if (runInParallel)
                return jobData.Schedule(dependsOn);
            
            dependsOn.Complete();
            jobData.Run();
            return default;
        }

        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<T> AsJobArray<T>(this NativeList<T> list, bool runInParallel)
            where T : unmanaged
        {
            if (runInParallel)
                return list.AsDeferredJobArray(); // <-- broken by unity
            return list.AsArray();
        }*/


        #region CheckDependencies
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckDependencies(bool runInParallel, JobHandle dependencies) { if (!runInParallel) dependencies.Complete(); }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJob
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, dependencies);
            writeDependencies.AddDependency(currentJobHandle);
            readDependencies.AddDependency(currentJobHandle);

            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, int arrayLength, int innerloopBatchCount, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJobParallelFor
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, arrayLength, innerloopBatchCount, dependencies);
            writeDependencies.AddDependency(currentJobHandle);
            readDependencies.AddDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, U>(this T jobData, bool runInParallel, NativeList<U> list, int innerloopBatchCount, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, list, innerloopBatchCount, dependencies);
            writeDependencies.AddDependency(currentJobHandle);
            readDependencies.AddDependency(currentJobHandle);
            return currentJobHandle;
        }
    }

    public struct DualJobHandle
    {
        public JobHandle writeBarrier;
        public JobHandle readWriteBarrier;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            readWriteBarrier.Complete();
        }
    }

    // Note: you're only supposed to use this struct in the "Schedule" method
    public unsafe struct ReadJobHandles
    {
        public JobHandle Handles;

        const int maxHandles = 32;
        static DualJobHandle*[] handleArray = new DualJobHandle*[maxHandles];
        static int handleArrayLength = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        static void Add(ref DualJobHandle jobHandle) 
        {
            UnityEngine.Debug.Assert(handleArrayLength < maxHandles);
            handleArray[handleArrayLength] = (DualJobHandle*)UnsafeUtility.AddressOf(ref jobHandle); 
            handleArrayLength++; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0)
        {
            Handles = jobHandle0.writeBarrier;
            handleArrayLength = 0;
            Add(ref jobHandle0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier, jobHandle6.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier, jobHandle6.writeBarrier, jobHandle7.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier, jobHandle6.writeBarrier, jobHandle7.writeBarrier, jobHandle8.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8, ref DualJobHandle jobHandle9) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier, jobHandle6.writeBarrier, jobHandle7.writeBarrier, jobHandle8.writeBarrier, jobHandle9.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8, ref DualJobHandle jobHandle9, ref DualJobHandle jobHandle10) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier, jobHandle6.writeBarrier, jobHandle7.writeBarrier, jobHandle8.writeBarrier, jobHandle9.writeBarrier, jobHandle10.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9); Add(ref jobHandle10);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8, ref DualJobHandle jobHandle9, ref DualJobHandle jobHandle10, ref DualJobHandle jobHandle11) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.writeBarrier, jobHandle1.writeBarrier, jobHandle2.writeBarrier, jobHandle3.writeBarrier, jobHandle4.writeBarrier, jobHandle5.writeBarrier, jobHandle6.writeBarrier, jobHandle7.writeBarrier, jobHandle8.writeBarrier, jobHandle9.writeBarrier, jobHandle10.writeBarrier, jobHandle11.writeBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9); Add(ref jobHandle10); Add(ref jobHandle11);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDependency(JobHandle newDependency)
        {
            if (handleArrayLength == 0)
                return;
            for (int i = 0; i < handleArrayLength; i++)
                handleArray[i]->readWriteBarrier.AddDependency(newDependency);
        }
    }

    // Note: you're only supposed to use this struct in the "Schedule" method
    public unsafe struct WriteJobHandles
    {
        public JobHandle Handles;

        const int maxHandles = 32;
        static DualJobHandle*[] handleArray = new DualJobHandle*[maxHandles];
        static int handleArrayLength = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        static void Add(ref DualJobHandle jobHandle) 
        {
            UnityEngine.Debug.Assert(handleArrayLength < maxHandles);
            handleArray[handleArrayLength] = (DualJobHandle*)UnsafeUtility.AddressOf(ref jobHandle); 
            handleArrayLength++; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0)
        {
            Handles = jobHandle0.readWriteBarrier;
            handleArrayLength = 0;
            Add(ref jobHandle0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier, jobHandle6.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier, jobHandle6.readWriteBarrier, jobHandle7.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier, jobHandle6.readWriteBarrier, jobHandle7.readWriteBarrier, jobHandle8.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8, ref DualJobHandle jobHandle9) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier, jobHandle6.readWriteBarrier, jobHandle7.readWriteBarrier, jobHandle8.readWriteBarrier, jobHandle9.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8, ref DualJobHandle jobHandle9, ref DualJobHandle jobHandle10) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier, jobHandle6.readWriteBarrier, jobHandle7.readWriteBarrier, jobHandle8.readWriteBarrier, jobHandle9.readWriteBarrier, jobHandle10.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9); Add(ref jobHandle10);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteJobHandles(ref DualJobHandle jobHandle0, ref DualJobHandle jobHandle1, ref DualJobHandle jobHandle2, ref DualJobHandle jobHandle3, ref DualJobHandle jobHandle4, ref DualJobHandle jobHandle5, ref DualJobHandle jobHandle6, ref DualJobHandle jobHandle7, ref DualJobHandle jobHandle8, ref DualJobHandle jobHandle9, ref DualJobHandle jobHandle10, ref DualJobHandle jobHandle11) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0.readWriteBarrier, jobHandle1.readWriteBarrier, jobHandle2.readWriteBarrier, jobHandle3.readWriteBarrier, jobHandle4.readWriteBarrier, jobHandle5.readWriteBarrier, jobHandle6.readWriteBarrier, jobHandle7.readWriteBarrier, jobHandle8.readWriteBarrier, jobHandle9.readWriteBarrier, jobHandle10.readWriteBarrier, jobHandle11.readWriteBarrier);
            handleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9); Add(ref jobHandle10); Add(ref jobHandle11);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDependency(JobHandle newDependency)
        {
            if (handleArrayLength == 0)
                return;

            for (int i = 0; i < handleArrayLength; i++)
            {
                handleArray[i]->writeBarrier.AddDependency(newDependency);
                handleArray[i]->readWriteBarrier.AddDependency(newDependency);
            }
        }
    }
}
