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
using Unity.Mathematics;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselBrush))]
    [CanEditMultipleObjects]
    public sealed class ChiselBrushEditor : ChiselGeneratorEditor<ChiselBrush>
    {
        static Dictionary<ChiselBrush, ChiselEditableOutline> activeOutlines = new Dictionary<ChiselBrush, ChiselEditableOutline>();

        protected override void OnUndoRedoPerformed()
        {
            CommitChanges();

            // TODO: do we actually need this? 
            var activeGenerators = activeOutlines.Keys.ToArray();
            foreach (var generator in activeGenerators)
            {
                generator.definition.brushOutline.Validate();
                generator.definition.brushOutline.CalculatePlanes();
                activeOutlines[generator] = new ChiselEditableOutline(generator);
            }
        }


        protected override void OnGeneratorSelected(ChiselBrush generator)
        {
            if (generator.definition.IsValid)
                CommitChanges();
            else
                CancelChanges();

            activeOutlines[generator] = new ChiselEditableOutline(generator);
        }

        protected override void OnGeneratorDeselected(ChiselBrush generator)
        {
            if (generator.definition.IsValid)
                CommitChanges();
            else
                CancelChanges();

            ChiselTopologySelectionManager.DeselectAll(generator);
            activeOutlines.Remove(generator);
        }



        protected override void OnScene(SceneView sceneView, ChiselBrush generator)
        {
            if (!activeOutlines.TryGetValue(generator, out ChiselEditableOutline editableOutline) ||
                editableOutline == null)
                return;

            var previousHotControl  = GUIUtility.hotControl;

            if (editableOutline.DoHandles(sceneView, generator.definition.IsInsideOut))
                UpdateEditableOutline(generator, editableOutline.brushMesh.vertices);

            var currentHotControl = GUIUtility.hotControl;
            // When we stop/start dragging or clicking something our hotControl changes. 
            // We detect this change, and together with generatorModified we know when a user operation is finished.
            if (generatorModified && (currentHotControl != previousHotControl))
                CommitChanges();
        }


        static bool generatorModified = false;

        // Creates a new optimized/fixed brushMesh based on the brushMesh inside of the generator
        // this will not be copied to the generator until the current operation is complete. 
        // This prevents, for example, dragging an edge over another edge DURING a dragging operation messing things up.
        void UpdateEditableOutline(ChiselBrush generator, float3[] vertices)
        {
            generatorModified = true;
            var outline = activeOutlines[generator];

            var internalBrushMesh = new BrushMesh(outline.brushMesh);
            
            // Removes infinitely thin polygons (for instance, when snapping edges to edges)
            internalBrushMesh.RemoveDegenerateTopology(out outline.edgeRemap, out outline.polygonRemap);

            internalBrushMesh.CalculatePlanes();

            // If the brush is concave, we set the generator to not be valid, so that when we commit, it will be reverted
            generator.definition.ValidState = !internalBrushMesh.IsConcave() && // TODO: eventually allow concave shapes
                                              !internalBrushMesh.IsSelfIntersecting() &&
                                               internalBrushMesh.HasVolume();

            generator.definition.brushOutline = internalBrushMesh;
            
            generator.OnValidate();

            outline.Rebuild();
        }

        // When moving an edge/vertex we want to keep using the original mesh since we might be collapsing polygons 
        // when snapping vertices/edges together. If we always use the current mesh then we'd suddenly may be moving
        // something else than we started out with. When we're done with the movement and release the mouse we want
        // this to become permanent however, and that's what this method does.
        void CommitChanges()
        {
            if (!generatorModified)
                return;

            var activeGenerators = activeOutlines.Keys.ToArray();
            if (activeGenerators.Length > 0)                
                Undo.RecordObjects(activeGenerators, activeGenerators.Length == 1 ? "Modified brush" : "Modified brushes");

            // Remove redundant vertices and fix the selection so that the correct edges/vertices etc. are selected
            foreach (var generator in activeGenerators)
            {
                var outline = activeOutlines[generator];

                // We remove redundant vertices here since in editableOutline we make the assumption that the vertices 
                // between the original and the 'fixed' brushMesh are identical. 
                // This makes it a lot easier to find 'soft edges'.
                outline.vertexRemap = generator.definition.brushOutline.RemoveUnusedVertices();
                ChiselEditableOutline.RemapSelection(generator.definition.brushOutline, outline.selection, outline.vertexRemap, outline.edgeRemap, outline.polygonRemap);
            }

            // Create new outlines for our generators
            foreach (var generator in activeGenerators)
                activeOutlines[generator] = new ChiselEditableOutline(generator); 

            generatorModified = false;
        }

        void CancelChanges()
        {
            if (!generatorModified)
                return;

            generatorModified = false;
            var activeGenerators = activeOutlines.Keys.ToArray();
            foreach (var generator in activeGenerators)
                activeOutlines[generator] = new ChiselEditableOutline(generator);
            UndoAllChanges();
        }
    }
}