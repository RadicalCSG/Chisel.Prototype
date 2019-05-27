using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core;
using Chisel.Assets;
using Chisel.Components;

namespace Chisel.Editors
{
    public sealed class CSGDragAndDropMaterial : IDragAndDropOperation
    {
        public CSGDragAndDropMaterial(Material dragMaterial)
        {
            Undo.IncrementCurrentGroup();
            this.dragMaterial = dragMaterial;
        }

        Material			    dragMaterial			= null;

        ChiselBrushMaterial[]	prevBrushMaterials		= null;
        Material[]			    prevMaterials			= null;

        CSGBrushMeshAsset[]     prevBrushMeshAssets	= null;

        public static IDragAndDropOperation AcceptDrag()
        {
            // Sometimes this method gets randomly called due to unity weirdness when we're not actually drag & dropping
            if (DragAndDrop.objectReferences == null ||
                DragAndDrop.objectReferences.Length == 0)
                return null;

            var dragMaterial = DragAndDrop.objectReferences[0] as Material;
            if (!dragMaterial)
                return null;

            // TODO: do this without allocation
            return new CSGDragAndDropMaterial(dragMaterial);
        }

        void UndoPrevSurface()
        {
            if (prevBrushMaterials == null)
                return;

            for (int i = 0; i < prevBrushMaterials.Length; i++)
                prevBrushMaterials[i].RenderMaterial = prevMaterials[i];

            prevMaterials = null;
            prevBrushMaterials = null;
            prevBrushMeshAssets = null;
        }

        void ApplyMaterialToSurface(CSGBrushMeshAsset[] brushMeshAssets, ChiselBrushMaterial[] surface)
        {
            if (surface == null)
                return;

            if (prevBrushMaterials == null ||
                prevBrushMaterials.Length != surface.Length)
            {
                prevMaterials = new Material[surface.Length];
                prevBrushMaterials  = new ChiselBrushMaterial[surface.Length];
            }
            for (int i = 0; i < surface.Length; i++)
            { 
                prevMaterials[i] = surface[i].RenderMaterial;
                prevBrushMaterials[i]  = surface[i];
                surface[i].RenderMaterial = dragMaterial;
            }
            prevBrushMeshAssets = brushMeshAssets;
        }

        static bool Equals(ChiselBrushMaterial[] surfacesA, ChiselBrushMaterial[] surfacesB)
        {
            if (surfacesA == null)
            {
                if (surfacesB == null)
                    return true;
                return false;
            }
            if (surfacesB == null)
                return false;

            if (surfacesA.Length != surfacesB.Length)
                return false;

            for (int i = 0; i < surfacesA.Length; i++)
            {
                if (surfacesA[i] != surfacesB[i])
                    return false;
            }
            return true;
        }

        ChiselBrushMaterial[] AddSelectedSurfaces(ChiselBrushMaterial[] surfaces)
        {
            if (surfaces == null ||
                surfaces.Length != 1)
                return surfaces;

            // TODO: remove special casing
            if (CSGEditModeManager.EditMode != CSGEditMode.SurfaceEdit)
                return surfaces;

            if (!CSGSurfaceSelectionManager.IsSelected(surfaces[0]))
                return surfaces;

            var surfaceHashSet = new HashSet<ChiselBrushMaterial>();
            surfaceHashSet.AddRange(CSGSurfaceSelectionManager.SelectedBrushMaterials);
            surfaceHashSet.AddRange(surfaces);
            return surfaceHashSet.ToArray();
        }

        public void UpdateDrag()
        {
            var selectAllSurfaces = UnityEngine.Event.current.shift;
            CSGBrushMeshAsset[] brushMeshAssets;
            ChiselBrushMaterial[]	surfaces;
            CSGClickSelectionManager.FindBrushMaterials(Event.current.mousePosition, out surfaces, out brushMeshAssets, selectAllSurfaces);
            if (!Equals(prevBrushMaterials, surfaces))
            {
                UndoPrevSurface();

                // Handle situation where we're hovering over a selected surface, then apply to all selected surfaces
                if (!selectAllSurfaces)
                    surfaces = AddSelectedSurfaces(surfaces);

                ApplyMaterialToSurface(brushMeshAssets, surfaces);
            }
        }

        public void PerformDrag()
        {
            var surfaces = prevBrushMaterials;
            var brushMeshAssets = prevBrushMeshAssets;
            UndoPrevSurface();
            if (surfaces == null)
                return;
            Undo.RecordObjects(brushMeshAssets, "Drag & drop material");
            ApplyMaterialToSurface(brushMeshAssets, surfaces);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.FlushUndoRecordObjects();
        }

        public void CancelDrag()
        {
            Undo.RevertAllInCurrentGroup();
            UndoPrevSurface();
        }
    }
}
