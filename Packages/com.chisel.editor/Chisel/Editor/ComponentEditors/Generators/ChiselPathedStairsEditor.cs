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
    public sealed class ChiselPathedStairsDetails : ChiselGeneratorDetails<ChiselPathedStairs>
    {
    }

    [CustomEditor(typeof(ChiselPathedStairs))]
    [CanEditMultipleObjects]
    public sealed class ChiselPathedStairsEditor : ChiselGeneratorEditor<ChiselPathedStairs>
    {
        [MenuItem("GameObject/Chisel/" + ChiselPathedStairs.kNodeTypeName)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselPathedStairs.kNodeTypeName); }

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

        static GUIContent   shapeContent	= new GUIContent("Shape");
        
        SerializedProperty shapeProp;
        SerializedProperty closedProp;
        SerializedProperty curveSegments;

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
            shapeProp			= null;
            closedProp			= null;
            curveSegments		= null;

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

            surfacesProp = null;
        }

        protected override void InitInspector()
        {
            var definitionProp = serializedObject.FindProperty(nameof(ChiselPathedStairs.definition));
            {
                shapeProp			= definitionProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.shape.controlPoints));
                closedProp			= definitionProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.shape.closed));
                curveSegments		= definitionProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.curveSegments));

                var stairsProp      = serializedObject.FindProperty(nameof(ChiselPathedStairs.definition.stairs));
                {
                    boundsProp			= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.bounds));
                    stepHeightProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.stepHeight));
                    stepDepthProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.stepDepth));
                    plateauHeightProp	= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.plateauHeight));
                    riserTypeProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.riserType));
                    riserDepthProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.riserDepth));
                    leftSideProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.leftSide));
                    rightSideProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.rightSide));
                    sideDepthProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.sideDepth));
                    sideWidthProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.sideWidth));
                    sideHeightProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.sideHeight));
                    treadHeightProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.treadHeight));
                    nosingDepthProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.nosingDepth));
                    nosingWidthProp		= stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.nosingWidth));

                    var surfDefProp     = stairsProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.surfaceDefinition));
                    {
                        surfacesProp    = surfDefProp.FindPropertyRelative(nameof(ChiselPathedStairs.definition.stairs.surfaceDefinition.surfaces));
                    }
                }
            }
        }

        protected override void OnInspector()
        {
            EditorGUILayout.PropertyField(shapeProp, shapeContent, true);
            EditorGUILayout.PropertyField(closedProp);
            EditorGUILayout.PropertyField(curveSegments);

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

        public static bool Intersect(Vector2 p1, Vector2 d1, 
                                     Vector2 p2, Vector2 d2, out Vector2 intersection)
        {
            const float kEpsilon = 0.0001f;

            var f = d1.y * d2.x - d1.x * d2.y;
            // check if the rays are parallel
            if (f >= -kEpsilon && f <= kEpsilon)
            {
                intersection = Vector2.zero;
                return false;
            }

            var c0 = p1 - p2;
            var t  = (d2.y * c0.x - d2.x * c0.y) / f;
            intersection = p1 + (t * d1);
            return true;
        }

        protected override void OnScene(ChiselPathedStairs generator)
        {
            var shape = generator.Shape;
            EditorGUI.BeginChangeCheck();
            {
                shape = UnitySceneExtensions.SceneHandles.Curve2DHandle(Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(90, Vector3.right), Vector3.one), shape);
                /*
                var shapeVertices = new List<Vector2>();
                var shapeSegmentIndices = new List<int>();
                BrushFactory.GetPathVertices(shape, generator.definition.curveSegments, shapeVertices, shapeSegmentIndices);
            
                var definition	= generator.definition;
                var depth		= definition.stairs.depth * 0.5f; // why * 0.5f?
                var height		= definition.stairs.height * 0.5f; // why * 0.5f?
                var width		= definition.stairs.width * 0.5f; // why * 0.5f?

                for (int vi0 = shapeVertices.Count - 3, vi1 = shapeVertices.Count - 2, vi2 = shapeVertices.Count - 1, vi3 = 0; vi3 < shapeVertices.Count; vi0 = vi1, vi1 = vi2, vi2 = vi3, vi3++)
                {
                    if (vi2 == 0 && !definition.shape.closed)
                        continue;

                    var v0 = shapeVertices[vi0];
                    var v1 = shapeVertices[vi1];
                    var v2 = shapeVertices[vi2];
                    var v3 = shapeVertices[vi3];

                    var m0 = (v0 + v1) * 0.5f;
                    var m1 = (v1 + v2) * 0.5f;
                    var m2 = (v2 + v3) * 0.5f;
                    var d0 = (v1 - v0);
                    var d1 = (v2 - v1);
                    var d2 = (v3 - v2);
                    var maxWidth0	= d0.magnitude;
                    var maxWidth1	= d1.magnitude;
                    var maxWidth2	= d2.magnitude;
                    var halfWidth0	= maxWidth0 * 0.5f;
                    var halfWidth1	= maxWidth1 * 0.5f;
                    var halfWidth2	= maxWidth2 * 0.5f;
                    d0 /= maxWidth0;
                    d1 /= maxWidth1;
                    d2 /= maxWidth2;
                    var depthVector0 = new Vector2(d0.y, -d0.x) * (depth * 2.0f);
                    var depthVector1 = new Vector2(d1.y, -d1.x) * (depth * 2.0f);
                    var depthVector2 = new Vector2(d2.y, -d2.x) * (depth * 2.0f);


                    var depthVector = new Vector3(d1.y, 0, -d1.x);

                    var lineCenter = new Vector3(m1.x, 0, m1.y);
                    m0 -= depthVector0;
                    m1 -= depthVector1;
                    m2 -= depthVector2;

                    var leftShear	= Vector3.Dot((d0 * (depth * 2.0f)), depthVector);
                    var rightShear	= Vector3.Dot((d2 * (depth * 2.0f)), depthVector);
                    
                    var lineA0		= m0 - (d0 * halfWidth0);
                    var lineB0		= m0 + (d0 * halfWidth0);
                    
                    var lineA1		= m1 - (d1 * halfWidth1);
                    var lineB1		= m1 + (d1 * halfWidth1);

                    var lineA2		= m2 - (d2 * halfWidth2);
                    var lineB2		= m2 + (d2 * halfWidth2);
                    
                    Vector2 output;
                    var minP		= Intersect(m1, d1, m0, d0, out output) ? output : lineA1;
                    var maxP		= Intersect(m1, d1, m2, d2, out output) ? output : lineB1;
                    
                    var lineCenter0 = new Vector3(m0.x,   (height * 2.0f), m0  .y);
                    var lineCenter1 = new Vector3(m1.x,   (height * 2.0f), m1  .y);
                    var lineCenter2 = new Vector3(m2.x,   (height * 2.0f), m2  .y);
                    var lineMinP	= new Vector3(minP.x, (height * 2.0f), minP.y);
                    var lineMaxP	= new Vector3(maxP.x, (height * 2.0f), maxP.y);

                    var point = lineCenter1;
                    var size = UnityEditor.HandleUtility.GetHandleSize(point) * UnitySceneExtensions.Handles.Curve2DHandleUtility.kCurvePointSize;
                    
                    UnitySceneExtensions.Handles.RenderBorderedDot(point, size);
                    
                    point = lineCenter0;
                    size = UnityEditor.HandleUtility.GetHandleSize(point) * UnitySceneExtensions.Handles.Curve2DHandleUtility.kCurvePointSize;
                    UnitySceneExtensions.Handles.RenderBorderedDot(point, size);
                    
                    point = lineCenter2;
                    size = UnityEditor.HandleUtility.GetHandleSize(point) * UnitySceneExtensions.Handles.Curve2DHandleUtility.kCurvePointSize;
                    UnitySceneExtensions.Handles.RenderBorderedDot(point, size);
                    
                    point = lineMinP;
                    size = UnityEditor.HandleUtility.GetHandleSize(point) * UnitySceneExtensions.Handles.Curve2DHandleUtility.kCurvePointSize;
                    UnitySceneExtensions.Handles.RenderBorderedDot(point, size);

                    point = lineMaxP;
                    size = UnityEditor.HandleUtility.GetHandleSize(point) * UnitySceneExtensions.Handles.Curve2DHandleUtility.kCurvePointSize;
                    UnitySceneExtensions.Handles.RenderBorderedDot(point, size);
                    
                    UnityEditor.Handles.color = Color.red;
                    UnityEditor.Handles.DrawLine(new Vector3(lineA0.x, (height * 2.0f), lineA0.y), new Vector3(lineB0.x, (height * 2.0f), lineB0.y));

                    UnityEditor.Handles.color = Color.green;
                    UnityEditor.Handles.DrawLine(new Vector3(lineA1.x, (height * 2.0f), lineA1.y), new Vector3(lineB1.x, (height * 2.0f), lineB1.y));

                    UnityEditor.Handles.color = Color.yellow;
                    UnityEditor.Handles.DrawLine(new Vector3(lineA2.x, (height * 2.0f), lineA2.y), new Vector3(lineB2.x, (height * 2.0f), lineB2.y));
                }*/
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.Shape = shape;
            }
        }
    }
}