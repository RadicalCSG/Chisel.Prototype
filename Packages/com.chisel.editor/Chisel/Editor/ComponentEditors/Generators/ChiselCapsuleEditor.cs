using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCapsule))]
    [CanEditMultipleObjects]
    public sealed class ChiselCapsuleEditor : ChiselGeneratorEditor<ChiselCapsule>
    {
        [MenuItem("GameObject/Chisel/" + ChiselCapsule.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCapsule.kNodeTypeName); }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(ChiselCapsuleDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            //var baseColor		= UnityEditor.Handles.yAxisColor;
            //var isDisabled	= UnitySceneExtensions.Handles.disabled;
            //var normal		= Vector3.up;
            var sides			= definition.sides;
            
            // TODO: share this logic with GenerateCapsuleVertices
            
            var topHemisphere		= definition.haveRoundedTop;
            var bottomHemisphere	= definition.haveRoundedBottom;
            var topSegments			= topHemisphere    ? definition.topSegments    : 0;
            var bottomSegments		= bottomHemisphere ? definition.bottomSegments : 0;
            
            var extraVertices		= definition.extraVertexCount;
            var bottomVertex		= definition.bottomVertex;
            var topVertex			= definition.topVertex;
            
            var rings				= definition.ringCount;
            var bottomRing			= (bottomHemisphere) ? (rings - bottomSegments) : rings - 1;
            var topRing				= (topHemisphere   ) ? (topSegments - 1) : 0;

            var prevColor = UnityEditor.Handles.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                if ((!definition.haveRoundedTop && i == topRing) ||
                    (!definition.haveRoundedBottom && i == bottomRing))
                    continue;
                bool isCapRing = (i == topRing || i == bottomRing);
                if (isCapRing)
                    continue;
                UnityEditor.Handles.color = (isCapRing ? prevColor : color);
                ChiselOutlineRenderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: (isCapRing ? kCapLineThickness : kHorzLineThickness), dashSize: (isCapRing ? 0 : kLineDash));
            }

            UnityEditor.Handles.color = color;
            for (int k = 0; k < sides; k++)
            {
                if (topHemisphere)
                    ChiselOutlineRenderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    ChiselOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (bottomHemisphere)
                    ChiselOutlineRenderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            UnityEditor.Handles.color = prevColor;
        }

        internal static int s_TopHash		    = "TopCapsuleHash".GetHashCode();
        internal static int s_BottomHash	    = "BottomCapsuleHash".GetHashCode();

        internal static int s_TopLoopHash       = "TopLoopHash".GetHashCode();
        internal static int s_BottomLoopHash    = "BottomLoopHash".GetHashCode();

        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?

        protected override void OnScene(SceneView sceneView, ChiselCapsule generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            if (!BrushMeshFactory.GenerateCapsuleVertices(ref generator.definition, ref vertices))
                return;

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);


            var topLoopID       = GUIUtility.GetControlID(s_TopLoopHash, FocusType.Keyboard);
            var bottomLoopID    = GUIUtility.GetControlID(s_BottomLoopHash, FocusType.Keyboard);


            var topPoint	= normal * (generator.definition.offsetY + generator.Height);
            var bottomPoint = normal * (generator.definition.offsetY);
            var middlePoint	= normal * (generator.definition.offsetY + (generator.Height * 0.5f));
            var radius2D	= new Vector2(generator.definition.diameterX, generator.definition.diameterZ) * 0.5f;

            var topHeight       = generator.definition.topHeight;
            var bottomHeight    = generator.definition.bottomHeight;

            var maxTopHeight    = generator.definition.height - bottomHeight;
            var maxBottomHeight = generator.definition.height - topHeight;

            if (generator.Height < 0)
                normal = -normal;

            EditorGUI.BeginChangeCheck();
            {
                UnityEditor.Handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                radius2D.x = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, middlePoint, radius2D.x);

                var topId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= ChiselCylinderEditor.IsSufaceBackFaced(topPoint, normal);
                    var topHasFocus			= (focusControl == topId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                    topPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(topId, topPoint, normal);

                    var topLoopHasFocus = (topHasFocus && !generator.HaveRoundedTop) || (focusControl == topLoopID);

                    var thickness = topLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;
                        
                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topLoopHasFocus, true, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(vertices, generator.definition.topVertexOffset, generator.definition.sides, lineMode: LineMode.NoZTest, thickness: thickness);
                        
                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topLoopHasFocus, false, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(vertices, generator.definition.topVertexOffset, generator.definition.sides, lineMode: LineMode.ZTest,   thickness: thickness);

                    {
                        var prevGUIChanged = GUI.changed;
                        for (int j = generator.definition.sides - 1, i = 0; i < generator.definition.sides; j = i, i++)
                        {
                            GUI.changed = false;
                            var from    = vertices[j + generator.definition.topVertexOffset];
                            var to      = vertices[i + generator.definition.topVertexOffset];
                            var edgeOffset = UnitySceneExtensions.SceneHandles.Edge1DHandleOffset(topLoopID, UnitySceneExtensions.Axis.Y, from, to, capFunction: null);
                            if (GUI.changed)
                            {
                                topHeight = Mathf.Clamp(topHeight - edgeOffset, 0, maxTopHeight);
                                prevGUIChanged = true;
                            }
                        }
                        GUI.changed = prevGUIChanged;
                    }
                }
                
                var bottomId = GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);
                {
                    var isBottomBackfaced	= ChiselCylinderEditor.IsSufaceBackFaced(bottomPoint, -normal);
                    var bottomHasFocus		= (focusControl == bottomId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                    bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomId, bottomPoint, -normal);

                    var bottomLoopHasFocus = (bottomHasFocus && !generator.HaveRoundedBottom) || (focusControl == bottomLoopID);

                    var thickness = bottomLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomLoopHasFocus, true, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(vertices, generator.definition.bottomVertexOffset, generator.definition.sides, lineMode: LineMode.NoZTest, thickness: thickness);
                    
                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomLoopHasFocus, false, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(vertices, generator.definition.bottomVertexOffset, generator.definition.sides, lineMode: LineMode.ZTest,   thickness: thickness);

                    {
                        var prevGUIChanged = GUI.changed;
                        for (int j = generator.definition.sides - 1, i = 0; i < generator.definition.sides; j = i, i++)
                        {
                            GUI.changed = false;
                            var from    = vertices[j + generator.definition.bottomVertexOffset];
                            var to      = vertices[i + generator.definition.bottomVertexOffset];
                            var edgeOffset = UnitySceneExtensions.SceneHandles.Edge1DHandleOffset(bottomLoopID, UnitySceneExtensions.Axis.Y, from, to, capFunction: null);
                            if (GUI.changed)
                            {
                                bottomHeight = Mathf.Clamp(bottomHeight + edgeOffset, 0, maxBottomHeight);
                                prevGUIChanged = true;
                            }
                        }
                        GUI.changed = prevGUIChanged;
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.definition.diameterX      = radius2D.x * 2.0f;
                generator.definition.height         = topPoint.y - bottomPoint.y;
                generator.definition.diameterZ      = radius2D.x * 2.0f;
                generator.definition.offsetY        = bottomPoint.y;
                generator.definition.topHeight      = topHeight;
                generator.definition.bottomHeight   = bottomHeight;
                generator.OnValidate();
                // TODO: handle sizing down (needs to modify transformation?)
            }
        }
    }
}