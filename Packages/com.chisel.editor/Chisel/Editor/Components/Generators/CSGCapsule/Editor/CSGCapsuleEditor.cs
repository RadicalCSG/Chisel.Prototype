using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public sealed class CSGCapsuleDetails : ChiselGeneratorDetails<CSGCapsule>
    {
    }


    [CustomEditor(typeof(CSGCapsule))]
    [CanEditMultipleObjects]
    public sealed class CSGCapsuleEditor : ChiselGeneratorEditor<CSGCapsule>
    {
        SerializedProperty heightProp;
        SerializedProperty topHeightProp;
        SerializedProperty bottomHeightProp;

        SerializedProperty diameterXProp;
        SerializedProperty diameterZProp;
        SerializedProperty rotationProp;

        SerializedProperty sidesProp;
        SerializedProperty topSegmentsProp;
        SerializedProperty bottomSegmentsProp;

        protected override void ResetInspector()
        { 
            heightProp			= null;
            topHeightProp		= null;
            bottomHeightProp	= null;

            diameterXProp		= null;
            diameterZProp		= null;
            rotationProp		= null;

            sidesProp			= null;
            topSegmentsProp		= null;
            bottomSegmentsProp	= null;
        }
        
        protected override void InitInspector()
        { 
            heightProp			= serializedObject.FindProperty("definition.height");
            topHeightProp		= serializedObject.FindProperty("definition.topHeight");
            bottomHeightProp	= serializedObject.FindProperty("definition.bottomHeight");

            diameterXProp		= serializedObject.FindProperty("definition.diameterX");
            diameterZProp		= serializedObject.FindProperty("definition.diameterZ");
            rotationProp		= serializedObject.FindProperty("definition.rotation");

            sidesProp			= serializedObject.FindProperty("definition.sides");
            topSegmentsProp		= serializedObject.FindProperty("definition.topSegments");
            bottomSegmentsProp	= serializedObject.FindProperty("definition.bottomSegments");
        }

        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(heightProp);
            EditorGUILayout.PropertyField(topHeightProp);
            EditorGUILayout.PropertyField(bottomHeightProp);

            EditorGUILayout.PropertyField(diameterXProp);
            EditorGUILayout.PropertyField(diameterZProp);
            EditorGUILayout.PropertyField(rotationProp);

            EditorGUILayout.PropertyField(sidesProp);
            EditorGUILayout.PropertyField(topSegmentsProp);
            EditorGUILayout.PropertyField(bottomSegmentsProp);
        }

        
        
        protected override void OnSceneInit(CSGCapsule generator) { }


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(CSGCapsuleDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            //var baseColor		= UnityEditor.Handles.yAxisColor;
            //var isDisabled	= UnitySceneExtensions.Handles.disabled;
            //var normal		= Vector3.up;
            var sides			= definition.sides;
            
            // TODO: share this logic with GenerateCapsuleVertices
            
            var topHemisphere		= definition.haveRoundedTop;
            var bottomHemisphere	= definition.haveRoundedBottom;
            var topSegments			= topHemisphere    ? definition.topSegments    : 0;
            var bottomSegments		= bottomHemisphere ? definition.bottomSegments : 0;
            
            var extraVertices		= definition.extraVertexCount;
            var bottomVertex		= definition.bottomVertex;
            var topVertex			= definition.topVertex;
            
            var rings				= definition.ringCount;
            var bottomRing			= (bottomHemisphere) ? (rings - bottomSegments) : rings - 1;
            var topRing				= (topHemisphere   ) ? (topSegments - 1) : 0;

            var prevColor = UnityEditor.Handles.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                if ((!definition.haveRoundedTop && i == topRing) ||
                    (!definition.haveRoundedBottom && i == bottomRing))
                    continue;
                bool isCapRing = (i == topRing || i == bottomRing);
                UnityEditor.Handles.color = (isCapRing ? prevColor : color);
                CSGOutlineRenderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: (isCapRing ? kCapLineThickness : kHorzLineThickness), dashSize: (isCapRing ? 0 : kLineDash));
            }

            UnityEditor.Handles.color = color;
            for (int k = 0; k < sides; k++)
            {
                if (topHemisphere)
                    CSGOutlineRenderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    CSGOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (bottomHemisphere)
                    CSGOutlineRenderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            UnityEditor.Handles.color = prevColor;
        }

        internal static int s_TopHash		= "TopCapsuleHash".GetHashCode();
        internal static int s_BottomHash	= "BottomCapsuleHash".GetHashCode();

        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?

        protected override void OnScene(CSGCapsule generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            if (!BrushMeshAssetFactory.GenerateCapsuleVertices(generator.definition, ref vertices))
                return;

            UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);
            

            var topPoint	= normal * generator.Height;
            var bottomPoint = normal * 0;
            var middlePoint	= normal * (generator.Height * 0.5f);
            var radius2D	= new Vector2(generator.definition.diameterX, generator.definition.diameterZ) * 0.5f;

            EditorGUI.BeginChangeCheck();
            {
                UnityEditor.Handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                radius2D.x = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, middlePoint, radius2D.x);

                var topId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= CSGCylinderEditor.IsSufaceBackFaced(topPoint, normal);
                    var topHasFocus			= (focusControl == topId);

                    UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                    topPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(topId, topPoint, normal);
                    if (!generator.HaveRoundedTop)
                    {
                        //var roundedTopPoint	= normal * generator.definition.topOffset;
                        var thickness = topHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                        UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, topHasFocus, true, isDisabled);
                        CSGOutlineRenderer.DrawLineLoop(vertices, generator.definition.topVertexOffset, generator.definition.sides, lineMode: LineMode.NoZTest, thickness: thickness);
                        
                        UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, topHasFocus, false, isDisabled);
                        CSGOutlineRenderer.DrawLineLoop(vertices, generator.definition.topVertexOffset, generator.definition.sides, lineMode: LineMode.ZTest,   thickness: thickness);
                    }
                }
                
                var bottomId = GUIUtility.GetControlID(s_BottomHash, FocusType.Passive);
                {
                    var isBottomBackfaced	= CSGCylinderEditor.IsSufaceBackFaced(bottomPoint, -normal);
                    var bottomHasFocus		= (focusControl == bottomId);

                    UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, bottomHasFocus, isBottomBackfaced, isDisabled);
                    bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomId, bottomPoint, -normal);
                    if (!generator.HaveRoundedBottom)
                    {
                        //var roundedBottomPoint	= normal * generator.definition.bottomOffset;
                        var thickness = bottomHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                        UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, bottomHasFocus, true, isDisabled);
                        CSGOutlineRenderer.DrawLineLoop(vertices, generator.definition.bottomVertexOffset, generator.definition.sides, lineMode: LineMode.NoZTest, thickness: thickness);
                    
                        UnityEditor.Handles.color = CSGCylinderEditor.GetColorForState(baseColor, bottomHasFocus, false, isDisabled);
                        CSGOutlineRenderer.DrawLineLoop(vertices, generator.definition.bottomVertexOffset, generator.definition.sides, lineMode: LineMode.ZTest,   thickness: thickness);
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.definition.height = topPoint.y - bottomPoint.y;
                generator.definition.diameterX = radius2D.x * 2.0f;
                generator.definition.diameterZ = radius2D.x * 2.0f;
                generator.OnValidate();
                // TODO: handle sizing down (needs to modify transformation?)
            }
        }
    }
}