using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Jobs
{
    /// <summary>
    /// Used by automatically generated code. Do not use in projects.
    /// </summary>
    public class EarlyInitHelpers
    {
        public delegate void EarlyInitFunction();

        private static List<EarlyInitFunction> s_PendingDelegates;

        public static void FlushEarlyInits()
        {
            while (s_PendingDelegates != null)
            {
                var oldList = s_PendingDelegates;
                s_PendingDelegates = null;

                for (int i = 0; i < oldList.Count; ++i)
                {
                    try
                    {
                        oldList[i]();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        public static void AddEarlyInitFunction(EarlyInitFunction f)
        {
            if (s_PendingDelegates == null)
                s_PendingDelegates = new List<EarlyInitFunction>();

            s_PendingDelegates.Add(f);
        }

        public static void JobReflectionDataCreationFailed(Exception ex, Type jobType)
        {
            Debug.LogError($"Failed to create job reflection data for type ${jobType}:");
            Debug.LogException(ex);
        }
    }

}
