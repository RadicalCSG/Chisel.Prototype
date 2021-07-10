using System;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Chisel.Core
{
    struct RoutingLookup
    {
        public int startIndex;
        public int endIndex;

        //public const int kRoutingOffset = 1 + (int)CategoryIndex.LastCategory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRoute([NoAlias] ref RoutingTable table, CategoryGroupIndex inputIndex, out CategoryRoutingRow routingRow)
        {
            var tableIndex = startIndex + (int)inputIndex;// (inputIndex == CategoryGroupIndex.First) ? (int)CategoryGroupIndex.First : ((int)inputIndex - kRoutingOffset);

            if (tableIndex < startIndex || tableIndex >= endIndex)
            {
                routingRow = new CategoryRoutingRow(inputIndex);
                return false;
            }

            //Debug.Assert(inputIndex == table.Value.inputs[tableIndex]);
            routingRow = table.routingRows[tableIndex];
            return true;
        }
    }
}
