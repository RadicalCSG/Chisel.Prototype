using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    static class JobExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<T, U>(this T jobData, NativeList<U> list)
            where T : struct, IJobParallelForDefer
            where U : struct
        {
            for (int index = 0; index < list.Length; index++)
                jobData.Execute(index); 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, U>(this T jobData, bool runInParallel, NativeList<U> list, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelForDefer
            where U : struct
        {
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
            if (runInParallel)
                return jobData.Schedule(dependsOn);
            
            dependsOn.Complete();
            jobData.Run();
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<T> AsJobArray<T>(this NativeList<T> list, bool runInParallel)
            where T : struct
        {
            if (runInParallel)
                return list.AsDeferredJobArray();
            return list.AsArray();
        }


        #region CheckDependencies
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckDependencies(bool runInParallel, JobHandle dependencies) { if (!runInParallel) dependencies.Complete(); }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJob
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            var currentJobHandle = jobData.Schedule(runInParallel, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);

            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, int arrayLength, int innerloopBatchCount, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJobParallelFor
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, arrayLength, innerloopBatchCount, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);
            return currentJobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, U>(this T jobData, bool runInParallel, NativeList<U> list, int innerloopBatchCount, ReadJobHandles readDependencies, WriteJobHandles writeDependencies)
            where T : struct, IJobParallelForDefer
            where U : struct
        {
            var dependencies = JobHandleExtensions.CombineDependencies(readDependencies.Handles, writeDependencies.Handles);
            CheckDependencies(runInParallel, dependencies);
            var currentJobHandle = jobData.Schedule(runInParallel, list, innerloopBatchCount, dependencies);
            writeDependencies.AddWriteDependency(currentJobHandle);
            return currentJobHandle;
        }
    }

    // Note: you're only supposed to use this struct in the "Schedule" method
    public struct ReadJobHandles
    {
        public JobHandle Handles;

        public ReadJobHandles(JobHandle jobHandle0) { Handles = jobHandle0; }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5, JobHandle jobHandle6) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5, JobHandle jobHandle6, JobHandle jobHandle7) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5, JobHandle jobHandle6, JobHandle jobHandle7, JobHandle jobHandle8) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5, JobHandle jobHandle6, JobHandle jobHandle7, JobHandle jobHandle8, JobHandle jobHandle9) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8, jobHandle9); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5, JobHandle jobHandle6, JobHandle jobHandle7, JobHandle jobHandle8, JobHandle jobHandle9, JobHandle jobHandle10) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8, jobHandle9, jobHandle10); }
        public ReadJobHandles(JobHandle jobHandle0, JobHandle jobHandle1, JobHandle jobHandle2, JobHandle jobHandle3, JobHandle jobHandle4, JobHandle jobHandle5, JobHandle jobHandle6, JobHandle jobHandle7, JobHandle jobHandle8, JobHandle jobHandle9, JobHandle jobHandle10, JobHandle jobHandle11) { Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8, jobHandle9, jobHandle10, jobHandle11); }
    }

    // Note: you're only supposed to use this struct in the "Schedule" method
    public unsafe struct WriteJobHandles
    {
        public JobHandle Handles;

        static JobHandle*[] writeHandleArray = new JobHandle*[32];
        static int writeHandleArrayLength = 0;

        static void Add(ref JobHandle jobHandle) { writeHandleArray[writeHandleArrayLength] = (JobHandle*)UnsafeUtility.AddressOf(ref jobHandle); writeHandleArrayLength++; }

        public WriteJobHandles(ref JobHandle jobHandle0)
        {
            Handles = jobHandle0;
            writeHandleArrayLength = 0;
            Add(ref jobHandle0);
        }

        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5, ref JobHandle jobHandle6) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5, ref JobHandle jobHandle6, ref JobHandle jobHandle7) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5, ref JobHandle jobHandle6, ref JobHandle jobHandle7, ref JobHandle jobHandle8) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5, ref JobHandle jobHandle6, ref JobHandle jobHandle7, ref JobHandle jobHandle8, ref JobHandle jobHandle9) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8, jobHandle9);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5, ref JobHandle jobHandle6, ref JobHandle jobHandle7, ref JobHandle jobHandle8, ref JobHandle jobHandle9, ref JobHandle jobHandle10) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8, jobHandle9, jobHandle10);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9); Add(ref jobHandle10);
        }
            
        public WriteJobHandles(ref JobHandle jobHandle0, ref JobHandle jobHandle1, ref JobHandle jobHandle2, ref JobHandle jobHandle3, ref JobHandle jobHandle4, ref JobHandle jobHandle5, ref JobHandle jobHandle6, ref JobHandle jobHandle7, ref JobHandle jobHandle8, ref JobHandle jobHandle9, ref JobHandle jobHandle10, ref JobHandle jobHandle11) 
        { 
            Handles = JobHandleExtensions.CombineDependencies(jobHandle0, jobHandle1, jobHandle2, jobHandle3, jobHandle4, jobHandle5, jobHandle6, jobHandle7, jobHandle8, jobHandle9, jobHandle10, jobHandle11);
            writeHandleArrayLength = 0;
            Add(ref jobHandle0); Add(ref jobHandle1); Add(ref jobHandle2); Add(ref jobHandle3); Add(ref jobHandle4); Add(ref jobHandle5); Add(ref jobHandle6); Add(ref jobHandle7); Add(ref jobHandle8); Add(ref jobHandle9); Add(ref jobHandle10); Add(ref jobHandle11);
        }

        public void AddWriteDependency(JobHandle newDependency)
        {
            if (writeHandleArray == null)
                return;
            for (int i = 0; i < writeHandleArrayLength; i++)
                writeHandleArray[i]->AddDependency(newDependency);
        }
    }
}
