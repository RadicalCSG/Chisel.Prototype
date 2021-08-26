using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;
using System.Reflection;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Chisel.Editors
{

    static class ChiselSelectionManager
    {
        [UnityEditor.InitializeOnLoadMethod]
        static void Initialize()
        {
            Selection.selectionChanged -= UpdateSelection;
            Selection.selectionChanged += UpdateSelection;
            UpdateSelection();
        }

        public static Action NodeOperationUpdated;
        //public static Action NodesSelectionUpdated;
        public static Action OperationNodesSelectionUpdated;
        public static Action GeneratorSelectionUpdated;

        static bool currHaveNodes = false;
        static bool currHaveOperationNodes = false;
        static bool currHaveGenerators = false;
        static CSGOperationType? currOperation = null;
        static List<ChiselNode> nodes = new List<ChiselNode>();
        static List<IChiselHasOperation> operationNodes = new List<IChiselHasOperation>();
        static List<ChiselGeneratorComponent> generators = new List<ChiselGeneratorComponent>();

        static void UpdateSelection()
        {
            nodes.Clear();
            nodes.AddRange(Selection.GetFiltered<ChiselNode>(SelectionMode.DeepAssets));
            generators.Clear();
            operationNodes.Clear();
            foreach (var node in nodes)
            {
                if (node is ChiselGeneratorComponent generator)
                {
                    generators.Add(generator);
                    operationNodes.Add(generator);
                } else if (node is IChiselHasOperation hasOperation)
                {
                    operationNodes.Add(hasOperation);
                }
            }

            var prevOperation = currOperation;
            UpdateOperationSelection();

            //var prevHaveNodes = currHaveNodes;
            currHaveNodes = nodes.Count > 0;

            var prevHaveGenerators = currHaveGenerators;
            currHaveGenerators = generators.Count > 0;

            var prevHaveOperationNodes = currHaveOperationNodes;
            currHaveOperationNodes = operationNodes.Count > 0;

            //if (prevHaveNodes || currHaveNodes)
            //    NodesSelectionUpdated?.Invoke();

            if (prevHaveGenerators || currHaveGenerators)
                GeneratorSelectionUpdated?.Invoke();

            if (prevHaveOperationNodes || currHaveOperationNodes)
                OperationNodesSelectionUpdated?.Invoke();

            if (prevOperation != currOperation)
                NodeOperationUpdated?.Invoke();
        }


        // TODO: needs to be called when any operation changes, anywhere
        public static void UpdateOperationSelection()
        {
            currOperation = null;
            bool found = false;
            foreach (var operationNode in operationNodes)
            {
                if (!found)
                {
                    currOperation = operationNode.Operation;
                    found = true;
                } else
                if (currOperation.HasValue && currOperation.Value != operationNode.Operation)
                    currOperation = null;
            }
        }

        public static void SetOperationForSelection(CSGOperationType newOperation)
        {
            if (currOperation == newOperation)
                return;

            foreach (var hasOperation in operationNodes)
                hasOperation.Operation = newOperation;

            var prevOperation = currOperation;
            UpdateOperationSelection();
            if (prevOperation != currOperation)
                NodeOperationUpdated?.Invoke();
        }

        public static IReadOnlyList<ChiselGeneratorComponent> SelectedGenerators { get { return generators; } }
        public static bool AreGeneratorsSelected { get { return generators.Count > 0; } }

        public static IReadOnlyList<ChiselNode> SelectedNodes { get { return nodes; } }
        public static bool AreNodesSelected { get { return nodes.Count > 0; } }

        public static IReadOnlyList<IChiselHasOperation> SelectedOperationNodes { get { return operationNodes; } }
        public static bool AreOperationNodesSelected { get { return operationNodes.Count > 0; } }
        public static CSGOperationType? OperationOfSelectedNodes { get { return currOperation; } }
    }
}
