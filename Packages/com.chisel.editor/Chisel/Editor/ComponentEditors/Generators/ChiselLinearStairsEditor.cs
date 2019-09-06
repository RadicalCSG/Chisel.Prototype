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
    [CustomEditor(typeof(ChiselLinearStairs))]
    [CanEditMultipleObjects]
    public sealed class ChiselLinearStairsEditor : ChiselGeneratorEditor<ChiselLinearStairs>
    {
        [MenuItem("GameObject/Chisel/" + ChiselLinearStairs.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselLinearStairs.kNodeTypeName); }


        #region Selection
        static Dictionary<ChiselLinearStairs, ChiselLinearStairsDefinition> activeGeneratorDefinitions = new Dictionary<ChiselLinearStairs, ChiselLinearStairsDefinition>();

        protected override void OnUndoRedoPerformed() { UpdateDefinitions(); }
        protected override void OnTargetModifiedInInspector() { generatorModified = true; UpdateDefinitions(); }

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

                var min			= new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z));
                var max			= new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z));

                
                var size        = (max - min);
                var center      = (max + min) * 0.5f;

                var heightStart = bounds.max.y + (bounds.size.y < 0 ? size.y : 0);

                var edgeHeight  = heightStart - stepHeight * stepCount;
                var pHeight0	= new Vector3(min.x, edgeHeight, max.z);
                var pHeight1	= new Vector3(max.x, edgeHeight, max.z);

                var depthStart = bounds.min.z - (bounds.size.z < 0 ? size.z : 0);

                var pDepth0		= new Vector3(min.x, max.y, depthStart + stepDepthOffset);
                var pDepth1		= new Vector3(max.x, max.y, depthStart + stepDepthOffset);

                var worldCenter     = generatorTransform.TransformPoint(center);
                var iconDirection   = (cameraPosition - worldCenter).normalized;

                var dotX        = Vector3.Dot(generatorTransform.TransformVector(new Vector3(1, 0, 0)), iconDirection);
                var dotZ        = Vector3.Dot(generatorTransform.TransformVector(new Vector3(0, 0, 1)), iconDirection);
                var dotY        = Vector3.Dot(generatorTransform.TransformVector(new Vector3(0, 1, 0)), iconDirection);

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

                // TODO: consider putting both buttons next to each other
                //  - buttons closer to each other, which is nicer when you need to go back and forth (although you could just click 3 times to go back)
                //  - since you'd only have 1 button group, the chance is higher it's outside of the screen. 
                //    so a better solution should be found to make sure the button group doesn't overlap the stairs, yet is close to it, and on screen.
                if (SceneHandles.ClickableLabel(pLabel1, (pLabel1 - center).normalized, clockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
                {
                    var newSize = bounds.size;
                    var t = newSize.x; newSize.x = newSize.z; newSize.z = t;
                    bounds.size = newSize;
                    newDefinition.bounds = bounds;
                    GUI.changed = true;

                    Undo.RecordObject(generatorTransform, "Rotated transform");
                    generatorTransform.RotateAround(generatorTransform.TransformPoint(center), generatorTransform.up, 90);
                }

                if (SceneHandles.ClickableLabel(pLabel0, (pLabel0 - center).normalized, antiClockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
                {
                    var newSize = bounds.size;
                    var t = newSize.x; newSize.x = newSize.z; newSize.z = t;
                    bounds.size = newSize;
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
                    var totalStepHeight = Mathf.Clamp((heightStart - edgeHeight), size.y % stepHeight, size.y);
                    const float kSmudgeValue = 0.0001f;
                    var oldStepCount = newDefinition.StepCount;
                    var newStepCount = Mathf.Max(1, Mathf.FloorToInt((Mathf.Abs(totalStepHeight) + kSmudgeValue) / stepHeight));

                    newDefinition.stepDepth     = (oldStepCount * newDefinition.stepDepth) / newStepCount;
                    newDefinition.plateauHeight = size.y - (stepHeight * newStepCount);
                }

                EditorGUI.BeginChangeCheck();
                {
                    stepDepthOffset = SceneHandles.Edge1DHandle(Axis.Z, pDepth0,  pDepth1, snappingStep: ChiselLinearStairsDefinition.kMinStepDepth) - depthStart;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    stepDepthOffset = Mathf.Clamp(stepDepthOffset, 0, currentDefinition.absDepth - ChiselLinearStairsDefinition.kMinStepDepth);
                    newDefinition.stepDepth = ((currentDefinition.absDepth - stepDepthOffset) / currentDefinition.StepCount);
                }

                var heightOffset = 0.0f;
                EditorGUI.BeginChangeCheck();
                {
                    var direction = Vector3.Cross(Vector3.forward, pHeight0 - pDepth0).normalized;
                    var height0 = Vector3.Dot(direction, SceneHandles.Edge1DHandleOffset(Axis.Y, pHeight0, pDepth0, direction, snappingStep: stepHeight));
                    var height1 = Vector3.Dot(direction, SceneHandles.Edge1DHandleOffset(Axis.Y, pHeight1, pDepth1, direction, snappingStep: stepHeight));
                    if (Mathf.Abs(height0) > Mathf.Abs(height1)) heightOffset = height0; else heightOffset = height1;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    newDefinition.plateauHeight += heightOffset;
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
