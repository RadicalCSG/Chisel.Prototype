namespace Chisel.Components
{
	public static class CSGObjectUtility
	{
		public static void SafeDestroy(UnityEngine.Object obj)
		{
			if (!obj)
				return;
			obj.hideFlags = UnityEngine.HideFlags.None;
#if UNITY_EDITOR
			if (!UnityEditor.EditorApplication.isPlaying)
				UnityEngine.Object.DestroyImmediate(obj);
			else
#endif
				UnityEngine.Object.Destroy(obj);
		}
	}
}
