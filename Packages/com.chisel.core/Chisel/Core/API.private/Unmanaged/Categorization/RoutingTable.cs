using System;
using Unity.Entities;

namespace Chisel.Core
{
    struct RoutingTable
    {
        public BlobArray<CategoryRoutingRow>	routingRows;
        public BlobArray<RoutingLookup>         routingLookups;
        public BlobArray<int>	                nodeIDToTableIndex;
        public int	                            nodeIDOffset;
    }
}
