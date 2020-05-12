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

namespace Chisel.Editors
{
    // TODO: add tooltips
    public static class ChiselSnappingOptionsOverlay
    {
        const int kPrimaryOrder = 99;

        const string                    kOverlayTitle               = "Snapping";
        static readonly ChiselOverlay   kOverlay                    = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);

        static readonly GUIContent      kGridLabel                  = EditorGUIUtility.TrTextContent("Grid");
        static readonly GUIContent      kSnapLabel                  = EditorGUIUtility.TrTextContent("Snap");


        static readonly GUIContent      kDoubleSnapDistanceButton   = EditorGUIUtility.TrTextContent("+", "Double the snapping distance.\nHotkey: ]");
        static readonly GUIContent      kHalveSnapDistanceButton    = EditorGUIUtility.TrTextContent("-", "Halve the snapping distance.\nHotkey: [");
        
        static readonly GUIContent      kSnapBoundsButton           = EditorGUIUtility.TrTextContent("Bounds",      "Snap to the grid with the bounds of the generator");
        static readonly GUIContent      kSnapPivotButton            = EditorGUIUtility.TrTextContent("Pivot",       "Snap to the grid with the pivot of the generator");
        static readonly GUIContent      kSnapEdgeButton             = EditorGUIUtility.TrTextContent("Edge",        "Snap to the edge of brushes");
        static readonly GUIContent      kSnapVertexButton           = EditorGUIUtility.TrTextContent("Vertex",      "Snap to vertices of brushes");
        static readonly GUIContent      kSnapSurfaceButton          = EditorGUIUtility.TrTextContent("Surface",     "Snap to surfaces of brushes");
        static readonly GUIContent      kSnapRotateButton           = EditorGUIUtility.TrTextContent("Rotation",    "Snap rotation");
        static readonly GUIContent      kSnapScaleButton            = EditorGUIUtility.TrTextContent("Scale",       "Snap scaling");
        
        static readonly GUIContent      kSnapUVGridButton           = EditorGUIUtility.TrTextContent("Grid",        "Snap to the grid");
        static readonly GUIContent      kSnapUVEdgeButton           = EditorGUIUtility.TrTextContent("Edge",        "Snap to the edge of surfaces");
        static readonly GUIContent      kSnapUVVertexButton         = EditorGUIUtility.TrTextContent("Vertex",      "Snap to vertices of surfaces");

        static GUILayoutOption  kSizeButtonWidthOption     = GUILayout.Width(16);
        static GUILayoutOption  kShowGridButtonWidthOption = GUILayout.Width(80);

        const float kLabelWidth = 40;
        static GUILayoutOption  kLabelWidthOption          = GUILayout.Width(kLabelWidth);

        class Styles
        {
            public GUIStyle miniButtonLeft;
            public GUIStyle miniButtonMid;
            public GUIStyle miniButtonRight;
            public Styles()
            {
                miniButtonLeft  = new GUIStyle(EditorStyles.miniButtonLeft);
                miniButtonLeft.margin.right += 4;

                miniButtonMid   = new GUIStyle(EditorStyles.miniButtonMid);
                miniButtonMid.margin.right += 4;

                miniButtonRight = new GUIStyle(EditorStyles.miniButtonRight);
            }
        }
        static Styles styles;


