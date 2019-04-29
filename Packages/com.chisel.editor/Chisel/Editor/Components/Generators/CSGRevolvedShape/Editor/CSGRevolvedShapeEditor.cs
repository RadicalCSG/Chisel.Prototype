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
    public sealed class CSGRevolvedShapeDetails : ChiselGeneratorDetails<CSGRevolvedShape>
    {
    }

    [CustomEditor(typeof(CSGRevolvedShape))]
    [CanEditMultipleObjects]
    public sealed class CSGRevolvedShapeEditor : ChiselGeneratorEditor<CSGRevolvedShape>
    {
        static GUIContent   shapeContent	= new GUIContent("Shape");

        SerializedProperty shapeProp;
        SerializedProperty curveSegments;
        SerializedProperty revolveSegments;
        SerializedProperty startAngleProp;
        SerializedProperty totalAngleProp;

        protected override void ResetInspector()
        {
            shapeProp				= null;
            curveSegments			= null;
            revolveSegments			= null;
            startAngleProp			= null;
            totalAngleProp			= null;
        }

        protected override void InitInspector()
        {
            curveSegments			= serializedObject.FindProperty("definition.curveSegments");
            revolveSegments			= serializedObject.FindProperty("definition.revolveSegments");
            startAngleProp			= serializedObject.FindProperty("definition.startAngle");
            totalAngleProp			= serializedObject.FindProperty("definition.totalAngle");
            shapeProp				= serializedObject.FindProperty("definition.shape.controlPoints");
        }

        protected override void OnInspector()
        {
            EditorGUILayout.PropertyField(shapeProp, shapeContent, true);
            EditorGUILayout.PropertyField(curveSegments);
            EditorGUILayout.PropertyField(revolveSegments);

            EditorGUILayout.PropertyField(startAngleProp);
            EditorGUILayout.PropertyField(totalAngleProp);
        }


        
        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        protected override void OnSceneInit(CSGRevolvedShape generator) { }

        protected override void OnScene(CSGRevolvedShape generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.forward;

            var shape			= generator.Shape;
            var controlPoints	= shape.controlPoints;
            /*
            for (int p0 = controlPoints.Length - 1, p1 = 0; p1 < controlPoints.Length; p0 = p1, p1++)
            {
                var point0 = controlPoints[p0].position;
                var point1 = controlPoints[p1].position;

            }
            */
            
            var shapeVertices		= new List<Vector2>();
            var shapeSegmentIndices = new List<int>();
            BrushMeshAssetFactory.GetPathVertices(generator.definition.shape, generator.definition.curveSegments, shapeVertices, shapeSegmentIndices);

            
            var horzSegments			= generator.definition.revolveSegments;
            var horzDegreePerSegment	= generator.definition.totalAngle / horzSegments;
            var horzOffset				= generator.definition.startAngle;
            
            var noZTestcolor = CSGCylinderEditor.GetColorForState(baseColor, false, true,  isDisabled);
            var zTestcolor	 = CSGCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            for (int h = 1, pr = 0; h < horzSegments + 1; pr = h, h++)
            {
                var hDegree0	= (pr * horzDegreePerSegment) + horzOffset;
                var hDegree1	= (h  * horzDegreePerSegment) + horzOffset;
                var rotation0	= Quaternion.AngleAxis(hDegree0, normal);
                var rotation1	= Quaternion.AngleAxis(hDegree1, normal);
                for (int p0 = controlPoints.Length - 1, p1 = 0; p1 < controlPoints.Length; p0 = p1, p1++)
                {
                    var point0	= controlPoints[p0].position;
                    //var point1	= controlPoints[p1].position;
                    var vertexA	= rotation0 * new Vector3(point0.x, 0, point0.y);
                    var vertexB	= rotation1 * new Vector3(point0.x, 0, point0.y);
                    //var vertexC	= rotation0 * new Vector3(point1.x, 0, point1.y);

                    UnityEditor.Handles.color = noZTestcolor;
                    CSGOutlineRenderer.DrawLine(vertexA, vertexB, lineMode: LineMode.NoZTest, thickness: kHorzLineThickness);//, dashSize: kLineDash);

                    UnityEditor.Handles.color = zTestcolor;
                    CSGOutlineRenderer.DrawLine(vertexA, vertexB, lineMode: LineMode.ZTest,   thickness: kHorzLineThickness);//, dashSize: kLineDash);
                }

                for (int v0 = shapeVertices.Count - 1, v1 = 0; v1 < shapeVertices.Count; v0=v1, v1++)
                {
                    var point0	= shapeVertices[v0];
                    var point1	= shapeVertices[v1];
                    var vertexA	= rotation0 * new Vector3(point0.x, 0, point0.y);
                    var vertexB	= rotation0 * new Vector3(point1.x, 0, point1.y);
                    
                    UnityEditor.Handles.color = noZTestcolor;
                    CSGOutlineRenderer.DrawLine(vertexA, vertexB, lineMode: LineMode.NoZTest, thickness: kHorzLineThickness, dashSize: kLineDash);

                    UnityEditor.Handles.color = zTestcolor;
                    CSGOutlineRenderer.DrawLine(vertexA, vertexB, lineMode: LineMode.ZTest,   thickness: kHorzLineThickness, dashSize: kLineDash);
                }
            }

            EditorGUI.BeginChangeCheck();
            {
                // TODO: make this work non grid aligned so we can place it upwards
                UnityEditor.Handles.color = baseColor;
                shape = UnitySceneExtensions.SceneHandles.Curve2DHandle(Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(90, Vector3.right), Vector3.one), shape);

                UnityEditor.Handles.DrawDottedLine(normal * 10, normal * -10, 4.0f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.Shape = shape;
            }
        }
    }
}