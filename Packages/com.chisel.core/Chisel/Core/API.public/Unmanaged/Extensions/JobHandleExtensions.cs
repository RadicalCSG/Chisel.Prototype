using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace Chisel.Core
{
    public static class JobHandleExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0) { return handle0; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1) { return JobHandle.CombineDependencies(handle0, handle1); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2) { return JobHandle.CombineDependencies(handle0, handle1, handle2); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), handle3); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), handle6); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7, handle8)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7, JobHandle.CombineDependencies(handle8, handle9))); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9, JobHandle handle10) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7, JobHandle.CombineDependencies(handle8, handle9, handle10))); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9, JobHandle handle10, JobHandle handle11) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, JobHandle.CombineDependencies(handle7, handle8, handle9), JobHandle.CombineDependencies(handle10, handle11))); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9, JobHandle handle10, JobHandle handle11, JobHandle handle12) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, JobHandle.CombineDependencies(handle7, handle8, handle9), JobHandle.CombineDependencies(handle10, handle11, handle12))); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, params JobHandle[] handles)
        {
            JobHandle handle = JobHandle.CombineDependencies(
                                    JobHandle.CombineDependencies(handle0, handle1, handle2),
                                    JobHandle.CombineDependencies(handle3, handle4, handle5),
                                    JobHandle.CombineDependencies(handle6, handle7, handle8)
                                );

            for (int i = 0; i < handles.Length; i++)
                handle = JobHandle.CombineDependencies(handle, handles[i]);
            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0) { self = CombineDependencies(self, handle0); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1) { self = CombineDependencies(self, handle0, handle1); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1, JobHandle handle2) { self = CombineDependencies(self, handle0, handle1, handle2); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3) { self = CombineDependencies(self, handle0, handle1, handle2, handle3); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4) { self = CombineDependencies(self, handle0, handle1, handle2, handle3, handle4); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5) { self = CombineDependencies(self, handle0, handle1, handle2, handle3, handle4, handle5); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6) { self = CombineDependencies(self, handle0, handle1, handle2, handle3, handle4, handle5, handle6); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddDependency(ref this JobHandle self, JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7) { self = CombineDependencies(self, handle0, handle1, handle2, handle3, handle4, handle5, handle6, handle7); }
    }
}
