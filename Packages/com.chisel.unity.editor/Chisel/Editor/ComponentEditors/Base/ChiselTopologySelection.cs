using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using Chisel.Components;
using System.Linq;

namespace Chisel.Editors
{
    [Serializable]
    public sealed class ChiselTopologySelection
    {
        public ChiselNode node;
        public int index;

        public readonly HashSet<int> selectedEdges = new HashSet<int>();
        public readonly HashSet<int> selectedVertices = new HashSet<int>();
        public readonly HashSet<int> selectedPolygons = new HashSet<int>();

        public void Clear()
        {
            selectedEdges.Clear();
            selectedVertices.Clear();
            selectedPolygons.Clear();
        }

        // Since we can't store HashSets when serializing, we have to store them in arrays instead
        [SerializeField] internal int[] selectedEdgesArray;
        [SerializeField] internal int[] selectedVerticesArray;
        [SerializeField] internal int[] selectedPolygonsArray;

        internal void OnAfterDeserialize()
        {
            if (selectedEdgesArray != null)
            {
                selectedEdges.Clear();
                for (int i = 0; i < selectedEdgesArray.Length; i++)
                    selectedEdges.Add(selectedEdgesArray[i]);
            }
            if (selectedVerticesArray != null)
            {
                selectedVertices.Clear();
                for (int i = 0; i < selectedVerticesArray.Length; i++)
                    selectedVertices.Add(selectedVerticesArray[i]);
            }
            if (selectedPolygonsArray != null)
            {
                selectedPolygons.Clear();
                for (int i = 0; i < selectedPolygonsArray.Length; i++)
                    selectedPolygons.Add(selectedPolygonsArray[i]);
            }
            selectedEdgesArray = null;
            selectedVerticesArray = null;
            selectedPolygonsArray = null;
        }

        internal void OnBeforeSerialize()
        {
            selectedEdgesArray = selectedEdges.ToArray();
            selectedVerticesArray = selectedVertices.ToArray();
            selectedPolygonsArray = selectedPolygons.ToArray();
        }

        public void RemapVertexSelection(int[] vertexRemap)
        {
            if (vertexRemap == null)
                return;
            var prevSelection = selectedVertices.ToArray();            
            selectedVertices.Clear();
            for (int v = 0; v < prevSelection.Length; v++)
            {
                var newSelection = vertexRemap[prevSelection[v]];
                if (newSelection == -1)
                    continue;
                selectedVertices.Add(newSelection);
            }
        }

        public void RemapEdgeSelection(int[] edgeRemap)
        {
            if (edgeRemap == null)
                return;
            var prevSelection = selectedEdges.ToArray();
            selectedEdges.Clear();
            for (int e = 0; e < prevSelection.Length; e++)
            {
                var newSelection = edgeRemap[prevSelection[e]];
                if (newSelection == -1)
                    continue;
                selectedEdges.Add(newSelection);
            }
        }

        public void RemapPolygonSelection(int[] polygonRemap)
        {
            if (polygonRemap == null)
                return;
            var prevSelection = selectedPolygons.ToArray();
            selectedPolygons.Clear();
            for (int p = 0; p < prevSelection.Length; p++)
            {
                var newSelection = polygonRemap[prevSelection[p]];
                if (newSelection == -1)
                    continue;
                selectedPolygons.Add(newSelection);
            }
        }
    }

