using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;
using System.Reflection;

namespace Chisel.Editors
{
    // TODO: add tooltips
    public static class ChiselSnappingOptionsOverlay
    {
        const int kPrimaryOrder = 99;

        const string kOverlayTitle = "Snapping";
        static readonly ChiselOverlay kOverlay = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);


        static readonly GUIContent kDoubleRotateSnap  = new GUIContent(string.Empty, "Double the rotation snap angle");
        static readonly GUIContent kHalfRotateSnap    = new GUIContent(string.Empty, "Half the rotation snap angle");
        
        static readonly GUIContent kDoubleScaleSnap   = new GUIContent(string.Empty, "Double the scale snap percentage");
        static readonly GUIContent kHalfScaleSnap     = new GUIContent(string.Empty, "Half the scale snap percentage");

        static readonly GUIContent kDoubleGridSize    = new GUIContent(string.Empty, "Double the grid size");
        static readonly GUIContent kHalfGridSize      = new GUIContent(string.Empty, "Half the grid size");


        const float             kSmallButtonWidth       = 30;
        static GUILayoutOption  kSmallButtonWidthOption = GUILayout.Width(kSmallButtonWidth);
        
        const int kButtonSize = 32 + (kButtonPadding * 2);
        const int kButtonMargin = 1;
        const int kButtonPadding = 2;
        const int kSmallButtonPadding = 0;            
        static GUILayoutOption  kToggleWidth            = GUILayout.Width(kButtonSize);
        class Styles
        {
            public GUIStyle toggleStyle;
            public GUIStyle toggleStyleLeft;
            public GUIStyle toggleStyleMid;
            public GUIStyle toggleStyleRight;
            public GUIStyle smallToggleStyle;

            public GUIStyle plus;
            public GUIStyle minus;

            public GUIContent[] boundsSnapIcons;
            public GUIContent[] pivotSnapIcons;
            public GUIContent[] edgeSnapIcons;
            public GUIContent[] vertexSnapIcons;
            public GUIContent[] surfaceSnapIcons;
            public GUIContent[] uvGridSnapIcons;
            public GUIContent[] uvEdgeSnapIcons;
            public GUIContent[] uvVertexSnapIcons;
            public GUIContent[] translateIcons;
            public GUIContent[] rotateIcons;
            public GUIContent[] scaleIcons;

            Dictionary<GUIStyle, GUIStyle> emptyCopies = new Dictionary<GUIStyle, GUIStyle>();

            public GUIStyle GetEmptyCopy(GUIStyle style)
            {
                if (emptyCopies.TryGetValue(style, out var emptyCopy))
                    return emptyCopy;

                emptyCopy = new GUIStyle(GUIStyle.none);
                emptyCopy.border = style.border;
                emptyCopy.margin = style.margin;
                emptyCopy.padding = style.padding;
                emptyCopy.alignment = style.alignment;
                emptyCopy.clipping = style.clipping;
                emptyCopy.contentOffset = style.contentOffset;
                emptyCopy.clipping = style.clipping;
                emptyCopy.fixedHeight = style.fixedHeight;
                emptyCopy.fixedWidth = style.fixedWidth;
                emptyCopy.font = style.font;
                emptyCopy.fontSize = style.fontSize;
                emptyCopy.fontStyle = style.fontStyle;
                emptyCopy.imagePosition = style.imagePosition;
                emptyCopy.richText = style.richText;
                emptyCopy.stretchHeight = style.stretchHeight;
                emptyCopy.stretchWidth = style.stretchWidth;
                emptyCopy.wordWrap = style.wordWrap;
                emptyCopy.active.textColor = style.active.textColor;
                emptyCopy.onActive.textColor = style.onActive.textColor;
                emptyCopy.focused.textColor = style.focused.textColor;
                emptyCopy.onFocused.textColor = style.onFocused.textColor;
                emptyCopy.hover.textColor = style.hover.textColor;
                emptyCopy.onHover.textColor = style.onHover.textColor;
                emptyCopy.normal.textColor = style.normal.textColor;
                emptyCopy.onNormal.textColor = style.onNormal.textColor;
                emptyCopies[style] = emptyCopy;
                return emptyCopy;
            }


