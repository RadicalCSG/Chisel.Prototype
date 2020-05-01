using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using UnityEngine;

namespace Chisel.Core
{
    public struct RoutingLookup
    {
        public RoutingLookup(int startIndex, int endIndex)
        {
            this.startIndex = startIndex;
            this.endIndex = endIndex;
        }

        public readonly int startIndex;
        public readonly int endIndex;

        //public const int kRoutingOffset = 1 + (int)CategoryIndex.LastCategory;

        public bool TryGetRoute(ref RoutingTable table, CategoryGroupIndex inputIndex, out CategoryRoutingRow routingRow)
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
