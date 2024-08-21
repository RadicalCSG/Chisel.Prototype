using System;

namespace Chisel.Core
{
    struct IndexOrder : IEquatable<IndexOrder>
    {
        public CompactNodeID compactNodeID;
        public int nodeOrder;

        public readonly bool Equals(IndexOrder other)
        {
            return compactNodeID == other.compactNodeID;
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is not IndexOrder)
                return false;
            return Equals((IndexOrder)obj);
        }

        public override readonly int GetHashCode()
        {
            return compactNodeID.GetHashCode();
        }
    }
}