            public Styles()
            {
                toggleStyle = new GUIStyle("AppCommand")
                {
                    padding = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
                toggleStyleLeft = new GUIStyle("AppCommandLeft")
                {
                    padding = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
                toggleStyleMid = new GUIStyle("AppCommandMid")
                {
                    padding = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };
                toggleStyleRight = new GUIStyle("AppCommandMid")
                {
                    padding = new RectOffset(kButtonPadding, kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding),
                    margin = new RectOffset(0, 0, kButtonMargin + 2, 0),
                    fixedWidth = kButtonSize + kButtonMargin,
                    fixedHeight = kButtonSize,
                };

                smallToggleStyle = new GUIStyle("AppCommand")
                {
                    padding = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding, 0, 0),
                    margin  = new RectOffset(0, 8, kButtonMargin + 1, 0),
                    fixedWidth = kButtonSize + kButtonMargin,
                    fixedHeight = 20
                };

                boundsSnapIcons     = ChiselEditorResources.GetIconContent("BoundsSnap", "Snap bounds against grid");
                pivotSnapIcons      = ChiselEditorResources.GetIconContent("PivotSnap", "Snap pivots against grid");
                edgeSnapIcons       = ChiselEditorResources.GetIconContent("EdgeSnap", "Snap against edges");
                vertexSnapIcons     = ChiselEditorResources.GetIconContent("VertexSnap", "Snap against vertices");
                surfaceSnapIcons    = ChiselEditorResources.GetIconContent("SurfaceSnap", "Snap against surfaces");

                uvGridSnapIcons     = ChiselEditorResources.GetIconContent("UVGridSnap", "Snap UV against grid");
                uvEdgeSnapIcons     = ChiselEditorResources.GetIconContent("UVEdgeSnap", "Snap UV against surface edges");
                uvVertexSnapIcons   = ChiselEditorResources.GetIconContent("UVVertexSnap", "Snap UV against surface vertices");

                translateIcons      = ChiselEditorResources.GetIconContent("moveTool", "Enable/Disable move snapping");
                rotateIcons         = ChiselEditorResources.GetIconContent("rotateTool", "Enable/Disable rotate snapping");
                scaleIcons          = ChiselEditorResources.GetIconContent("scaleTool", "Enable/Disable scale snapping");

                //ChiselEditorResources.debug = true;
                var plusIcons   = ChiselEditorResources.LoadIconImages("ol_plus");
                var minusIcons  = ChiselEditorResources.LoadIconImages("ol_minus");
                //ChiselEditorResources.debug = false;


                plus = new GUIStyle();
                plus.margin         = new RectOffset(0, 0, 2, 0);
                plus.padding        = new RectOffset();
                plus.fixedWidth     = 16;
                plus.fixedHeight    = 16;
                plus.normal.background      = plusIcons[0] as Texture2D;
                plus.onNormal.background    = plusIcons[0] as Texture2D;
                plus.active.background      = plusIcons[1] as Texture2D;
                plus.onActive.background    = plusIcons[1] as Texture2D;
                plus.focused.background     = plusIcons[0] as Texture2D;
                plus.onFocused.background   = plusIcons[1] as Texture2D;

                minus = new GUIStyle();
                minus.margin        = new RectOffset(0, 0, 2, 0);
                minus.padding       = new RectOffset();
                minus.fixedWidth    = 16;
                minus.fixedHeight   = 16;
                minus.normal.background     = minusIcons[0] as Texture2D;
                minus.onNormal.background   = minusIcons[0] as Texture2D;
                minus.active.background     = minusIcons[1] as Texture2D;
                minus.onActive.background   = minusIcons[1] as Texture2D;
                minus.focused.background    = minusIcons[0] as Texture2D;
                minus.onFocused.background  = minusIcons[1] as Texture2D;
            }
        }
        static Styles styles;

        internal static ReflectedField<TextEditor> s_RecycledEditor = typeof(EditorGUI).GetStaticField<TextEditor>("s_RecycledEditor");

        static readonly MethodInfo DoFloatFieldMethodInfo = typeof(EditorGUI).GetStaticMethod("DoFloatField", 8);

