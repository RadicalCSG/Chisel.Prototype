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

        #region Inspector
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
            EditorGUI.BeginChangeCheck();
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
            if (EditorGUI.EndChangeCheck())
            {
                generatorModified = true;
                UpdateDefinitions();
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
        #endregion

        #region Selection
        static Dictionary<ChiselLinearStairs, ChiselLinearStairsDefinition> activeGeneratorDefinitions = new Dictionary<ChiselLinearStairs, ChiselLinearStairsDefinition>();

        protected override void OnUndoRedoPerformed() { UpdateDefinitions(); }

        protected override void OnGeneratorSelected(ChiselLinearStairs generator)
        {
            UpdateDefinitions();
            activeGeneratorDefinitions[generator] = generator.definition;
        }

        protected override void OnGeneratorDeselected(ChiselLinearStairs generator)
        {
            activeGeneratorDefinitions.Remove(generator);
            UpdateDefinitions();
        }

        void UpdateDefinitions()
        {
            if (!generatorModified)
                return;

            var activeGenerators = activeGeneratorDefinitions.Keys.ToArray();

            foreach (var generator in activeGenerators)
                activeGeneratorDefinitions[generator] = generator.definition;

            generatorModified = false;
        }
        #endregion


        static bool generatorModified = false;
        protected override void OnScene(SceneView sceneView, ChiselLinearStairs generator)
        {
            var previousHotControl = GUIUtility.hotControl;

            if (ShowGeneratorHandles(generator, activeGeneratorDefinitions[generator]))
                generatorModified = true;

            var currentHotControl = GUIUtility.hotControl;
            // When we stop/start dragging or clicking something our hotControl changes. 
            // We detect this change, and together with generatorModified we know when a user operation is finished.
            if (generatorModified && (currentHotControl != previousHotControl))
                UpdateDefinitions();
        }
        
        static GUIContent clockWiseRotation         = new GUIContent("↻");
        static GUIContent antiClockWiseRotation     = new GUIContent("↺");

        // TODO: put somewhere else
        public static Color iconColor = new Color(201f / 255, 200f / 255, 144f / 255, 1.00f);

        static bool ShowGeneratorHandles(ChiselLinearStairs generator, ChiselLinearStairsDefinition cachedDefinition)
        {
            ref readonly var currentDefinition = ref generator.definition;
            var newDefinition = currentDefinition;
            { 
                newDefinition.stepHeight       = cachedDefinition.stepHeight;
                newDefinition.stepDepth        = cachedDefinition.stepDepth;
                newDefinition.plateauHeight    = cachedDefinition.plateauHeight;
                newDefinition.bounds           = cachedDefinition.bounds;
            }

            EditorGUI.BeginChangeCheck();
            {
                var generatorTransform = generator.transform;
                var stepDepthOffset = currentDefinition.StepDepthOffset;
                var stepHeight      = currentDefinition.stepHeight;
                var stepCount       = currentDefinition.StepCount;
                var plateauHeight   = currentDefinition.plateauHeight;
                var bounds          = currentDefinition.bounds;
                var cameraPosition  = Camera.current.transform.position;

                var steps		    = Snapping.MoveSnappingSteps;
                steps.y			    = stepHeight;
                
                EditorGUI.BeginChangeCheck();
                {
                    bounds = SceneHandles.BoundsHandle(currentDefinition.bounds, Quaternion.identity, steps);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    newDefinition.bounds = bounds;
                }

                var min			= bounds.min;
                var max			= bounds.max;
                if (min.y > max.y) { var t = min.y; min.y = max.y; max.y = t; }
                if (min.x > max.x) { var t = min.x; min.x = max.x; max.x = t; }
                if (min.z > max.z) { var t = min.z; min.z = max.x; max.z = t; }
                
                var edgeHeight  = max.y - stepHeight * stepCount;
                var pHeight0	= new Vector3(min.x, edgeHeight, max.z);
                var pHeight1	= new Vector3(max.x, edgeHeight, max.z);

                var pDepth0		= new Vector3(min.x, max.y, min.z + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, min.z + stepDepthOffset);

                var center      = bounds.center;
                var worldCenter = generatorTransform.TransformPoint(center);
                var direction   = (cameraPosition - worldCenter).normalized;

                var boundsAxi   = new Vector3(Mathf.Sign(bounds.size.x), Mathf.Sign(bounds.size.y), Mathf.Sign(bounds.size.z));

                var dotX        = Vector3.Dot(generatorTransform.TransformVector(new Vector3(boundsAxi.x, 0, 0)), direction);
                var dotZ        = Vector3.Dot(generatorTransform.TransformVector(new Vector3(0, 0, boundsAxi.z)), direction);
                var dotY        = Vector3.Dot(generatorTransform.TransformVector(new Vector3(0, boundsAxi.y, 0)), direction);

                if ((dotX > -0.2f) && (dotX < 0.2f)) dotX = 0;
                if ((dotZ > -0.2f) && (dotZ < 0.2f)) dotZ = 0;

                var axisY  = (dotY > 0);
                var axis0X = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX > 0) : ((dotX < 0) ^ (dotX < 0) ^ (dotZ > 0));
                var axis0Z = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX < 0) : ((dotZ > 0) ^ (dotX < 0) ^ (dotZ > 0));
                var axis1X = (dotX == 0) ? (dotZ < 0) : (dotZ == 0) ? (dotX > 0) : ((dotX > 0) ^ (dotX < 0) ^ (dotZ > 0));
                var axis1Z = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX > 0) : ((dotZ < 0) ^ (dotX < 0) ^ (dotZ > 0));

                var pLabel0     = new Vector3(axis0X ? min.x : max.x, axisY ? max.y : min.y, axis0Z ? min.z : max.z);
                var pLabel1     = new Vector3(axis1X ? min.x : max.x, axisY ? max.y : min.y, axis1Z ? min.z : max.z);

                Handles.color = iconColor;

                // TODO: put both buttons next to each other?
                if (SceneHandles.ClickableLabel(pLabel1, (pLabel1 - bounds.center).normalized, clockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
                {
                    bounds.center = Vector3.zero;
                    var size = bounds.size;
                    var sizeX = size.x;
                    var sizeZ = size.z;
                    size.x = sizeZ;
                    size.z = sizeX;
                    bounds.size = size;
                    bounds.center = center;
                    newDefinition.bounds = bounds;
                    GUI.changed = true;

                    Undo.RecordObject(generatorTransform, "Rotated transform");
                    generatorTransform.RotateAround(generatorTransform.TransformPoint(center), generatorTransform.up, 90);
                }

                if (SceneHandles.ClickableLabel(pLabel0, (pLabel0 - bounds.center).normalized, antiClockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
                {
                    bounds.center = Vector3.zero;
                    var size = bounds.size;
                    var sizeX = size.x;
                    var sizeZ = size.z;
                    size.x = sizeZ;
                    size.z = sizeX;
                    bounds.size = size;
                    bounds.center = center;
                    newDefinition.bounds = bounds;
                    GUI.changed = true;

                    Undo.RecordObject(generatorTransform, "Rotated transform");
                    generatorTransform.RotateAround(generatorTransform.TransformPoint(center), generatorTransform.up, -90);
                }
                 

                EditorGUI.BeginChangeCheck();
                {
                    edgeHeight = SceneHandles.Edge1DHandle(Axis.Y, pHeight0, pHeight1, snappingStep: stepHeight);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    var totalStepHeight = (max.y - Mathf.Clamp(edgeHeight, stepHeight, max.y));
                    const float kSmudgeValue = 0.0001f;
                    var oldStepCount = newDefinition.StepCount;
                    var newStepCount = Mathf.Max(1, Mathf.FloorToInt((Mathf.Abs(totalStepHeight) + kSmudgeValue) / stepHeight));

                    newDefinition.stepDepth        = (oldStepCount * newDefinition.stepDepth) / newStepCount;
                    newDefinition.plateauHeight    = bounds.size.y - (stepHeight * newStepCount);
                }

                EditorGUI.BeginChangeCheck();
                {
                    stepDepthOffset = SceneHandles.Edge1DHandle(Axis.Z, pDepth0,  pDepth1,  snappingStep: ChiselLinearStairsDefinition.kMinStepDepth) - min.z;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    stepDepthOffset = Mathf.Clamp(stepDepthOffset, 0, currentDefinition.depth - ChiselLinearStairsDefinition.kMinStepDepth);
                    newDefinition.stepDepth = (currentDefinition.depth - stepDepthOffset) / currentDefinition.StepCount;
                }

                var depthOffset = 0.0f;
                EditorGUI.BeginChangeCheck();
                {
                    var depth0 = SceneHandles.Edge1DHandleOffset(Axis.Z, pHeight0, pDepth0, snappingStep: stepHeight);
                    var depth1 = SceneHandles.Edge1DHandleOffset(Axis.Z, pHeight1, pDepth1, snappingStep: stepHeight);
                    if (Mathf.Abs(depth0) > Mathf.Abs(depth1)) depthOffset = depth0; else depthOffset = depth1;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    newDefinition.plateauHeight += depthOffset;
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Modified " + generator.NodeTypeName);

                generator.definition = newDefinition;
                generator.OnValidate();
                return true;
            }
            return false;
        }
    }
}
