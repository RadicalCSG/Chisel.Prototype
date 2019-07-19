using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    // This class connects the native code with the unity log
    public sealed class NativeLogging
    {
        public delegate void StringLog([MarshalAs(UnmanagedType.LPStr)] string text, int uniqueObjectID);
        [return: MarshalAs(UnmanagedType.LPStr)] public delegate string ReturnStringMethod(int uniqueObjectID);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        struct UnityMethods
        {
            public StringLog DebugLog;
            public StringLog DebugLogError;
            public StringLog DebugLogWarning;
            public ReturnStringMethod NameForUserID;
        }

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void RegisterMethods([In] ref UnityMethods unityMethods);

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ClearMethods();

        [RuntimeInitializeOnLoadMethod]
        public static void RegisterUnityMethods()
        {
            UnityMethods unityMethods;

            unityMethods.DebugLog = delegate (string message, int uniqueObjectID)
            {
#if UNITY_EDITOR
                Debug.Log(message, (uniqueObjectID != 0) ? UnityEditor.EditorUtility.InstanceIDToObject(uniqueObjectID) : null);
#else
                Debug.Log(message);
#endif
            };

            unityMethods.DebugLogError = delegate (string message, int uniqueObjectID)
            {
#if UNITY_EDITOR
                Debug.LogError(message, (uniqueObjectID != 0) ? UnityEditor.EditorUtility.InstanceIDToObject(uniqueObjectID) : null);
#else
                Debug.LogError(message);
#endif
            };

            unityMethods.DebugLogWarning = delegate (string message, int uniqueObjectID)
            {
#if UNITY_EDITOR
                Debug.LogWarning(message, (uniqueObjectID != 0) ? UnityEditor.EditorUtility.InstanceIDToObject(uniqueObjectID) : null);
#else
                Debug.LogWarning(message);
#endif
            };

            unityMethods.NameForUserID = delegate (int uniqueObjectID)
            {
#if UNITY_EDITOR
                var obj = (uniqueObjectID != 0) ? UnityEditor.EditorUtility.InstanceIDToObject(uniqueObjectID) : null;
                if (obj)
                    return obj.name;
#endif
                return "<unknown>";
            };

            RegisterMethods(ref unityMethods);
        }

        public static void ClearUnityMethods()
        {
            ClearMethods();
        }
    }
}
