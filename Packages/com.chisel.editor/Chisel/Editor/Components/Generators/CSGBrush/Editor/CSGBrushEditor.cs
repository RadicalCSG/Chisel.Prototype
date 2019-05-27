using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public sealed class CSGBrushDetails : ChiselGeneratorDetails<CSGBrush>
    {
    }

    [CustomEditor(typeof(CSGBrush))]
    [CanEditMultipleObjects]
    public sealed class CSGBrushEditor : ChiselGeneratorEditor<CSGBrush>
    {
        SerializedProperty brushMeshAssetProp;

        protected override void ResetInspector()
        {
            brushMeshAssetProp = null;
        }

        protected override void InitInspector()
        {
            brushMeshAssetProp	= serializedObject.FindProperty("brushMeshAsset");
        }
        
        protected override void OnInspector()
        {
            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.PropertyField(brushMeshAssetProp);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        protected override void OnScene(CSGBrush generator)
        {
            var targetBrushMeshAsset	= generator.BrushMeshAsset;
            if (!targetBrushMeshAsset)
                return;

            var subMeshes = targetBrushMeshAsset.SubMeshes;
            if (subMeshes == null)
                return;

            for (int m = 0; m < subMeshes.Length; m++)
            { 
                var subMesh = subMeshes[m];
                if (subMesh == null)
                    continue;

                ref var brushMesh = ref subMesh.brushMesh;

                EditorGUI.BeginChangeCheck();

                var modelMatrix = CSGNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);

                var vertices		= brushMesh.vertices;
                var halfEdges		= brushMesh.halfEdges;

                //HashSet<CSGTreeBrush> foundBrushes = new HashSet<CSGTreeBrush>();
                //targetBrush.GetAllTreeBrushes(foundBrushes, false)
                //foreach (var brush in CSGSyncSelection.GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)generator.TopNode))
                {
                    var brush = (CSGTreeBrush)generator.TopNode;
                    var transformation = modelMatrix * brush.NodeToTreeSpaceMatrix;
                    for (int e = 0; e < halfEdges.Length; e++)
                    {
                        var vertexIndex1 = halfEdges[e].vertexIndex;
                        var vertexIndex2 = halfEdges[halfEdges[e].twinIndex].vertexIndex;

                        var from	= vertices[vertexIndex1];
                        var to		= vertices[vertexIndex2];
                        CSGOutlineRenderer.DrawLine(transformation, from, to, UnityEditor.Handles.yAxisColor, thickness: 1.0f);
                    }
                }

                //var newBounds = CSGHandles.BoundsHandle(originalBounds, Quaternion.identity, CSGHandles.DotHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    //Undo.RecordObject(target, "Changed shape of Brush");
                    //brush.Bounds = newBounds;
                }
            }
        }
    }	
}