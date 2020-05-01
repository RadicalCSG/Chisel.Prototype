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
    public class ChiselPlacementToolsSelectionWindow : EditorWindow
    {
        const float kSingleLineHeight   = 28f;
        const float kToolBarButtonSize  = 32f;
        const float kSingleSpacing      = 0.0f;


        [MenuItem("Window/Chisel/Placement Tools")]
        public static void Create()
        {
            var window = (ChiselPlacementToolsSelectionWindow)GetWindow(typeof(ChiselPlacementToolsSelectionWindow), false, "Chisel Placement Tools");
            window.Initialize();
            window.ShowAuxWindow();
        }

        void Initialize()
        {
            this.minSize = new Vector2(100, 100);
        }

        private void OnEnable()
        {
            ChiselGeneratorManager.GeneratorSelectionChanged -= GeneratorSelectionChanged;
            ChiselGeneratorManager.GeneratorSelectionChanged += GeneratorSelectionChanged;
            EditorTools.activeToolChanged -= EditModeSelectionChanged;
            EditorTools.activeToolChanged += EditModeSelectionChanged;
        }

        private void OnDisable()
        {
            ChiselGeneratorManager.GeneratorSelectionChanged -= GeneratorSelectionChanged;
            EditorTools.activeToolChanged -= EditModeSelectionChanged;
        }

        public void EditModeSelectionChanged()
        {
            Repaint();
        }

        public void GeneratorSelectionChanged(ChiselGeneratorMode prevGenerator, ChiselGeneratorMode nextGenerator)
        {
            Repaint();
        }

        static bool ToggleButton(Rect togglePosition, GUIContent content, bool isSelected, GUIStyle style, bool isActive)
        {
            var prevBackgroundColor = GUI.backgroundColor;
            if (isSelected && !isActive)
            {
                var color = Color.white;
                color.a = 0.25f;
                GUI.backgroundColor = color;
            }
            var result = GUI.Toggle(togglePosition, isSelected, content, style);
            GUI.backgroundColor = prevBackgroundColor;
            return result;
        }

        static bool ToggleButton(GUIContent content, bool isSelected, GUIStyle style, bool isActive)
        {
            var prevBackgroundColor = GUI.backgroundColor;
            if (isSelected && !isActive)
            {
                var color = Color.white;
                color.a = 0.25f;
                GUI.backgroundColor = color;
            }
            var result = GUILayout.Toggle(isSelected, content, style);
            GUI.backgroundColor = prevBackgroundColor;
            return result;
        }

        

        static void NamedGeneratorButton(ChiselGeneratorMode generator, Rect togglePosition, GUIStyle style, bool isActive)
        {
            var temp = togglePosition;
            temp.xMin += 5;
            temp.width = 20;
            {
                EditorGUI.BeginChangeCheck();
                var value = GUI.Toggle(temp, generator.InToolBox, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                {
                    SetInToolBox(generator, value);
                }
            }
            temp = togglePosition;
            temp.xMin += 25; 
            {
                EditorGUI.BeginChangeCheck();
                var content         = ChiselEditorResources.GetIconContentWithName(generator.ToolName, generator.ToolName)[0];
                var isSelected      = ChiselGeneratorManager.GeneratorMode == generator;
                var value           = ToggleButton(temp, content, isSelected, style, isActive);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    // TODO: make undoable
                    generator.InToolBox = true;
                    ChiselCreateTool.ActivateTool();
                    ChiselGeneratorManager.GeneratorMode = generator;
                    ChiselEditorSettings.Save();
                    SceneView.RepaintAll();
                }
            }
        }

        // TODO: move somewhere else
        public static void SetInToolBox(ChiselGeneratorMode generator, bool value)
        {
            // TODO: make undoable
            generator.InToolBox = value;
            if (ChiselGeneratorManager.GeneratorMode == generator)
            {
                if (!DeselectGeneratorMode())
                    generator.InToolBox = true;
            }
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }

        static bool DeselectGeneratorMode()
        {
            if (PrevGeneratorMode())
                return true;
            if (NextGeneratorMode())
                return true;
            return false;
        }

        public static bool PrevGeneratorMode()
        {
            var currentGeneratorMode = ChiselGeneratorManager.GeneratorMode;
            var generatorModes = ChiselGeneratorManager.generatorModes;
            var index = currentGeneratorMode == null ? 1 : ArrayUtility.IndexOf(generatorModes, currentGeneratorMode);
            do { index--; } while (index >= 0 && !generatorModes[index].InToolBox);
            if (index < 0)
                return false;
            ChiselGeneratorManager.GeneratorMode = generatorModes[index];
            return true;
        }

        public static bool NextGeneratorMode()
        {
            var currentGeneratorMode = ChiselGeneratorManager.GeneratorMode;
            var generatorModes = ChiselGeneratorManager.generatorModes;
            var index = currentGeneratorMode == null ? generatorModes.Length - 1 : ArrayUtility.IndexOf(generatorModes, currentGeneratorMode);
            do { index++; } while (index < generatorModes.Length && !generatorModes[index].InToolBox);
            if (index >= generatorModes.Length)
                return false;
            ChiselGeneratorManager.GeneratorMode = generatorModes[index];
            return true;
        }

        static void GeneratorButton(Rect position, ChiselGeneratorMode generator, GUIStyle style, bool isActive)
        {
            EditorGUI.BeginChangeCheck();
            var content     = generator.Content;
            var isSelected  = ChiselGeneratorManager.GeneratorMode == generator;
            var value       = ToggleButton(position, content, isSelected && isActive, style, isActive);
            if (EditorGUI.EndChangeCheck())
            {
                if (Event.current.button == 2)
                {
                    ChiselPlacementToolsSelectionWindow.SetInToolBox(generator, false);
                } else
                { 
                    ChiselCreateTool.ActivateTool();
                    ChiselGeneratorManager.GeneratorMode = generator;
                    if (value)
                        ChiselEditorSettings.Save();
                    SceneView.RepaintAll();
                }
            }
        }

        class Styles
        {
            public GUIStyle namedToggleStyle;
            public GUIStyle toggleStyle;
            public GUIStyle addStyle;
            public GUIStyle groupTitleStyle;
        }

        static Styles styles = null;
        static void InitStyles()
        {
            if (styles == null)
            {
                ChiselEditorSettings.Load();
                styles = new Styles
                {
                    namedToggleStyle = new GUIStyle(GUI.skin.button)
                    {
                        alignment   = TextAnchor.MiddleLeft,
                        fixedHeight = kSingleLineHeight - 2,
                        padding     = new RectOffset(2, 2, 2, 2)
                    },
                    toggleStyle = new GUIStyle(GUI.skin.button)
                    {
                        fixedWidth  = kToolBarButtonSize,
                        fixedHeight = kToolBarButtonSize,
                        padding     = new RectOffset(2, 2, 2, 2)
                    },
                    addStyle = new GUIStyle(GUI.skin.button)
                    {
                        fixedWidth  = kToolBarButtonSize - 8,
                        fixedHeight = kToolBarButtonSize - 8,
                        padding     = new RectOffset(2, 2, 1, 3),
                        margin      = new RectOffset(7, 7, 6, 6)
                    },
                    groupTitleStyle =  new GUIStyle(EditorStyles.boldLabel)
                };
            }
        }

        Vector2 scrollPosition = Vector2.zero;

        public void OnGUI()
        {
            // TODO: add search functionality
            // TODO: add automatic finding node based generators in project

            InitStyles();
            var generatorModes  = ChiselGeneratorManager.generatorModes;
            var isActive        = ChiselCreateTool.IsActive();

            float height = 0;
            var previousGroup = string.Empty;
            for (int i = 0; i < generatorModes.Length; i++)
            {
                var generatorMode = generatorModes[i];
                if (previousGroup != generatorMode.Group)
                {
                    height += kSingleLineHeight + kSingleSpacing;
                    previousGroup = generatorMode.Group;
                }
                height += kSingleLineHeight + kSingleSpacing;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            // TODO: figure out how to do this without needing layout stuff?
            EditorGUILayout.GetControlRect(false, height);

            var togglePosition  = new Rect(0,0, ChiselEditorUtility.ContextWidth, kSingleLineHeight);
            var style           = styles.namedToggleStyle;
            previousGroup   = string.Empty;
            for (int i = 0; i < generatorModes.Length; i++)
            {
                var generatorMode = generatorModes[i];
                if (previousGroup != generatorMode.Group)
                {
                    EditorGUI.LabelField(togglePosition, generatorMode.Group, styles.groupTitleStyle);
                    togglePosition.y += kSingleLineHeight + kSingleSpacing;
                    previousGroup = generatorMode.Group;
                }
                NamedGeneratorButton(generatorModes[i], togglePosition, style, isActive);
                togglePosition.y += kSingleLineHeight + kSingleSpacing;
            }
            EditorGUILayout.EndScrollView();
        }

        public static readonly GUILayoutOption kMinInnerWidthLayout = GUILayout.MinWidth(ChiselOverlay.kMinWidth - 8);

        const int kToolsWide = 8;

        public static void RenderCreationTools()
        {
            InitStyles();

            var generatorModes  = ChiselGeneratorManager.generatorModes;
            var isActive        = ChiselCreateTool.IsActive();

            var style = styles.toggleStyle;

            int usedModes = 0;
            for (int i = 0; i < generatorModes.Length; i++)
            {
                if (!generatorModes[i].InToolBox &&
                    ChiselGeneratorManager.GeneratorMode != generatorModes[i])
                    continue;
                usedModes++;
            }

            int rows = Mathf.CeilToInt((usedModes + 1) / (float)kToolsWide);
            var boxStyle = GUI.skin.box;
            var groupRect = EditorGUILayout.GetControlRect(false, (rows * style.fixedHeight) + boxStyle.margin.vertical, ChiselOverlay.kMinWidthLayout);

            if (Event.current.type == EventType.Repaint)
                boxStyle.Draw(groupRect, false, false, false, false);
            
            var topX            = groupRect.x;
            var topY            = groupRect.y;


            var leftMargin      = style.margin.left + topX;
            var topMargin       = style.margin.top  + topY;
            var buttonWidth     = style.fixedWidth  + style.margin.left;
            var buttonHeight    = style.fixedHeight + style.margin.top;
            var position        = new Rect(0, 0, buttonWidth, buttonHeight);

            int xpos = 0, ypos = 0;
            for (int i = 0; i < generatorModes.Length; i++)
            {
                if (!generatorModes[i].InToolBox && 
                    ChiselGeneratorManager.GeneratorMode != generatorModes[i])
                    continue;
                if (xpos >= kToolsWide) { ypos++; xpos = 0; }
                position.x = leftMargin + xpos * buttonWidth;
                position.y = topMargin  + ypos * buttonHeight;
                GeneratorButton(position, generatorModes[i], style, isActive);
                xpos++;
            }
            if (xpos >= kToolsWide) { ypos++; xpos = 0; }
            {
                position.x = styles.addStyle.margin.left + topX + xpos * buttonWidth;
                position.y = styles.addStyle.margin.top  + topY + ypos * buttonHeight;
                if (GUI.Button(position, "+", styles.addStyle))
                    Create();
            }
        }
    }
}
