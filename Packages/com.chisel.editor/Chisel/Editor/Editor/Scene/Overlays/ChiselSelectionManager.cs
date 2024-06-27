using System;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;

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

        //static bool s_CurrHaveNodes = false;
        static bool s_CurrHaveOperationNodes = false;
        static bool s_CurrHaveGenerators = false;
        static CSGOperationType? s_CurrOperation = null;
        static readonly List<ChiselNode> s_Nodes = new();
        static readonly List<IChiselHasOperation> s_OperationNodes = new();
        static readonly List<ChiselGeneratorComponent> s_Generators = new();

        static void UpdateSelection()
        {
            s_Nodes.Clear();
            s_Nodes.AddRange(Selection.GetFiltered<ChiselNode>(SelectionMode.DeepAssets));
            s_Generators.Clear();
            s_OperationNodes.Clear();
            foreach (var node in s_Nodes)
            {
                if (node is ChiselGeneratorComponent generator)
                {
                    s_Generators.Add(generator);
                    s_OperationNodes.Add(generator);
                } else if (node is IChiselHasOperation hasOperation)
                {
                    s_OperationNodes.Add(hasOperation);
                }
            }

            var prevOperation = s_CurrOperation;
            UpdateOperationSelection();

            //var prevHaveNodes = currHaveNodes;
            //s_CurrHaveNodes = s_Nodes.Count > 0;

            var prevHaveGenerators = s_CurrHaveGenerators;
            s_CurrHaveGenerators = s_Generators.Count > 0;

            var prevHaveOperationNodes = s_CurrHaveOperationNodes;
            s_CurrHaveOperationNodes = s_OperationNodes.Count > 0;

            //if (prevHaveNodes || currHaveNodes)
            //    NodesSelectionUpdated?.Invoke();

            if (prevHaveGenerators || s_CurrHaveGenerators)
                GeneratorSelectionUpdated?.Invoke();

            if (prevHaveOperationNodes || s_CurrHaveOperationNodes)
                OperationNodesSelectionUpdated?.Invoke();

            if (prevOperation != s_CurrOperation)
                NodeOperationUpdated?.Invoke();
        }


        // TODO: needs to be called when any operation changes, anywhere
        public static void UpdateOperationSelection()
        {
            s_CurrOperation = null;
            bool found = false;
            foreach (var operationNode in s_OperationNodes)
            {
                if (!found)
                {
                    s_CurrOperation = operationNode.Operation;
                    found = true;
                } else
                if (s_CurrOperation.HasValue && s_CurrOperation.Value != operationNode.Operation)
                    s_CurrOperation = null;
            }
        }

        public static void SetOperationForSelection(CSGOperationType newOperation)
        {
            if (s_CurrOperation == newOperation)
                return;

            foreach (var hasOperation in s_OperationNodes)
                hasOperation.Operation = newOperation;

            var prevOperation = s_CurrOperation;
            UpdateOperationSelection();
            if (prevOperation != s_CurrOperation)
                NodeOperationUpdated?.Invoke();
        }

        public static IReadOnlyList<ChiselGeneratorComponent> SelectedGenerators { get { return s_Generators; } }
        public static bool AreGeneratorsSelected { get { return s_Generators.Count > 0; } }

        public static IReadOnlyList<ChiselNode> SelectedNodes { get { return s_Nodes; } }
        public static bool AreNodesSelected { get { return s_Nodes.Count > 0; } }

        public static IReadOnlyList<IChiselHasOperation> SelectedOperationNodes { get { return s_OperationNodes; } }
        public static bool AreOperationNodesSelected { get { return s_OperationNodes.Count > 0; } }
        public static CSGOperationType? OperationOfSelectedNodes { get { return s_CurrOperation; } }
    }
}
