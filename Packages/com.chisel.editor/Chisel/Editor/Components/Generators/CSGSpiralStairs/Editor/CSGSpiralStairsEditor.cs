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
    [CustomEditor(typeof(CSGSpiralStairs))]
    [CanEditMultipleObjects]
    public sealed class CSGSpiralStairsEditor : GeneratorEditor<CSGSpiralStairs> 
    {
        SerializedProperty heightProp;
        SerializedProperty outerDiameterProp;
        SerializedProperty outerSegmentsProp;
        SerializedProperty innerDiameterProp;
        SerializedProperty innerSegmentsProp;
        SerializedProperty stepHeightProp;
        SerializedProperty nosingDepthProp;
        SerializedProperty nosingWidthProp;
        SerializedProperty treadHeightProp;
        SerializedProperty startAngleProp;
        SerializedProperty rotationProp;
        SerializedProperty riserTypeProp;
        SerializedProperty riserDepthProp;
        SerializedProperty bottomSmoothingGroupProp;
        
        protected override void ResetInspector()
        {
            heightProp					= null;
            outerDiameterProp			= null;
            outerSegmentsProp			= null;
            innerDiameterProp			= null;
            innerSegmentsProp			= null;
            stepHeightProp				= null;
            nosingDepthProp				= null;
            nosingWidthProp				= null;
            treadHeightProp				= null;
            startAngleProp				= null;
            rotationProp				= null;
            riserTypeProp				= null;
            riserDepthProp				= null;
            bottomSmoothingGroupProp	= null;
        }
        
        protected override void InitInspector()
        { 
            heightProp					= serializedObject.FindProperty("definition.height");
            outerDiameterProp			= serializedObject.FindProperty("definition.outerDiameter");
            outerSegmentsProp			= serializedObject.FindProperty("definition.outerSegments");
            innerDiameterProp			= serializedObject.FindProperty("definition.innerDiameter");
            innerSegmentsProp			= serializedObject.FindProperty("definition.innerSegments");
            stepHeightProp				= serializedObject.FindProperty("definition.stepHeight");
            treadHeightProp				= serializedObject.FindProperty("definition.treadHeight");
            nosingDepthProp				= serializedObject.FindProperty("definition.nosingDepth");
            nosingWidthProp				= serializedObject.FindProperty("definition.nosingWidth");
            startAngleProp				= serializedObject.FindProperty("definition.startAngle");
            rotationProp				= serializedObject.FindProperty("definition.rotation");
            riserTypeProp				= serializedObject.FindProperty("definition.riserType");
            riserDepthProp				= serializedObject.FindProperty("definition.riserDepth");
            bottomSmoothingGroupProp	= serializedObject.FindProperty("definition.bottomSmoothingGroup");
        }
        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(heightProp);
            EditorGUILayout.PropertyField(outerDiameterProp);
            EditorGUILayout.PropertyField(outerSegmentsProp);
            EditorGUILayout.PropertyField(innerDiameterProp);
            EditorGUILayout.PropertyField(innerSegmentsProp);
            EditorGUILayout.PropertyField(stepHeightProp);
            EditorGUILayout.PropertyField(treadHeightProp);
            EditorGUILayout.PropertyField(nosingDepthProp);
            EditorGUILayout.PropertyField(nosingWidthProp);
            EditorGUILayout.PropertyField(startAngleProp);
            EditorGUILayout.PropertyField(rotationProp);
            EditorGUILayout.PropertyField(riserTypeProp);
            EditorGUILayout.PropertyField(riserDepthProp);
            EditorGUILayout.PropertyField(bottomSmoothingGroupProp);
        }
        
                        
        // TODO: put somewhere else

        static readonly int s_RotatedEdge2DHash = "RotatedEdge2D".GetHashCode();

        public static float RotatedEdge2DHandle(float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize = 0.0f, UnitySceneExtensions.SceneHandles.CapFunction capFunction = null, Axes axes = Axes.None)
        {
            var id = GUIUtility.GetControlID (s_RotatedEdge2DHash, FocusType.Keyboard);
            return RotatedEdge2DHandle(id, angle, origin, diameter, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes);
        }

        static float rotatedStartAngle = 0.0f;
        static float rotatedAngleOffset = 0.0f;
        public static float RotatedEdge2DHandle(int id, float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize = 0.0f, UnitySceneExtensions.SceneHandles.CapFunction capFunction = null, Axes axes = Axes.None) 
        {
            var from		= origin;
            var vector		= Quaternion.AngleAxis(angle, handleDir) * Vector3.forward;
            var to			= from + (vector * diameter);
            var position	= from + (vector * (diameter * 0.5f));

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToLine(from, to) * 0.5f);
                    break;
                }
                case EventType.Repaint:
                {
                    var sceneView = SceneView.currentDrawingSceneView;
                    if (sceneView)
                    {
                        if (UnityEditor.HandleUtility.nearestControl == id || EditorGUIUtility.hotControl == id)
                        {
                            var rect = sceneView.position;
                            rect.min = Vector2.zero;
                            EditorGUIUtility.AddCursorRect(rect, UnitySceneExtensions.SceneHandleUtility.GetCursorForEdge(from, to));
                        }
                    }
                    if (EditorGUIUtility.keyboardControl == id)
                        UnityEditor.Handles.DrawAAPolyLine(3.0f, from, to);
                    else
                        UnityEditor.Handles.DrawAAPolyLine(2.5f, from, to);
                    break;
                }
            }

            if (handleSize == 0.0f)
                handleSize = UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f;

            
            if (evt.GetTypeForControl(id) == EventType.MouseDown &&
                GUIUtility.hotControl == 0 &&
                ((UnityEditor.HandleUtility.nearestControl == id && evt.button == 0) ||
                 (GUIUtility.keyboardControl == id && evt.button == 2)))
            {
                rotatedStartAngle = angle;
                rotatedAngleOffset = 0.0f;
            }

            var newPosition = UnitySceneExtensions.SceneHandles.Slider2D.Do(id, to, position, Vector3.zero, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes);
            
            if (GUIUtility.hotControl != id)
                return angle;

            rotatedAngleOffset += Utilities.GeometryMath.SignedAngle(vector, (newPosition - origin).normalized, handleDir);

            
            // TODO: put somewhere else
            if (!Snapping.RotateSnappingActive)
            {
                return rotatedStartAngle + rotatedAngleOffset;
            }

            var rotateSnap = CSGEditorSettings.RotateSnap;
            var newAngle		= rotatedStartAngle + rotatedAngleOffset;
            var snappedAngle	= (int)Mathf.Round(newAngle / rotateSnap) * rotateSnap;
            return snappedAngle;
        }

        


        Vector3[] innerVertices;
        Vector3[] outerVertices;

        
        protected override void OnScene(CSGSpiralStairs generator)
        {
            var normal					= Vector3.up;
            var topDirection			= Vector3.forward;
            var lowDirection			= Vector3.forward;

            var originalOuterDiameter	= generator.OuterDiameter;
            var originalInnerDiameter	= generator.InnerDiameter;
            var originalStartAngle		= generator.StartAngle;
            var originalStepHeight		= generator.StepHeight;
            var originalRotation		= generator.Rotation;
            var originalHeight			= generator.Height;
            var originalOrigin			= generator.Origin;
            var cylinderTop				= new CSGCircleDefinition (1, originalOrigin.y + originalHeight);
            var cylinderLow				= new CSGCircleDefinition (1, originalOrigin.y);
            var originalTopPoint		= normal * cylinderTop.height;
            var originalLowPoint		= normal * cylinderLow.height;
            var originalMidPoint		= (originalTopPoint + originalLowPoint) * 0.5f;
                    
            var outerDiameter		= originalOuterDiameter;
            var innerDiameter		= originalInnerDiameter;
            var topPoint			= originalTopPoint;
            var lowPoint			= originalLowPoint;
            var midPoint			= originalMidPoint;
            var startAngle			= originalStartAngle;
            var rotation			= originalRotation;

            EditorGUI.BeginChangeCheck();
            {
                var startRotateEdgeID	= GUIUtility.GetControlID ("SpiralStairsStartAngle".GetHashCode(), FocusType.Keyboard);
                var endRotateEdgeID		= GUIUtility.GetControlID ("SpiralStairsEndAngle".GetHashCode(), FocusType.Keyboard);
                        
                // TODO: properly show things as backfaces
                // TODO: temporarily show inner or outer diameter as disabled when resizing one or the other
                // TODO: FIXME: why aren't there any arrows?
                topPoint		= UnitySceneExtensions.SceneHandles.DirectionHandle(topPoint,  normal, snappingStep: originalStepHeight);
                topPoint.y		= Mathf.Max(lowPoint.y + originalStepHeight, topPoint.y);
                lowPoint		= UnitySceneExtensions.SceneHandles.DirectionHandle(lowPoint, -normal, snappingStep: originalStepHeight);
                lowPoint.y		= Mathf.Min(topPoint.y - originalStepHeight, lowPoint.y);

                float minOuterDiameter = innerDiameter + CSGSpiralStairsDefinition.kMinStairsDepth;						
                outerDiameter		= Mathf.Max(minOuterDiameter, UnitySceneExtensions.SceneHandles.RadiusHandle(Vector3.up, topPoint, outerDiameter * 0.5f, renderDisc: false) * 2.0f);
                outerDiameter		= Mathf.Max(minOuterDiameter, UnitySceneExtensions.SceneHandles.RadiusHandle(Vector3.up, lowPoint, outerDiameter * 0.5f, renderDisc: false) * 2.0f);
                        
                float maxInnerDiameter = outerDiameter - CSGSpiralStairsDefinition.kMinStairsDepth;
                innerDiameter		= Mathf.Min(maxInnerDiameter, UnitySceneExtensions.SceneHandles.RadiusHandle(Vector3.up, midPoint, innerDiameter * 0.5f, renderDisc: false) * 2.0f);

                startAngle = RotatedEdge2DHandle(startRotateEdgeID, startAngle           , lowPoint, outerDiameter * 0.5f, normal, lowDirection, Vector3.Cross(normal, lowDirection));
                rotation   = RotatedEdge2DHandle(endRotateEdgeID,   startAngle + rotation, topPoint, outerDiameter * 0.5f, normal, topDirection, Vector3.Cross(normal, topDirection)) - startAngle;




                // TODO: somehow put this into a separate renderer
                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalInnerDiameter;
                BrushMeshAssetFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, generator.InnerSegments, ref innerVertices);

                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalOuterDiameter;
                BrushMeshAssetFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, generator.OuterSegments, ref outerVertices);
                
                var originalColor	= UnityEditor.Handles.yAxisColor;
                var color			= Color.Lerp(originalColor, UnitySceneExtensions.SceneHandles.staticColor, UnitySceneExtensions.SceneHandles.staticBlend);
                var outlineColor	= Color.black;
                outlineColor.a = color.a;

                UnityEditor.Handles.color = outlineColor;
                {
                    var sides = generator.OuterSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = outerVertices[i];
                        var t1 = outerVertices[j];
                        var b0 = outerVertices[i + sides];
                        var b1 = outerVertices[j + sides];

                        UnityEditor.Handles.DrawAAPolyLine(3.0f, t0, b0);
                        UnityEditor.Handles.DrawAAPolyLine(3.0f, t0, t1);
                        UnityEditor.Handles.DrawAAPolyLine(3.0f, b0, b1);
                    }
                }
                {
                    var sides = generator.InnerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = innerVertices[i];
                        var t1 = innerVertices[j];
                        var b0 = innerVertices[i + sides];
                        var b1 = innerVertices[j + sides];

                        UnityEditor.Handles.DrawAAPolyLine(3.0f, t0, b0);
                        UnityEditor.Handles.DrawAAPolyLine(3.0f, t0, t1);
                        UnityEditor.Handles.DrawAAPolyLine(3.0f, b0, b1);
                    }
                }

                UnityEditor.Handles.color = originalColor;
                {
                    var sides = generator.OuterSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = outerVertices[i];
                        var t1 = outerVertices[j];
                        var b0 = outerVertices[i + sides];
                        var b1 = outerVertices[j + sides];

                        UnityEditor.Handles.DrawAAPolyLine(2.0f, t0, b0);
                        UnityEditor.Handles.DrawAAPolyLine(2.0f, t0, t1);
                        UnityEditor.Handles.DrawAAPolyLine(2.0f, b0, b1);
                    }
                }
                {
                    var sides = generator.InnerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = innerVertices[i];
                        var t1 = innerVertices[j];
                        var b0 = innerVertices[i + sides];
                        var b1 = innerVertices[j + sides];

                        UnityEditor.Handles.DrawAAPolyLine(2.0f, t0, b0);
                        UnityEditor.Handles.DrawAAPolyLine(2.0f, t0, t1);
                        UnityEditor.Handles.DrawAAPolyLine(2.0f, b0, b1);

                        var m0 = (t0 + b0) * 0.5f;
                        var m1 = (t1 + b1) * 0.5f;
                        UnityEditor.Handles.DrawDottedLine(m0, m1, 4.0f);
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.OuterDiameter = outerDiameter;
                generator.InnerDiameter = innerDiameter;
                generator.StartAngle  = startAngle;
                generator.Rotation	  = rotation;

                if (topPoint != originalTopPoint)
                    generator.Height	= topPoint.y - lowPoint.y;

                if (lowPoint != originalLowPoint)
                {
                    generator.Height	= topPoint.y - lowPoint.y;
                    var newOrigin = originalOrigin;
                    newOrigin.y += lowPoint.y - originalLowPoint.y;
                    generator.Origin = newOrigin;
                }

                generator.OnValidate();
            }
        }
    }
}
