using System.Linq;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using CanEditMultipleObjects = UnityEditor.CanEditMultipleObjects;
using CustomEditor           = UnityEditor.CustomEditor;
using Undo                   = UnityEditor.Undo;
using GUIUtility             = UnityEngine.GUIUtility;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselBrushComponent))]
    [CanEditMultipleObjects]
    public sealed class ChiselBrushEditor : ChiselGeneratorEditor<ChiselBrushComponent>
    {
        static Dictionary<ChiselBrushComponent, ChiselEditableOutline> activeOutlines = new Dictionary<ChiselBrushComponent, ChiselEditableOutline>();

        protected override void OnUndoRedoPerformed()
        {
            base.OnUndoRedoPerformed();
            var activeGenerators = activeOutlines.Keys.ToArray();
            foreach (var generator in activeGenerators)
            {
                generator.definition.brushOutline.Validate();
                generator.definition.brushOutline.CalculatePlanes();
                activeOutlines[generator] = new ChiselEditableOutline(generator);
                UpdateEditableOutline(generator);
            }
        }


        protected override void OnGeneratorSelected(ChiselBrushComponent generator)
        {
            if (generator.definition.IsValid)
                CommitChanges();
            else
                CancelChanges();

            activeOutlines[generator] = new ChiselEditableOutline(generator);
        }

        protected override void OnGeneratorDeselected(ChiselBrushComponent generator)
        {
            if (generator.definition.IsValid)
                CommitChanges();
            else
                CancelChanges();

            ChiselTopologySelectionManager.DeselectAll(generator);
            activeOutlines.Remove(generator);
            if (generator.definition.EnsurePlanarPolygons())
                generator.OnValidate();
        }

        protected override void OnScene(IChiselHandles handles, ChiselBrushComponent generator)
        {
            if (!activeOutlines.TryGetValue(generator, out ChiselEditableOutline editableOutline) ||
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
            var outline = activeOutlines[generator];

            var internalBrushMesh = new BrushMesh(outline.brushMesh);
            
            // Removes infinitely thin polygons (for instance, when snapping edges to edges)
            internalBrushMesh.RemoveDegenerateTopology(out outline.edgeRemap, out outline.polygonRemap);

            internalBrushMesh.CalculatePlanes();

            // If the brush is concave, we set the generator to not be valid, so that when we commit, it will be reverted
            generator.definition.ValidState = internalBrushMesh.HasVolume() &&          // TODO: implement this, so we know if a brush is a 0D/1D/2D shape
                                              !internalBrushMesh.IsConcave() &&         // TODO: eventually allow concave shapes
                                              !internalBrushMesh.IsSelfIntersecting();  // TODO: in which case this needs to be implemented

            generator.definition.brushOutline = internalBrushMesh;

            generator.definition.EnsurePlanarPolygons();
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

                // Ensure Undo registers any changes
                generator.definition.brushOutline = outline.brushMesh;
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