    public class ChiselTopologySelectionManager : ScriptableObject, ISerializationCallbackReceiver
    {
        #region Instance
        static ChiselTopologySelectionManager _instance;
        public static ChiselTopologySelectionManager Selection
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = ScriptableObject.CreateInstance<ChiselTopologySelectionManager>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;
            }
        }
        #endregion

        #region ChiselNodeSelection
        [Serializable]
        public class ChiselNodeSelection
        {
            public ChiselNode node;
            [SerializeField] internal Dictionary<int, ChiselTopologySelection> brushMeshSelections = new Dictionary<int, ChiselTopologySelection>();

            public ChiselTopologySelection this[int index]
            {
                get
                {
                    if (!brushMeshSelections.TryGetValue(index, out ChiselTopologySelection brushMeshSelection))
                    {
                        brushMeshSelection = new ChiselTopologySelection { node = node, index = index };
                        brushMeshSelections[index] = brushMeshSelection;
                        return brushMeshSelection;
                    }

                    Debug.Assert(brushMeshSelection.node == node);
                    Debug.Assert(brushMeshSelection.index == index);
                    return brushMeshSelection;
                }
                internal set
                {
                    Debug.Assert(value.node == node);
                    Debug.Assert(value.index == index);
                    brushMeshSelections[index] = value;
                }
            }

            internal void Deselect(int index)
            {
                if (!brushMeshSelections.TryGetValue(index, out ChiselTopologySelection brushMeshSelection))
                    return;

                Debug.Assert(brushMeshSelection.node == node);
                Debug.Assert(brushMeshSelection.index == index);
                brushMeshSelections.Remove(index);
            }
        }
        #endregion

        [SerializeField] ChiselTopologySelection[] topologySelection;
        private object brushMeshSelection;
        readonly Dictionary<ChiselNode, ChiselNodeSelection> nodeSelectionLookup = new Dictionary<ChiselNode, ChiselNodeSelection>();

        public ChiselNodeSelection this[ChiselNode node]
        {
            get
            {
                if (!nodeSelectionLookup.TryGetValue(node, out ChiselNodeSelection nodeSelection))
                {
                    Undo.RecordObject(Selection, "Select node");
                    nodeSelection = new ChiselNodeSelection { node = node };
                    nodeSelectionLookup[node] = nodeSelection;
                    return nodeSelection;
                }

                Debug.Assert(nodeSelection.node == node);
                return nodeSelection;
            }
        }


        public static void RemapVertexSelection(ChiselBrushComponent generator, int[] vertexRemap)
        {
            if (vertexRemap == null)
                return;
            var selection = ChiselTopologySelectionManager.Selection[generator][0];
            selection.RemapVertexSelection(vertexRemap);
        }

        public static void RemapEdgeSelection(ChiselBrushComponent generator, int[] edgeRemap)
        {
            if (edgeRemap == null)
                return;
            var selection = ChiselTopologySelectionManager.Selection[generator][0];
            selection.RemapEdgeSelection(edgeRemap);
        }

        public static void RemapPolygonSelection(ChiselBrushComponent generator, int[] polygonRemap)
        {
            if (polygonRemap == null)
                return;
            var selection = ChiselTopologySelectionManager.Selection[generator][0];
            selection.RemapPolygonSelection(polygonRemap);
        }

        #region DeselectAll
        public static void DeselectAll(ChiselNode node)
        {
            if (!Selection.nodeSelectionLookup.TryGetValue(node, out ChiselNodeSelection nodeSelection))
                return;

            Undo.RecordObject(Selection, "Deselect node");
            Debug.Assert(nodeSelection.node == node);
            Selection.nodeSelectionLookup.Remove(node);
        }

        public static void DeselectAll(ChiselNode node, int index)
        {
            if (!Selection.nodeSelectionLookup.TryGetValue(node, out ChiselNodeSelection nodeSelection))
                return;

            Undo.RecordObject(Selection, "Deselect node");
            Debug.Assert(nodeSelection.node == node);
            nodeSelection.Deselect(index);
        }
        #endregion

        #region ISerializationCallbackReceiver
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            _instance = this; // We know that *this* instance is valid, so we ensure that we're initialized
            var nodeSelectionList = new List<ChiselTopologySelection>();
            foreach (var nodeSelection in nodeSelectionLookup.Values)
            {
                if (!nodeSelection.node)
                    continue;
                foreach (var brushMeshSelection in nodeSelection.brushMeshSelections.Values)
                {
                    brushMeshSelection.OnBeforeSerialize();
                    nodeSelectionList.Add(brushMeshSelection);
                }
            }
            topologySelection = nodeSelectionList.ToArray();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _instance = this; // We know that *this* instance is valid, so we ensure that we're initialized
            nodeSelectionLookup.Clear();
            for (int i = topologySelection.Length - 1; i >= 0; i--)
            {
                var brushSelection = topologySelection[i];
                if (!brushSelection.node)
                    continue;
                brushSelection.OnAfterDeserialize();
                if (!nodeSelectionLookup.TryGetValue(brushSelection.node, out ChiselNodeSelection nodeSelection))
                {
                    nodeSelection = new ChiselNodeSelection { node = brushSelection.node };
                    nodeSelectionLookup[brushSelection.node] = nodeSelection;
                }
                nodeSelection[brushSelection.index] = brushSelection;
            }
            topologySelection = null;
        }
        #endregion
    }
}