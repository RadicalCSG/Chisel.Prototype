using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Jobs
{
    [JobProducerType(typeof(IJobParallelForBatchExtensions.JobParallelForBatchProducer<>))]
    public interface IJobParallelForBatch
    {
        void Execute(int startIndex, int count);
    }

    public static class IJobParallelForBatchExtensions
    {
        internal struct JobParallelForBatchProducer<T> where T : struct, IJobParallelForBatch
        {
            static IntPtr s_JobReflectionData;

            public static IntPtr Initialize()
            {
                if (s_JobReflectionData == IntPtr.Zero)
                {
#if UNITY_2020_2_OR_NEWER
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(T), typeof(T), (ExecuteJobFunction)Execute);
#else
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(T), typeof(T),
                        JobType.ParallelFor, (ExecuteJobFunction)Execute);
#endif
                }

                return s_JobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref T jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            public unsafe static void Execute(ref T jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(
                        ref ranges,
                        jobIndex, out int begin, out int end))
                        return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobData), begin, end - begin);
#endif

                    jobData.Execute(begin, end - begin);
                }
            }
        }

        public static unsafe JobHandle ScheduleBatch<T>(this T jobData, int arrayLength, int minIndicesPerJobCount,
            JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForBatch
        {
            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobData),
                JobParallelForBatchProducer<T>.Initialize(),
                dependsOn,
#if UNITY_2020_2_OR_NEWER
                ScheduleMode.Parallel
#else
                ScheduleMode.Batched
#endif
            );

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, arrayLength, minIndicesPerJobCount);
        }

        public static unsafe void RunBatch<T>(this T jobData, int arrayLength) where T : struct, IJobParallelForBatch
        {
            var scheduleParams =
                new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobData),
                    JobParallelForBatchProducer<T>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, arrayLength, arrayLength);
        }
    }
}
