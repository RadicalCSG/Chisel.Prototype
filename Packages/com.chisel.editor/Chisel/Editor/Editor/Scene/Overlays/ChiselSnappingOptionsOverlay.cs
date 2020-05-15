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


        static GUILayoutOption kSmallButtonWidthOption = GUILayout.Width(30);
        class Styles
        {
            public const int kButtonSize = 32 + (kButtonPadding * 2);
            public const int kButtonMargin = 1;
            public const int kButtonPadding = 2;
            public const int kSmallButtonPadding = 0;
            
            public GUIStyle toggleStyle;
            public GUIStyle toggleStyleLeft;
            public GUIStyle toggleStyleMid;
            public GUIStyle toggleStyleRight;
            public GUIStyle smallToggleStyle;

            public GUIStyle plus;
            public GUIStyle minus;

            GUIContent[] boundsSnapIcons;
            GUIContent[] pivotSnapIcons;
            GUIContent[] edgeSnapIcons;
            GUIContent[] vertexSnapIcons;
            GUIContent[] surfaceSnapIcons;
            GUIContent[] uvGridSnapIcons;
            GUIContent[] uvEdgeSnapIcons;
            GUIContent[] uvVertexSnapIcons;
            GUIContent[] translateIcons;
            GUIContent[] rotateIcons;
            GUIContent[] scaleIcons;

            public GUIContent boundsSnapIcon { get { return boundsSnapIcons[0]; } }
            public GUIContent pivotSnapIcon { get { return pivotSnapIcons[0]; } }
            public GUIContent edgeSnapIcon { get { return edgeSnapIcons[0]; } }
            public GUIContent vertexSnapIcon { get { return vertexSnapIcons[0]; } }
            public GUIContent surfaceSnapIcon { get { return surfaceSnapIcons[0]; } }
            public GUIContent uvGridSnapIcon { get { return uvGridSnapIcons[0]; } }
            public GUIContent uvEdgeSnapIcon { get { return uvEdgeSnapIcons[0]; } }
            public GUIContent uvVertexSnapIcon { get { return uvVertexSnapIcons[0]; } }

            public GUIContent translateIcon { get { return translateIcons[0]; } }
            public GUIContent rotateIcon { get { return rotateIcons[0]; } }
            public GUIContent scaleIcon { get { return scaleIcons[0]; } }

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

                boundsSnapIcons = ChiselEditorResources.GetIconContent("BoundsSnap", "Snap bounds against grid");
                pivotSnapIcons = ChiselEditorResources.GetIconContent("PivotSnap", "Snap pivots against grid");
                edgeSnapIcons = ChiselEditorResources.GetIconContent("EdgeSnap", "Snap against edges");
                vertexSnapIcons = ChiselEditorResources.GetIconContent("VertexSnap", "Snap against vertices");
                surfaceSnapIcons = ChiselEditorResources.GetIconContent("SurfaceSnap", "Snap against surfaces");

                uvGridSnapIcons = ChiselEditorResources.GetIconContent("UVGridSnap", "Snap UV against grid");
                uvEdgeSnapIcons = ChiselEditorResources.GetIconContent("UVEdgeSnap", "Snap UV against surface edges");
                uvVertexSnapIcons = ChiselEditorResources.GetIconContent("UVVertexSnap", "Snap UV against surface vertices");

                translateIcons = ChiselEditorResources.GetIconContent("moveTool", "Enable/Disable move snapping");
                rotateIcons = ChiselEditorResources.GetIconContent("rotateTool", "Enable/Disable rotate snapping");
                scaleIcons  = ChiselEditorResources.GetIconContent("scaleTool", "Enable/Disable scale snapping");

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

        static MethodInfo DoFloatFieldMethodInfo = typeof(EditorGUI).GetStaticMethod("DoFloatField", 8);

        static float DoFloatField(Rect position, Rect dragHotZone, int id, float value, GUIStyle style, bool draggable)
        {
            const string kFloatFieldFormatString = "g7";

            var editor = s_RecycledEditor.Value;
            return (float)DoFloatFieldMethodInfo.Invoke(null, new object[]{
                editor, position, dragHotZone, id, value, kFloatFieldFormatString, style, draggable
            });
        }

        
        public static float FloatField(Rect position, int id, float value, GUIStyle style)
        {
            return FloatFieldInternal(position, id, value, style);
        }

        private static readonly int s_FloatFieldHash = "EditorTextField".GetHashCode();
        internal static string kFloatFieldFormatString = "g7";
        internal static float FloatFieldInternal(Rect position, int id, float value, GUIStyle style)
        {
            return DoFloatField(EditorGUI.IndentedRect(position), new Rect(0, 0, 0, 0), id, value, style, false);
        }


        public delegate float ModifyValue();
        public static float PlusMinusFloatField(float value, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
        {
            Rect position = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2, EditorStyles.numberField);
            return PlusMinusFloatField(position, value, EditorStyles.numberField, plusContent, minusContent, plus, minus);
        }
        public static float PlusMinusFloatField(float value, GUIStyle style, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
        {
            Rect position = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2, style);
            return PlusMinusFloatField(position, value, style, plusContent, minusContent, plus, minus);
        }

        public static float PlusMinusFloatField(Rect position, float value, GUIStyle style, GUIContent plusContent, GUIContent minusContent, ModifyValue plus, ModifyValue minus)
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
            return GUILayout.Toggle(value, content, styles.smallToggleStyle, kSmallButtonWidthOption);
        }


        public static bool ToggleButton(bool value, SnapSettings active, SnapSettings flag, GUIContent content, GUIStyle style)
        {
            if ((active & flag) != flag)
            {
                using (var disableScope = new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Toggle(!ChiselEditorResources.isProSkin, content, style);
                }
            } else
                value = GUILayout.Toggle(value, content, style);
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

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(1);
                    Snapping.BoundsSnappingEnabled      = ToggleButton(Snapping.BoundsSnappingEnabled,   usedSnappingModes, SnapSettings.GeometryBoundsToGrid,  styles.boundsSnapIcon,   styles.toggleStyleMid);
                    Snapping.PivotSnappingEnabled       = ToggleButton(Snapping.PivotSnappingEnabled,    usedSnappingModes, SnapSettings.GeometryPivotToGrid,   styles.pivotSnapIcon,    styles.toggleStyleLeft);
                    Snapping.EdgeSnappingEnabled        = ToggleButton(Snapping.EdgeSnappingEnabled,     usedSnappingModes, SnapSettings.GeometryEdge,          styles.edgeSnapIcon,     styles.toggleStyleMid);
                    Snapping.VertexSnappingEnabled      = ToggleButton(Snapping.VertexSnappingEnabled,   usedSnappingModes, SnapSettings.GeometryVertex,        styles.vertexSnapIcon,   styles.toggleStyleMid);
                    Snapping.SurfaceSnappingEnabled     = ToggleButton(Snapping.SurfaceSnappingEnabled,  usedSnappingModes, SnapSettings.GeometrySurface,       styles.surfaceSnapIcon,  styles.toggleStyleMid);
                    
                    Snapping.UVGridSnappingEnabled      = ToggleButton(Snapping.UVGridSnappingEnabled,   usedSnappingModes, SnapSettings.UVGeometryGrid,        styles.uvGridSnapIcon,   styles.toggleStyleMid);
                    Snapping.UVEdgeSnappingEnabled      = ToggleButton(Snapping.UVEdgeSnappingEnabled,   usedSnappingModes, SnapSettings.UVGeometryEdges,       styles.uvEdgeSnapIcon,   styles.toggleStyleMid);
                    Snapping.UVVertexSnappingEnabled    = ToggleButton(Snapping.UVVertexSnappingEnabled, usedSnappingModes, SnapSettings.UVGeometryVertices,    styles.uvVertexSnapIcon, styles.toggleStyleRight);
                    GUILayout.Space(1);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(1);
                    {
                        Snapping.TranslateSnappingEnabled = SmallToggleButton(Snapping.TranslateSnappingEnabled, styles.translateIcon);
                        ChiselEditorSettings.UniformSnapSize = PlusMinusFloatField(ChiselEditorSettings.UniformSnapSize, kDoubleGridSize, kHalfGridSize, SnappingKeyboard.DoubleGridSizeRet, SnappingKeyboard.HalfGridSizeRet);
                    }
                    GUILayout.Space(1);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space(1);
                    {
                        Snapping.RotateSnappingEnabled = SmallToggleButton(Snapping.RotateSnappingEnabled, styles.rotateIcon);
                        ChiselEditorSettings.RotateSnap = PlusMinusFloatField(ChiselEditorSettings.RotateSnap, kDoubleRotateSnap, kHalfRotateSnap, SnappingKeyboard.DoubleRotateSnapRet, SnappingKeyboard.HalfRotateSnapRet);
                    }
                    {
                        Snapping.ScaleSnappingEnabled = SmallToggleButton(Snapping.ScaleSnappingEnabled, styles.scaleIcon);
                        ChiselEditorSettings.ScaleSnap = PlusMinusFloatField(ChiselEditorSettings.ScaleSnap, kDoubleScaleSnap, kHalfScaleSnap, SnappingKeyboard.DoubleScaleSnapRet, SnappingKeyboard.HalfScaleSnapRet);
                    }
                    GUILayout.Space(1);
                }
                GUILayout.EndHorizontal();
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
