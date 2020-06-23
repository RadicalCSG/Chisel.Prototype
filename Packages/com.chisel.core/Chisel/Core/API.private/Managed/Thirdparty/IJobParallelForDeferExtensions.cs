using System;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Chisel.Core
{

    // TODO: propertly encapusulate this w/ license etc.

    // https://jacksondunstan.com/articles/4857

    public static class IJobParallelForDeferExtensions
    {
        internal struct ParallelForJobStruct<T> where T : struct, IJobParallelFor
        {
            public static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    var attribute = (JobProducerTypeAttribute)typeof(IJobParallelFor).GetCustomAttribute(typeof(JobProducerTypeAttribute));
                    var jobStruct = attribute.ProducerType.MakeGenericType(typeof(T));
                    var method = jobStruct.GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    var res = method.Invoke(null, new object[0]);
                    jobReflectionData = (IntPtr)res;
                }

                return jobReflectionData;
            }
        }

        unsafe public static JobHandle Schedule<T, U>(this T jobData, NativeList<U> list, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelFor
            where U : struct
        {
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobData), ParallelForJobStruct<T>.Initialize(), dependsOn, ScheduleMode.Batched);
            void* atomicSafetyHandlePtr = null;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list);
            atomicSafetyHandlePtr = UnsafeUtility.AddressOf(ref safety);
#endif
            return JobsUtility.ScheduleParallelForDeferArraySize(ref scheduleParams, innerloopBatchCount, NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref list), atomicSafetyHandlePtr);
        }
    }



    public static class IJobRunExtensions
    {
        public static JobHandle Run<T, U>(this T jobData, NativeList<U> list, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelFor
            where U : struct
        {
            for (int i = 0; i < list.Length; i++)
                jobData.Execute(i);
            return dependsOn;
        }

        public static JobHandle Run<T>(this T jobData, JobHandle dependsOn = default)
            where T : struct, IJob
        {
            jobData.Execute();
            return dependsOn;
        }
    }
}