using Chisel.Core;
using Chisel.Assets;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Editors
{
    [Serializable]
    public class SurfaceSelection : ISingletonData
    {
        public HashSet<SurfaceReference> selectedSurfaces	= new HashSet<SurfaceReference>();
        public SurfaceReference[] selectedSurfacesArray;
        
        [NonSerialized]
        public HashSet<SurfaceReference> hoverSurfaces	= new HashSet<SurfaceReference>();
        
        public void OnAfterDeserialize()
        {
            selectedSurfaces.Clear();
            if (selectedSurfacesArray != null)
                selectedSurfaces.AddRange(selectedSurfacesArray);
        }

        public void OnBeforeSerialize()
        {
            selectedSurfacesArray = selectedSurfaces.ToArray();
        }
    }
    
    public class CSGSurfaceSelectionManager : SingletonManager<SurfaceSelection, CSGSurfaceSelectionManager>
    {
        public static Action selectionChanged;
        public static Action hoverChanged;

        protected override void Initialize() 
        {
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
        }
        
        protected override void Shutdown()
        {
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
        }

        public static HashSet<SurfaceReference> Selection 
        {
            get { return Data.selectedSurfaces; }
        }
        
        public static HashSet<SurfaceReference> Hovered
        {
            get { return Data.hoverSurfaces; }
        }

        public static HashSet<CSGNode> SelectedNodes 
        {
            get
            {
                var selectedSurfaces	= Data.selectedSurfaces;
                var uniqueNodes			= new HashSet<CSGNode>();

                foreach (var selectedSurface in selectedSurfaces)
                    uniqueNodes.Add(selectedSurface.node);
                return uniqueNodes;
            }
        }
        
        public static HashSet<GameObject> SelectedGameObjects
        {
            get
            {
                var selectedSurfaces	= Data.selectedSurfaces;
                var uniqueNodes			= new HashSet<GameObject>();

                foreach (var selectedSurface in selectedSurfaces)
                    uniqueNodes.Add(selectedSurface.node.gameObject);
                return uniqueNodes;
            }
        }
        
        public static HashSet<CSGBrushMeshAsset> SelectedBrushMeshes
        {
            get
            {
                var selectedSurfaces		= Data.selectedSurfaces;
                var uniqueBrushMeshAssets	= new HashSet<CSGBrushMeshAsset>();

                foreach (var selectedSurface in selectedSurfaces)
                    uniqueBrushMeshAssets.Add(selectedSurface.brushMeshAsset);
                return uniqueBrushMeshAssets;
            }
        }
        
        public static HashSet<ChiselBrushMaterial> SelectedBrushMaterials	
        {
            get
            {
                var selectedSurfaces = Data.selectedSurfaces;
                var uniqueBrushMaterials = new HashSet<ChiselBrushMaterial>();

                foreach (var selectedSurface in selectedSurfaces)
                    uniqueBrushMaterials.Add(selectedSurface.BrushMaterial);
                return uniqueBrushMaterials;
            }
        }


        public static bool IsSelected(SurfaceReference surface)
        {
            return Data.selectedSurfaces.Contains(surface);
        }

        public static bool IsSelected(ChiselBrushMaterial brushMaterial)
        {
            var selectedSurfaces = Data.selectedSurfaces;
            foreach(var selectedSurface in selectedSurfaces)
            {
                if (selectedSurface.BrushMaterial == brushMaterial)
                    return true;
            }
            return false;
        }

        public static bool IsAnySelected(ChiselBrushMaterial[] brushMaterials)
        {
            foreach(var brushMaterial in brushMaterials)
            {
                if (SelectedBrushMaterials.Contains(brushMaterial))
                    return true;
            }
            return false;
        }

        public static bool IsAnySelected(IEnumerable<SurfaceReference> selection)
        {
            foreach(var surface in selection)
            {
                if (Data.selectedSurfaces.Contains(surface))
                    return true;
            }
            return false;
        }


        public static bool Hover(HashSet<SurfaceReference> surfaces)
        {
            if (!HoverInternal(surfaces))
                return false;
            if (hoverChanged != null)
                hoverChanged.Invoke();
            return true;
        }

        public static bool Unhover(HashSet<SurfaceReference> surfaces)
        {
            if (!UnhoverInternal(surfaces))
                return false;
            if (hoverChanged != null)
                hoverChanged.Invoke();
            return true;
        }

        public static bool UnhoverAll()
        {
            if (!UnhoverAllInternal())
                return false;
            if (hoverChanged != null)
                hoverChanged.Invoke();
            return true;
        }




        static bool HoverInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            bool modified = Data.hoverSurfaces.AddRange(surfaces);
            return modified;
        }
        
        static bool UnhoverInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            bool modified = Data.hoverSurfaces.RemoveRange(surfaces);
            return modified;
        }

        static bool UnhoverAllInternal()
        {
            if (Data.hoverSurfaces.Count == 0)
                return false;
            
            Data.hoverSurfaces.Clear();
            return true;
        }



        public static bool Select(HashSet<SurfaceReference> surfaces)
        {
            if (!SelectInternal(surfaces))
                return false;
            if (selectionChanged != null)
                selectionChanged.Invoke();
            return true;
        }

        public static bool Deselect(HashSet<SurfaceReference> surfaces)
        {
            if (!DeselectInternal(surfaces))
                return false;
            if (selectionChanged != null)
                selectionChanged.Invoke();
            return true;
        }

        public static bool DeselectAll()
        {
            if (!DeselectAllInternal())
                return false;
            if (selectionChanged != null)
                selectionChanged.Invoke();
            return true;
        }



        static bool SelectInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            RecordUndo("Modified Surface Selection");
            bool modified = Data.selectedSurfaces.AddRange(surfaces);
            return modified;
        }
        
        static bool DeselectInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            RecordUndo("Modified Surface Selection");
            bool modified = Data.selectedSurfaces.RemoveRange(surfaces);
            return modified;
        }

        static bool DeselectAllInternal()
        {
            if (Data.selectedSurfaces.Count == 0)
                return false;
            
            RecordUndo("Deselected All Surfaces");
            Data.selectedSurfaces.Clear();
            return true;
        }


        static void OnSelectionChanged()
        {
            var gameObjects			= new HashSet<GameObject>(UnityEditor.Selection.gameObjects);
            var usedGameObjects		= new HashSet<GameObject>();
            var newSelectedSurfaces = new HashSet<SurfaceReference>();
            foreach(var selection in Data.selectedSurfaces)
            {
                if (!selection.node)
                    continue;
                var gameObject = selection.node.gameObject;
                if (!gameObject ||
                    !gameObjects.Contains(gameObject))
                    continue;
                newSelectedSurfaces.Add(selection);
                usedGameObjects.Add(gameObject);
            }

            foreach(var gameObject in gameObjects)
            {
                if (usedGameObjects.Contains(gameObject))
                    continue;

                var node = gameObject.GetComponent<CSGNode>();
                if (!node)
                    continue;

                var surfaces = node.GetAllSurfaceReferences();
                // It's possible that the node has not (yet) been set up correctly ...
                if (surfaces == null)
                    continue;

                newSelectedSurfaces.AddRange(node.GetAllSurfaceReferences());
            }

            UpdateSelection(SelectionType.Replace, newSelectedSurfaces);
        }

        public static bool UpdateSelection(SelectionType selectionType, HashSet<SurfaceReference> surfaces)
        {
            bool modified = false;
            if (selectionType == SelectionType.Replace)
            {
                modified = DeselectAllInternal();
            }
            
            // TODO: handle replace in a single step (using 'Set') to detect modifications

            if (surfaces != null)
            { 
                if (selectionType == SelectionType.Subtractive)
                    modified = DeselectInternal(surfaces) || modified;
                else
                    modified = SelectInternal(surfaces) || modified;
            }

            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.objects = SelectedGameObjects.ToArray();
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
            if (modified && selectionChanged != null)
                selectionChanged.Invoke();
            return modified;
        }

        
        public static bool SetHovering(SelectionType selectionType, HashSet<SurfaceReference> surfaces)
        {
            bool modified = false;
            if (surfaces != null)
            {
                switch (selectionType)
                {
                    default:
                    {
                        modified = Data.hoverSurfaces.Set(surfaces);
                        break;
                    }
                    case SelectionType.Subtractive:
                    {
                        modified = Data.hoverSurfaces.SetCommon(surfaces, Data.selectedSurfaces);
                        break;
                    }
                }
            } else
                modified = UnhoverAllInternal();

            if (modified && hoverChanged != null)
                hoverChanged.Invoke();
            return modified;
        }
    }

}
