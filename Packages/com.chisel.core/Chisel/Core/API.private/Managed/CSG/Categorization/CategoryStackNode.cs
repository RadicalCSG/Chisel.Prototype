using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core 
{
    internal struct CategoryStackNode
    {
        const int bitShift  = 24;
        const int bitMask   = (1 << bitShift) - 1;

        int  nodeIndexInput; // contains both input (max value 255) and nodeIndex (max 24 bit value)
        public CategoryRoutingRow   routingRow;


        public CategoryGroupIndex   Input       { get => (CategoryGroupIndex)(nodeIndexInput >> bitShift); set => nodeIndexInput = (nodeIndexInput & bitMask) | ((int)value << bitShift); }
        public int                  NodeIDValue      { get => nodeIndexInput & bitMask; set => nodeIndexInput = (value & bitMask) | (nodeIndexInput & ~bitMask); }
    }
}