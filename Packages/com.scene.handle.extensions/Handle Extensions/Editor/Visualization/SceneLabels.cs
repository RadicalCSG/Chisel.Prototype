using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnitySceneExtensions
{
    public partial class SceneHandles
    {
        struct StyleLookup : IEqualityComparer<StyleLookup>, IEquatable<StyleLookup>
        {
            public int          padding;
            public int          fontSize;
            public FontStyle    fontStyle;
            public Color        color;

            public bool Equals(StyleLookup x, StyleLookup y) { return x.padding == y.padding && x.fontSize == y.fontSize && x.fontStyle == y.fontStyle && x.color == y.color; }
            public bool Equals(StyleLookup other) { return Equals(this, other); }

            public int GetHashCode(StyleLookup obj)
            {
                return  (padding.GetHashCode() + fontSize.GetHashCode() + fontStyle.GetHashCode() + color.GetHashCode()) ^ 
                        (padding.GetHashCode() * fontSize.GetHashCode() * fontStyle.GetHashCode() * color.GetHashCode());
            }
        }

        static Dictionary<StyleLookup, GUIStyle> labelColorStyle = new Dictionary<StyleLookup, GUIStyle>();

        static GUIStyle GetLabelStyle(Color color, int padding, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal)
        {
            var lookup = new StyleLookup() { padding = padding, color = color, fontSize = fontSize, fontStyle = fontStyle };
            if (labelColorStyle.TryGetValue(lookup, out GUIStyle style))
                return style;

            style = new UnityEngine.GUIStyle
            {
                alignment   = UnityEngine.TextAnchor.UpperLeft,
                fontSize    = fontSize,

                // some eyeballed offsets because CalcSize returns a non-centered rect
                padding = new RectOffset
                {
                    left    = 4 + padding,
                    right   = padding,
                    top     = 1 + padding,
                    bottom  = 4 + padding
                },
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor  = SceneHandles.color;

            labelColorStyle[lookup] = style;
            return style;
        }

        public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, string text, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var style = GetLabelStyle(SceneHandles.color, padding, fontSize, fontStyle);
            return DrawLabel(position, alignmentDirection, new GUIContent(text), style);
        }

        public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var style = GetLabelStyle(SceneHandles.color, padding, fontSize, fontStyle);
            return DrawLabel(position, alignmentDirection, content, style);
        }

        static Rect GetLabelRect(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, GUIStyle style)
        {
            var size		= style.CalcSize(content);
            var halfSize	= size * 0.5f;
            var screenpos	= UnityEditor.HandleUtility.WorldToGUIPoint(position);
            var screendir	= (UnityEditor.HandleUtility.WorldToGUIPoint(position + alignmentDirection) - screenpos).normalized;

            // align on the rect around the text in the direction of alignmentDirection
            screenpos.x += (screendir.x - 1) * halfSize.x;
            screenpos.y += (screendir.y - 1) * halfSize.y;

            return new Rect(screenpos.x, screenpos.y, size.x, size.y);
        }

        static Rect emptyRect = new Rect();

        public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint)
            { 
                var matrix  = SceneHandles.matrix;
                var pt      = UnityEngine.Camera.current.WorldToViewportPoint(matrix.MultiplyPoint(position));

                // cull if behind camera
                if (pt.z < 0)
                    return emptyRect;

                var rect = GetLabelRect(position, alignmentDirection, content, style);
                SceneHandles.BeginGUI();
                {
                    GUI.Label(rect, content, style);
                }
                SceneHandles.EndGUI();
                return rect;
            } else
            if (Event.current.type == EventType.Layout)
                return GetLabelRect(position, alignmentDirection, content, style);
            return emptyRect;
        }


        public static bool ClickableLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, string text, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var style = GetLabelStyle(SceneHandles.color, padding, fontSize, fontStyle);
            return ClickableLabel(position, alignmentDirection, new GUIContent(text), style);
        }

        public static bool ClickableLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, int fontSize = 11, FontStyle fontStyle = FontStyle.Normal, int padding = 4)
        {
            var style = GetLabelStyle(SceneHandles.color, padding, fontSize, fontStyle);
            return ClickableLabel(position, alignmentDirection, content, style);
        }

        static bool canClick = false;
        static readonly int s_ClickableLabelHash = "ClickableLabel".GetHashCode();
        public static bool ClickableLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, GUIContent content, GUIStyle style)
        {
            var rect = DrawLabel(position, alignmentDirection, content, style);
            if (SceneHandles.disabled)
                return false;

            var id      = GUIUtility.GetControlID(s_ClickableLabelHash, FocusType.Keyboard);
            var evt     = Event.current;
            var type    = evt.GetTypeForControl(id);
            switch (type)
            {
                case EventType.Layout:
                {
                    if (InCameraOrbitMode)
                        break;

                    if (rect.Contains(Event.current.mousePosition))
                        UnityEditor.HandleUtility.AddControl(id, 3);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (InCameraOrbitMode)
                        break;

                    if (GUIUtility.hotControl != 0)
                        break;

                    if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                        (GUIUtility.keyboardControl != id || evt.button != 2))
                        break;

                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    evt.Use();
                    UnityEditor.EditorGUIUtility.SetWantsMouseJumping(1);
                    canClick = true;
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
                        break;

                    canClick = false;
                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id || (evt.button != 0 && evt.button != 2))
                        break;

                    GUIUtility.hotControl = 0;
                    GUIUtility.keyboardControl = 0;
                    evt.Use();
                    UnityEditor.EditorGUIUtility.SetWantsMouseJumping(0);
                    var result = canClick;
                    canClick = false;
                    return result;
                }
            }
            return false;
        }
    }
}
