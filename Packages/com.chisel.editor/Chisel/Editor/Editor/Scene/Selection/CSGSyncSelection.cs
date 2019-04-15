using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public class CSGSyncSelection : ScriptableObject, ISerializationCallbackReceiver
    {
        static CSGSyncSelection _instance;
        public static CSGSyncSelection Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                var foundInstances = UnityEngine.Object.FindObjectsOfType<CSGSyncSelection>();
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<CSGSyncSelection>();					
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    return _instance;
                }
                               
                _instance = foundInstances[0];
                return _instance;
            }
        }

        [SerializeField] CSGTreeBrush[] selectedBrushesArray;
        readonly HashSet<CSGTreeBrush> selectedBrushesLookup = new HashSet<CSGTreeBrush>();


        // TODO: can we make this work across domain reloads?
        public void OnBeforeSerialize()
        {
            selectedBrushesArray = selectedBrushesLookup.ToArray();
        }

        public void OnAfterDeserialize()
        {
            selectedBrushesLookup.Clear();
            if (selectedBrushesArray != null)
            {
                foreach (var brush in selectedBrushesArray)
                    selectedBrushesLookup.Add(brush);
            }
        } 


        
        public static void ClearBrushVariants(CSGTreeBrush brush)
        {
            Undo.RecordObject(CSGSyncSelection.Instance, "ClearBrushVariants variant");
            var node = CSGNodeHierarchyManager.FindCSGNodeByTreeNode(brush);
            if (node) node.hierarchyItem.SetBoundsDirty();
            var modified = false;
            if (modified)
                CSGOutlineRenderer.Instance.OnSelectionChanged();
        }

        public static void DeselectBrushVariant(CSGTreeBrush brush)
        {
            Undo.RecordObject(CSGSyncSelection.Instance, "Deselected brush variant");
            var node = CSGNodeHierarchyManager.FindCSGNodeByTreeNode(brush);
            if (node) node.hierarchyItem.SetBoundsDirty();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            var modified = selectedBrushesLookup.Remove(brush);
            if (modified)
                CSGOutlineRenderer.Instance.OnSelectionChanged();
        }

        public static void SelectBrushVariant(CSGTreeBrush brush, bool uniqueSelection = false)
        {
            Undo.RecordObject(CSGSyncSelection.Instance, "Selected brush variant");
            var node = CSGNodeHierarchyManager.FindCSGNodeByTreeNode(brush);
            if (node) node.hierarchyItem.SetBoundsDirty();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            var modified = false;/*
            if (uniqueSelection)
            {
                foreach (var variant in brush.AllSynchronizedVariants)
                {
                    if (variant != brush)
                        modified = selectedBrushesLookup.Remove(variant) || modified;
                }
            }*/
            modified = selectedBrushesLookup.Add(brush);
            if (modified)
                CSGOutlineRenderer.Instance.OnSelectionChanged();
        }

        public static bool IsBrushVariantSelected(CSGTreeBrush brush)
        {
            if (Instance.selectedBrushesLookup.Contains(brush))
                return true;

            return false;
        }
        
        public static bool GetSelectedVariantsOfBrush(CSGTreeBrush brush, List<CSGTreeBrush> selectedVariants)
        {
            selectedVariants.Clear();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            selectedVariants.Add(brush);
            return selectedVariants.Count > 0;
        }


        public static void GetSelectedVariantsOfBrushOrSelf(CSGTreeBrush brush, List<CSGTreeBrush> selectedVariants)
        {
            selectedVariants.Clear();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            selectedVariants.Add(brush);
            if (selectedVariants.Count > 0)
                return;
            selectedVariants.Add(brush);
        }

        public static IEnumerable<CSGTreeBrush> GetSelectedVariantsOfBrushOrSelf(CSGTreeBrush brush)
        {
            yield return brush;
        }

        public static bool IsAnyBrushVariantSelected(CSGTreeBrush brush)
        {
            return Instance.selectedBrushesLookup.Contains(brush);
        }

        public static bool IsAnyBrushVariantSelected(IEnumerable<CSGTreeBrush> brushes)
        {
            foreach (var variant in brushes)
            {
                if (Instance.selectedBrushesLookup.Contains(variant))
                    return true;
            }
            return false;
        }
    }
}
