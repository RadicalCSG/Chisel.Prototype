namespace Chisel.Components
{
    //TODO: Move this somewhere else
    public static class ChiselObjectUtility
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