        static void DisplayControls(SceneView sceneView)
        {
            if (styles == null)
                styles = new Styles();
            EditorGUI.BeginChangeCheck();
            {
                GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);
                {
                    GUILayout.Label(kGridLabel, kLabelWidthOption);
                    ChiselEditorSettings.ShowGrid = GUILayout.Toggle(ChiselEditorSettings.ShowGrid, "Show Grid", EditorStyles.miniButton, kShowGridButtonWidthOption);
                    //using (var disableScope = new EditorGUI.DisabledScope(!ChiselEditorSettings.ShowGrid))
                    {
                        ChiselEditorSettings.UniformSnapSize = EditorGUILayout.FloatField(ChiselEditorSettings.UniformSnapSize);
                        if (GUILayout.Button(kHalveSnapDistanceButton, styles.miniButtonLeft, kSizeButtonWidthOption))
                        {
                            SnappingKeyboard.HalfGridSize();
                        }
                        if (GUILayout.Button(kDoubleSnapDistanceButton, styles.miniButtonRight, kSizeButtonWidthOption))
                        {
                            SnappingKeyboard.DoubleGridSize();
                        }
                    }
                }
                GUILayout.EndHorizontal();
                bool haveLabel = false;
                {
                    // TODO: show all "lines" for transform tool
                    // TODO: implement all snapping types
                    // TODO: add units
                    var toolType = CurrentToolType();
                    if (ChiselOptionsOverlay.ShowSnappingToolUV) // TODO: should be a mask
                    {
                        GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.UVGridSnappingEnabled   = GUILayout.Toggle(Snapping.UVGridSnappingEnabled,     kSnapUVGridButton,   styles.miniButtonLeft);
                        Snapping.UVEdgeSnappingEnabled   = GUILayout.Toggle(Snapping.UVEdgeSnappingEnabled,     kSnapUVEdgeButton,   styles.miniButtonMid);
                        Snapping.UVVertexSnappingEnabled = GUILayout.Toggle(Snapping.UVVertexSnappingEnabled,   kSnapUVVertexButton, styles.miniButtonRight);
                        GUILayout.EndHorizontal();
                    } else
                    if (toolType == Tool.Move || toolType == Tool.Transform)
                    {
                        GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.BoundsSnappingEnabled  = GUILayout.Toggle(Snapping.BoundsSnappingEnabled,  kSnapBoundsButton,  styles.miniButtonLeft);
                        Snapping.PivotSnappingEnabled   = GUILayout.Toggle(Snapping.PivotSnappingEnabled,   kSnapPivotButton,   styles.miniButtonMid);
                        Snapping.EdgeSnappingEnabled    = GUILayout.Toggle(Snapping.EdgeSnappingEnabled,    kSnapEdgeButton,    styles.miniButtonMid);
                        Snapping.VertexSnappingEnabled  = GUILayout.Toggle(Snapping.VertexSnappingEnabled,  kSnapVertexButton,  styles.miniButtonMid);
                        Snapping.SurfaceSnappingEnabled = GUILayout.Toggle(Snapping.SurfaceSnappingEnabled, kSnapSurfaceButton, styles.miniButtonRight);
                        GUILayout.EndHorizontal();
                    }
                    if (toolType == Tool.Rotate || toolType == Tool.Transform)
                    {
                        GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.RotateSnappingEnabled = GUILayout.Toggle(Snapping.RotateSnappingEnabled, kSnapRotateButton, EditorStyles.miniButton, kShowGridButtonWidthOption);
                        using (var disableScope = new EditorGUI.DisabledScope(!Snapping.RotateSnappingEnabled))
                        {
                            ChiselEditorSettings.RotateSnap = EditorGUILayout.FloatField(ChiselEditorSettings.RotateSnap);
                            if (GUILayout.Button(kHalveSnapDistanceButton, styles.miniButtonLeft, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.HalfRotateSnap();
                            }
                            if (GUILayout.Button(kDoubleSnapDistanceButton, styles.miniButtonRight, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.DoubleRotateSnap();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    if (toolType == Tool.Scale || toolType == Tool.Rect || toolType == Tool.Transform)
                    {
                        GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.ScaleSnappingEnabled = GUILayout.Toggle(Snapping.ScaleSnappingEnabled, kSnapScaleButton, EditorStyles.miniButton, kShowGridButtonWidthOption);
                        using (var disableScope = new EditorGUI.DisabledScope(!Snapping.ScaleSnappingEnabled))
                        {
                            ChiselEditorSettings.ScaleSnap = EditorGUILayout.FloatField(ChiselEditorSettings.ScaleSnap);
                            if (GUILayout.Button(kHalveSnapDistanceButton, styles.miniButtonLeft, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.HalfScaleSnap();
                            }
                            if (GUILayout.Button(kDoubleSnapDistanceButton, styles.miniButtonRight, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.DoubleScaleSnap();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }

        static Tool CurrentToolType()
        {
            switch (Tools.current)
            {
                case Tool.Move:
                case Tool.Rotate:
                case Tool.Rect:
                case Tool.Scale:
                case Tool.Transform:    ChiselOptionsOverlay.ShowSnappingToolUV = false; return Tools.current;
                case Tool.Custom:       return ChiselOptionsOverlay.ShowSnappingTool;
            }
            return Tool.None;
        }

        static bool IsValidTool()
        {
            if (CurrentToolType() == Tool.None)
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
