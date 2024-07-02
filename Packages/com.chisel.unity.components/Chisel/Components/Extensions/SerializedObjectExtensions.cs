namespace Chisel.Components
{
#if UNITY_EDITOR
    public static class SerializedObjectExtensions
    {
        public static void SetPropertyValue(this UnityEditor.SerializedObject serializedObject, string name, bool value)
        {
            var prop = serializedObject.FindProperty(name);
            if (prop != null)
                prop.boolValue = value;
        }

        public static void SetPropertyValue(this UnityEditor.SerializedObject serializedObject, string name, float value)
        {
            var prop = serializedObject.FindProperty(name);
            if (prop != null)
                prop.floatValue = value;
        }

        public static void SetPropertyValue(this UnityEditor.SerializedObject serializedObject, string name, int value)
        {
            var prop = serializedObject.FindProperty(name);
            if (prop != null)
                prop.intValue = value;
        }
    }
#endif
}
