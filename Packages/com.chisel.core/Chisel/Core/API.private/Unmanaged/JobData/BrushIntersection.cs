using System;

namespace Chisel.Core
{
    // Note: Stored in BlobAsset at runtime/editor-time
    struct BrushIntersection
    {
        public IndexOrder       nodeIndexOrder;
        public IntersectionType type;
        public int              bottomUpStart;
        public int              bottomUpEnd;

        public override readonly string ToString() { return $"({nameof(nodeIndexOrder.compactNodeID)}: {nodeIndexOrder.compactNodeID}, {nameof(type)}: {type}, {nameof(bottomUpStart)}: {bottomUpStart}, {nameof(bottomUpEnd)}: {bottomUpEnd})"; }
    }

}
