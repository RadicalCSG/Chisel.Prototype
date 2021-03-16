using Chisel.Core;
using Chisel.Nodes;

public class TransformNode : ChiselGraphNode
{
    public override CSGTreeNode GetNode()
    {
        var branch = CSGTreeBranch.Create(GetInstanceID());
        return branch;
    }
}