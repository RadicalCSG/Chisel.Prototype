using System;

namespace Chisel.Core
{
    struct CompactHierarchyNode
    {
        // TODO: combine bits
        public CSGNodeType      Type;
        public CSGOperationType Operation;
        public CompactNodeID    CompactNodeID;
        public int              childCount;
        public int              childOffset;

        public override string ToString() { return $"({nameof(Type)}: {Type}, {nameof(childCount)}: {childCount}, {nameof(childOffset)}: {childOffset}, {nameof(Operation)}: {Operation}, {nameof(CompactNodeID)}: {CompactNodeID})"; }
    }
    
    struct BrushAncestorLegend
    {
        public int  ancestorStartIDValue;
        public int  ancestorEndIDValue;

        public override string ToString() { return $"({nameof(ancestorStartIDValue)}: {ancestorStartIDValue}, {nameof(ancestorEndIDValue)}: {ancestorEndIDValue})"; }
    }

    struct CompactTree
    {
        public ChiselBlobArray<CompactHierarchyNode>      compactHierarchy;
        public ChiselBlobArray<BrushAncestorLegend>       brushAncestorLegend;
        public ChiselBlobArray<int>                       brushAncestors;

        public int                                  minBrushIDValue;
        public ChiselBlobArray<int>                       brushIDValueToAncestorLegend;
        public int                                  minNodeIDValue;
        public int                                  maxNodeIDValue;
    }
}
