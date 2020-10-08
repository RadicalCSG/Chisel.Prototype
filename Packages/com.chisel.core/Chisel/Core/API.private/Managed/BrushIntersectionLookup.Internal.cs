using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    internal struct BrushIntersectionLookup : IDisposable
    {
        const int bits = 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BrushIntersectionLookup(int _offset, int _length, Allocator allocator)
        {
            var size = ((_length * bits) + 31) / 32;
            if (size <= 0)
                this.twoBits = new NativeArray<UInt32>(1, allocator);
            else
                this.twoBits = new NativeArray<UInt32>(size, allocator);
            Length = (twoBits.Length * 32) / bits;
            Offset = _offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (twoBits.IsCreated)
                twoBits.Dispose();
        }

        public NativeArray<UInt32> twoBits;
        public readonly int Offset;
        public readonly int Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            twoBits.ClearValues();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, IntersectionType value)
        {
            Debug.Assert(value != IntersectionType.InvalidValue);
            index -= Offset;
            if (index < 0 || index >= Length)
            {
                Debug.Assert(false);
                return;
            }

            index <<= 1;
            var int32Index  = index >> 5;	// divide by 32
            var bitIndex    = index & 31;	// remainder
            var twoBit      = ((uint)3) << bitIndex;
            var twoBitValue = ((uint)value) << bitIndex;

            var originalInt32 = twoBits[int32Index];

            twoBits[int32Index] = (originalInt32 & ~twoBit) | twoBitValue;
        }
    };
}
