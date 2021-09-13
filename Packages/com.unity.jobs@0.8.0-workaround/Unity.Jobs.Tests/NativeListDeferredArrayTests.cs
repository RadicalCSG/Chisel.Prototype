using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

public class NativeListDeferredArrayTests
{
    private bool JobsDebuggerWasEnabled;
    struct AliasJob : IJob
    {
        public NativeArray<int> array;
        public NativeList<int> list;

        public void Execute()
        {
        }
    }

    struct SetListLengthJob : IJob
    {
        public int ResizeLength;
        public NativeList<int> list;

        public void Execute()
        {
            list.Resize(ResizeLength, NativeArrayOptions.UninitializedMemory);
        }
    }

    struct SetArrayValuesJobParallel : IJobParallelForDefer
    {
        public NativeArray<int> array;

        public void Execute(int index)
        {
            array[index] = array.Length;
        }
    }

    struct GetArrayValuesJobParallel : IJobParallelForDefer
    {
        [ReadOnly]
        public NativeArray<int> array;

        public void Execute(int index)
        {
        }
    }


    struct ParallelForWithoutList : IJobParallelForDefer
    {
        public void Execute(int index)
        {
        }
    }

    [SetUp]
    public void Setup()
    {
        // Many ECS tests will only pass if the Jobs Debugger enabled;
        // force it enabled for all tests, and restore the original value at teardown.
        JobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
        JobsUtility.JobDebuggerEnabled = true;

#if UNITY_DOTSRUNTIME
        Unity.Core.TempMemoryScope.EnterScope();
#endif
    }

    [Test]
    public void ResizedListToDeferredJobArray([Values(0, 1, 2, 3, 4, 5, 6, 42, 97, 1023)] int length)
    {
        var list = new NativeList<int>(Allocator.TempJob);

        var setLengthJob = new SetListLengthJob { list = list, ResizeLength = length };
        var jobHandle = setLengthJob.Schedule();

        var setValuesJob = new SetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        setValuesJob.Schedule(list, 3, jobHandle).Complete();

        Assert.AreEqual(length, list.Length);
        for (int i = 0; i != list.Length; i++)
            Assert.AreEqual(length, list[i]);

        list.Dispose();
    }

    [Test]
    public unsafe void DeferredParallelForFromIntPtr()
    {
        int length = 10;

        var lengthValue = new NativeArray<int>(1, Allocator.TempJob);
        lengthValue[0] = length;
        var array = new NativeArray<int>(length, Allocator.TempJob);

        var setValuesJob = new SetArrayValuesJobParallel { array = array };
        setValuesJob.Schedule((int*)lengthValue.GetUnsafePtr(), 3).Complete();

        for (int i = 0; i != array.Length; i++)
            Assert.AreEqual(length, array[i]);

        lengthValue.Dispose();
        array.Dispose();
    }

    [Test]
    public void ResizeListBeforeSchedule([Values(5)] int length)
    {
        var list = new NativeList<int>(Allocator.TempJob);

        var setLengthJob = new SetListLengthJob { list = list, ResizeLength = length }.Schedule();
        var setValuesJob = new SetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        setLengthJob.Complete();

        setValuesJob.Schedule(list, 3).Complete();

        Assert.AreEqual(length, list.Length);
        for (int i = 0; i != list.Length; i++)
            Assert.AreEqual(length, list[i]);

        list.Dispose();
    }

    [Test]
    public void ResizedListToDeferredJobArray()
    {
        var list = new NativeList<int>(Allocator.TempJob);
        list.Add(1);

        var array = list.AsDeferredJobArray();
#pragma warning disable 0219 // assigned but its value is never used
        Assert.Throws<IndexOutOfRangeException>(() => { var value = array[0]; });
#pragma warning restore 0219
        Assert.AreEqual(0, array.Length);

        list.Dispose();
    }

    [Test]
    public void ResizeListWhileJobIsRunning()
    {
        var list = new NativeList<int>(Allocator.TempJob);
        list.Resize(42, NativeArrayOptions.UninitializedMemory);

        var setValuesJob = new GetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        var jobHandle = setValuesJob.Schedule(list, 3);

        Assert.Throws<InvalidOperationException>(() => list.Resize(1, NativeArrayOptions.UninitializedMemory));

        jobHandle.Complete();
        list.Dispose();
    }

    [Test]
    public void AliasArrayThrows()
    {
        var list = new NativeList<int>(Allocator.TempJob);

        var aliasJob = new AliasJob { list = list, array = list.AsDeferredJobArray() };
        Assert.Throws<InvalidOperationException>(() => aliasJob.Schedule());

        list.Dispose();
    }

    [Test]
    public void DeferredListMustExistInJobData()
    {
        var list = new NativeList<int>(Allocator.TempJob);

        var job = new ParallelForWithoutList();
        Assert.Throws<InvalidOperationException>(() => job.Schedule(list, 64));

        list.Dispose();
    }

    [Test]
    public void DeferredListCantBeDeletedWhileJobIsRunning()
    {
        var list = new NativeList<int>(Allocator.TempJob);
        list.Resize(42, NativeArrayOptions.UninitializedMemory);

        var setValuesJob = new GetArrayValuesJobParallel { array = list.AsDeferredJobArray() };
        var jobHandle = setValuesJob.Schedule(list, 3);

        Assert.Throws<InvalidOperationException>(() => list.Dispose());

        jobHandle.Complete();

        // Actually clean up memory to avoid DisposeSentinel complaint
        list.Dispose();
    }

    [Test]
    public void DeferredArrayCantBeAccessedOnMainthread()
    {
        var list = new NativeList<int>(Allocator.TempJob);
        list.Add(1);

        var defer = list.AsDeferredJobArray();

        Assert.AreEqual(0, defer.Length);
        Assert.Throws<IndexOutOfRangeException>(() => defer[0] = 5);

        list.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
#if UNITY_DOTSRUNTIME
        Unity.Core.TempMemoryScope.ExitScope();
#endif

        JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;
    }
}
