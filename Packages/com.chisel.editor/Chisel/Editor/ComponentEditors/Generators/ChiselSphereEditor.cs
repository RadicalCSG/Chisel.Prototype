using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public sealed class ChiselSphereDetails : ChiselGeneratorDetails<ChiselSphere>
    {
    }

    [CustomEditor(typeof(ChiselSphere))]
    [CanEditMultipleObjects]
    public sealed class ChiselSphereEditor : ChiselGeneratorEditor<ChiselSphere>
    {
        [MenuItem("GameObject/Chisel/" + ChiselSphere.kNodeTypeName)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselSphere.kNodeTypeName); }

        SerializedProperty diameterXYZProp;
        SerializedProperty rotationProp;
        SerializedProperty horizontalSegmentsProp;
        SerializedProperty verticalSegmentsProp;
        SerializedProperty surfacesProp;

        protected override void ResetInspector()
        { 
            diameterXYZProp		    = null;
            rotationProp		    = null;
            horizontalSegmentsProp  = null;
            verticalSegmentsProp    = null;
            surfacesProp            = null;
        }
        
        protected override void InitInspector()
        { 
            var definitionProp = serializedObject.FindProperty(nameof(ChiselSphere.definition));
            {
                diameterXYZProp		    = definitionProp.FindPropertyRelative(nameof(ChiselSphere.definition.diameterXYZ));
                rotationProp		    = definitionProp.FindPropertyRelative(nameof(ChiselSphere.definition.rotation));
                horizontalSegmentsProp  = definitionProp.FindPropertyRelative(nameof(ChiselSphere.definition.horizontalSegments));
                verticalSegmentsProp    = definitionProp.FindPropertyRelative(nameof(ChiselSphere.definition.verticalSegments));

                var surfDefProp         = definitionProp.FindPropertyRelative(nameof(ChiselSphere.definition.surfaceDefinition));
                {
                    surfacesProp        = surfDefProp.FindPropertyRelative(nameof(ChiselSphere.definition.surfaceDefinition.surfaces));
                }
            }
        }

        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(diameterXYZProp);
            EditorGUILayout.PropertyField(rotationProp);
            EditorGUILayout.PropertyField(horizontalSegmentsProp);
            EditorGUILayout.PropertyField(verticalSegmentsProp);

            ShowSurfaces(surfacesProp);
        }
        
        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(ChiselSphereDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            //var baseColor		= UnityEditor.Handles.yAxisColor;
            //var isDisabled		= UnitySceneExtensions.Handles.disabled;
            //var normal			= Vector3.up;
            var sides			= definition.horizontalSegments;
            
            var extraVertices	= 2;
            var bottomVertex	= 1;
            var topVertex		= 0;
            
            var rings			= (vertices.Length - extraVertices) / sides;

            var prevColor = UnityEditor.Handles.color;
            var color = prevColor;
            color.a *= 0.6f;

            UnityEditor.Handles.color = color;
            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                ChiselOutlineRenderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: kHorzLineThickness, dashSize: kLineDash);
            }

            for (int k = 0; k < sides; k++)
            {
                ChiselOutlineRenderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    ChiselOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                ChiselOutlineRenderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            UnityEditor.Handles.color = prevColor;
        }

        internal static int s_TopHash		= "TopSphereHash".GetHashCode();
        internal static int s_BottomHash	= "BottomSphereHash".GetHashCode();


        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?
        
        protected override void OnScene(ChiselSphere generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            if (!BrushMeshFactory.GenerateSphereVertices(generator.definition, ref vertices))
                return;

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);

            Vector3 center, topPoint, bottomPoint;
            if (!generator.GenerateFromCenter)
            {
                center      = normal * (generator.definition.offsetY + (generator.DiameterXYZ.y * 0.5f));
                topPoint    = normal * (generator.definition.offsetY + generator.DiameterXYZ.y);
                bottomPoint = normal * (generator.definition.offsetY);
            } else
            {
                center      = normal * (generator.definition.offsetY);
                topPoint    = normal * (generator.definition.offsetY + (generator.DiameterXYZ.y *  0.5f));
                bottomPoint = normal * (generator.definition.offsetY + (generator.DiameterXYZ.y * -0.5f));
            }

            if (generator.DiameterXYZ.y < 0)
                normal = -normal;

            var radius2D = new Vector2(generator.definition.diameterXYZ.x, generator.definition.diameterXYZ.z) * 0.5f;

            EditorGUI.BeginChangeCheck();
            {
                UnityEditor.Handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                radius2D.x = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, center, radius2D.x);

                var bottomId = GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);
                {
                    var isBottomBackfaced	= false; // TODO: how to do this?
                    var bottomHasFocus		= (focusControl == bottomId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                    bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomId, bottomPoint, -normal);
                }

                var topId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= false; // TODO: how to do this?
                    var topHasFocus			= (focusControl == topId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                    topPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(topId, topPoint, normal);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                var diameter = generator.DiameterXYZ;
                diameter.y = topPoint.y - bottomPoint.y;
                diameter.x = radius2D.x * 2.0f;
                diameter.z = radius2D.x * 2.0f;
                generator.definition.offsetY    = bottomPoint.y;
                generator.DiameterXYZ = diameter;
                // TODO: handle sizing down (needs to modify transformation?)
            }
        }
    }
}