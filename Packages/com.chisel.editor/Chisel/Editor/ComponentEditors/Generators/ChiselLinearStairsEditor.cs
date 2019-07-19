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
    public sealed class ChiselLinearStairsDetails : ChiselGeneratorDetails<ChiselLinearStairs>
    {
    }
    
    [CustomEditor(typeof(ChiselLinearStairs))]
    [CanEditMultipleObjects]
    public sealed class ChiselLinearStairsEditor : ChiselGeneratorEditor<ChiselLinearStairs>
    {
        [MenuItem("GameObject/Chisel/" + ChiselLinearStairs.kNodeTypeName)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselLinearStairs.kNodeTypeName); }

        static readonly GUIContent[] kSurfaceContentNames = new[]
        {
            new GUIContent("Top"),
            new GUIContent("Bottom"),
            new GUIContent("Right"),
            new GUIContent("Left"),
            new GUIContent("Front"),
            new GUIContent("Back"),
            new GUIContent("Tread"),
            new GUIContent("Step")
        };

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
        SerializedProperty surfacesProp;

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

            surfacesProp        = null;
        }

        protected override void InitInspector()
        {
            var definitionProp = serializedObject.FindProperty(nameof(ChiselLinearStairs.definition));
            {
                boundsProp		    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.bounds));
                stepHeightProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.stepHeight));
                stepDepthProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.stepDepth));
                plateauHeightProp	= definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.plateauHeight));
                riserTypeProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.riserType));
                riserDepthProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.riserDepth));
                leftSideProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.leftSide));
                rightSideProp		= definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.rightSide));
                sideDepthProp		= definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.sideDepth));
                sideWidthProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.sideWidth));
                sideHeightProp	    = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.sideHeight));
                treadHeightProp		= definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.treadHeight));
                nosingDepthProp		= definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.nosingDepth));
                nosingWidthProp		= definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.nosingWidth));

                var surfDefProp     = definitionProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.surfaceDefinition));
                {
                    surfacesProp    = surfDefProp.FindPropertyRelative(nameof(ChiselLinearStairs.definition.surfaceDefinition.surfaces));
                }
            }
        }
        
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


            ShowSurfaces(surfacesProp, kSurfaceContentNames, kSurfaceContentNames.Length);
        }
        
        protected override void OnScene(ChiselLinearStairs generator)
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
                if (min.y > max.y) { var t = min.y; min.y = max.y; max.y = t; }
                if (min.x > max.x) { var t = min.x; min.x = max.x; max.x = t; }
                if (min.z > max.z) { var t = min.z; min.z = max.x; max.z = t; }

                var pHeight0	= new Vector3(min.x, min.y + plateauHeight, max.z);
                var pHeight1	= new Vector3(max.x, min.y + plateauHeight, max.z);

                var pDepth0		= new Vector3(min.x, max.y, min.z + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, min.z + stepDepthOffset);

                plateauHeight   = UnitySceneExtensions.SceneHandles.Edge1DHandle(Axis.Y, pHeight0, pHeight1, snappingStep: generator.StepHeight) - min.y;
                stepDepthOffset = UnitySceneExtensions.SceneHandles.Edge1DHandle(Axis.Z, pDepth0,  pDepth1,  snappingStep: ChiselLinearStairsDefinition.kMinStepDepth) - min.z;
                        
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
                        stepDepthOffset			= Mathf.Clamp(stepDepthOffset, 0, generator.Depth - ChiselLinearStairsDefinition.kMinStepDepth);
                        generator.StepDepth		= (generator.Depth - stepDepthOffset) / generator.StepCount;
                    }
                }
                //generator.OnValidate();
            }
        }
    }
}
