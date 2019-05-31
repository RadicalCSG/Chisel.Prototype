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
    public sealed class ChiselCylinderDetails : ChiselGeneratorDetails<ChiselCylinder>
    {
    }
    
    // TODO: why did resetting this generator not work?
    // TODO: make drag & drop of materials on generator side work
    [CustomEditor(typeof(ChiselCylinder))]
    [CanEditMultipleObjects]
    public sealed class ChiselCylinderEditor : ChiselGeneratorEditor<ChiselCylinder>
    {
        GUIContent[] kSurfaceNameContent = new[]
        {
            new GUIContent("Top"),
            new GUIContent("Bottom")
        };

        SerializedProperty typeProp;
        SerializedProperty topHeightProp;
        SerializedProperty topDiameterXProp;
        SerializedProperty topDiameterZProp;
        SerializedProperty bottomHeightProp;
        SerializedProperty bottomDiameterXProp;
        SerializedProperty bottomDiameterZProp;
        SerializedProperty rotationProp;
        SerializedProperty isEllipsoidProp;
        SerializedProperty smoothingGroupProp;
        SerializedProperty sidesProp;
        SerializedProperty surfacesProp;
        
        protected override void ResetInspector()
        { 
            typeProp				= null;
            topHeightProp			= null;
            topDiameterXProp		= null;
            topDiameterZProp		= null;
            bottomHeightProp		= null;
            bottomDiameterXProp		= null;
            bottomDiameterZProp		= null;
            isEllipsoidProp			= null;
            smoothingGroupProp		= null;
            sidesProp				= null;

            surfacesProp            = null;
        }
        
        protected override void InitInspector()
        { 
            var definitionProp      = serializedObject.FindProperty(nameof(ChiselCylinder.definition));
            { 
                typeProp			    = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.type));

                var topProp             = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.top));
                { 
                    topHeightProp	    = topProp.FindPropertyRelative(nameof(ChiselCylinder.definition.top.height));
                    topDiameterXProp    = topProp.FindPropertyRelative(nameof(ChiselCylinder.definition.top.diameterX));
                    topDiameterZProp    = topProp.FindPropertyRelative(nameof(ChiselCylinder.definition.top.diameterZ));
                }
            
                var bottomProp          = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.bottom));
                { 
                    bottomHeightProp    = bottomProp.FindPropertyRelative(nameof(ChiselCylinder.definition.bottom.height));
                    bottomDiameterXProp = bottomProp.FindPropertyRelative(nameof(ChiselCylinder.definition.bottom.diameterX));
                    bottomDiameterZProp = bottomProp.FindPropertyRelative(nameof(ChiselCylinder.definition.bottom.diameterZ));
                }

                rotationProp		    = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.rotation));
                isEllipsoidProp		    = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.isEllipsoid));
                smoothingGroupProp	    = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.smoothingGroup));
                sidesProp			    = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.sides));
                
                var surfDefProp         = definitionProp.FindPropertyRelative(nameof(ChiselCylinder.definition.surfaceDefinition));
                {
                    surfacesProp        = surfDefProp.FindPropertyRelative(nameof(ChiselCylinder.definition.surfaceDefinition.surfaces));
                }
            }
        }
        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(typeProp);
            EditorGUILayout.PropertyField(isEllipsoidProp);
            
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Top");
            EditorGUI.indentLevel++;
            {
                EditorGUILayout.PropertyField(topHeightProp);
                if ((CylinderShapeType)typeProp.enumValueIndex == CylinderShapeType.ConicalFrustum)
                {
                    EditorGUILayout.PropertyField(topDiameterXProp);
                    if (isEllipsoidProp.boolValue)
                        EditorGUILayout.PropertyField(topDiameterZProp);
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Bottom");
            EditorGUI.indentLevel++;
            {
                EditorGUILayout.PropertyField(bottomHeightProp);
                EditorGUILayout.PropertyField(bottomDiameterXProp);
                if (isEllipsoidProp.boolValue)
                    EditorGUILayout.PropertyField(bottomDiameterZProp);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            {
                EditorGUILayout.PropertyField(sidesProp);
                EditorGUILayout.PropertyField(smoothingGroupProp);
                EditorGUILayout.PropertyField(rotationProp);
            }
            EditorGUILayout.Space();


            ShowSurfaces(surfacesProp, kSurfaceNameContent);
        }
        
        // TODO: put somewhere else
        public static Color GetColorForState(Color baseColor, bool hasFocus, bool isBackfaced, bool isDisabled)
        {
            var nonSelectedColor = baseColor;
            if (isBackfaced) nonSelectedColor.a *= UnitySceneExtensions.SceneHandles.backfaceAlphaMultiplier;
            var focusColor = (hasFocus) ? UnitySceneExtensions.SceneHandles.selectedColor : nonSelectedColor;
            return isDisabled ? Color.Lerp(focusColor, UnitySceneExtensions.SceneHandles.staticColor, UnitySceneExtensions.SceneHandles.staticBlend) : focusColor;
        }

        // TODO: put somewhere else
        public static bool IsSufaceBackFaced(Vector3 point, Vector3 normal)
        {
            var camera					= Camera.current;
            var inverseMatrix			= UnityEditor.Handles.inverseMatrix;
            var cameraLocalPos			= inverseMatrix.MultiplyPoint(camera.transform.position);
            var cameraLocalForward		= inverseMatrix.MultiplyVector(camera.transform.forward);
            var isCameraOrthographic	= camera.orthographic;
                
            var cosV = isCameraOrthographic ? Vector3.Dot(normal, -cameraLocalForward) :
                                              Vector3.Dot(normal, (cameraLocalPos - point));

            return (cosV < -0.0001f);
        }

        struct CylinderHandle
        {
            public void Init(ChiselCylinder generator)
            {
                this.generator		= generator;
                bottomDiameterX		= generator.BottomDiameterX;
                bottomDiameterZ		= generator.IsEllipsoid ? generator.BottomDiameterZ : generator.BottomDiameterX;

                topDiameterX		= generator.TopDiameterX;
                topDiameterZ		= generator.IsEllipsoid ? generator.TopDiameterZ : generator.TopDiameterX;

                rotate				= Quaternion.AngleAxis(generator.Rotation, Vector3.up);
                topXVector			= rotate * Vector3.right   * topDiameterX * 0.5f;
                topZVector			= rotate * Vector3.forward * topDiameterZ * 0.5f;
                bottomXVector		= rotate * Vector3.right   * bottomDiameterX * 0.5f;
                bottomZVector		= rotate * Vector3.forward * bottomDiameterZ * 0.5f;
                topHeight			= Vector3.up * generator.TopHeight;
                bottomHeight		= Vector3.up * generator.BottomHeight;
                normal				= Vector3.up;

                if (!generator.IsEllipsoid)
                {
                    bottomZVector	= bottomZVector.normalized * bottomXVector.magnitude;
                    topZVector		= topZVector.normalized * topXVector.magnitude;
                }

                prevBottomXVector	= bottomXVector;
                prevBottomZVector	= bottomZVector;
                prevTopXVector		= topXVector;
                prevTopZVector		= topZVector;

                topPoint	= topHeight;
                bottomPoint = bottomHeight;

                vertices = new Vector3[generator.Sides * 2];
            }

            ChiselCylinder generator;

            float bottomDiameterX;
            float bottomDiameterZ;

            float topDiameterX;
            float topDiameterZ;

            Quaternion rotate;
            Vector3 topXVector;
            Vector3 topZVector;
            Vector3 bottomXVector;
            Vector3 bottomZVector;
            Vector3 topHeight;
            Vector3 bottomHeight;
            Vector3 normal;

            Vector3 prevBottomXVector;
            Vector3 prevTopXVector;
            Vector3 prevBottomZVector;
            Vector3 prevTopZVector;

            Vector3 topPoint;
            Vector3 bottomPoint;

            Vector3[] vertices;
            Vector3[] dottedVertices;
            
            internal static int s_TopHash				= "TopCylinderHash".GetHashCode();
            internal static int s_BottomHash			= "BottomCylinderHash".GetHashCode();
            internal static int s_TopRotationHash		= "TopRotationCylinderHash".GetHashCode();
            internal static int s_BottomRotationHash	= "BottomRotationCylinderHash".GetHashCode();

            public void ShowInstance()
            {
                var tempTop		= generator.Top;
                var tempBottom	= generator.Bottom;
                var sides		= generator.Sides;


                var topId				= GUIUtility.GetControlID(s_TopHash,    FocusType.Passive);
                var bottomId			= GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);

                var focusControl		= UnitySceneExtensions.SceneHandleUtility.focusControl;
                var color				= UnityEditor.Handles.yAxisColor;
                var isDisabled			= UnitySceneExtensions.SceneHandles.disabled;

                if (!generator.IsEllipsoid)
                { tempTop.diameterZ = tempTop.diameterX; tempBottom.diameterZ = tempBottom.diameterX; }

                EditorGUI.BeginChangeCheck();
                {
                    switch (generator.Type)
                    {
                        case CylinderShapeType.Cylinder:
                        {
                            if (generator.IsEllipsoid)
                            {
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(topPoint,     normal, ref bottomXVector, ref bottomZVector, renderDisc: false);
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, ref bottomXVector, ref bottomZVector, renderDisc: false);
                            } else
                            {
                                bottomXVector = UnitySceneExtensions.SceneHandles.Radius2DHandle(topPoint,     normal, bottomXVector, renderDisc: false);
                                bottomXVector = UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, bottomXVector, renderDisc: false);
                                
                                bottomZVector = bottomXVector;
                            }
                            topXVector = bottomXVector;
                            topZVector = bottomZVector;
                            tempTop.diameterX = tempBottom.diameterX;
                            tempTop.diameterZ = tempBottom.diameterZ;
                            break;
                        }
                        case CylinderShapeType.ConicalFrustum:
                        {
                            if (generator.IsEllipsoid)
                            {
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(topPoint,     normal, ref topXVector,    ref topZVector,    renderDisc: false);
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, ref bottomXVector, ref bottomZVector, renderDisc: false);
                            } else
                            {
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(topPoint,     normal, ref topXVector,    ref topXVector,    renderDisc: false);
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, ref bottomXVector, ref bottomXVector, renderDisc: false);

                                bottomXVector = UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, bottomXVector, renderDisc: false);
                                bottomZVector = bottomXVector;
                            }
                            break;
                        }
                        case CylinderShapeType.Cone:
                        {
                            if (generator.IsEllipsoid)
                            {
                                UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, ref bottomXVector, ref bottomZVector, renderDisc: false);
                            } else
                            {
                                bottomXVector = UnitySceneExtensions.SceneHandles.Radius2DHandle(bottomPoint, -normal, bottomXVector, renderDisc: false);
                                bottomZVector = bottomXVector;
                            }
                            topXVector = bottomXVector;
                            topZVector = bottomZVector;
                            tempTop.diameterX = 0;
                            tempTop.diameterZ = 0;
                            break;
                        }
                    }


                    // TODO: add cylinder horizon "side-lines"
                }
                if (EditorGUI.EndChangeCheck())
                {
                    topZVector.y = 0;
                    topXVector.y = 0;

                    bottomZVector.y = 0;
                    bottomXVector.y = 0;

                    Undo.RecordObject(generator, "Modified " + generator.NodeTypeName);
                    if (!generator.IsEllipsoid)
                    {
                        if (prevBottomXVector != bottomXVector)
                        {
                            bottomZVector = Vector3.Cross(normal, bottomXVector.normalized) * bottomXVector.magnitude;
                        }
                        if (prevTopXVector != topXVector)
                        {
                            topZVector = Vector3.Cross(normal, topXVector.normalized) * topXVector.magnitude;
                        }
                    }

                    if (prevTopXVector != topXVector)
                    {
                        generator.Rotation = Utilities.GeometryMath.SignedAngle(Vector3.right, topXVector.normalized, Vector3.up);
                    }
                    else if (prevBottomXVector != bottomXVector)
                    {
                        generator.Rotation = Utilities.GeometryMath.SignedAngle(Vector3.right, bottomXVector.normalized, Vector3.up);
                    }

                    if (generator.IsEllipsoid)
                    {
                        generator.BottomDiameterX = bottomXVector.magnitude * 2.0f;
                        generator.BottomDiameterZ = bottomZVector.magnitude * 2.0f;

                        generator.TopDiameterX = topXVector.magnitude * 2.0f;
                        generator.TopDiameterZ = topZVector.magnitude * 2.0f;
                    } else
                    {
                        if (prevBottomZVector != bottomZVector)
                        {
                            generator.BottomDiameterX = bottomZVector.magnitude * 2.0f;
                            generator.BottomDiameterZ = bottomZVector.magnitude * 2.0f;
                        } else
                        {
                            generator.BottomDiameterX = bottomXVector.magnitude * 2.0f;
                            generator.BottomDiameterZ = bottomXVector.magnitude * 2.0f;
                        }

                        if (prevTopZVector != topZVector)
                        {
                            generator.TopDiameterX = topZVector.magnitude * 2.0f;
                            generator.TopDiameterZ = topZVector.magnitude * 2.0f;
                        } else
                        {
                            generator.TopDiameterX = topXVector.magnitude * 2.0f;
                            generator.TopDiameterZ = topXVector.magnitude * 2.0f;
                        }
                    }
                }
                
                const float kLineDash					= 2.0f;
                const float kLineThickness				= 1.0f;
                const float kCircleThickness			= 1.5f;
                const float kCapLineThickness			= 2.0f;
                const float kCapLineThicknessSelected	= 2.5f;
                 
                const int kMaxOutlineSides	= 32;
                const int kMinimumSides		= 8;
                
                var baseColor				= UnityEditor.Handles.yAxisColor;
                
                BrushMeshFactory.GetConicalFrustumVertices(tempBottom, tempTop, generator.Rotation, sides, ref vertices);

                if (generator.TopHeight < generator.BottomHeight)
                    normal = -normal;
                    
                var isTopBackfaced	= IsSufaceBackFaced(topPoint, normal);
                var topHasFocus		= (focusControl == topId);
                var topThickness	= topHasFocus ? kCapLineThicknessSelected : kCapLineThickness;
                    
                var isBottomBackfaced	= IsSufaceBackFaced(bottomPoint, -normal);
                var bottomHasFocus		= (focusControl == bottomId);
                var bottomThickness		= bottomHasFocus ? kCapLineThicknessSelected : kCapLineThickness;


                UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                ChiselOutlineRenderer.DrawLineLoop(vertices, 0, sides, thickness: bottomThickness);

                UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                ChiselOutlineRenderer.DrawLineLoop(vertices, sides, sides, thickness: topThickness);
                                        
                UnityEditor.Handles.color = GetColorForState(baseColor, false, false, isDisabled);
                for (int i = 0; i < sides; i++)
                    ChiselOutlineRenderer.DrawLine(vertices[i], vertices[i + sides], lineMode: LineMode.ZTest, thickness: kLineThickness);
                    
                UnityEditor.Handles.color = GetColorForState(baseColor, false, true, isDisabled);
                for (int i = 0; i < sides; i++)
                    ChiselOutlineRenderer.DrawLine(vertices[i], vertices[i + sides], lineMode: LineMode.NoZTest, thickness: kLineThickness);

                /*
                var point0    = camera.WorldToScreenPoint(topPoint);
                var direction = camera.ScreenToWorldPoint(point0 - Vector3.right);
                var point1	  = camera.WorldToScreenPoint(point0 - (direction * tempTop.diameterX));
                var size	  = Mathf.Max(point1.x - point0.x, point1.y - point0.y);
                */
                // TODO: figure out how to reduce the sides of the circle depending on radius & distance
                int outlineSides =  kMaxOutlineSides;
                if (sides <= kMinimumSides)
                {
                    BrushMeshFactory.GetConicalFrustumVertices(tempBottom, tempTop, generator.Rotation, outlineSides, ref dottedVertices);

                    UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, false, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(dottedVertices, outlineSides, outlineSides, lineMode: LineMode.ZTest,   thickness: kCircleThickness, dashSize: kLineDash);
                        
                    UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, true, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(dottedVertices, outlineSides, outlineSides, lineMode: LineMode.NoZTest, thickness: kCircleThickness, dashSize: kLineDash);

                    UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, false, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(dottedVertices, 0, outlineSides, lineMode: LineMode.ZTest, thickness: kCircleThickness, dashSize: kLineDash);
                        
                    UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, true, isDisabled);
                    ChiselOutlineRenderer.DrawLineLoop(dottedVertices, 0, outlineSides, lineMode: LineMode.NoZTest, thickness: kCircleThickness, dashSize: kLineDash);
                }
                
                EditorGUI.BeginChangeCheck();
                {
                    UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                    bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomId, bottomPoint, -normal);

                    UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                    topPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(topId, topPoint, normal);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(generator, "Modified " + generator.NodeTypeName);
                    generator.TopHeight = Vector3.Dot(Vector3.up, topPoint);
                    generator.BottomHeight = Vector3.Dot(Vector3.up, bottomPoint);
                }
            }
        }

        static CylinderHandle cylinderHandle = new CylinderHandle();

        // TODO: prevent "outer" outlines from being rendered
        protected override void OnGeneratorSelected(ChiselCylinder generator)
        {
            cylinderHandle.Init(generator);
        }

        protected override void OnScene(ChiselCylinder generator)
        {
            cylinderHandle.ShowInstance();
        }
    }
}