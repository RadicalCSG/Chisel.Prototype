using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Core
{
    static class JobExtensions
    {
        public static void Run<T, U>(this T jobData, NativeList<U> list)
            where T : struct, IJobParallelForDefer
            where U : struct
        {
            for (int index = 0; index < list.Length; index++)
                jobData.Execute(index); 
        }

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

        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelFor
        {
            if (runInParallel)
                return jobData.Schedule(arrayLength, innerloopBatchCount, dependsOn);
            
            dependsOn.Complete();
            jobData.Run(arrayLength);
            return default;
        }

        public static JobHandle Schedule<T>(this T jobData, bool runInParallel, JobHandle dependsOn = default)
            where T : struct, IJob
        {
            if (runInParallel)
                return jobData.Schedule(dependsOn);
            
            dependsOn.Complete();
            jobData.Run();
            return default;
        }

        public static NativeArray<T> AsJobArray<T>(this NativeList<T> list, bool runInParallel)
            where T : struct
        {
            if (runInParallel)
                return list.AsDeferredJobArray();
            return list.AsArray();
        }
    }


}