        static object[] s_FloatFieldArray = new object[]{
                null, null, null, null, null, kFloatFieldFormatString, null, null
        };
        static float DoFloatField(Rect position, Rect dragHotZone, int id, float value, GUIStyle style, bool draggable)
        {
            s_FloatFieldArray[0] = s_RecycledEditor.Value;
            s_FloatFieldArray[1] = position;
            s_FloatFieldArray[2] = dragHotZone;
            s_FloatFieldArray[3] = id;
            s_FloatFieldArray[4] = value;
            //s_FloatFieldArray[5] = kFloatFieldFormatString;
            s_FloatFieldArray[6] = style;
            s_FloatFieldArray[7] = draggable;
            return (float)DoFloatFieldMethodInfo.Invoke(null, s_FloatFieldArray);
        }

        
        public static float FloatField(Rect position, int id, float value, GUIStyle style)
        {
            return FloatFieldInternal(position, id, value, style);
        }

        private static readonly int s_FloatFieldHash = "EditorTextField".GetHashCode();
        const string kFloatFieldFormatString = "g7";
        internal static float FloatFieldInternal(Rect position, int id, float value, GUIStyle style)
        {
            return DoFloatField(EditorGUI.IndentedRect(position), new Rect(0, 0, 0, 0), id, value, style, false);
        }


        public delegate float ModifyValue();
        public static float PlusMinusFloatField(float value, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
        {
            Rect position = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2, EditorStyles.numberField);
            return PlusMinusFloatField(position, value, plusContent, minusContent, plus, minus);
        }

        public static float PlusMinusFloatField(float value, GUIStyle style, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
        {
            Rect position = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2, style);
            return PlusMinusFloatField(position, value, style, plusContent, minusContent, plus, minus);
        }

