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

        Material			dragMaterial			= null;

        CSGSurfaceAsset[]	prevSurfaces			= null;
        Material[]			prevMaterials			= null;

        CSGBrushMeshAsset[] prevBrushMeshAssets	= null;

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
            if (prevSurfaces == null)
                return;

            for (int i = 0; i < prevSurfaces.Length; i++)
                prevSurfaces[i].RenderMaterial = prevMaterials[i];

            prevMaterials = null;
            prevSurfaces = null;
            prevBrushMeshAssets = null;
        }

        void ApplyMaterialToSurface(CSGBrushMeshAsset[] brushMeshAssets, CSGSurfaceAsset[] surface)
        {
            if (surface == null)
                return;

            if (prevSurfaces == null ||
                prevSurfaces.Length != surface.Length)
            {
                prevMaterials = new Material[surface.Length];
                prevSurfaces  = new CSGSurfaceAsset[surface.Length];
            }
            for (int i = 0; i < surface.Length; i++)
            { 
                prevMaterials[i] = surface[i].RenderMaterial;
                prevSurfaces[i]  = surface[i];
                surface[i].RenderMaterial = dragMaterial;
            }
            prevBrushMeshAssets = brushMeshAssets;
        }

        static bool Equals(CSGSurfaceAsset[] surfacesA, CSGSurfaceAsset[] surfacesB)
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

        CSGSurfaceAsset[] AddSelectedSurfaces(CSGSurfaceAsset[] surfaces)
        {
            if (surfaces == null ||
                surfaces.Length != 1)
                return surfaces;

            // TODO: remove special casing
            if (CSGEditModeManager.EditMode != CSGEditMode.SurfaceEdit)
                return surfaces;

            if (!CSGSurfaceSelectionManager.IsSelected(surfaces[0]))
                return surfaces;

            var surfaceHashSet = new HashSet<CSGSurfaceAsset>();
            surfaceHashSet.AddRange(CSGSurfaceSelectionManager.SelectedSurfaceAssets);
            surfaceHashSet.AddRange(surfaces);
            return surfaceHashSet.ToArray();
        }

        public void UpdateDrag()
        {
            var selectAllSurfaces = UnityEngine.Event.current.shift;
            CSGBrushMeshAsset[] brushMeshAssets;
            CSGSurfaceAsset[]	surfaces;
            CSGClickSelectionManager.FindSurfaceAsset(Event.current.mousePosition, out surfaces, out brushMeshAssets, selectAllSurfaces);
            if (!Equals(prevSurfaces, surfaces))
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
            var surfaces = prevSurfaces;
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
