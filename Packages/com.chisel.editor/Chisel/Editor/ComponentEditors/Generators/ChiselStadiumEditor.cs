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
    public sealed class ChiselStadiumDetails : ChiselGeneratorDetails<ChiselStadium>
    {
    }
    
    [CustomEditor(typeof(ChiselStadium))]
    [CanEditMultipleObjects]
    public sealed class ChiselStadiumEditor : ChiselGeneratorEditor<ChiselStadium>
    {
        [MenuItem("GameObject/Chisel/" + ChiselStadium.kNodeTypeName)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselStadium.kNodeTypeName); }

        SerializedProperty heightProp;
        SerializedProperty lengthProp;
        SerializedProperty topLengthProp;
        SerializedProperty bottomLengthProp;

        SerializedProperty diameterProp;
        
        SerializedProperty topSidesProp;
        SerializedProperty bottomSidesProp;

        SerializedProperty surfacesProp;

        protected override void ResetInspector()
        {
            heightProp			= null;
            lengthProp			= null;
            topLengthProp		= null;
            bottomLengthProp	= null;

            diameterProp		= null;
        
            topSidesProp		= null;
            bottomSidesProp		= null;

            surfacesProp        = null;
        }

        protected override void InitInspector()
        {
            var definitionProp = serializedObject.FindProperty(nameof(ChiselStadium.definition));
            {
                heightProp			= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.height));

                lengthProp			= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.length));
                topLengthProp		= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.topLength));
                bottomLengthProp	= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.bottomLength));

                diameterProp		= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.diameter));

                topSidesProp		= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.topSides));
                bottomSidesProp		= definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.bottomSides));

                var surfDefProp     = definitionProp.FindPropertyRelative(nameof(ChiselStadium.definition.surfaceDefinition));
                {
                    surfacesProp    = surfDefProp.FindPropertyRelative(nameof(ChiselStadium.definition.surfaceDefinition.surfaces));
                }
            }
        }

        protected override void OnInspector()
        {
            EditorGUILayout.PropertyField(heightProp);
            EditorGUILayout.PropertyField(lengthProp);
            EditorGUILayout.PropertyField(topLengthProp);
            EditorGUILayout.PropertyField(bottomLengthProp);

            EditorGUILayout.PropertyField(diameterProp);
        
            EditorGUILayout.PropertyField(topSidesProp);
            EditorGUILayout.PropertyField(bottomSidesProp);
            
            ShowSurfaces(surfacesProp);
        }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kSideLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(ChiselStadiumDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides				= definition.sides;
            var topSides			= Mathf.Max(definition.topSides, 1) + 1;
            var bottomSides			= Mathf.Max(definition.bottomSides, 1) + 1;

            var haveRoundedTop		= definition.haveRoundedTop;
            var haveRoundedBottom	= definition.haveRoundedBottom;
            var haveCenter			= definition.haveCenter;
            //CSGOutlineRenderer.DrawLineLoop(vertices,     0, sides, lineMode: lineMode, thickness: kCapLineThickness);
            //CSGOutlineRenderer.DrawLineLoop(vertices, sides, sides, lineMode: lineMode, thickness: kCapLineThickness);

            var firstTopSide = definition.firstTopSide;
            var lastTopSide  = definition.lastTopSide;
            for (int k = firstTopSide; k <= lastTopSide; k++)
            {
                var sideLine	= !haveRoundedTop || (k == firstTopSide) || (k == lastTopSide);
                var thickness	= (sideLine ? kSideLineThickness : kVertLineThickness);
                var dashSize	= (sideLine ? 0                  : kLineDash);
                ChiselOutlineRenderer.DrawLine(vertices[k], vertices[sides + k], lineMode: lineMode, thickness: thickness, dashSize: dashSize);
            }
            
            var firstBottomSide = definition.firstBottomSide;
            var lastBottomSide  = definition.lastBottomSide;
            for (int k = firstBottomSide; k <= lastBottomSide; k++)
            {
                var sideLine	= haveCenter && (!haveRoundedBottom || (k == firstBottomSide) || (k == lastBottomSide));
                var thickness	= (sideLine ? kSideLineThickness : kVertLineThickness);
                var dashSize	= (sideLine ? 0                  : kLineDash);
                ChiselOutlineRenderer.DrawLine(vertices[k], vertices[sides + k], lineMode: lineMode, thickness: thickness, dashSize: dashSize);
            }
            
            //CSGOutlineRenderer.DrawLine(vertices[firstBottomSide], vertices[lastBottomSide], lineMode: lineMode, thickness: kVertLineThickness);
            //CSGOutlineRenderer.DrawLine(vertices[firstTopSide   ], vertices[lastTopSide   ], lineMode: lineMode, thickness: kVertLineThickness);
            
            //CSGOutlineRenderer.DrawLine(vertices[sides + firstBottomSide], vertices[sides + lastBottomSide], lineMode: lineMode, thickness: kVertLineThickness);
            //CSGOutlineRenderer.DrawLine(vertices[sides + firstTopSide   ], vertices[sides + lastTopSide   ], lineMode: lineMode, thickness: kVertLineThickness);
        }

        internal static int s_TopHash		= "TopStadiumHash".GetHashCode();
        internal static int s_BottomHash	= "BottomStadiumHash".GetHashCode();


        protected override void OnScene(SceneView sceneView, ChiselStadium generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var upVector		= Vector3.up;
            var rightVector		= Vector3.right;
            var forwardVector	= Vector3.forward;

            Vector3[] vertices = null;
            if (!BrushMeshFactory.GenerateStadiumVertices(generator.definition, ref vertices))
                return;


            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);

            var height		= generator.definition.height;
            var length		= generator.definition.length;
            var diameter	= generator.definition.diameter;
            var sides		= generator.definition.sides;
            
            var firstTopSide	= generator.definition.firstTopSide;
            var lastTopSide		= generator.definition.lastTopSide;
            var firstBottomSide = generator.definition.firstBottomSide;
            var lastBottomSide  = generator.definition.lastBottomSide;

            var haveRoundedTop		= generator.definition.haveRoundedTop;
            var haveRoundedBottom	= generator.definition.haveRoundedBottom;
            var haveCenter			= generator.definition.haveCenter;
            var topLength			= generator.definition.topLength;
            var bottomLength		= generator.definition.bottomLength;
            

            var midY		= height * 0.5f;
            var halfLength	= length * 0.5f;
            var midZ		= ((halfLength - (haveRoundedTop ? topLength : 0)) - (halfLength - (haveRoundedBottom ? bottomLength : 0))) * -0.5f;
            //	haveCenter ? ((vertices[firstTopSide].z + vertices[firstBottomSide].z) * 0.5f) : 0;

            var topPoint	= new Vector3(0, height			, midZ);
            var bottomPoint = new Vector3(0, 0				, midZ);
            var frontPoint	= new Vector3(0, midY,  halfLength);
            var backPoint	= new Vector3(0, midY, -halfLength);
            var leftPoint	= new Vector3(diameter *  0.5f, midY, midZ);
            var rightPoint	= new Vector3(diameter * -0.5f, midY, midZ);

            EditorGUI.BeginChangeCheck();
            {
                var topId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= ChiselCylinderEditor.IsSufaceBackFaced(topPoint, upVector);
                    var topHasFocus			= (focusControl == topId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                    topPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(topId, topPoint, upVector);
                    //if (generator.definition.haveRoundedTop)
                    {
                        var thickness = topHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                        UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topHasFocus, true, isDisabled);
                        ChiselOutlineRenderer.DrawLineLoop(vertices, sides, sides, lineMode: LineMode.NoZTest, thickness: thickness);
                        if (haveRoundedTop)
                            ChiselOutlineRenderer.DrawLine(vertices[sides + firstTopSide   ], vertices[sides + lastTopSide   ], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            ChiselOutlineRenderer.DrawLine(vertices[sides + firstBottomSide], vertices[sides + lastBottomSide], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);
                        
                        UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topHasFocus, false, isDisabled);
                        ChiselOutlineRenderer.DrawLineLoop(vertices, sides, sides, lineMode: LineMode.ZTest,   thickness: thickness);
                        if (haveRoundedTop)
                            ChiselOutlineRenderer.DrawLine(vertices[sides + firstTopSide   ], vertices[sides + lastTopSide   ], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            ChiselOutlineRenderer.DrawLine(vertices[sides + firstBottomSide], vertices[sides + lastBottomSide], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                    }
                }
                
                var bottomId = GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);
                {
                    var isBottomBackfaced	= ChiselCylinderEditor.IsSufaceBackFaced(bottomPoint, -upVector);
                    var bottomHasFocus		= (focusControl == bottomId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                    bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomId, bottomPoint, -upVector);
                    //if (haveRoundedBottom)
                    {
                        var thickness = bottomHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                        UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomHasFocus, true, isDisabled);
                        ChiselOutlineRenderer.DrawLineLoop(vertices,     0, sides, lineMode: LineMode.NoZTest, thickness: thickness);
                        if (haveRoundedTop)
                            ChiselOutlineRenderer.DrawLine(vertices[firstTopSide   ], vertices[lastTopSide   ], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            ChiselOutlineRenderer.DrawLine(vertices[firstBottomSide], vertices[lastBottomSide], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);
                    
                        UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, bottomHasFocus, false, isDisabled);
                        ChiselOutlineRenderer.DrawLineLoop(vertices,     0, sides, lineMode: LineMode.ZTest,   thickness: thickness);
                        if (haveRoundedTop)
                            ChiselOutlineRenderer.DrawLine(vertices[firstTopSide   ], vertices[lastTopSide   ], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            ChiselOutlineRenderer.DrawLine(vertices[firstBottomSide], vertices[lastBottomSide], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                    }
                }

                var frontId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= ChiselCylinderEditor.IsSufaceBackFaced(frontPoint, forwardVector);
                    var frontHasFocus		= (focusControl == frontId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, frontHasFocus, isTopBackfaced, isDisabled);
                    frontPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(frontId, frontPoint, forwardVector);
                }
                
                var backId = GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);
                {
                    var isBottomBackfaced	= ChiselCylinderEditor.IsSufaceBackFaced(backPoint, -forwardVector);
                    var backHasFocus		= (focusControl == backId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, backHasFocus, isBottomBackfaced, isDisabled);
                    backPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(backId, backPoint, -forwardVector);
                }

                var leftId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= ChiselCylinderEditor.IsSufaceBackFaced(leftPoint, rightVector);
                    var leftHasFocus		= (focusControl == leftId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, leftHasFocus, isTopBackfaced, isDisabled);
                    leftPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(leftId, leftPoint, rightVector);
                }
                
                var rightId = GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);
                {
                    var isBottomBackfaced	= ChiselCylinderEditor.IsSufaceBackFaced(rightPoint, -rightVector);
                    var rightHasFocus		= (focusControl == rightId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, rightHasFocus, isBottomBackfaced, isDisabled);
                    rightPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(rightId, rightPoint, -rightVector);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.definition.height		= topPoint.y - bottomPoint.y;
                generator.definition.length		= Mathf.Max(0, frontPoint.z - backPoint.z);
                generator.definition.diameter	= leftPoint.x - rightPoint.x;
                generator.OnValidate();
                // TODO: handle sizing in some directions (needs to modify transformation?)
            }
        }
    }
}