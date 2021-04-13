using Chisel.Components;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.EditorTools;
using UnitySceneExtensions;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    public abstract partial class ChiselPlacementToolInstanceWithDefinition<PlacementToolDefinitionType, DefinitionType, Generator> 
        : ChiselPlacementToolInstance
        // Placement tool definition needs to be a ScriptableObject so we can create an Editor for it
        where PlacementToolDefinitionType : ScriptableObject 
        // We need the DefinitionType to be able to strongly type the Generator
        where DefinitionType              : IChiselGenerator, IBrushGenerator, new()
        where Generator                   : ChiselDefinedBrushGeneratorComponent<DefinitionType>
    {
        public override void OnActivate()   { EnsureInitialized(); base.OnActivate(); generatedComponent = null; forceOperation = null; LoadValues(PlacementToolDefinition); }
        public override void OnDeactivate() { base.OnActivate(); generatedComponent = null; }


        SerializedObject            serializedObject;
        PlacementToolDefinitionType placementToolDefinitionInternal;
        void EnsureInitialized()
        {
            // We can't just new the PlacementToolDefinitionType, we need to use CreateInstance because it's a ScriptableObject
            if (placementToolDefinitionInternal == null)
            {
                placementToolDefinitionInternal = ScriptableObject.CreateInstance<PlacementToolDefinitionType>();
                placementToolDefinitionInternal.hideFlags = HideFlags.DontSave;
            }
            // We need a serializedObject pointing to our PlacementToolDefinition (ScriptableObject) so we can let Unity show 
            // the contents of the placementToolDefinition, the same way we show things in the inspector
            if (serializedObject == null)
                serializedObject = new SerializedObject(placementToolDefinitionInternal);
        }
        

        protected PlacementToolDefinitionType      PlacementToolDefinition
        {
            get
            {
                EnsureInitialized();
                return placementToolDefinitionInternal;
            }
        }
        protected CSGOperationType?                                     forceOperation  = null;
        protected Type                                                  generatorType   = typeof(Generator);
        protected ChiselDefinedBrushGeneratorComponent<DefinitionType>  generatedComponent;

        static readonly string[] excludeProperties = new[] { "m_Script" };

        // Sadly the DrawPropertiesExcluding method is protected, so we need to use reflection to be able to render the
        // placementToolDefinition properties, without a "PlacementToolDefinition" foldout, and a "script" property.
        delegate void DrawPropertiesExcludingDelegate(SerializedObject obj, params string[] propertyToExclude);
        static DrawPropertiesExcludingDelegate DrawPropertiesExcluding = ReflectionExtensions.CreateDelegate<DrawPropertiesExcludingDelegate>(typeof(Editor), "DrawPropertiesExcluding");

        public override void OnSceneSettingsGUI()
        {
            GUILayout.BeginVertical();
            {
                EnsureInitialized();
                if (serializedObject != null)
                {
                    var prevGUIChanged = GUI.changed;
                    GUI.changed = false;
                    serializedObject.Update();
                    var prevLabelWidth = EditorGUIUtility.labelWidth;
                    try
                    {
                        EditorGUIUtility.labelWidth = 115;
                        // This renders our placementToolDefinition like in the inspector, but excludes the properties named in excludeProperties
                        DrawPropertiesExcluding(serializedObject, excludeProperties);
                    } catch (Exception ex) 
                    { 
                        Debug.LogException(ex); }
                    if (GUI.changed)
                    {
                        EditorGUIUtility.labelWidth = prevLabelWidth;
                        serializedObject.ApplyModifiedProperties();
                        prevGUIChanged = true;
                    }
                    GUI.changed = prevGUIChanged;
                }
                ChiselCompositeGUI.ChooseGeneratorOperation(ref forceOperation);
            }
            GUILayout.EndVertical();
        }
    }

    public abstract class ChiselPlacementToolInstance 
    {
        public abstract string  ToolName        { get; }
        public virtual string   Group           { get; }

        public GUIContent       Content         { get { return ChiselEditorResources.GetIconContent(ToolName, ToolName)[0]; } }


        public bool             IsGenerating    { get; private set; }

        public bool             InToolBox 
        {
            get { return ChiselEditorSettings.IsInToolBox(ToolName, true); }
            set { ChiselEditorSettings.SetInToolBox(ToolName, value);  }
        }

        public virtual void     OnActivate()    { IsGenerating = false; Reset(); }

        public virtual void     OnDeactivate()  { IsGenerating = false; Reset(); }

        public virtual void     Reset()
        {
            RectangleExtrusionHandle.Reset();
            ShapeExtrusionHandle.Reset();
        }

        public void Commit(GameObject newGameObject)
        {
            if (!newGameObject)
            {
                Cancel();
                return;
            }
            IsGenerating = false;
            UnityEditor.Selection.selectionChanged -= OnDelayedSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnDelayedSelectionChanged;
            UnityEditor.Selection.activeGameObject = newGameObject;
            Undo.IncrementCurrentGroup();
            Reset();
        }

        // Unity bug workaround
        void OnDelayedSelectionChanged()
        {
            UnityEditor.Selection.selectionChanged -= OnDelayedSelectionChanged;

            ToolManager.SetActiveTool(typeof(ChiselEditGeneratorTool));
        }

        public void Cancel()
        {
            IsGenerating = false;
            Reset();
            Undo.RevertAllInCurrentGroup();
            EditorGUIUtility.ExitGUI();
        }

        public void             OnSceneSettingsGUI(SceneView sceneView) { OnSceneSettingsGUI(); }
        public virtual void     OnSceneSettingsGUI() {}

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);

        public virtual void ShowSceneGUI(SceneView sceneView, Rect dragArea)
        {
            if (Event.current.type == EventType.MouseDown)
                IsGenerating = true;

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

                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
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

                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        evt.Use();
                        GUIUtility.ExitGUI();
                    }
                    break;
                }
            }

            var prevColor = Handles.color;
            Handles.color = SceneHandles.handleColor;
            OnSceneGUI(sceneView, dragArea);
            Handles.color = prevColor;
        }


        #region Settings

        protected void LoadValues<T>(T definition) where T : ScriptableObject
        {
            var reflectedValues = GetReflectedValues<T>();
            foreach(var field in reflectedValues.reflectedFields)
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
                field.fieldInfo.SetValue(definition, value);
            }
        }

        protected void SaveValues<T>(T definition) where T : ScriptableObject
        {
            var reflectedValues = GetReflectedValues<T>();
            foreach (var field in reflectedValues.reflectedFields)
            {
                var value = field.fieldInfo.GetValue(definition);
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

        class DefinitionFieldReflection
        {
            public string name;
            public string settingsName;
            public GUIContent niceName;

            public object defaultValue;
            public System.Reflection.FieldInfo fieldInfo;
        }

        class DefinitionReflection
        {
            public Type settingsType;
            public string settingsTypeName;
            public DefinitionFieldReflection[] reflectedFields;
        }

        static Dictionary<Type, DefinitionReflection> DefinitionReflectionLookup = new Dictionary<Type, DefinitionReflection>();

        static DefinitionReflection GetReflectedValues<T>() where T : ScriptableObject
        {
            var settingsType = typeof(T);
            if (!DefinitionReflectionLookup.TryGetValue(settingsType, out DefinitionReflection settings))
            {
                var fieldList           = new List<DefinitionFieldReflection>();
                var settingsTypeName    = settingsType.FullName;
                var defaultSettings     = ScriptableObject.CreateInstance<T>();
                defaultSettings.hideFlags = HideFlags.DontSaveInEditor;
                try
                { 
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

                        fieldList.Add(new DefinitionFieldReflection()
                        {
                            name         = name,
                            niceName     = niceName,
                            settingsName = settingsName,
                            defaultValue = defaultValue,
                            fieldInfo        = field
                        });
                    }
                }
                finally
                {
                    defaultSettings.hideFlags = HideFlags.None;
                    UnityEngine.Object.DestroyImmediate(defaultSettings);
                }
                settings = new DefinitionReflection()
                {
                    settingsType        = settingsType,
                    settingsTypeName    = settingsTypeName,
                    reflectedFields     = fieldList.ToArray()
                };
                DefinitionReflectionLookup[settingsType] = settings;
            }
            return settings;
        }
        #endregion
    }
}