        public static float PlusMinusFloatField(Rect position, float value, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
        {
            return PlusMinusFloatField(position, value, EditorStyles.numberField, plusContent, minusContent, plus, minus);
        }

        static float PlusMinusFloatField(Rect position, float value, GUIStyle style, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
        {
            int id = GUIUtility.GetControlID(s_FloatFieldHash, FocusType.Keyboard, position);
            var emptyStyle = styles.GetEmptyCopy(style);
            if (Event.current.type == EventType.Repaint)
            {
                style.Draw(position, GUIContent.none, id, false, position.Contains(Event.current.mousePosition));
            }
                                
            var temp = position; 
            temp.xMax -= styles.plus.fixedWidth * 2;
            value = FloatField(temp, id, value, emptyStyle);
                                
            var buttonRect = position;
            buttonRect.xMin = buttonRect.xMax -= styles.plus.fixedWidth + 2;
            buttonRect.yMin += 2;
            buttonRect.width = styles.plus.fixedWidth;
            buttonRect.height = styles.plus.fixedHeight;
            if (GUI.Button(buttonRect, plusContent, styles.plus))
                value = plus();
            buttonRect.xMin -= styles.minus.fixedWidth - 2;
            if (GUI.Button(buttonRect, minusContent, styles.minus))
                value = minus();
            return value;
        }

        public static bool SmallToggleButton(bool value, GUIContent content)
        {
            var style = styles.smallToggleStyle;
            var rect = EditorGUILayout.GetControlRect(false, style.fixedHeight, style, kSmallButtonWidthOption);
            return SmallToggleButton(rect, value, content);
        }

        public static bool SmallToggleButton(bool value, GUIContent[] content)
        {
            var style = styles.smallToggleStyle;
            var rect = EditorGUILayout.GetControlRect(false, style.fixedHeight, style, kSmallButtonWidthOption);
            return SmallToggleButton(rect, value, content);
        }

        public static bool SmallToggleButton(Rect rect, bool value, GUIContent content)
        {
            return GUI.Toggle(rect, value, content, styles.smallToggleStyle);
        }

        public static bool SmallToggleButton(Rect rect, bool value, GUIContent[] content)
        {
            return GUI.Toggle(rect, value, value ? content[1] : content[0], styles.smallToggleStyle);
        }

        public static bool ToggleButton(bool value, SnapSettings active, SnapSettings flag, GUIContent content, GUIStyle style, GUILayoutOption option)
        {
            var rect = EditorGUILayout.GetControlRect(false, style.fixedHeight, style, option);
            return ToggleButton(rect, value, active, flag, content, style);
        }

        public static bool ToggleButton(bool value, SnapSettings active, SnapSettings flag, GUIContent[] content, GUIStyle style, GUILayoutOption option)
        {
            var rect = EditorGUILayout.GetControlRect(false, style.fixedHeight, style, option);
            return ToggleButton(rect, value, active, flag, content, style);
        }

        public static bool ToggleButton(Rect rect, bool value, SnapSettings active, SnapSettings flag, GUIContent content, GUIStyle style)
        {
            if ((active & flag) != flag)
            {
                using (var disableScope = new EditorGUI.DisabledScope(true))
                {
                    GUI.Toggle(rect, !ChiselEditorResources.isProSkin, content, style);
                }
            } else
                value = GUI.Toggle(rect, value, content, style);
            return value;
        }

        public static bool ToggleButton(Rect rect, bool value, SnapSettings active, SnapSettings flag, GUIContent[] content, GUIStyle style)
        {
            if ((active & flag) != flag)
            {
                using (var disableScope = new EditorGUI.DisabledScope(true))
                {
                    GUI.Toggle(rect, !ChiselEditorResources.isProSkin, content[0], style);
                }
            } else
                value = GUI.Toggle(rect, value, value ? content[1] : content[0], style);
            return value;
        }


        static void DisplayControls(SceneView sceneView)
        {
            if (styles == null)
                styles = new Styles();
            EditorGUI.BeginChangeCheck();
            {
                // TODO: implement all snapping types
                // TODO: add units (EditorGUI.s_UnitString?)
                //ChiselEditorSettings.ShowGrid = GUILayout.Toggle(ChiselEditorSettings.ShowGrid, "Show Grid", EditorStyles.miniButton, kShowGridButtonWidthOption);

                var usedSnappingModes = CurrentSnapSettings();

                {
                    var buttonRect = EditorGUILayout.GetControlRect(false, styles.toggleStyleLeft.fixedHeight, styles.toggleStyleLeft, kToggleWidth);
                    buttonRect.x++;
                    Snapping.BoundsSnappingEnabled      = ToggleButton(buttonRect, Snapping.BoundsSnappingEnabled,   usedSnappingModes, SnapSettings.GeometryBoundsToGrid,  styles.boundsSnapIcons,   styles.toggleStyleLeft);
                    buttonRect.x += buttonRect.width;
                    Snapping.PivotSnappingEnabled       = ToggleButton(buttonRect, Snapping.PivotSnappingEnabled,    usedSnappingModes, SnapSettings.GeometryPivotToGrid,   styles.pivotSnapIcons,    styles.toggleStyleMid);
                    buttonRect.x += buttonRect.width;
                    Snapping.EdgeSnappingEnabled        = ToggleButton(buttonRect, Snapping.EdgeSnappingEnabled,     usedSnappingModes, SnapSettings.GeometryEdge,          styles.edgeSnapIcons,     styles.toggleStyleMid);
                    buttonRect.x += buttonRect.width;
                    Snapping.VertexSnappingEnabled      = ToggleButton(buttonRect, Snapping.VertexSnappingEnabled,   usedSnappingModes, SnapSettings.GeometryVertex,        styles.vertexSnapIcons,   styles.toggleStyleMid);
                    buttonRect.x += buttonRect.width;
                    Snapping.SurfaceSnappingEnabled     = ToggleButton(buttonRect, Snapping.SurfaceSnappingEnabled,  usedSnappingModes, SnapSettings.GeometrySurface,       styles.surfaceSnapIcons,  styles.toggleStyleMid);

                    buttonRect.x += buttonRect.width;
                    Snapping.UVGridSnappingEnabled      = ToggleButton(buttonRect, Snapping.UVGridSnappingEnabled,   usedSnappingModes, SnapSettings.UVGeometryGrid,        styles.uvGridSnapIcons,   styles.toggleStyleMid);
                    buttonRect.x += buttonRect.width;
                    Snapping.UVEdgeSnappingEnabled      = ToggleButton(buttonRect, Snapping.UVEdgeSnappingEnabled,   usedSnappingModes, SnapSettings.UVGeometryEdges,       styles.uvEdgeSnapIcons,   styles.toggleStyleMid);
                    buttonRect.x += buttonRect.width;
                    Snapping.UVVertexSnappingEnabled    = ToggleButton(buttonRect, Snapping.UVVertexSnappingEnabled, usedSnappingModes, SnapSettings.UVGeometryVertices,    styles.uvVertexSnapIcons, styles.toggleStyleRight);
                }
                
                EditorGUILayout.GetControlRect(false, 1);
                
                {
                    var lineRect = EditorGUILayout.GetControlRect(false, styles.smallToggleStyle.fixedHeight);
                    lineRect.xMin++;
                    lineRect.xMax--;
                    {
                        var buttonRect = lineRect;
                        buttonRect.width = kSmallButtonWidth + styles.smallToggleStyle.margin.horizontal;
                        Snapping.TranslateSnappingEnabled = SmallToggleButton(buttonRect, Snapping.TranslateSnappingEnabled, styles.translateIcons);
                        var floatRect = lineRect;
                        floatRect.x = buttonRect.xMax + 1;
                        floatRect.width -= buttonRect.width + 1;
                        ChiselEditorSettings.UniformSnapSize = PlusMinusFloatField(floatRect, ChiselEditorSettings.UniformSnapSize, kDoubleGridSize, kHalfGridSize, SnappingKeyboard.DoubleGridSizeRet, SnappingKeyboard.HalfGridSizeRet);
                    }
                }

                {
                    var lineRect = EditorGUILayout.GetControlRect(false, styles.smallToggleStyle.fixedHeight);
                    lineRect.xMin++;
                    lineRect.xMax--;

                    var buttonRect1 = lineRect;
                    var buttonRect2 = lineRect;
                    var floatRect1 = lineRect;
                    var floatRect2 = lineRect;
                    buttonRect1.width = kSmallButtonWidth + styles.smallToggleStyle.margin.horizontal;
                    buttonRect2.width = kSmallButtonWidth + styles.smallToggleStyle.margin.horizontal;

                    var floatLeftover     = lineRect.width - (buttonRect1.width + buttonRect2.width) - 2;
                    var halfFloatLeftover = Mathf.CeilToInt(floatLeftover / 2);
                    floatRect1.width = halfFloatLeftover - 1;
                    floatRect2.width = (floatLeftover - floatRect1.width) - 2;

                    floatRect1.x = buttonRect1.xMax + 1;
                    buttonRect2.x = floatRect1.xMax + 3;
                    floatRect2.x = buttonRect2.xMax;

                    {
                        Snapping.RotateSnappingEnabled = SmallToggleButton(buttonRect1, Snapping.RotateSnappingEnabled, styles.rotateIcons);
                        ChiselEditorSettings.RotateSnap = PlusMinusFloatField(floatRect1, ChiselEditorSettings.RotateSnap, kDoubleRotateSnap, kHalfRotateSnap, SnappingKeyboard.DoubleRotateSnapRet, SnappingKeyboard.HalfRotateSnapRet);
                    }
                    {
                        Snapping.ScaleSnappingEnabled = SmallToggleButton(buttonRect2, Snapping.ScaleSnappingEnabled, styles.scaleIcons);
                        ChiselEditorSettings.ScaleSnap = PlusMinusFloatField(floatRect2, ChiselEditorSettings.ScaleSnap, kDoubleScaleSnap, kHalfScaleSnap, SnappingKeyboard.DoubleScaleSnapRet, SnappingKeyboard.HalfScaleSnapRet);
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }

        static SnapSettings CurrentSnapSettings()
        {
            switch (Tools.current)
            {
                case Tool.Move:         return SnapSettings.AllGeometry;
                case Tool.Transform:    return SnapSettings.AllGeometry;
                case Tool.Rotate:       return SnapSettings.None;
                case Tool.Rect:         return SnapSettings.None;
                case Tool.Scale:        return SnapSettings.None;
                case Tool.Custom:       return Snapping.SnapMask;
            }
            return SnapSettings.None;
        }

        static bool IsValidTool()
        {
            if (Tools.current == Tool.None)
                return false;
            return true;
        }

        public static void Show()
        {
            if (IsValidTool())
                kOverlay.Show();
        }
    }
}
