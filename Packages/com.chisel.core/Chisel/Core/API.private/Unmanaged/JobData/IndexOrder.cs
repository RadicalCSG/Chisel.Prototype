using System;

namespace Chisel.Core
{
    struct IndexOrder : IEquatable<IndexOrder>
    {
        public CompactNodeID compactNodeID;
        public int nodeOrder;

        public bool Equals(IndexOrder other)
        {
            return compactNodeID == other.compactNodeID;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is IndexOrder))
                return false;
            return Equals((IndexOrder)obj);
        }

        public override int GetHashCode()
        {
            return compactNodeID.GetHashCode();
        }
    }
}
