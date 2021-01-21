using Chisel.Core;
using Chisel.Nodes;
using UnityEngine;

public class SubGraphNode : ChiselGraphNode
{
    public ChiselGraph subgraph;

    public override CSGTreeNode GetNode()
    {
        return default;
    }

    public override void OnParseNode(CSGTreeBranch parentBranch)
    {
        var branch = CSGTreeBranch.Create();
        branch.LocalTransformation = Matrix4x4.TRS(localPosition, Quaternion.Euler(localRotation), Vector3.one);
        branch.Operation = operation;
        parentBranch.Add(branch);
        subgraph.active.ParseNode(branch);
    }
}