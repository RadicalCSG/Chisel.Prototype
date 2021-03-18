using System;

namespace Chisel.Core 
{
    struct CategoryStackNode
    {
        const int bitShift  = 24;
        const int bitMask   = (1 << bitShift) - 1;

        int  nodeIndexInput; // contains both input (max value 255) and nodeIndex (max 24 bit value)
        public CategoryRoutingRow   routingRow;


        public CategoryGroupIndex   Input       { get => (CategoryGroupIndex)(nodeIndexInput >> bitShift); set => nodeIndexInput = (nodeIndexInput & bitMask) | ((int)value << bitShift); }
        public int                  NodeIDValue      { get => nodeIndexInput & bitMask; set => nodeIndexInput = (value & bitMask) | (nodeIndexInput & ~bitMask); }
    }
}