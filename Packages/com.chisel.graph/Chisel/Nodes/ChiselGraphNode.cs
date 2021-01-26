using System;
using UnityEngine;
using XNode;
using Chisel.Core;

namespace Chisel.Nodes
{
    public abstract class ChiselGraphNode : Node
    {
        [Input, HideInInspector] public CSG input;
        [Output, HideInInspector] public CSG output;

        [Input] public Vector3 localPosition;
        [Input] public Vector3 localRotation;


        public Action onStateChange;
        public ChiselGraph chiselGraph => graph as ChiselGraph;

        public CSGOperationType operation = CSGOperationType.Additive;

        public void SetActive()
        {
            chiselGraph.SetActiveNode(this);
        }

        public abstract CSGTreeNode GetNode();

        public void ParseNode(CSGTreeBranch branch)
        {
            var inputPort = GetInputPort("input");
            if (inputPort.IsConnected)
            {
                var connections = inputPort.GetConnections();
                foreach (var connection in connections)
                {
                    var chiselNode = connection.node as ChiselGraphNode;
                    chiselNode.ParseNode(branch);
                }
            }

            var node = GetNode();
            if (node.Valid)
            {
                node.LocalTransformation = Matrix4x4.TRS(localPosition, Quaternion.Euler(localRotation), Vector3.one);
                node.Operation = operation;
                branch.Add(node);
            }
        }

        public override object GetValue(NodePort port)
        {
            return null;
        }

        public override void OnCreateConnection(NodePort from, NodePort to)
        {
            chiselGraph.UpdateCSG();
        }

        public override void OnRemoveConnection(NodePort port)
        {
            chiselGraph.UpdateCSG();
        }

        void OnValidate()
        {
            chiselGraph.UpdateProperties();
        }

        protected int GetGraphNodeID()
        {
            return chiselGraph.nodes.IndexOf(this);
        }

        [Serializable]
        public class CSG { }
    }
}