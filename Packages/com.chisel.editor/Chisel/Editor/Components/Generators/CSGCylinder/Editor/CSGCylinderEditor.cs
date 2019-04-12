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
    // TODO: why did resetting this generator not work?
    // TODO: make drag & drop of materials on generator side work
    [CustomEditor(typeof(CSGCylinder))]
    [CanEditMultipleObjects]
    public sealed class CSGCylinderEditor : GeneratorEditor<CSGCylinder>
    {
        // TODO: make these shared resources since this name is used in several places (with identical context)
        const string        SurfaceFormat           = "Surface {0}";
        static GUIContent   surfacesContent         = new GUIContent("Surfaces");
        static GUIContent   descriptionContent      = new GUIContent("Description");
        static GUIContent   surfaceAssetContent     = new GUIContent("Surface Asset");
        static GUIContent[] surfacePropertyContent  = null;
        
        SerializedProperty surfaceDescriptionProp;
        SerializedProperty surfaceAssetProp;
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
        
        protected override void ResetInspector()
        { 
            surfaceDescriptionProp	= null;
            surfaceAssetProp		= null;

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
        }
        
        protected override void InitInspector()
        { 
            surfaceDescriptionProp	= serializedObject.FindProperty("definition.surfaceDescriptions");
            surfaceAssetProp		= serializedObject.FindProperty("definition.surfaceAssets");

            typeProp				= serializedObject.FindProperty("definition.type");
            topHeightProp			= serializedObject.FindProperty("definition.top.height");
            topDiameterXProp		= serializedObject.FindProperty("definition.top.diameterX");
            topDiameterZProp		= serializedObject.FindProperty("definition.top.diameterZ");
            bottomHeightProp		= serializedObject.FindProperty("definition.bottom.height");
            bottomDiameterXProp		= serializedObject.FindProperty("definition.bottom.diameterX");
            bottomDiameterZProp		= serializedObject.FindProperty("definition.bottom.diameterZ");
            rotationProp			= serializedObject.FindProperty("definition.rotation");
            isEllipsoidProp			= serializedObject.FindProperty("definition.isEllipsoid");
            smoothingGroupProp		= serializedObject.FindProperty("definition.smoothingGroup");
            sidesProp				= serializedObject.FindProperty("definition.sides");

            surfacesVisible         = SessionState.GetBool(kSurfacesVisibleKey, false);
        }

        const string kSurfacesVisibleKey = "CSGLinearStairsEditor.SubmeshesVisible";
        bool	surfacesVisible;
        bool[]  surfacePropertyVisible = new bool[0];
        
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


            EditorGUI.BeginChangeCheck();
            surfacesVisible = EditorGUILayout.Foldout(surfacesVisible, surfacesContent);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetBool(kSurfacesVisibleKey, surfacesVisible);
            if (surfacesVisible)
            {
                EditorGUI.indentLevel++;
                SerializedProperty elementProperty;

                if (surfacePropertyContent == null ||
                    surfacePropertyContent.Length < surfaceDescriptionProp.arraySize)
                {
                    surfacePropertyContent	= new GUIContent[surfaceDescriptionProp.arraySize];
                    for (int i = 0; i < surfaceDescriptionProp.arraySize; i++)
                    {
                        surfacePropertyContent[i] = new GUIContent(string.Format(SurfaceFormat, i));
                    }
                }
                        
                if (surfaceDescriptionProp.arraySize > surfacePropertyVisible.Length)
                {
                    var oldSize = surfacePropertyVisible.Length;
                    Array.Resize(ref surfacePropertyVisible, surfaceDescriptionProp.arraySize);
                    for (int i = oldSize; i < surfaceDescriptionProp.arraySize; i++)
                        surfacePropertyVisible[i] = true;
                }
                for (int i = 0; i < surfaceDescriptionProp.arraySize; i++)
                {
                    /*
                    const float kSingleLineHeight = 16f;
                    Rect r = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, EditorGUIUtility.fieldWidth, kSingleLineHeight, kSingleLineHeight, EditorStyles.foldout);
                    if (r.Contains(Event.current.mousePosition)) // NOTE: This doesn't work well since the inspector is not redrawn all the time
                    {
                        // TODO: set hover over surface
                    } else
                        // TODO: unset hover over surface? how to handle that? register somewhere & check in scenegui update?
                    surfacePropertyVisible[i] = EditorGUI.Foldout(r, surfacePropertyVisible[i], surfacePropertyContent[i], false, EditorStyles.foldout);
                    */
                    surfacePropertyVisible[i] = EditorGUILayout.Foldout(surfacePropertyVisible[i], surfacePropertyContent[i]);
                    EditorGUI.indentLevel++;
                    if (surfacePropertyVisible[i])
                    {
                        elementProperty = surfaceDescriptionProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(elementProperty, descriptionContent, true);

                        elementProperty = surfaceAssetProp.GetArrayElementAtIndex((i>2)?2:i);
                        EditorGUILayout.PropertyField(elementProperty, surfaceAssetContent, true);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
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
            public void Init(CSGCylinder generator)
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

            CSGCylinder generator;

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
                
                BrushMeshAssetFactory.GetConicalFrustumVertices(tempBottom, tempTop, generator.Rotation, sides, ref vertices);

                if (generator.TopHeight < generator.BottomHeight)
                    normal = -normal;
                    
                var isTopBackfaced	= IsSufaceBackFaced(topPoint, normal);
                var topHasFocus		= (focusControl == topId);
                var topThickness	= topHasFocus ? kCapLineThicknessSelected : kCapLineThickness;
                    
                var isBottomBackfaced	= IsSufaceBackFaced(bottomPoint, -normal);
                var bottomHasFocus		= (focusControl == bottomId);
                var bottomThickness		= bottomHasFocus ? kCapLineThicknessSelected : kCapLineThickness;


                UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                CSGOutlineRenderer.DrawLineLoop(vertices, 0, sides, thickness: bottomThickness);

                UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                CSGOutlineRenderer.DrawLineLoop(vertices, sides, sides, thickness: topThickness);
                                        
                UnityEditor.Handles.color = GetColorForState(baseColor, false, false, isDisabled);
                for (int i = 0; i < sides; i++)
                    CSGOutlineRenderer.DrawLine(vertices[i], vertices[i + sides], lineMode: LineMode.ZTest, thickness: kLineThickness);
                    
                UnityEditor.Handles.color = GetColorForState(baseColor, false, true, isDisabled);
                for (int i = 0; i < sides; i++)
                    CSGOutlineRenderer.DrawLine(vertices[i], vertices[i + sides], lineMode: LineMode.NoZTest, thickness: kLineThickness);

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
                    BrushMeshAssetFactory.GetConicalFrustumVertices(tempBottom, tempTop, generator.Rotation, outlineSides, ref dottedVertices);

                    UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, false, isDisabled);
                    CSGOutlineRenderer.DrawLineLoop(dottedVertices, outlineSides, outlineSides, lineMode: LineMode.ZTest,   thickness: kCircleThickness, dashSize: kLineDash);
                        
                    UnityEditor.Handles.color = GetColorForState(baseColor, topHasFocus, true, isDisabled);
                    CSGOutlineRenderer.DrawLineLoop(dottedVertices, outlineSides, outlineSides, lineMode: LineMode.NoZTest, thickness: kCircleThickness, dashSize: kLineDash);

                    UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, false, isDisabled);
                    CSGOutlineRenderer.DrawLineLoop(dottedVertices, 0, outlineSides, lineMode: LineMode.ZTest, thickness: kCircleThickness, dashSize: kLineDash);
                        
                    UnityEditor.Handles.color = GetColorForState(baseColor, bottomHasFocus, true, isDisabled);
                    CSGOutlineRenderer.DrawLineLoop(dottedVertices, 0, outlineSides, lineMode: LineMode.NoZTest, thickness: kCircleThickness, dashSize: kLineDash);
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
        protected override void OnSceneInit(CSGCylinder generator)
        {
            cylinderHandle.Init(generator);
        }

        protected override void OnScene(CSGCylinder generator)
        {
            cylinderHandle.ShowInstance();
        }
    }
}