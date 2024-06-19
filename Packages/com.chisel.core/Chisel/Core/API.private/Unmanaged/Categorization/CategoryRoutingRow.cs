using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    [DebuggerTypeProxy(typeof(CategoryRoutingRow.DebuggerProxy))]
    [StructLayout(LayoutKind.Explicit)]
    readonly struct CategoryRoutingRow
    {
        internal sealed class DebuggerProxy
        {
            public int inside;
            public int aligned;
            public int reverseAligned;
            public int outside;
            public DebuggerProxy(CategoryRoutingRow v)
            {
                inside          = (int)v.inside;
                aligned         = (int)v.aligned;
                reverseAligned  = (int)v.reverseAligned;
                outside         = (int)v.outside;
            }
        }

        const byte Invalid            = (byte)255;
        const byte Inside             = (byte)CategoryIndex.Inside;
        const byte Aligned            = (byte)CategoryIndex.Aligned;
        const byte ReverseAligned     = (byte)CategoryIndex.ReverseAligned;
        const byte Outside            = (byte)CategoryIndex.Outside;
        
        public readonly static CategoryRoutingRow Identity              = new CategoryRoutingRow(Inside,  Aligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow AllInvalid            = new CategoryRoutingRow(Invalid, Invalid, Invalid, Invalid);
        public readonly static CategoryRoutingRow AllSelfAligned        = new CategoryRoutingRow(Aligned, Aligned, Aligned, Aligned);
        public readonly static CategoryRoutingRow AllSelfReverseAligned = new CategoryRoutingRow(ReverseAligned, ReverseAligned, ReverseAligned, ReverseAligned);
        public readonly static CategoryRoutingRow AllOutside            = new CategoryRoutingRow(Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow AllInside             = new CategoryRoutingRow(Inside,  Inside, Inside, Inside);

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
        //[FieldOffset(0)] fixed byte destination[Length];
        [FieldOffset(0)] readonly uint destination;
        [FieldOffset(0)] public readonly byte inside;
        [FieldOffset(1)] public readonly byte aligned;
        [FieldOffset(2)] public readonly byte reverseAligned;
        [FieldOffset(3)] public readonly byte outside;

        #region Operation tables            
        public readonly static byte[] kOperationTables = // NOTE: burst supports static readonly tables like this
        {
            // Regular Operation Tables
            // Additive set operation on polygons: output = (left-node || right-node)
            // 
            //  right node                                                              | Additive Operation
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
                Inside,           Inside,           Inside,           Inside            , // inside
                Inside,           Aligned,          Inside,           Aligned           , // aligned
                Inside,           Inside,           ReverseAligned,   ReverseAligned    , // reverse-aligned
                Inside,           Aligned,          ReverseAligned,   Outside           , // outside
            //},

            // Subtractive set operation on polygons: output = !(!left-node || right-node)
            //
            //  right node                                                              | Subtractive Operation
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
                Outside,          ReverseAligned,   Aligned,          Inside            , // inside
                Outside,          Outside,          Aligned,          Aligned           , // aligned
                Outside,          ReverseAligned,   Outside,          ReverseAligned    , // reverse-aligned
                Outside,          Outside,          Outside,          Outside           , // outside
            //},

            // Common set operation on polygons: output = !(!left-node || !right-node)
            //
            //  right node                                                              | Intersection Operation
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
                Inside,           Aligned,          ReverseAligned,   Outside           , // inside
                Aligned,          Aligned,          Outside,          Outside           , // aligned
	            ReverseAligned,   Outside,          ReverseAligned,   Outside           , // reverse-aligned
                Outside,          Outside,          Outside,          Outside           , // outside
            //},

	        //  right node                                                              |
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
	            Invalid,          Invalid,          Invalid,          Invalid           , // inside
                Invalid,          Invalid,          Invalid,          Invalid           , // aligned
                Invalid,          Invalid,          Invalid,          Invalid           , // reverse-aligned
                Invalid,          Invalid,          Invalid,          Invalid           , // outside
            //}
            
            // Remove Overlapping Tables
            // Additive set operation on polygons: output = (left-node || right-node)
            //
	        //  right node                                                              | Additive Operation
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
	            Inside,           Inside,           Inside,           Inside            , // inside
                Inside,           Inside,           Inside,           Aligned           , // aligned
                Inside,           Inside,           Inside,           ReverseAligned    , // reverse-aligned
                Inside,           Inside,           Inside,           Outside           , // outside
            //},

            // Subtractive set operation on polygons: output = !(!left-node || right-node)
            //
	        //  right node                                                              | Subtractive Operation
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
                Outside,          Outside,          Outside,          Inside            , // inside
                Outside,          Outside,          Outside,          Aligned           , // aligned
                Outside,          Outside,          Outside,          ReverseAligned    , // reverse-aligned
                Outside,          Outside,          Outside,          Outside           , // outside
            //}, 

            // Common set operation on polygons: output = !(!left-node || !right-node)
            //
	        //  right node                                                              | Subtractive Operation
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
	            Inside,           Outside,          Outside,          Outside           , // inside
                Aligned,          Outside,          Outside,          Outside           , // aligned
                ReverseAligned,   Outside,          Outside,          Outside           , // reverse-aligned
                Outside,          Outside,          Outside,          Outside           , // outside
            //},

	        //  right node                                                              |
            //  inside            aligned           reverse-aligned   outside           |     left-node       
            //-----------------------------------------------------------------------------------------------
	            Invalid,          Invalid,          Invalid,          Invalid           , // inside
                Invalid,          Invalid,          Invalid,          Invalid           , // aligned
                Invalid,          Invalid,          Invalid,          Invalid           , // reverse-aligned
                Invalid,          Invalid,          Invalid,          Invalid           , // outside
            //}
        };

        public const int RemoveOverlappingOffset = 4;
        public const int OperationStride         = 4 * 4;
        public const int RowStride               = 4;
        #endregion

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(int operationIndex, CategoryIndex left, in CategoryRoutingRow right)
        {
            var operationOffset = operationIndex * OperationStride + ((int)left * RowStride);
            this.destination    = 0;
            this.inside         = kOperationTables[(int)(operationOffset + (int)right.inside)];
            this.aligned        = kOperationTables[(int)(operationOffset + (int)right.aligned)];
            this.reverseAligned = kOperationTables[(int)(operationOffset + (int)right.reverseAligned)];
            this.outside        = kOperationTables[(int)(operationOffset + (int)right.outside)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CategoryRoutingRow operator +(CategoryRoutingRow oldRow, int offset)
        {
            return new CategoryRoutingRow
            (
                inside          : (byte)(oldRow.inside + offset),
                aligned         : (byte)(oldRow.aligned + offset),
                reverseAligned  : (byte)(oldRow.reverseAligned + offset),
                outside         : (byte)(oldRow.outside + offset)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(byte inside, byte aligned, byte reverseAligned, byte outside)
        {
            this.destination    = 0;
            this.inside         = inside;
            this.aligned        = aligned;
            this.reverseAligned = reverseAligned;
            this.outside        = outside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(byte value)
        {
            this.destination    = 0;
            this.inside         = (byte)value;
            this.aligned        = (byte)value;
            this.reverseAligned = (byte)value;
            this.outside        = (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllTheSame()
        {
            return inside           == aligned &&
                   aligned          == reverseAligned &&
                   reverseAligned   == outside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllValue(int value)
        {
            return (inside          == value &&
                    aligned         == value &&
                    reverseAligned  == value &&
                    outside         == value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CategoryRoutingRow other)
        {
            return (inside          == other.inside &&
                    aligned         == other.aligned &&
                    reverseAligned  == other.reverseAligned &&
                    outside         == other.outside);
        }

        public byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                UnityEngine.Debug.Assert(index >= 0 && index < CategoryRoutingRow.Length);
                return (byte)((destination << (index * 8)) & 255);
            }
        }
    }
}