using Chisel.Core;
using Chisel.Nodes;
using UnityEngine;

public class SubGraphNode : ChiselGraphNode
{
    public ChiselGraph subgraph;

    public override CSGTreeNode GetNode()
    {
        var branch = CSGTreeBranch.Create(GetInstanceID());
        subgraph.active.ParseNode(branch);
        return branch;
    }
}