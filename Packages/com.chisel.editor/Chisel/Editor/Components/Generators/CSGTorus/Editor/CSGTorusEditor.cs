﻿using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using Chisel.Assets;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    [CustomEditor(typeof(CSGTorus))]
    [CanEditMultipleObjects]
    public sealed class CSGTorusEditor : GeneratorEditor<CSGTorus>
    {
        static GUIContent   InnerDiameterContent = new GUIContent("Inner Diameter");

        SerializedProperty outerDiameterProp;
        SerializedProperty tubeWidthProp;
        SerializedProperty tubeHeightProp;
        SerializedProperty tubeRotationProp;
        SerializedProperty horizontalSegmentsProp;
        SerializedProperty verticalSegmentsProp;
        SerializedProperty startAngleProp;
        SerializedProperty totalAngleProp;
        SerializedProperty fitCircleProp;

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
        }
        
        protected override void InitInspector()
        { 
            outerDiameterProp		= serializedObject.FindProperty("definition.outerDiameter");
            tubeWidthProp			= serializedObject.FindProperty("definition.tubeWidth");
            tubeHeightProp			= serializedObject.FindProperty("definition.tubeHeight");
            tubeRotationProp		= serializedObject.FindProperty("definition.tubeRotation");
            horizontalSegmentsProp	= serializedObject.FindProperty("definition.horizontalSegments");
            verticalSegmentsProp	= serializedObject.FindProperty("definition.verticalSegments");
            startAngleProp			= serializedObject.FindProperty("definition.startAngle");
            totalAngleProp			= serializedObject.FindProperty("definition.totalAngle");
            fitCircleProp			= serializedObject.FindProperty("definition.fitCircle");
        }

        void InnerDiameterPropertyField()
        {
            var content		= InnerDiameterContent;
            var position	= GUILayoutUtility.GetRect(content, EditorStyles.numberField);
            content = EditorGUI.BeginProperty(position, content, tubeWidthProp);
            {
                EditorGUI.showMixedValue = outerDiameterProp.hasMultipleDifferentValues || 
                                           tubeWidthProp.hasMultipleDifferentValues;
                float innerDiameter;
                EditorGUI.BeginChangeCheck();
                {
                    innerDiameter = CSGTorusDefinition.CalcInnerDiameter(outerDiameterProp.floatValue, tubeWidthProp.floatValue);
                    innerDiameter = EditorGUI.FloatField(position, content, innerDiameter);
                }
                if (EditorGUI.EndChangeCheck())
                    tubeWidthProp.floatValue = CSGTorusDefinition.CalcTubeWidth(outerDiameterProp.floatValue, innerDiameter);
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
        }

        
        
        protected override void OnSceneInit(CSGTorus generator) { }

        
        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(CSGTorusDefinition definition, Vector3[] vertices, LineMode lineMode)
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
                CSGOutlineRenderer.DrawLineLoop(vertices, j, vertSegments, lineMode: lineMode, thickness: kVertLineThickness);
            }

            for (int k = 0; k < vertSegments; k++)
            {
                for (int i = 0, j = 0; i < horzSegments - 1; i++, j += vertSegments)
                    CSGOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + vertSegments], lineMode: lineMode, thickness: kHorzLineThickness);
            }
            if (definition.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    CSGOutlineRenderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            UnityEditor.Handles.color = prevColor;
        }


        protected override void OnScene(CSGTorus generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            Vector3[] vertices = null;
            if (!BrushMeshAssetFactory.GenerateTorusVertices(generator.definition, ref vertices))
                return;
            
            UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
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