using System;

namespace Chisel.Core
{
    struct RoutingTable
    {
        public ChiselBlobArray<CategoryRoutingRow>	routingRows;
        public ChiselBlobArray<RoutingLookup>       routingLookups;
        public ChiselBlobArray<int>	                nodeIDToTableIndex;
        public int	                                nodeIDOffset;
    }
}
