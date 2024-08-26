using System.Linq;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using CanEditMultipleObjects = UnityEditor.CanEditMultipleObjects;
using CustomEditor           = UnityEditor.CustomEditor;
using Undo                   = UnityEditor.Undo;
using GUIUtility             = UnityEngine.GUIUtility;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselBrushComponent))]
    [CanEditMultipleObjects]
    public sealed class ChiselBrushEditor : ChiselGeneratorEditor<ChiselBrushComponent>
    {
        static readonly Dictionary<ChiselBrushComponent, ChiselEditableOutline> s_ActiveOutlines = new();

        protected override void OnUndoRedoPerformed()
        {
            base.OnUndoRedoPerformed();
            var activeGenerators = s_ActiveOutlines.Keys.ToArray();
            foreach (var generator in activeGenerators)
            {
                generator.definition.ResetValidState();
				generator.definition.BrushOutline.Validate();
                generator.definition.BrushOutline.CalculatePlanes();
                s_ActiveOutlines[generator] = new ChiselEditableOutline(generator);
                UpdateEditableOutline(generator);
            }
        }


        protected override void OnGeneratorSelected(ChiselBrushComponent generator)
        {
            if (generator.definition.IsValid)
                CommitChanges();
            else
                CancelChanges();

            s_ActiveOutlines[generator] = new ChiselEditableOutline(generator);
        }

        protected override void OnGeneratorDeselected(ChiselBrushComponent generator)
        {
            if (generator.definition.IsValid)
                CommitChanges();
            else
                CancelChanges();

            ChiselTopologySelectionManager.DeselectAll(generator);
            s_ActiveOutlines.Remove(generator);
            if (generator.definition.EnsurePlanarPolygons())
                generator.OnValidate();
        }

		protected override void OnScene(IChiselHandles handles, ChiselBrushComponent generator)
        {
            if (!s_ActiveOutlines.TryGetValue(generator, out ChiselEditableOutline editableOutline) ||
                editableOutline == null)
                return;

            var previousHotControl  = GUIUtility.hotControl;

            if (editableOutline.DoHandles(handles, generator.definition.IsInsideOut))
                UpdateEditableOutline(generator);

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
        void UpdateEditableOutline(ChiselBrushComponent generator)
        {
            generatorModified = true;
            var outline = s_ActiveOutlines[generator];

            var internalBrushMesh = new BrushMesh(outline.brushMesh);
            
            // Removes infinitely thin polygons (for instance, when snapping edges to edges)
            internalBrushMesh.RemoveDegenerateTopology(out outline.edgeRemap, out outline.polygonRemap);

            generator.OnValidate();

			internalBrushMesh.CalculatePlanes();

			generator.definition.BrushOutline = internalBrushMesh;

            if (generator.definition.EnsurePlanarPolygons())
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

            var activeGenerators = s_ActiveOutlines.Keys.ToList();
            for (int i = activeGenerators.Count - 1; i >= 0; i--)
                if (activeGenerators[i] == null) activeGenerators.RemoveAt(i);
            if (activeGenerators.Count > 0)
            {
                Undo.RecordObjects(activeGenerators.ToArray(), activeGenerators.Count == 1 ? "Modified brush" : "Modified brushes"); ;

                // Remove redundant vertices and fix the selection so that the correct edges/vertices etc. are selected
                foreach (var generator in activeGenerators)
                {
                    var outline = s_ActiveOutlines[generator];

                    // We remove redundant vertices here since in editableOutline we make the assumption that the vertices 
                    // between the original and the 'fixed' brushMesh are identical. 
                    // This makes it a lot easier to find 'soft edges'.
                    outline.vertexRemap = generator.definition.BrushOutline.RemoveUnusedVertices();
                    ChiselEditableOutline.RemapSelection(generator.definition.BrushOutline, outline.selection, outline.vertexRemap, outline.edgeRemap, outline.polygonRemap);

                    // Ensure Undo registers any changes
                    generator.definition.BrushOutline = new BrushMesh(generator.definition.BrushOutline);
                }

                // Create new outlines for our generators
                foreach (var generator in activeGenerators)
                    s_ActiveOutlines[generator] = new ChiselEditableOutline(generator);
            }

            generatorModified = false;
        }

        void CancelChanges()
        {
            if (!generatorModified)
                return;

            generatorModified = false;
            var activeGenerators = s_ActiveOutlines.Keys.ToArray();
            foreach (var generator in activeGenerators)
                s_ActiveOutlines[generator] = new ChiselEditableOutline(generator);
            UndoAllChanges();
        }
    }
}