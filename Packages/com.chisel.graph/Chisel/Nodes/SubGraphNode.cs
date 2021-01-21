using Chisel.Core;
using Chisel.Nodes;

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
        branch.Operation = operation;
        parentBranch.Add(branch);
        subgraph.active.ParseNode(branch);
    }
}