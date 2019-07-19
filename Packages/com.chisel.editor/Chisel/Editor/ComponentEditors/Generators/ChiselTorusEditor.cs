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
    public sealed class ChiselTorusDetails : ChiselGeneratorDetails<ChiselTorus>
    {
    }

    [CustomEditor(typeof(ChiselTorus))]
    [CanEditMultipleObjects]
    public sealed class ChiselTorusEditor : ChiselGeneratorEditor<ChiselTorus>
    {
        [MenuItem("GameObject/Chisel/" + ChiselTorus.kNodeTypeName)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselTorus.kNodeTypeName); }

        static readonly GUIContent  kInnerDiameterContent   = new GUIContent("Inner Diameter");

        SerializedProperty outerDiameterProp;
        SerializedProperty tubeWidthProp;
        SerializedProperty tubeHeightProp;
        SerializedProperty tubeRotationProp;
        SerializedProperty horizontalSegmentsProp;
        SerializedProperty verticalSegmentsProp;
        SerializedProperty startAngleProp;
        SerializedProperty totalAngleProp;
        SerializedProperty fitCircleProp;

        SerializedProperty surfacesProp;

        protected override void ResetInspector()
        { 
            outerDiameterProp		= null;
            tubeWidthProp			= null;
            tubeHeightProp			= null;
            tubeRotationProp		= null;
            horizontalSegmentsProp	= null;
            verticalSegmentsProp	= null;
            startAngleProp			= null;
            totalAngleProp			= null;
            fitCircleProp			= null;

            surfacesProp            = null;
        }
        
        protected override void InitInspector()
        { 
            var definitionProp = serializedObject.FindProperty(nameof(ChiselTorus.definition));
            {
                outerDiameterProp		= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.outerDiameter));
                tubeWidthProp			= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.tubeWidth));
                tubeHeightProp			= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.tubeHeight));
                tubeRotationProp		= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.tubeRotation));
                horizontalSegmentsProp	= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.horizontalSegments));
                verticalSegmentsProp	= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.verticalSegments));
                startAngleProp			= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.startAngle));
                totalAngleProp			= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.totalAngle));
                fitCircleProp			= definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.fitCircle));

                var surfDefProp         = definitionProp.FindPropertyRelative(nameof(ChiselTorus.definition.surfaceDefinition));
                {
                    surfacesProp        = surfDefProp.FindPropertyRelative(nameof(ChiselTorus.definition.surfaceDefinition.surfaces));
                }
            }
        }

        void InnerDiameterPropertyField()
        {
            var content		= kInnerDiameterContent;
            var position	= GUILayoutUtility.GetRect(content, EditorStyles.numberField);
            content = EditorGUI.BeginProperty(position, content, tubeWidthProp);
            {
                EditorGUI.showMixedValue = outerDiameterProp.hasMultipleDifferentValues || 
                                           tubeWidthProp.hasMultipleDifferentValues;
                float innerDiameter;
                EditorGUI.BeginChangeCheck();
                {
                    innerDiameter = ChiselTorusDefinition.CalcInnerDiameter(outerDiameterProp.floatValue, tubeWidthProp.floatValue);
                    innerDiameter = EditorGUI.FloatField(position, content, innerDiameter);
                }
                if (EditorGUI.EndChangeCheck())
                    tubeWidthProp.floatValue = ChiselTorusDefinition.CalcTubeWidth(outerDiameterProp.floatValue, innerDiameter);
            }
            EditorGUI.EndProperty();
        }

        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(outerDiameterProp);
            InnerDiameterPropertyField();
            EditorGUILayout.PropertyField(tubeWidthProp);
            EditorGUILayout.PropertyField(tubeHeightProp);
            EditorGUILayout.PropertyField(fitCircleProp);
            EditorGUILayout.PropertyField(tubeRotationProp);

            EditorGUILayout.PropertyField(horizontalSegmentsProp);
            EditorGUILayout.PropertyField(verticalSegmentsProp);

            EditorGUILayout.PropertyField(startAngleProp);
            EditorGUILayout.PropertyField(totalAngleProp);

            ShowSurfaces(surfacesProp);
        }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(ChiselTorusDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            //var baseColor		= UnityEditor.Handles.yAxisColor;
            //var isDisabled		= UnitySceneExtensions.Handles.disabled;
            var normal			= Vector3.up;

            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;
            
            if (definition.totalAngle != 360)
                horzSegments++;
            
            var prevColor		= UnityEditor.Handles.color;
            prevColor.a *= 0.8f;
            var color			= prevColor;
            color.a *= 0.6f;

            UnityEditor.Handles.color = color;
            for (int i = 0, j = 0; i < horzSegments; i++, j += vertSegments)
            {
                ChiselOutlineRenderer.DrawLineLoop(vertices, j, vertSegments, lineMode: lineMode, thickness: kVertLineThickness);
            }

            for (int k = 0; k < vertSegments; k++)
            {
                for (int i = 0, j = 0; i < horzSegments - 1; i++, j += vertSegments)
                    ChiselOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + vertSegments], lineMode: lineMode, thickness: kHorzLineThickness);
            }
            if (definition.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    ChiselOutlineRenderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            UnityEditor.Handles.color = prevColor;
        }


        protected override void OnScene(SceneView sceneView, ChiselTorus generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            Vector3[] vertices = null;
            if (!BrushMeshFactory.GenerateTorusVertices(generator.definition, ref vertices))
                return;
            
            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);


            var outerRadius = generator.definition.outerDiameter * 0.5f;
            var innerRadius = generator.definition.innerDiameter * 0.5f;
            var topPoint	= normal * ( generator.definition.tubeHeight * 0.5f);
            var bottomPoint	= normal * (-generator.definition.tubeHeight * 0.5f);
            
            EditorGUI.BeginChangeCheck();
            {
                UnityEditor.Handles.color = baseColor;
                outerRadius = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, Vector3.zero, outerRadius);
                innerRadius = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, Vector3.zero, innerRadius);
                bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomPoint, -normal);
                topPoint	= UnitySceneExtensions.SceneHandles.DirectionHandle(topPoint,     normal);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.definition.outerDiameter	= outerRadius * 2.0f;
                generator.definition.innerDiameter	= innerRadius * 2.0f;
                generator.definition.tubeHeight		= (topPoint.y - bottomPoint.y);
                // TODO: handle sizing down
                generator.OnValidate();
            }
        }
    }
}