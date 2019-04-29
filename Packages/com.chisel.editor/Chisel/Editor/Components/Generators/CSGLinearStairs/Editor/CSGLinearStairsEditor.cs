using UnityEngine;
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
    public sealed class CSGLinearStairsDetails : ChiselGeneratorDetails<CSGLinearStairs>
    {
    }
    
    [CustomEditor(typeof(CSGLinearStairs))]
    [CanEditMultipleObjects]
    public sealed class CSGLinearStairsEditor : ChiselGeneratorEditor<CSGLinearStairs>
    {
        SerializedProperty boundsProp;
        SerializedProperty stepHeightProp;
        SerializedProperty stepDepthProp;
        SerializedProperty riserTypeProp;
        SerializedProperty riserDepthProp;
        SerializedProperty treadHeightProp;
        SerializedProperty nosingDepthProp;
        SerializedProperty nosingWidthProp;
        SerializedProperty plateauHeightProp;
        SerializedProperty leftSideProp;
        SerializedProperty rightSideProp;
        SerializedProperty sideDepthProp;
        SerializedProperty sideWidthProp;
        SerializedProperty sideHeightProp;
        
        SerializedProperty surfaceSideTopProp;
        SerializedProperty surfaceSideBottomProp;
        SerializedProperty surfaceSideLeftProp;
        SerializedProperty surfaceSideRightProp;
        SerializedProperty surfaceSideForwardProp;
        SerializedProperty surfaceSideBackProp;
        SerializedProperty surfaceSideTreadProp;
        SerializedProperty surfaceSideStepProp;

        protected override void ResetInspector()
        {
            boundsProp			= null;
            stepHeightProp		= null;
            stepDepthProp		= null;
            plateauHeightProp	= null;
            riserTypeProp		= null;
            leftSideProp		= null;
            rightSideProp		= null;
            riserDepthProp		= null;
            sideDepthProp		= null;
            sideWidthProp		= null;
            sideHeightProp		= null;
            treadHeightProp		= null;
            nosingDepthProp		= null;
            nosingWidthProp		= null;
            
            surfaceSideTopProp		= null;
            surfaceSideBottomProp	= null;
            surfaceSideLeftProp		= null;
            surfaceSideRightProp	= null;
            surfaceSideForwardProp	= null;
            surfaceSideBackProp		= null;
            surfaceSideTreadProp	= null;
            surfaceSideStepProp		= null;
        }

        protected override void InitInspector()
        {
            // TODO: could we populate these using attributes on the properties?
            boundsProp			= serializedObject.FindProperty("definition.bounds");
            stepHeightProp		= serializedObject.FindProperty("definition.stepHeight");
            stepDepthProp		= serializedObject.FindProperty("definition.stepDepth");
            plateauHeightProp	= serializedObject.FindProperty("definition.plateauHeight");
            riserTypeProp		= serializedObject.FindProperty("definition.riserType");
            riserDepthProp		= serializedObject.FindProperty("definition.riserDepth");
            leftSideProp		= serializedObject.FindProperty("definition.leftSide");
            rightSideProp		= serializedObject.FindProperty("definition.rightSide");
            sideDepthProp		= serializedObject.FindProperty("definition.sideDepth");
            sideWidthProp		= serializedObject.FindProperty("definition.sideWidth");
            sideHeightProp		= serializedObject.FindProperty("definition.sideHeight");
            treadHeightProp		= serializedObject.FindProperty("definition.treadHeight");
            nosingDepthProp		= serializedObject.FindProperty("definition.nosingDepth");
            nosingWidthProp		= serializedObject.FindProperty("definition.nosingWidth");
            
            surfaceSideTopProp		= serializedObject.FindProperty("definition.topSurface");
            surfaceSideBottomProp	= serializedObject.FindProperty("definition.bottomSurface");
            surfaceSideLeftProp		= serializedObject.FindProperty("definition.leftSurface");
            surfaceSideRightProp	= serializedObject.FindProperty("definition.rightSurface");
            surfaceSideForwardProp	= serializedObject.FindProperty("definition.forwardSurface");
            surfaceSideBackProp		= serializedObject.FindProperty("definition.backSurface");
            surfaceSideTreadProp	= serializedObject.FindProperty("definition.treadSurface");
            surfaceSideStepProp		= serializedObject.FindProperty("definition.stepSurface");
            
            surfacesVisible			= SessionState.GetBool(kSurfacesVisibleKey, false);
        }
        
        static GUIContent	surfacesContent			= new GUIContent("Surfaces");	
        const string		kSurfacesVisibleKey		= "CSGLinearStairsEditor.SubmeshesVisible";
        bool surfacesVisible;
        
        protected override void OnInspector()
        {
            EditorGUILayout.PropertyField(boundsProp);
            EditorGUILayout.PropertyField(stepHeightProp);
            EditorGUILayout.PropertyField(stepDepthProp);
            EditorGUILayout.PropertyField(treadHeightProp);
            EditorGUILayout.PropertyField(nosingDepthProp);
            EditorGUILayout.PropertyField(nosingWidthProp);
            EditorGUILayout.PropertyField(plateauHeightProp);
            EditorGUILayout.PropertyField(riserTypeProp);
            EditorGUILayout.PropertyField(riserDepthProp);
            EditorGUILayout.PropertyField(leftSideProp);
            EditorGUILayout.PropertyField(rightSideProp);
            EditorGUILayout.PropertyField(sideWidthProp);
            EditorGUILayout.PropertyField(sideHeightProp);
            EditorGUILayout.PropertyField(sideDepthProp);

            EditorGUI.BeginChangeCheck();
            surfacesVisible = EditorGUILayout.Foldout(surfacesVisible, surfacesContent);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetBool(kSurfacesVisibleKey, surfacesVisible);
            if (surfacesVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(surfaceSideTopProp);
                EditorGUILayout.PropertyField(surfaceSideBottomProp);
                EditorGUILayout.PropertyField(surfaceSideLeftProp);
                EditorGUILayout.PropertyField(surfaceSideRightProp);
                EditorGUILayout.PropertyField(surfaceSideForwardProp);
                EditorGUILayout.PropertyField(surfaceSideBackProp);
                EditorGUILayout.PropertyField(surfaceSideTreadProp);
                EditorGUILayout.PropertyField(surfaceSideStepProp);
                EditorGUI.indentLevel--;
            }
        }
        
        protected override void OnScene(CSGLinearStairs generator)
        {
            var originalStepDepthOffset = generator.StepDepthOffset;
            var originalPlateauHeight	= generator.PlateauHeight;
            var stepDepthOffset			= originalStepDepthOffset;
            var plateauHeight			= originalPlateauHeight;
            var originalBounds			= generator.Bounds;
            Bounds newBounds;
            EditorGUI.BeginChangeCheck();
            {
                var steps		= Snapping.MoveSnappingSteps;
                steps.y			= generator.StepHeight;

                // TODO: need ability to turn 90 degrees (without changing shape of bounds)
                // TODO: scaling up/down should be in step size	
                // TODO: turn this shape into a handle that is used by both linear-ramp and linear-stairs
                newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(originalBounds, Quaternion.identity, steps);


                var min			= newBounds.min;
                var max			= newBounds.max;

                var pHeight0	= new Vector3(min.x, min.y + plateauHeight, max.z);
                var pHeight1	= new Vector3(max.x, min.y + plateauHeight, max.z);

                var pDepth0		= new Vector3(min.x, max.y, min.z + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, min.z + stepDepthOffset);

                plateauHeight   = UnitySceneExtensions.SceneHandles.Edge1DHandle(Axis.Y, pHeight0, pHeight1, snappingStep: generator.StepHeight) - min.y;
                stepDepthOffset = UnitySceneExtensions.SceneHandles.Edge1DHandle(Axis.Z, pDepth0,  pDepth1,  snappingStep: CSGLinearStairsDefinition.kMinStepDepth) - min.z;
                        
                UnityEditor.Handles.DrawLine(pHeight0, pDepth0);
                UnityEditor.Handles.DrawLine(pHeight1, pDepth1);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);

                generator.Bounds		= newBounds;
                generator.PlateauHeight = plateauHeight;

                //if (originalBounds.size.y != newBounds.size.y)
                { 
                    if (stepDepthOffset != originalStepDepthOffset ||
                        plateauHeight != originalPlateauHeight)
                    {
                        stepDepthOffset			= Mathf.Clamp(stepDepthOffset, 0, generator.Depth - CSGLinearStairsDefinition.kMinStepDepth);
                        generator.StepDepth		= (generator.Depth - stepDepthOffset) / generator.StepCount;
                    }
                }
                //generator.OnValidate();
            }
        }
    }
}
