using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using XNodeEditor;

namespace Chisel.Nodes
{
    [CustomEditor(typeof(ChiselGraphInstance))]
    public class ChiselGraphInstanceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var instance = target as ChiselGraphInstance;

            if (GUILayout.Button("Edit", GUI.skin.GetStyle("button")))
            {
                instance.graph.instance = instance;

                NodeEditorWindow.Open(instance.graph);
            }

            if (GUILayout.Button("UpdateCSG", GUI.skin.GetStyle("button")))
            {
                instance.UpdateCSG();
            }

            if (GUILayout.Button("Rebuild", GUI.skin.GetStyle("button")))
            {
                instance.Rebuild();
            }


            EditorGUI.BeginChangeCheck();

            ChiselGraphPropertyEditor.OnGUI(instance);

            if (EditorGUI.EndChangeCheck())
            {
                instance.IsDirty = true;
                instance.UpdateProperties();
                instance.UpdateCSG();
            }
        }
    }

    public class ChiselGraphPropertyEditor
    {
        static Dictionary<Type, ChiselGraphPropertyDrawer> s_ParameterDrawers;

        static ChiselGraphPropertyEditor()
        {
            s_ParameterDrawers = new Dictionary<Type, ChiselGraphPropertyDrawer>();
            ReloadDecoratorTypes();
        }

        [DidReloadScripts]
        static void OnEditorReload()
        {
            ReloadDecoratorTypes();
        }

        static void ReloadDecoratorTypes()
        {
            s_ParameterDrawers.Clear();

            // Look for all the valid parameter drawers
            var types = GetAllTypesDerivedFrom<ChiselGraphPropertyDrawer>()
                .Where(
                    t => t.IsDefined(typeof(GraphPropertyDrawerAttribute), false)
                    && !t.IsAbstract
                    );

            // Store them
            foreach (var type in types)
            {
                var attr = (GraphPropertyDrawerAttribute)type.GetCustomAttributes(typeof(GraphPropertyDrawerAttribute), false)[0];
                var decorator = (ChiselGraphPropertyDrawer)Activator.CreateInstance(type);
                s_ParameterDrawers.Add(attr.propertyType, decorator);
            }
        }

        public static void OnGUI(ChiselGraphInstance instance)
        {
            if (instance.properties != null)
                foreach (var property in instance.properties)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawOverrideCheckbox(property);
                        s_ParameterDrawers.TryGetValue(property.GetType(), out var drawer);

                        if (drawer != null)
                            using (new EditorGUI.DisabledScope(!property.overrideValue))
                                drawer.OnGUI(property);
                    }
        }

        static void DrawOverrideCheckbox(GraphProperty property)
        {
            var overrideRect = GUILayoutUtility.GetRect(17f, 17f, GUILayout.ExpandWidth(false));
            overrideRect.yMin += 4f;
            property.overrideValue = GUI.Toggle(overrideRect, property.overrideValue, EditorGUIUtility.TrTextContent("", "Override this setting."), ChiselGrpahEditorStyles.smallTickbox);
        }

        public static IEnumerable<Type> GetAllTypesDerivedFrom<T>()
        {
#if UNITY_EDITOR && UNITY_2019_2_OR_NEWER
            return TypeCache.GetTypesDerivedFrom<T>();
#else
            return GetAllAssemblyTypes().Where(t => t.IsSubclassOf(typeof(T)));
#endif
        }
    }

    [GraphPropertyDrawer(typeof(FloatProperty))]
    sealed class FloatPropertyDrawer : ChiselGraphPropertyDrawer
    {
        public override void OnGUI(GraphProperty property)
        {
            var floatProperty = property as FloatProperty;
            floatProperty.Value = EditorGUILayout.FloatField(property.Name, floatProperty.Value);
        }
    }

    public abstract class ChiselGraphPropertyDrawer
    {
        public abstract void OnGUI(GraphProperty parameter);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GraphPropertyDrawerAttribute : Attribute
    {
        public readonly Type propertyType;
        public GraphPropertyDrawerAttribute(Type propertyType)
        {
            this.propertyType = propertyType;
        }
    }
}