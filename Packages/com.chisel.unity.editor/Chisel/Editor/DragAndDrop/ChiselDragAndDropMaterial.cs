using System.Collections.Generic;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace Chisel.Editors
{
    // TODO: register all changes in undo to ensure that even when something goes wrong, it's still undoable
    public sealed class ChiselDragAndDropMaterial : IChiselDragAndDropOperation
    {
        public ChiselDragAndDropMaterial(Material dragMaterial)
        {
            Undo.IncrementCurrentGroup();
            this.dragMaterial = dragMaterial;
        }

        Material			    dragMaterial			= null;

        SurfaceReference[]      prevSurfaceReferences   = null;
        Material[]			    prevMaterials			= null;

        ChiselNode[]            prevNodes	            = null;

        public static IChiselDragAndDropOperation AcceptDrag()
        {
            // Sometimes this method gets randomly called due to unity weirdness when we're not actually drag & dropping
            if (DragAndDrop.objectReferences == null ||
                DragAndDrop.objectReferences.Length == 0)
                return null;

            var dragMaterial = DragAndDrop.objectReferences[0] as Material;
            if (!dragMaterial)
                return null;

            // TODO: do this without allocation
            return new ChiselDragAndDropMaterial(dragMaterial);
        }

        void UndoPrevSurfaceReference()
        {
            if (prevSurfaceReferences == null)
                return;

            for (int i = 0; i < prevSurfaceReferences.Length; i++)
                prevSurfaceReferences[i].RenderMaterial = prevMaterials[i];

            prevMaterials = null;
            prevSurfaceReferences = null;
            prevNodes = null;
        }


        void ApplyMaterialToSurface(SurfaceReference[] surfaceReferences)
        {
            if (surfaceReferences == null ||
                surfaceReferences.Length == 0)
                return;

            if (prevSurfaceReferences == null ||
                prevSurfaceReferences.Length != surfaceReferences.Length)
            {
                prevMaterials           = new Material[surfaceReferences.Length];
                prevSurfaceReferences   = new SurfaceReference[surfaceReferences.Length];
            }

            var tempNodes = HashSetPool<ChiselNode>.Get();
            try
            {
                for (int i = 0; i < surfaceReferences.Length; i++)
                {
                    tempNodes.Add(surfaceReferences[i].node);
                    prevMaterials[i] = surfaceReferences[i].Surface.RenderMaterial;
                    prevSurfaceReferences[i] = surfaceReferences[i];
                    surfaceReferences[i].RenderMaterial = dragMaterial;
                }
                prevNodes = tempNodes.ToArray();
            }
            finally
            {
                HashSetPool<ChiselNode>.Release(tempNodes);
            }
        }

        static bool Equals(SurfaceReference[] surfacesA, SurfaceReference[] surfacesB)
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

        SurfaceReference[] AddSelectedSurfaces(SurfaceReference[] surfaceReferences)
        {
            if (surfaceReferences == null ||
                surfaceReferences.Length != 1)
                return surfaceReferences;

            if (ChiselUVMoveTool.IsActive() ||
                ChiselUVRotateTool.IsActive() ||
                ChiselUVScaleTool.IsActive())
                return surfaceReferences;

            // TODO: implement the ability to query this from the edit mode
            if (!ChiselSurfaceSelectionManager.IsSelected(surfaceReferences[0]))
                return surfaceReferences;

            var surfaceHashSet = new HashSet<SurfaceReference>();
            surfaceHashSet.AddRange(ChiselSurfaceSelectionManager.Selection);
            surfaceHashSet.AddRange(surfaceReferences);
            return surfaceHashSet.ToArray();
        }

        static readonly List<SurfaceReference>      s_FoundSurfaceReferences    = new List<SurfaceReference>();

        public void UpdateDrag()
        {
            var selectAllSurfaces = UnityEngine.Event.current.shift;
            
            { 
                s_FoundSurfaceReferences.Clear();
                ChiselClickSelectionManager.FindSurfaceReferences(Event.current.mousePosition, selectAllSurfaces, s_FoundSurfaceReferences);
                var surfaceReferences = s_FoundSurfaceReferences.ToArray();
                s_FoundSurfaceReferences.Clear();
                if (!Equals(prevSurfaceReferences, surfaceReferences))
                {
                    UndoPrevSurfaceReference();

                    // Handle situation where we're hovering over a selected surface, then apply to all selected surfaces
                    if (!selectAllSurfaces)
                        surfaceReferences = AddSelectedSurfaces(surfaceReferences);

                    ApplyMaterialToSurface(surfaceReferences);
                }
            }
        }

        public void PerformDrag()
        {
            var surfaceReferences = prevSurfaceReferences;
            var nodes             = prevNodes;
            UndoPrevSurfaceReference();
            if (nodes == null || nodes.Length == 0)
                return;
            Undo.RecordObjects(nodes, "Drag & drop material");
            ApplyMaterialToSurface(surfaceReferences);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.FlushUndoRecordObjects();
        }

        public void CancelDrag()
        {
            Undo.RevertAllInCurrentGroup();
            UndoPrevSurfaceReference();
        }
    }
}
