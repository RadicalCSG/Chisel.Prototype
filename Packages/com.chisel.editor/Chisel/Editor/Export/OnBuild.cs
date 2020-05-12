using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Jobs;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    public class OnBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPostprocessBuild(BuildReport report)
        {
        }

        public void OnPreprocessBuild(BuildReport report)
        {
        }
    }
}
