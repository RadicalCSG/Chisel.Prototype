using Chisel.Components;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    

    public abstract class ChiselGeneratorModeWithSettings<Settings, Generator> : ChiselGeneratorMode 
        where Settings : class, new()
        where Generator : ChiselGeneratorComponent
    {
        public override void OnActivate() { Reset(); generatedComponent = null; forceOperation = null; LoadSettings(settings); }
        public override void OnDeactivate() { Reset(); generatedComponent = null; }

        protected Settings          settings        = new Settings();
        protected CSGOperationType? forceOperation  = null;
        protected Generator         generatedComponent;

        public override void OnSceneSettingsGUI()
        {
            GUILayout.BeginVertical();
            {
                ShowSceneSettings(settings);
                ChiselOperationGUI.ChooseGeneratorOperation(ref forceOperation);
            }
            GUILayout.EndVertical();
        }
    }

    public abstract class ChiselGeneratorMode
    {
        public abstract string  ToolName        { get; }
        public virtual string   Group           { get; }

        public GUIContent       Content         { get { return ChiselEditorResources.GetIconContent(ToolName, ToolName)[0]; } }

        public bool             InToolBox 
        {
            get { return ChiselEditorSettings.IsInToolBox(ToolName, true); }
            set { ChiselEditorSettings.SetInToolBox(ToolName, value);  }
        }

        public virtual void     OnActivate()    { Reset(); }

        public virtual void     OnDeactivate()  { Reset(); }

        public virtual void     Reset() { }

        public void Commit(GameObject newGameObject)
        {
            if (!newGameObject)
            {
                Cancel();
                return;
            }
            UnityEditor.Selection.selectionChanged -= OnDelayedSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnDelayedSelectionChanged;
            UnityEditor.Selection.activeGameObject = newGameObject;
            Reset();
        }

        // Unity bug workaround
        void OnDelayedSelectionChanged()
        {
            UnityEditor.Selection.selectionChanged -= OnDelayedSelectionChanged;

            EditorTools.SetActiveTool(typeof(ChiselEditGeneratorTool));
        }

        public void Cancel()
        { 
            Reset();
            Undo.RevertAllInCurrentGroup();
            EditorGUIUtility.ExitGUI();
        }

        public void             OnSceneSettingsGUI(SceneView sceneView) { OnSceneSettingsGUI(); }
        public virtual void     OnSceneSettingsGUI() {}

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);

        public virtual void ShowSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.KeyDown:
                case EventType.ValidateCommand:
                {
                    if (SceneHandles.InCameraOrbitMode ||
                        (evt.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != EventModifiers.None ||
                        GUIUtility.hotControl != 0)
                        break;

                    if (evt.keyCode == KeyCode.Escape)
                    {
                        evt.Use();
                        break;
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (SceneHandles.InCameraOrbitMode ||
                        (evt.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != EventModifiers.None ||
                        GUIUtility.hotControl != 0)
                        break;

                    if (evt.keyCode == KeyCode.Escape)
                    {
                        evt.Use();
                        GUIUtility.ExitGUI();
                    }
                    break;
                }
            }
            OnSceneGUI(sceneView, dragArea);
        }


        #region Settings

        protected void LoadSettings<T>(T settings) where T : class, new()
        {
            var reflectedSettings = GetReflectedSettings<T>();
            foreach(var field in reflectedSettings.reflectedFields)
            {
                object value;
                switch (field.defaultValue)
                {
                    case Int32  castValue:  value = EditorPrefs.GetInt(field.settingsName, castValue); break;
                    case float  castValue:  value = EditorPrefs.GetFloat(field.settingsName, castValue); break;
                    case bool   castValue:  value = EditorPrefs.GetBool(field.settingsName, castValue); break;
                    case string castValue:  value = EditorPrefs.GetString(field.settingsName, castValue); break;
                    case Enum   _:
                    {
                        var defaultValue = Convert.ToInt32(field.defaultValue as Enum);
                        var intValue     = EditorPrefs.GetInt(field.settingsName, defaultValue);
                        value = Enum.ToObject(field.fieldInfo.FieldType, intValue) as Enum;
                        break;
                    }
                    default:
                    {
                        Debug.LogWarning("Unsupported type");
                        value = field.defaultValue;
                        break;
                    }
                }
                field.fieldInfo.SetValue(settings, value);
            }
        }

        protected void SaveSettings<T>(T settings) where T : class, new()
        {
            var reflectedSettings = GetReflectedSettings<T>();
            foreach (var field in reflectedSettings.reflectedFields)
            {
                var value = field.fieldInfo.GetValue(settings);
                switch (value)
                {
                    case Int32  castValue: EditorPrefs.SetInt(field.settingsName, castValue); break;
                    case float  castValue: EditorPrefs.SetFloat(field.settingsName, castValue); break;
                    case bool   castValue: EditorPrefs.SetBool(field.settingsName, castValue); break;
                    case string castValue: EditorPrefs.SetString(field.settingsName, castValue); break;
                    case Enum _:
                    {
                        var intValue = Convert.ToInt32(value as Enum);
                        EditorPrefs.SetInt(field.settingsName, intValue); break;
                    }
                }
            }
        }

        protected void ShowSceneSettings<T>(T settings) where T : class, new()
        {
            var reflectedSettings = GetReflectedSettings<T>();
            EditorGUI.BeginChangeCheck();
            {
                foreach (var field in reflectedSettings.reflectedFields)
                {
                    var value = field.fieldInfo.GetValue(settings);
                    EditorGUI.BeginChangeCheck();
                    switch (value)
                    {
                        case Int32 castValue:   value = EditorGUILayout.IntField(field.niceName, castValue); break;
                        case float castValue:   value = EditorGUILayout.FloatField(field.niceName, castValue); break;
                        case bool castValue:    value = EditorGUILayout.Toggle(field.niceName, castValue); break;
                        case string castValue:  value = EditorGUILayout.TextField(field.niceName, castValue); break;
                        case Enum castValue:    value = EditorGUILayout.EnumPopup(field.niceName, castValue); break;
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        field.fieldInfo.SetValue(settings, value);
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
                SaveSettings(settings);
        }

        class SettingsFieldReflection
        {
            public string name;
            public string settingsName;
            public GUIContent niceName;

            public object defaultValue;
            public System.Reflection.FieldInfo fieldInfo;
        }

        class SettingsReflection
        {
            public Type settingsType;
            public string settingsTypeName;
            public SettingsFieldReflection[] reflectedFields;
        }

        static Dictionary<Type, SettingsReflection> SettingsReflectionLookup = new Dictionary<Type, SettingsReflection>();

        static SettingsReflection GetReflectedSettings<T>() where T : class, new()
        {
            var settingsType = typeof(T);
            if (!SettingsReflectionLookup.TryGetValue(settingsType, out SettingsReflection settings))
            {
                var fieldList = new List<SettingsFieldReflection>();
                var settingsTypeName = settingsType.FullName;
                var defaultSettings = new T();
                var fields = settingsType.GetFields();
                foreach (var field in fields)
                {
                    var name = field.Name;
                    var niceName = EditorGUIUtility.TrTextContent(ObjectNames.NicifyVariableName(name));
                    var settingsName = $"{settingsTypeName}.{name}";

                    var defaultValue = field.GetValue(defaultSettings);
                    if (defaultValue == null)
                    {
                        if (field.FieldType.IsValueType)
                            defaultValue = Activator.CreateInstance(field.FieldType);
                    }

                    fieldList.Add(new SettingsFieldReflection()
                    {
                        name         = name,
                        niceName     = niceName,
                        settingsName = settingsName,
                        defaultValue = defaultValue,
                        fieldInfo        = field
                    });
                }
                settings = new SettingsReflection()
                {
                    settingsType = settingsType,
                    settingsTypeName = settingsTypeName,
                    reflectedFields = fieldList.ToArray()
                };
                SettingsReflectionLookup[settingsType] = settings;
            }
            return settings;
        }
        #endregion
    }
}
