using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using System.Reflection;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(ToggleFlagsAttribute))]
    public sealed class ToggleFlagsPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Styles.kButtonSize;// base.GetPropertyHeight(property, label);
        }
        
	    static Type GetBaseProperty(SerializedProperty prop)
	    {
		    // Separate the steps it takes to get to this property
		    var separatedPaths      = prop.propertyPath.Split('.');
 
		    // Go down to the root of this serialized property
		    var reflectionTarget    = prop.serializedObject.targetObject as object;
		    // Walk down the path to get the target object
		    foreach (var path in separatedPaths)
		    {
			    var fieldInfo = reflectionTarget.GetType().GetField(path);
			    reflectionTarget = fieldInfo.GetValue(reflectionTarget);
		    }
		    return reflectionTarget.GetType();
	    }

        struct ToggleIcons
        {
            public ToggleFlagAttribute  attribute;
            public MemberInfo           memberInfo;
            public int                  value;
            public GUIContent[]         iconsOn;
            public GUIContent[]         iconsOff;
            public string               name;
        }

        class ObjectTypeLookup
        {
            public Dictionary<string, Type>         propertyTypeLookup  = new Dictionary<string, Type>();
            public Dictionary<Type, ToggleIcons[]>  toggleIconLookup    = new Dictionary<Type, ToggleIcons[]>();
        }

        static Dictionary<Type, ObjectTypeLookup> objectTypeLookup = new Dictionary<Type, ObjectTypeLookup>();
        
        class Styles
        {
            public const int kButtonSize = 32 + (kButtonPadding * 2);
            public const int kButtonMargin = 1;
            public const int kButtonPadding = 2;
            
            public GUIStyle toggleStyle;
            public GUIStyle toggleStyleLeft;
            public GUIStyle toggleStyleMid;
            public GUIStyle toggleStyleRight;
            
            public Styles()
            {
                toggleStyle = new GUIStyle("AppCommand")
                {
                    padding     = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin      = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth  = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
                toggleStyleLeft = new GUIStyle("AppCommandLeft")
                {
                    padding     = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin      = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth  = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
                toggleStyleMid = new GUIStyle("AppCommandMid")
                {
                    padding     = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin      = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth  = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
                toggleStyleRight = new GUIStyle("AppCommandMid")
                {
                    padding     = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin      = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth  = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
            }
        }
        static Styles stylesInstance;
        static Styles styles => stylesInstance ?? (stylesInstance = new Styles());

        static ToggleIcons[] GetIconsForProperty(SerializedProperty property)
        {
            var serializedObjectType = property.serializedObject.targetObject.GetType();
            if (!objectTypeLookup.TryGetValue(serializedObjectType, out var lookup))
                objectTypeLookup[serializedObjectType] = lookup = new ObjectTypeLookup();

            var propertyPath = property.propertyPath;
            if (!lookup.propertyTypeLookup.TryGetValue(propertyPath, out var type))
                lookup.propertyTypeLookup[propertyPath] = type = GetBaseProperty(property);

            if (!lookup.toggleIconLookup.TryGetValue(type, out var toggleIcons))
                lookup.toggleIconLookup[type] = toggleIcons = GetIconsForProperty(type);

            return toggleIcons;
        }

        static ToggleIcons[] GetIconsForProperty(Type type)
        {
            var members         = type.GetMembers(BindingFlags.Public | BindingFlags.Static);
            var toggleItems     = new List<ToggleIcons>();
            foreach (var member in members)
            {
                var attribute   = member.GetCustomAttribute<ToggleFlagAttribute>();
                var value       = (int)Enum.Parse(type, member.Name);

                GUIContent[] iconsOn, iconsOff;
                if (attribute == null)
                {
                    iconsOff =
                    iconsOn = ChiselEditorResources.GetIconContent(member.Name, string.Empty);
                } else
                {
                    iconsOn  = ChiselEditorResources.GetIconContent(attribute.ActiveIcon, attribute.ActiveDescription);
                    iconsOff = ChiselEditorResources.GetIconContent(attribute.InactiveIcon, attribute.InactiveDescription);
                }
                
                toggleItems.Add(new ToggleIcons
                {
                    attribute   = attribute,
                    memberInfo  = member,
                    iconsOn     = iconsOn,
                    iconsOff    = iconsOff,
                    value       = value,
                    name        = ObjectNames.NicifyVariableName(member.Name)
                });
            }
            return toggleItems.ToArray();
        }
        
        public static int ToggleButton(Rect position, int current, int flag, GUIContent[] iconsOn, GUIContent[] iconsOff, GUIStyle style)
        {
            if (iconsOn == null)
                return current;
            var enabled = (current & flag) == flag;
            if (GUI.Button(position, enabled ? iconsOn[0] : iconsOff[0], style))
            {
                if (!enabled)
                    current |= flag;
                else
                    current &= ~flag;
            }
            return current;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                Debug.Assert(false);
                return;
            }

            var toggleFlags = attribute as ToggleFlagsAttribute;
            EditorGUILayout.BeginHorizontal();
            if (toggleFlags.ShowPrefix)
                position = EditorGUI.PrefixLabel(position, label);

            var includeFlags = toggleFlags.IncludeFlags;
            var excludeFlags = toggleFlags.ExcludeFlags;

            var icons = GetIconsForProperty(property);
            var enumValue = property.intValue;
            position.width = Styles.kButtonSize;

            int lastIndex = icons.Length - 1;
            for (int i = lastIndex; i >= 0; i--)
            {
                var icon = icons[i];
                if ((icon.value & includeFlags) == icon.value &&
                    (icon.value & excludeFlags) != icon.value)
                    break;
                lastIndex--;
            }


            bool first = true;
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i];
                if ((icon.value & includeFlags) != icon.value ||
                    (icon.value & excludeFlags) == icon.value)
                    continue;

                var last = (i == lastIndex);
                enumValue = ToggleButton(position, enumValue, icon.value, icon.iconsOn, icon.iconsOff,
                    first ? styles.toggleStyleLeft :
                    last ? styles.toggleStyleRight : 
                    styles.toggleStyleMid);
                first = false;
                position.x += Styles.kButtonSize;
            }
            property.intValue = enumValue;
            EditorGUILayout.EndHorizontal();
        }
    }
}
