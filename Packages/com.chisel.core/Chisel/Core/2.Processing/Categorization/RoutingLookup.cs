using System.Runtime.CompilerServices;
using Unity.Burst;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    struct RoutingLookup
    {
        public int startIndex;
        public int endIndex;

        //public const int kRoutingOffset = 1 + (int)CategoryIndex.LastCategory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRoute([NoAlias, ReadOnly] ref RoutingTable table, byte inputIndex, out CategoryRoutingRow routingRow)
        {
            var tableIndex = startIndex + (int)inputIndex;// (inputIndex == 0) ? (int)0 : ((int)inputIndex - kRoutingOffset);

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
