﻿using UnityEngine;
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
    [CustomEditor(typeof(ChiselHemisphere))]
    [CanEditMultipleObjects]
    public sealed class ChiselHemisphereEditor : ChiselGeneratorEditor<ChiselHemisphere>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselHemisphere.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselHemisphere.kNodeTypeName); }

        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(ChiselHemisphereDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            //var baseColor		= UnityEditor.Handles.yAxisColor;
            //var isDisabled		= UnitySceneExtensions.Handles.disabled;
            //var normal			= Vector3.up;
            var sides			= definition.horizontalSegments;

            var topSegments		= Mathf.Max(definition.verticalSegments,    0);
            var bottomCap		= false;
            var topCap			= (topSegments    != 0);
            var extraVertices	= ((topCap) ? 1 : 0) + ((bottomCap) ? 1 : 0);
            var bottomVertex	= 0;
            //var topVertex		= (bottomCap) ? 1 : 0;
            
            var rings			= (vertices.Length - extraVertices) / sides;
            var bottomRing		= 0;

            var prevColor = UnityEditor.Handles.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                UnityEditor.Handles.color = ((i == bottomRing) ? prevColor : color);
                ChiselOutlineRenderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: ((i == bottomRing) ? kCapLineThickness : kHorzLineThickness), dashSize: ((i == bottomRing) ? 0 : kLineDash));
            }

            UnityEditor.Handles.color = color;
            for (int k = 0; k < sides; k++)
            {
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    ChiselOutlineRenderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (topCap)
                    ChiselOutlineRenderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            UnityEditor.Handles.color = prevColor;
        }
        
        internal static int s_TopHash		= "TopHemisphereHash".GetHashCode();


        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?
        
        protected override void OnScene(SceneView sceneView, ChiselHemisphere generator)
        {
            var baseColor		= UnityEditor.Handles.yAxisColor;
            var isDisabled		= UnitySceneExtensions.SceneHandles.disabled;
            var focusControl	= UnitySceneExtensions.SceneHandleUtility.focusControl;
            var normal			= Vector3.up;

            if (!BrushMeshFactory.GenerateHemisphereVertices(ref generator.definition, ref vertices))
                return;
            
            
            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, false, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.ZTest);

            UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, false, true, isDisabled);
            DrawOutline(generator.definition, vertices, lineMode: LineMode.NoZTest);
            

            var topPoint	= normal * generator.DiameterXYZ.y;
            var radius2D	= new Vector2(generator.definition.diameterXYZ.x, generator.definition.diameterXYZ.z) * 0.5f;

            if (generator.DiameterXYZ.y < 0)
                normal = -normal;

            EditorGUI.BeginChangeCheck();
            {
                UnityEditor.Handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                radius2D.x = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, Vector3.zero, radius2D.x);

                var topId = GUIUtility.GetControlID(s_TopHash, FocusType.Passive);
                {
                    var isTopBackfaced		= false; // TODO: how to do this?
                    var topHasFocus			= (focusControl == topId);

                    UnityEditor.Handles.color = ChiselCylinderEditor.GetColorForState(baseColor, topHasFocus, isTopBackfaced, isDisabled);
                    topPoint = UnitySceneExtensions.SceneHandles.DirectionHandle(topId, topPoint, normal);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                var diameter = generator.DiameterXYZ;
                diameter.y = topPoint.y;
                diameter.x = radius2D.x * 2.0f;
                diameter.z = radius2D.x * 2.0f;
                generator.DiameterXYZ = diameter;
            }
        }
    }
}