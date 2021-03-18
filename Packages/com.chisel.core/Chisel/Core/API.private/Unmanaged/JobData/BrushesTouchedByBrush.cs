using System;
using Unity.Entities;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    struct BrushIntersectWith
    {
        public int              brushNodeOrder1;
        public IntersectionType type;
    }
    
    struct BrushPair : System.IEquatable<BrushPair>, 
                       System.IComparable<BrushPair>, 
                       System.Collections.Generic.IEqualityComparer<BrushPair>, 
                       System.Collections.Generic.IComparer<BrushPair>
    {
        public int              brushNodeOrder0;
        public int              brushNodeOrder1;
        public IntersectionType type;

        public void Flip()
        {
            if      (type == IntersectionType.AInsideB) type = IntersectionType.BInsideA;
            else if (type == IntersectionType.BInsideA) type = IntersectionType.AInsideB;
            { var t = brushNodeOrder0; brushNodeOrder0 = brushNodeOrder1; brushNodeOrder1 = t; }
        }

        #region Equals
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is BrushPair))
                return false;

            var other = (BrushPair)obj;
            return Equals(other);
        }

        public bool Equals(BrushPair x, BrushPair y) { return x.Equals(y); }

        public bool Equals(BrushPair other)
        {
            return ((brushNodeOrder0 == other.brushNodeOrder0) && 
                    (brushNodeOrder1 == other.brushNodeOrder1));
        }
        #endregion

        #region Compare
        public int Compare(BrushPair x, BrushPair y) { return x.CompareTo(y); }

        public int CompareTo(BrushPair other)
        {
            if (brushNodeOrder0 < other.brushNodeOrder0)
                return -1;
            if (brushNodeOrder0 > other.brushNodeOrder0)
                return 1;
            if (brushNodeOrder1 < other.brushNodeOrder1)
                return -1;
            if (brushNodeOrder1 > other.brushNodeOrder1)
                return 1;
            if (type < other.type)
                return -1;
            if (type > other.type)
                return 1;
            return 0;
        }
        #endregion

        #region GetHashCode
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public int GetHashCode(BrushPair obj)
        {
            return ((ulong)obj.brushNodeOrder0 + ((ulong)obj.brushNodeOrder1 << 32)).GetHashCode();
        }
        #endregion
    }

    struct BrushPair2 : System.IEquatable<BrushPair2>,
                        System.IComparable<BrushPair2>,
                        System.Collections.Generic.IEqualityComparer<BrushPair2>,
                        System.Collections.Generic.IComparer<BrushPair2>
    {
        public IndexOrder       brushIndexOrder0;
        public IndexOrder       brushIndexOrder1;
        public IntersectionType type;

        public void Flip()
        {
            if      (type == IntersectionType.AInsideB) type = IntersectionType.BInsideA;
            else if (type == IntersectionType.BInsideA) type = IntersectionType.AInsideB;
            { var t = brushIndexOrder0; brushIndexOrder0 = brushIndexOrder1; brushIndexOrder1 = t; }
        }

        #region Equals
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is BrushPair2))
                return false;

            var other = (BrushPair2)obj;
            return Equals(other);
        }

        public bool Equals(BrushPair2 x, BrushPair2 y) { return x.Equals(y); }

        public bool Equals(BrushPair2 other)
        {
            return ((brushIndexOrder0.nodeOrder == other.brushIndexOrder0.nodeOrder) && 
                    (brushIndexOrder1.nodeOrder == other.brushIndexOrder1.nodeOrder));
        }
        #endregion

        #region Compare
        public int Compare(BrushPair2 x, BrushPair2 y) { return x.CompareTo(y); }

        public int CompareTo(BrushPair2 other)
        {
            if (brushIndexOrder0.nodeOrder < other.brushIndexOrder0.nodeOrder)
                return -1;
            if (brushIndexOrder0.nodeOrder > other.brushIndexOrder0.nodeOrder)
                return 1;
            if (brushIndexOrder1.nodeOrder < other.brushIndexOrder1.nodeOrder)
                return -1;
            if (brushIndexOrder1.nodeOrder > other.brushIndexOrder1.nodeOrder)
                return 1;
            if (type < other.type)
                return -1;
            if (type > other.type)
                return 1;
            return 0;
        }
        #endregion

        #region GetHashCode
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public int GetHashCode(BrushPair2 obj)
        {
            return ((ulong)obj.brushIndexOrder0.nodeOrder + ((ulong)obj.brushIndexOrder1.nodeOrder << 32)).GetHashCode();
        }
        #endregion
    }

    enum IntersectionType : byte
    {
        NoIntersection,
        Intersection,
        AInsideB,
        BInsideA,

        InvalidValue
    };
    
    // Note: Stored in BlobAsset at runtime/editor-time
    struct SurfaceInfo
    {
        public ushort               basePlaneIndex;
        public CategoryGroupIndex   interiorCategory;
    }

    struct IndexSurfaceInfo
    {
        public IndexOrder           brushIndexOrder;
        public ushort               basePlaneIndex;
        public CategoryGroupIndex   interiorCategory;
    }

    struct BrushIntersectionLoop
    {
        public IndexOrder           indexOrder0;
        public IndexOrder           indexOrder1;
        public SurfaceInfo          surfaceInfo;
        public int                  loopVertexIndex;
        public int                  loopVertexCount;
    }

    struct BrushesTouchedByBrush
    {
        public BlobArray<BrushIntersection> brushIntersections;
        public BlobArray<uint>              intersectionBits;
        public int BitCount;
        public int BitOffset;

        public IntersectionType Get(CompactNodeID nodeID)
        {
            var idValue = nodeID.value;
            idValue -= BitOffset;
            if (idValue < 0 || idValue >= BitCount)
            {
                //Debug.Assert(false);
                return IntersectionType.InvalidValue;
            }

            idValue <<= 1;
            var int32Index  = idValue >> 5;	// divide by 32
            var bitIndex    = idValue & 31;	// remainder
            var twoBit      = ((uint)3) << bitIndex;

            var bitShifted  = (uint)intersectionBits[int32Index] & (uint)twoBit;
            var value       = (IntersectionType)((uint)bitShifted >> (int)bitIndex);
            Debug.Assert(value != IntersectionType.InvalidValue);
            return value;
        }
    }
}
