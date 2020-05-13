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
        
        static readonly GUIContent      kSnapRotateButton           = EditorGUIUtility.TrTextContent("Rotation",    "Snap rotation");
        static readonly GUIContent      kSnapScaleButton            = EditorGUIUtility.TrTextContent("Scale",       "Snap scaling");
        
        static readonly GUIContent      kSnapUVGridButton           = EditorGUIUtility.TrTextContent("Grid",        "Snap to the grid");
        static readonly GUIContent      kSnapUVEdgeButton           = EditorGUIUtility.TrTextContent("Edge",        "Snap to the edge of surfaces");
        static readonly GUIContent      kSnapUVVertexButton         = EditorGUIUtility.TrTextContent("Vertex",      "Snap to vertices of surfaces");

        static GUILayoutOption  kSizeButtonWidthOption     = GUILayout.Width(16);
        static GUILayoutOption  kShowGridButtonWidthOption = GUILayout.Width(80);

        const float kLabelWidth = 40;
        static GUILayoutOption  kLabelWidthOption          = GUILayout.Width(kLabelWidth);

        const int IconSize = 24;


        class Styles
        {
            public GUIStyle increaseButton;
            public GUIStyle decreaseButton;

            public GUIStyle miniButtonLeft;
            public GUIStyle miniButtonMid;
            public GUIStyle miniButtonRight;

            GUIContent[] boundsSnapIcons;
            GUIContent[] pivotSnapIcons;
            GUIContent[] edgeSnapIcons;
            GUIContent[] vertexSnapIcons;
            GUIContent[] surfaceSnapIcons;
            GUIContent[] uvGridSnapIcons;
            GUIContent[] uvEdgeSnapIcons;
            GUIContent[] uvVertexSnapIcons;

            public GUIContent boundsSnapIcon    { get { return EditorGUIUtility.isProSkin ? boundsSnapIcons[0] : boundsSnapIcons[1]; } }
            public GUIContent pivotSnapIcon     { get { return EditorGUIUtility.isProSkin ? pivotSnapIcons[0] : pivotSnapIcons[1]; } }
            public GUIContent edgeSnapIcon      { get { return EditorGUIUtility.isProSkin ? edgeSnapIcons[0] : edgeSnapIcons[1]; } }
            public GUIContent vertexSnapIcon    { get { return EditorGUIUtility.isProSkin ? vertexSnapIcons[0] : vertexSnapIcons[1]; } }
            public GUIContent surfaceSnapIcon   { get { return EditorGUIUtility.isProSkin ? surfaceSnapIcons[0] : surfaceSnapIcons[1]; } }
            public GUIContent uvGridSnapIcon    { get { return EditorGUIUtility.isProSkin ? uvGridSnapIcons[0] : uvGridSnapIcons[1]; } }
            public GUIContent uvEdgeSnapIcon    { get { return EditorGUIUtility.isProSkin ? uvEdgeSnapIcons[0] : uvEdgeSnapIcons[1]; } }
            public GUIContent uvVertexSnapIcon  { get { return EditorGUIUtility.isProSkin ? uvVertexSnapIcons[0] : uvVertexSnapIcons[1]; } }

            public Styles()
            {
                miniButtonLeft  = new GUIStyle(EditorStyles.miniButtonLeft);
                miniButtonLeft.margin.right += 4;
                increaseButton = new GUIStyle(miniButtonLeft);
                miniButtonLeft.fixedWidth = IconSize + miniButtonLeft.padding.horizontal;
                miniButtonLeft.fixedHeight = IconSize + miniButtonLeft.padding.vertical;

                miniButtonMid   = new GUIStyle(EditorStyles.miniButtonMid);
                miniButtonMid.margin.right += 4;
                miniButtonMid.fixedWidth = IconSize + miniButtonLeft.padding.horizontal;
                miniButtonMid.fixedHeight = IconSize + miniButtonMid.padding.vertical;

                miniButtonRight = new GUIStyle(EditorStyles.miniButtonRight);
                decreaseButton = new GUIStyle(miniButtonRight);
                miniButtonRight.fixedWidth = IconSize + miniButtonLeft.padding.horizontal;
                miniButtonRight.fixedHeight = IconSize + miniButtonRight.padding.vertical;
                 
                boundsSnapIcons     = ChiselEditorResources.GetIconContent("BoundsSnap",    "Snap bounds against grid");
                pivotSnapIcons      = ChiselEditorResources.GetIconContent("PivotSnap",     "Snap pivots against grid");
                edgeSnapIcons       = ChiselEditorResources.GetIconContent("EdgeSnap",      "Snap against edges");
                vertexSnapIcons     = ChiselEditorResources.GetIconContent("VertexSnap",    "Snap against vertices");
                surfaceSnapIcons    = ChiselEditorResources.GetIconContent("SurfaceSnap",   "Snap against surfaces");
                
                uvGridSnapIcons     = ChiselEditorResources.GetIconContent("UVGridSnap",    "Snap UV against grid");
                uvEdgeSnapIcons     = ChiselEditorResources.GetIconContent("UVEdgeSnap",    "Snap UV against surface edges");
                uvVertexSnapIcons   = ChiselEditorResources.GetIconContent("UVVertexSnap",  "Snap UV against surface vertices");
            }
        }
        static Styles styles;

         
        static void DisplayControls(SceneView sceneView)
        {
            if (styles == null)
                styles = new Styles();
            EditorGUI.BeginChangeCheck();
            {
                bool haveLabel = false;
                {
                    // TODO: show all "lines" for transform tool
                    // TODO: implement all snapping types
                    // TODO: add units
                    var toolType = CurrentToolType();
                    if (ChiselToolsOverlay.ShowSnappingToolUV) // TODO: should be a mask
                    {
                        GUILayout.BeginHorizontal();
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.UVGridSnappingEnabled   = GUILayout.Toggle(Snapping.UVGridSnappingEnabled,     styles.uvGridSnapIcon,   styles.miniButtonLeft);
                        Snapping.UVEdgeSnappingEnabled   = GUILayout.Toggle(Snapping.UVEdgeSnappingEnabled,     styles.uvEdgeSnapIcon,   styles.miniButtonMid);
                        Snapping.UVVertexSnappingEnabled = GUILayout.Toggle(Snapping.UVVertexSnappingEnabled,   styles.uvVertexSnapIcon, styles.miniButtonRight);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    } else
                    if (toolType == Tool.Move || toolType == Tool.Transform)
                    {
                        GUILayout.BeginHorizontal();
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.BoundsSnappingEnabled  = GUILayout.Toggle(Snapping.BoundsSnappingEnabled,  styles.boundsSnapIcon,  styles.miniButtonLeft);
                        Snapping.PivotSnappingEnabled   = GUILayout.Toggle(Snapping.PivotSnappingEnabled,   styles.pivotSnapIcon,   styles.miniButtonMid);
                        Snapping.EdgeSnappingEnabled    = GUILayout.Toggle(Snapping.EdgeSnappingEnabled,    styles.edgeSnapIcon,    styles.miniButtonMid);
                        Snapping.VertexSnappingEnabled  = GUILayout.Toggle(Snapping.VertexSnappingEnabled,  styles.vertexSnapIcon,  styles.miniButtonMid);
                        Snapping.SurfaceSnappingEnabled = GUILayout.Toggle(Snapping.SurfaceSnappingEnabled, styles.surfaceSnapIcon, styles.miniButtonRight);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    if (toolType == Tool.Rotate || toolType == Tool.Transform)
                    {
                        GUILayout.BeginHorizontal();
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.RotateSnappingEnabled = GUILayout.Toggle(Snapping.RotateSnappingEnabled, kSnapRotateButton, EditorStyles.miniButton, kShowGridButtonWidthOption);
                        using (var disableScope = new EditorGUI.DisabledScope(!Snapping.RotateSnappingEnabled))
                        {
                            ChiselEditorSettings.RotateSnap = EditorGUILayout.FloatField(ChiselEditorSettings.RotateSnap);
                            if (GUILayout.Button(kHalveSnapDistanceButton, styles.increaseButton, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.HalfRotateSnap();
                            }
                            if (GUILayout.Button(kDoubleSnapDistanceButton, styles.decreaseButton, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.DoubleRotateSnap();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    if (toolType == Tool.Scale || toolType == Tool.Rect || toolType == Tool.Transform)
                    {
                        GUILayout.BeginHorizontal();
                        if (!haveLabel) { GUILayout.Label(kSnapLabel, kLabelWidthOption); haveLabel = true; } else { GUILayout.Space(kLabelWidth + GUI.skin.label.padding.horizontal); }
                        Snapping.ScaleSnappingEnabled = GUILayout.Toggle(Snapping.ScaleSnappingEnabled, kSnapScaleButton, EditorStyles.miniButton, kShowGridButtonWidthOption);
                        using (var disableScope = new EditorGUI.DisabledScope(!Snapping.ScaleSnappingEnabled))
                        {
                            ChiselEditorSettings.ScaleSnap = EditorGUILayout.FloatField(ChiselEditorSettings.ScaleSnap);
                            if (GUILayout.Button(kHalveSnapDistanceButton, styles.increaseButton, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.HalfScaleSnap();
                            }
                            if (GUILayout.Button(kDoubleSnapDistanceButton, styles.decreaseButton, kSizeButtonWidthOption))
                            {
                                SnappingKeyboard.DoubleScaleSnap();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(kGridLabel, kLabelWidthOption);
                    ChiselEditorSettings.ShowGrid = GUILayout.Toggle(ChiselEditorSettings.ShowGrid, "Show Grid", EditorStyles.miniButton, kShowGridButtonWidthOption);
                    //using (var disableScope = new EditorGUI.DisabledScope(!ChiselEditorSettings.ShowGrid))
                    {
                        ChiselEditorSettings.UniformSnapSize = EditorGUILayout.FloatField(ChiselEditorSettings.UniformSnapSize);
                        if (GUILayout.Button(kHalveSnapDistanceButton, styles.increaseButton, kSizeButtonWidthOption))
                        {
                            SnappingKeyboard.HalfGridSize();
                        }
                        if (GUILayout.Button(kDoubleSnapDistanceButton, styles.decreaseButton, kSizeButtonWidthOption))
                        {
                            SnappingKeyboard.DoubleGridSize();
                        }
                    }
                }
                GUILayout.EndHorizontal();
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
                case Tool.Transform:    ChiselToolsOverlay.ShowSnappingToolUV = false; return Tools.current;
                case Tool.Custom:       return ChiselToolsOverlay.ShowSnappingTool;
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
