using System;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Jobs
{
    [JobProducerType(typeof(JobParallelIndexListExtensions.JobParallelForFilterProducer<>))]
    public interface IJobParallelForFilter
    {
        bool Execute(int index);
    }

    public static class JobParallelIndexListExtensions
    {
        internal struct JobParallelForFilterProducer<T> where T : struct, IJobParallelForFilter
        {
            public struct JobWrapper
            {
                [NativeDisableParallelForRestriction]
                public NativeList<int> outputIndices;
                public int appendCount;
                public T JobData;
            }

            static IntPtr s_JobReflectionData;

            public static IntPtr Initialize()
            {
                if (s_JobReflectionData == IntPtr.Zero)
                    // @TODO: Use parallel for job... (Need to expose combine jobs)

                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobWrapper), typeof(T),
#if !UNITY_2020_2_OR_NEWER
                        JobType.Single,
#endif
                        (ExecuteJobFunction)Execute);

                return s_JobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobWrapper jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            // @TODO: Use parallel for job... (Need to expose combine jobs)

            public static void Execute(ref JobWrapper jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                if (jobWrapper.appendCount == -1)
                    ExecuteFilter(ref jobWrapper, bufferRangePatchData);
                else
                    ExecuteAppend(ref jobWrapper, bufferRangePatchData);
            }

            public static unsafe void ExecuteAppend(ref JobWrapper jobWrapper, System.IntPtr bufferRangePatchData)
            {
                int oldLength = jobWrapper.outputIndices.Length;
                jobWrapper.outputIndices.Capacity = math.max(jobWrapper.appendCount + oldLength, jobWrapper.outputIndices.Capacity);

                int* outputPtr = (int*)jobWrapper.outputIndices.GetUnsafePtr();
                int outputIndex = oldLength;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobWrapper),
                    0, jobWrapper.appendCount);
#endif
                for (int i = 0; i != jobWrapper.appendCount; i++)
                {
                    if (jobWrapper.JobData.Execute(i))
                    {
                        outputPtr[outputIndex] = i;
                        outputIndex++;
                    }
                }

                jobWrapper.outputIndices.ResizeUninitialized(outputIndex);
            }

            public static unsafe void ExecuteFilter(ref JobWrapper jobWrapper, System.IntPtr bufferRangePatchData)
            {
                int* outputPtr = (int*)jobWrapper.outputIndices.GetUnsafePtr();
                int inputLength = jobWrapper.outputIndices.Length;

                int outputCount = 0;
                for (int i = 0; i != inputLength; i++)
                {
                    int inputIndex = outputPtr[i];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobWrapper), inputIndex, 1);
#endif

                    if (jobWrapper.JobData.Execute(inputIndex))
                    {
                        outputPtr[outputCount] = inputIndex;
                        outputCount++;
                    }
                }

                jobWrapper.outputIndices.ResizeUninitialized(outputCount);
            }
        }

        public static unsafe JobHandle ScheduleAppend<T>(this T jobData, NativeList<int> indices, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForFilter
        {
            JobParallelForFilterProducer<T>.JobWrapper jobWrapper = new JobParallelForFilterProducer<T>.JobWrapper()
            {
                JobData = jobData,
                outputIndices = indices,
                appendCount = arrayLength
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobWrapper), JobParallelForFilterProducer<T>.Initialize(), dependsOn,
#if UNITY_2020_2_OR_NEWER
                ScheduleMode.Parallel
#else
                ScheduleMode.Batched
#endif
            );
            return JobsUtility.Schedule(ref scheduleParams);
        }

        public static unsafe JobHandle ScheduleFilter<T>(this T jobData, NativeList<int> indices, int innerloopBatchCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForFilter
        {
            JobParallelForFilterProducer<T>.JobWrapper jobWrapper = new JobParallelForFilterProducer<T>.JobWrapper()
            {
                JobData = jobData,
                outputIndices = indices,
                appendCount = -1
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobWrapper), JobParallelForFilterProducer<T>.Initialize(), dependsOn,
#if UNITY_2020_2_OR_NEWER
                ScheduleMode.Parallel
#else
                ScheduleMode.Batched
#endif
            );
            return JobsUtility.Schedule(ref scheduleParams);
        }

        //@TODO: RUN
    }
}
