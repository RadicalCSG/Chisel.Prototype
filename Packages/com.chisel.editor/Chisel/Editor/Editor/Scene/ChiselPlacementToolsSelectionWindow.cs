using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    public class ChiselPlacementToolsSelectionWindow : EditorWindow
    {
        const float kSingleLineHeight   = 28f;
        const float kSingleSpacing      = 0.0f;
        
        public const int   kButtonSize      = 32 + (kButtonPadding * 2);
        public const int   kButtonMargin    = 1;
        public const int   kButtonPadding   = 2;


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
            ToolManager.activeToolChanged -= EditModeSelectionChanged;
            ToolManager.activeToolChanged += EditModeSelectionChanged;
        }

        private void OnDisable()
        {
            ChiselGeneratorManager.GeneratorSelectionChanged -= GeneratorSelectionChanged;
            ToolManager.activeToolChanged -= EditModeSelectionChanged;
        }

        public void EditModeSelectionChanged()
        {
            Repaint();
        }

        public void GeneratorSelectionChanged(ChiselPlacementToolInstance prevGenerator, ChiselPlacementToolInstance nextGenerator)
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
                

        static void NamedGeneratorButton(ChiselPlacementToolInstance generator, Rect togglePosition, GUIStyle style, bool isActive)
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
                    ChiselPlacementTool.ActivateTool();
                    ChiselGeneratorManager.GeneratorMode = generator;
                    ChiselEditorSettings.Save();
                    SceneView.RepaintAll();
                }
            }
        }

        // TODO: move somewhere else
        public static void SetInToolBox(ChiselPlacementToolInstance generator, bool value)
        {
            // TODO: make undoable
            generator.InToolBox = value;
            if (ChiselGeneratorManager.GeneratorMode == generator)
            {
                if (!DeselectGeneratorMode())
                    generator.InToolBox = true;
            }
            ChiselEditorSettings.Save();
            if (Event.current.type != EventType.Repaint)
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

        static void GeneratorButton(Rect position, ChiselPlacementToolInstance generator, GUIStyle style, bool isActive)
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
                    if (value)
                    {
                        ChiselPlacementTool.ActivateTool();
                        ChiselGeneratorManager.GeneratorMode = generator;
                        ToolManager.RefreshAvailableTools();
                        ChiselEditorSettings.Save();
                    }
                    SceneView.RepaintAll();
                }
            }
        }

        class Styles
        {
            public GUIStyle namedToggleStyle;
            public GUIStyle toggleStyleLeft;
            public GUIStyle toggleStyleMid;
            public GUIStyle toggleStyleRight;
            public GUIStyle addStyle;
            public GUIStyle groupTitleStyle;
            public GUIStyle boxStyle;
            public float boxStyleVerticalMargin;
        }

        static Styles styles = null;
        static void InitStyles()
        {
            if (styles == null)
            {
                ChiselEditorSettings.Load();
                styles = new Styles
                {
                    boxStyle  = new GUIStyle(GUI.skin.box),
                    namedToggleStyle = new GUIStyle(GUI.skin.button)
                    {
                        alignment   = TextAnchor.MiddleLeft,
                        fixedHeight = kSingleLineHeight - 2,
                        padding     = new RectOffset(kButtonPadding, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(kButtonMargin, kButtonMargin, kButtonMargin, kButtonMargin)
                    },
                    toggleStyleLeft = new GUIStyle(EditorStyles.miniButtonLeft)
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    toggleStyleMid = new GUIStyle(EditorStyles.miniButtonMid)
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    toggleStyleRight = new GUIStyle(EditorStyles.miniButtonRight)
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    addStyle = new GUIStyle(GUI.skin.button)
                    {
                        fixedWidth  = kButtonSize,
                        fixedHeight = kButtonSize,
                        padding     = new RectOffset(kButtonPadding, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(kButtonMargin, kButtonMargin, kButtonMargin, kButtonMargin)
                    },
                    groupTitleStyle =  new GUIStyle(EditorStyles.boldLabel)
                };
                styles.boxStyleVerticalMargin = styles.boxStyle.margin.vertical;
            }
        }

        Vector2 scrollPosition = Vector2.zero;

        public void OnGUI()
        {
            // TODO: add search functionality
            // TODO: add automatic finding node based generators in project

            InitStyles();
            var generatorModes  = ChiselGeneratorManager.generatorModes;
            var isActive        = ChiselPlacementTool.IsActive();

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
        
        // TODO: CLEAN THIS UP
        public const int kMinWidth = ((248 + 32) - ((32 + 2) * ChiselPlacementToolsSelectionWindow.kToolsWide)) + (ChiselPlacementToolsSelectionWindow.kButtonSize * ChiselPlacementToolsSelectionWindow.kToolsWide);
        public static readonly GUILayoutOption kMinWidthLayout = GUILayout.MinWidth(kMinWidth);
        public static readonly GUILayoutOption kMinInnerWidthLayout = GUILayout.MinWidth(kMinWidth - 8);

        public const int kToolsWide = 8;


        const string kAddIconName = "Add";
        const string kAddTooltip = "Add or remove generators";
        static GUIContent kAddButton;
        [InitializeOnLoadMethod]
        internal static void InitializeIcons()
        {
            kAddButton = ChiselEditorResources.GetIconContent(kAddIconName, kAddTooltip)[0];
        }


        public static void RenderGeneratorTools()
        {
            InitStyles();

            var generatorModes  = ChiselGeneratorManager.generatorModes;
            var isActive        = ChiselPlacementTool.IsActive();

            var style           = styles.toggleStyleMid;

            int usedModes = 0;
            for (int i = 0; i < generatorModes.Length; i++)
            {
                if (!generatorModes[i].InToolBox &&
                    ChiselGeneratorManager.GeneratorMode != generatorModes[i])
                    continue;
                usedModes++;
            }

            int rows = Mathf.CeilToInt((usedModes + 1) / (float)kToolsWide);
            var groupRect = EditorGUILayout.GetControlRect(false, (rows * style.fixedHeight) + styles.boxStyleVerticalMargin, kMinWidthLayout);
            groupRect.xMin -= 2;
            groupRect.yMin += 5;
            groupRect.xMax += 3;
            groupRect.yMax += 6;

            if (Event.current.type == EventType.Repaint)
            {
                var oldColor = GUI.color;
                var newColor = Color.black;
                newColor.a = 0.5f;
                GUI.color = newColor;
                GUI.DrawTexture(new Rect(groupRect.x, groupRect.y - 1, groupRect.width, 1), Texture2D.whiteTexture);
                GUI.color = oldColor;

                styles.boxStyle.Draw(groupRect, false, false, false, false);
                styles.boxStyle.Draw(groupRect, false, false, false, false);
            }

            var topX            = groupRect.x + 3;
            var topY            = groupRect.y + 3;


            var leftMargin      = style.margin.left + topX + 1;
            var topMargin       = style.margin.top  + topY;
            var buttonWidth     = style.fixedWidth  + style.margin.horizontal - 1;
            var buttonHeight    = style.fixedHeight + style.margin.vertical;
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


                var toggleStyle = (xpos == 0) ? styles.toggleStyleLeft :
                                  (xpos == kToolsWide - 1) || (i == generatorModes.Length - 1) ? styles.toggleStyleRight :
                                  styles.toggleStyleMid;

                GeneratorButton(position, generatorModes[i], toggleStyle, isActive);
                xpos++;
            }
            if (xpos >= kToolsWide) { ypos++; xpos = 0; }
            {
                var addStyle = styles.addStyle;
                position.x = addStyle.margin.left + topX + xpos * buttonWidth;
                position.y = addStyle.margin.top  + topY + ypos * buttonHeight;
                if (GUI.Button(position, kAddButton, addStyle))
                    Create();
            }
        }
    }
}
