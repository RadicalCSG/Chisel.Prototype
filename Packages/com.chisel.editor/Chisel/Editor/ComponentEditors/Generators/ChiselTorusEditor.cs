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
    [CustomEditor(typeof(ChiselTorus))]
    [CanEditMultipleObjects]
    public sealed class ChiselTorusEditor : ChiselGeneratorEditor<ChiselTorus>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselTorus.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselTorus.kNodeTypeName); }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(ChiselTorusDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            //var baseColor		= UnityEditor.Handles.yAxisColor;
            //var isDisabled		= UnitySceneExtensions.Handles.disabled;
            var normal			= Vector3.up;

            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;

            if (definition.totalAngle != 360)
                horzSegments++;

            var prevColor		= UnityEditor.Handles.color;
            prevColor.a *= 0.8f;
            var color			= prevColor;
            color.a *= 0.6f;

            UnityEditor.Handles.color = color;
            for (int i = 0, j = 0; i < horzSegments; i++, j += vertSegments)
            {
                ChiselOutlineRenderer.DrawLineLoop(vertices, j, vertSegments, lineMode: lineMode, thickness: kVertLineThickness);
            }

            for (int k = 0; k < vertSegments; k++)
            {
                for (int i = 0, j = 0; i < horzSegments - 1; i++, j += vertSegments)
                    ChiselOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + vertSegments], lineMode: lineMode, thickness: kHorzLineThickness);
            }
            if (definition.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    ChiselOutlineRenderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            UnityEditor.Handles.color = prevColor;
        }


        protected override void OnScene(SceneView sceneView, ChiselTorus generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            Vector3[] vertices = null;
            if (!BrushMeshFactory.GenerateTorusVertices(generator.definition, ref vertices))
                return;

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);


            var outerRadius = generator.definition.outerDiameter * 0.5f;
            var innerRadius = generator.definition.innerDiameter * 0.5f;
            var topPoint	= normal * ( generator.definition.tubeHeight * 0.5f);
            var bottomPoint	= normal * (-generator.definition.tubeHeight * 0.5f);

            EditorGUI.BeginChangeCheck();
            {
                UnityEditor.Handles.color = baseColor;
                outerRadius = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, Vector3.zero, outerRadius);
                innerRadius = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, Vector3.zero, innerRadius);
                bottomPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(bottomPoint, -normal);
                topPoint	= UnitySceneExtensions.SceneHandles.DirectionHandle(topPoint,     normal);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.definition.outerDiameter	= outerRadius * 2.0f;
                generator.definition.innerDiameter	= innerRadius * 2.0f;
                generator.definition.tubeHeight		= (topPoint.y - bottomPoint.y);
                // TODO: handle sizing down
                generator.OnValidate();
            }
        }

        protected override void OnInspector()
        {
            base.OnInspector();

            if( !HasValidState() )
            {
                foreach( var target in targets )
                {
                    var generator = target as ChiselTorus;
                    if(!generator)
                        continue;

                }
            }
        }
    }
}
