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
    public sealed class CSGExtrudedShapeDetails : ChiselGeneratorDetails<CSGExtrudedShape>
    {
    }
    
    [CustomEditor(typeof(CSGExtrudedShape))]
    [CanEditMultipleObjects]
    public sealed class CSGExtrudedShapeEditor : ChiselGeneratorEditor<CSGExtrudedShape> 
    {
        static GUIContent   shapeContent	= new GUIContent("Shape");
        static GUIContent   pathContent		= new GUIContent("Path");

        SerializedProperty shapeProp;
        SerializedProperty pathProp;
        
        protected override void ResetInspector()
        {
            shapeProp		= null;
            pathProp		= null;
        }
        
        protected override void InitInspector()
        { 
            shapeProp		= serializedObject.FindProperty("shape.controlPoints");
            pathProp		= serializedObject.FindProperty("path.segments");
        }
        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(shapeProp, shapeContent, true);
            EditorGUILayout.PropertyField(pathProp, pathContent, true);
        }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;


        internal static int s_Curve2DDHash = "Curve2DHash".GetHashCode();
        protected override void OnScene(CSGExtrudedShape generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.forward;
            
            var noZTestcolor	= CSGCylinderEditor.GetColorForState(baseColor, false, true,  isDisabled);
            var zTestcolor		= CSGCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            
            /*
            var shapeVertices2D		= new List<Vector2>();
            var shapeSegmentIndices = new List<int>();
            BrushFactory.GetPathVertices(generator.Shape, generator.curveSegments, shapeVertices2D, shapeSegmentIndices);
            
            var shapeVertices3D		= new Vector3[shapeVertices2D.Count];
            for (int v = 0; v < shapeVertices2D.Count; v++)
                shapeVertices3D[v] = new Vector3(shapeVertices2D[v].x, shapeVertices2D[v].y, 0);
            */
            var shape		= generator.Shape;
            var	path		= generator.Path;
            var prevMatrix	= UnityEditor.Handles.matrix;
            for (int i = 0; i < path.segments.Length; i++)
            {
                var pathPoint	= path.segments[i];
                var currMatrix	= pathPoint.ToMatrix();

                UnityEditor.Handles.color = baseColor;
                EditorGUI.BeginChangeCheck();
                {
                    shape = UnitySceneExtensions.SceneHandles.Curve2DHandle(currMatrix, shape);//, renderLines: false);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                    generator.Shape = shape;
                }
                
                //UnityEditor.Handles.color = noZTestcolor;
                //CSGOutlineRenderer.DrawLineLoop(prevMatrix * currMatrix, shapeVertices3D, 0, shapeVertices3D.Length, lineMode: LineMode.NoZTest, thickness: kCapLineThickness);

                //UnityEditor.Handles.color = zTestcolor;
                //CSGOutlineRenderer.DrawLineLoop(prevMatrix * currMatrix, shapeVertices3D, 0, shapeVertices3D.Length, lineMode: LineMode.ZTest,   thickness: kCapLineThickness);
                

                // Draw lines between different segments
                if (i + 1 < path.segments.Length)
                {
                    var nextPoint		= path.segments[i + 1];
                    var nextMatrix		= nextPoint.ToMatrix();
                    var controlPoints	= shape.controlPoints;


                    for (int c = 0; c < controlPoints.Length; c++)
                    {
                        var controlPoint = controlPoints[c].position;
                        var pointA		 = currMatrix.MultiplyPoint(controlPoint);
                        var pointB		 = nextMatrix.MultiplyPoint(controlPoint);
                        UnityEditor.Handles.color = noZTestcolor;
                        CSGOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        UnityEditor.Handles.color = zTestcolor;
                        CSGOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    {
                        var pointA = currMatrix.MultiplyPoint(Vector3.zero);
                        var pointB = nextMatrix.MultiplyPoint(Vector3.zero);
                        UnityEditor.Handles.color = zTestcolor;
                        CSGOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        UnityEditor.Handles.color = zTestcolor;
                        CSGOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    UnityEditor.Handles.color = baseColor;
                }

                // TODO: cannot rotate so far that one path plane intersects with shape on another plane
                //			... or just fail when it's wrong?
            }

            for (int i = 0; i < path.segments.Length; i++)
            {
                var pathPoint = path.segments[i];
                EditorGUI.BeginChangeCheck();
                {
                    switch (Tools.current)
                    {
                        case Tool.Move:
                        {
                            pathPoint.position = UnitySceneExtensions.SceneHandles.PositionHandle(pathPoint.position, pathPoint.rotation);
                            break;
                        }
                        case Tool.Rotate:
                        {
                            if (Event.current.type == EventType.Repaint)
                                UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(0, pathPoint.position, pathPoint.rotation, UnityEditor.HandleUtility.GetHandleSize(pathPoint.position) * 0.05f, Event.current.type);
                            pathPoint.rotation = UnityEditor.Handles.RotationHandle(pathPoint.rotation, pathPoint.position);
                            break;
                        }
                        case Tool.Scale:
                        {
                            var scale2D = pathPoint.scale;
                            // TODO: create a 2D planar/bounds scale handle
                            var scale3D = UnityEditor.Handles.ScaleHandle(new Vector3(scale2D.x, 1, scale2D.y), pathPoint.position, pathPoint.rotation, UnityEditor.HandleUtility.GetHandleSize(pathPoint.position));
                            pathPoint.scale = new Vector2(scale3D.x, scale3D.z);
                        }
                        break;
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "Changed path of CSGShape");
                    var originalSegments = generator.Path.segments;
                    path = new Path(new PathPoint[originalSegments.Length]);
                    Array.Copy(originalSegments, path.segments, originalSegments.Length);
                    path.segments[i] = pathPoint;
                    generator.Path = path;
                }
            }


            // TODO: draw curved path
        }
    }
}
