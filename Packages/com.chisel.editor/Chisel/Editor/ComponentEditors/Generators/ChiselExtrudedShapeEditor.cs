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
    public sealed class ChiselExtrudedShapeDetails : ChiselGeneratorDetails<ChiselExtrudedShape>
    {
    }
    
    [CustomEditor(typeof(ChiselExtrudedShape))]
    [CanEditMultipleObjects]
    public sealed class ChiselExtrudedShapeEditor : ChiselGeneratorEditor<ChiselExtrudedShape>
    {
        [MenuItem("GameObject/Chisel/" + ChiselExtrudedShape.kNodeTypeName)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselExtrudedShape.kNodeTypeName); }

        static readonly GUIContent  kShapeContent   = new GUIContent("Shape");
        static readonly GUIContent  kPathContent    = new GUIContent("Path");

        SerializedProperty controlPointsProp;
        SerializedProperty pathSegmentsProp;
        SerializedProperty surfacesProp;

        protected override void ResetInspector()
        {
            controlPointsProp   = null;
            pathSegmentsProp    = null;

            surfacesProp        = null;
        }
        
        protected override void InitInspector()
        {
            var definitionProp  = serializedObject.FindProperty(nameof(ChiselExtrudedShape.definition));
            {
                var shapeProp		= definitionProp.FindPropertyRelative(nameof(ChiselExtrudedShape.definition.shape));
                controlPointsProp   = shapeProp.FindPropertyRelative(nameof(ChiselExtrudedShape.definition.shape.controlPoints));

                var pathProp		= definitionProp.FindPropertyRelative(nameof(ChiselExtrudedShape.definition.path));
                pathSegmentsProp	= pathProp.FindPropertyRelative(nameof(ChiselExtrudedShape.definition.path.segments));

                var surfDefProp     = definitionProp.FindPropertyRelative(nameof(ChiselExtrudedShape.definition.surfaceDefinition));
                {
                    surfacesProp    = surfDefProp.FindPropertyRelative(nameof(ChiselExtrudedShape.definition.surfaceDefinition.surfaces));
                }
            }
        }
        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(controlPointsProp, kShapeContent, true);
            EditorGUILayout.PropertyField(pathSegmentsProp,  kPathContent,  true);

            ShowSurfaces(surfacesProp);
        }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;


        internal static int s_Curve2DDHash = "Curve2DHash".GetHashCode();
        protected override void OnScene(ChiselExtrudedShape generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.forward;
            
            var noZTestcolor	= ChiselCylinderEditor.GetColorForState(baseColor, false, true,  isDisabled);
            var zTestcolor		= ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            
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
                        ChiselOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        UnityEditor.Handles.color = zTestcolor;
                        ChiselOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    {
                        var pointA = currMatrix.MultiplyPoint(Vector3.zero);
                        var pointB = nextMatrix.MultiplyPoint(Vector3.zero);
                        UnityEditor.Handles.color = zTestcolor;
                        ChiselOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        UnityEditor.Handles.color = zTestcolor;
                        ChiselOutlineRenderer.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
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
                    path = new ChiselPath(new ChiselPathPoint[originalSegments.Length]);
                    Array.Copy(originalSegments, path.segments, originalSegments.Length);
                    path.segments[i] = pathPoint;
                    generator.Path = path;
                }
            }


            // TODO: draw curved path
        }
    }
}
