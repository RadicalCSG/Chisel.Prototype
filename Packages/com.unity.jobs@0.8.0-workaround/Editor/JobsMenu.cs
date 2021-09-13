using UnityEditor;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

class JobsMenu
{
    private static int savedJobWorkerCount = JobsUtility.JobWorkerCount;

    const string kUseJobThreads = "Jobs/Use Job Threads";

    [MenuItem(kUseJobThreads, false)]
    static void SwitchUseJobThreads()
    {
        if (JobsUtility.JobWorkerCount > 0)
        {
            savedJobWorkerCount = JobsUtility.JobWorkerCount;
            try
            {
                JobsUtility.JobWorkerCount = 0;
            }
            catch (System.ArgumentOutOfRangeException e) when (e.ParamName == "JobWorkerCount")
            {
                UnityEngine.Debug.LogWarning("Disabling Job Threads requires Unity Version 2020.1.a15 or newer");
            }
        }
        else
        {
            JobsUtility.JobWorkerCount = savedJobWorkerCount;
            if (savedJobWorkerCount == 0)
            {
                JobsUtility.ResetJobWorkerCount();
            }
        }
    }

    [MenuItem(kUseJobThreads, true)]
    static bool SwitchUseJobThreadsValidate()
    {
        Menu.SetChecked(kUseJobThreads, (JobsUtility.JobWorkerCount > 0));

        return true;
    }

    const string kDebuggerMenu = "Jobs/JobsDebugger";

    [MenuItem(kDebuggerMenu, false)]
    static void SwitchJobsDebugger()
    {
        JobsUtility.JobDebuggerEnabled = !JobsUtility.JobDebuggerEnabled;
    }

    [MenuItem(kDebuggerMenu, true)]
    static bool SwitchJobsDebuggerValidate()
    {
        Menu.SetChecked(kDebuggerMenu, JobsUtility.JobDebuggerEnabled);
        return true;
    }

    const string kLeakOff = "Jobs/Leak Detection/Off";
    const string kLeakOn = "Jobs/Leak Detection/On";
    const string kLeakDetectionFull = "Jobs/Leak Detection/Full Stack Traces (Expensive)";

    [MenuItem(kLeakOff)]
    static void SwitchLeaksOff()
    {
        NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
    }

    [MenuItem(kLeakOn)]
    static void SwitchLeaksOn()
    {
        NativeLeakDetection.Mode = NativeLeakDetectionMode.Enabled;
    }

    [MenuItem(kLeakDetectionFull)]
    static void SwitchLeaksFull()
    {
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
    }

    [MenuItem(kLeakOff, true)]
    static bool SwitchLeaksOffValidate()
    {
        Menu.SetChecked(kLeakOff, NativeLeakDetection.Mode == NativeLeakDetectionMode.Disabled);
        Menu.SetChecked(kLeakOn, NativeLeakDetection.Mode == NativeLeakDetectionMode.Enabled);
        Menu.SetChecked(kLeakDetectionFull, NativeLeakDetection.Mode == NativeLeakDetectionMode.EnabledWithStackTrace);
        return true;
    }
}
