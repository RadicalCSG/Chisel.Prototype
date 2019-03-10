using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chisel.Core
{
#if USE_MANAGED_CSG_IMPLEMENTATION
    internal enum IntersectionType
    {
        NoIntersection,
        Intersection,
        AInsideB,
        BInsideA,

        InvalidValue
    };

    internal class IntersectionLookup
    {
        const int bits = 2;
        public IntersectionLookup(int _length)
        {
            var size = ((_length * bits) + 31) / 32;
            if (size <= 0)
                this.twoBits = new UInt32[0];
            else
                this.twoBits = new UInt32[size];
        }

        UInt32[]  twoBits;

        public  void Clear()
        {
            var size = (((twoBits.Length * bits) + 31) / 32) * sizeof(UInt32);
            Array.Clear(twoBits, 0, twoBits.Length);
        }

        public IntersectionType GetUnsafe(int index) 
        {
            index <<= 1;
            var int32Index = index >> 5;	// divide by 32
            var bitIndex   = index & 31;	// remainder
            var twoBit     = ((UInt32)3) << bitIndex;

            return (IntersectionType) ((twoBits[int32Index] & twoBit) >> bitIndex);
        }

        public IntersectionType Get(int index) 
        {
            if ((UInt32) index > (UInt32)twoBits.Length) return (IntersectionType)0;
                
            index <<= 1;
            var int32Index = index >> 5;	// divide by 32
            var bitIndex   = index & 31;	// remainder
            var twoBit     = ((UInt32)3) << bitIndex;

            return (IntersectionType) ((twoBits[int32Index] & twoBit) >> bitIndex);
        }

        public bool Is(int index, IntersectionType value) 
        {
            if (index< 0 || (UInt32) index > (UInt32) twoBits.Length) return false;

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
            if (index< 0 || (UInt32) index > (UInt32)twoBits.Length) return;

            index <<= 1;
            var int32Index   = index >> 5;	// divide by 32
            var bitIndex     = index & 31;	// remainder
            var twoBit       = (UInt32)3 << bitIndex;
            var twoBitValue  = ((UInt32)value) << bitIndex;

            var originalInt32 = twoBits[int32Index];

            twoBits[int32Index] = (originalInt32 & ~twoBit) | twoBitValue;
        }
    };
#endif
}
