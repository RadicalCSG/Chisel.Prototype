using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    public struct Range
    {
        public int start;
        public int end;
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return end - start; }
        }

        public int Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return start + ((end - start) / 2); }
        }
    }
}