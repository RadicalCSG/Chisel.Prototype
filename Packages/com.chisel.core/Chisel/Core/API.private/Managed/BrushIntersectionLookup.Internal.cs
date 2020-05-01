using System;
using Unity.Collections;

namespace Chisel.Core
{
    internal struct BrushIntersectionLookup : IDisposable
    {
        const int bits = 2;

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

        public void Dispose()
        {
            if (twoBits.IsCreated)
                twoBits.Dispose();
        }

        public NativeArray<UInt32> twoBits;
        public readonly int Offset;
        public readonly int Length;

        public void Clear()
        {
            twoBits.ClearValues();
        }

        public IntersectionType Get(int index)
        {
            index -= Offset;
            if (index < 0 || index >= Length)
                return IntersectionType.InvalidValue;
                
            index <<= 1;
            var int32Index = index >> 5;	// divide by 32
            var bitIndex   = index & 31;	// remainder
            var twoBit     = ((UInt32)3) << bitIndex;

            return (IntersectionType) ((twoBits[int32Index] & twoBit) >> bitIndex);
        }

        public bool Is(int index, IntersectionType value)
        {
            index -= Offset;
            if (index < 0 || index >= Length)
				return false;

            index <<= 1;
            var int32Index   = index >> 5;	// divide by 32
            var bitIndex     = index & 31;	// remainder
            var twoBit       = ((UInt32)3) << bitIndex;
            var twoBitValue  = ((UInt32)value) << bitIndex;

            var originalInt32 = twoBits[int32Index];
                
            return (originalInt32 & twoBit) == twoBitValue;
        }

        public void Set(int index, IntersectionType value)
        {
            index -= Offset;
            if (index < 0 || index >= Length)
                return;

            index <<= 1;
            var int32Index   = index >> 5;	// divide by 32
            var bitIndex     = index & 31;	// remainder
            var twoBit       = (UInt32)3 << bitIndex;
            var twoBitValue  = ((UInt32)value) << bitIndex;

            var originalInt32 = twoBits[int32Index];

            twoBits[int32Index] = (originalInt32 & ~twoBit) | twoBitValue;
        }
    };
}
