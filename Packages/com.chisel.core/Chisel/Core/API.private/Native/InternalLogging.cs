using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
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
				Debug.Log(message, (uniqueObjectID != 0) ? EditorUtility.InstanceIDToObject(uniqueObjectID) : null);
			};

			unityMethods.DebugLogError = delegate (string message, int uniqueObjectID)
			{
				Debug.LogError(message, (uniqueObjectID != 0) ? EditorUtility.InstanceIDToObject(uniqueObjectID) : null); 
			};

			unityMethods.DebugLogWarning = delegate (string message, int uniqueObjectID)
			{
				Debug.LogWarning(message, (uniqueObjectID != 0) ? EditorUtility.InstanceIDToObject(uniqueObjectID) : null);
			};

			unityMethods.NameForUserID = delegate (int uniqueObjectID)
			{
				var obj = (uniqueObjectID != 0) ? EditorUtility.InstanceIDToObject(uniqueObjectID) : null;
				if (obj == null)
					return "<unknown>";
				else
					return obj.name;
			};

			RegisterMethods(ref unityMethods);
		}

		public static void ClearUnityMethods()
		{
			ClearMethods();
		}
	}
}
