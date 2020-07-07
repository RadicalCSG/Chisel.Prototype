#if DEBUG
//#define DEBUG_CATEGORIES // visual studio debugging bug work around
//#define HAVE_SELF_CATEGORIES
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public enum CategoryGroupIndex : short
    {
        First = 0,
        Invalid = 255
    }

    [DebuggerTypeProxy(typeof(CategoryRoutingRow.DebuggerProxy))]
    public unsafe struct CategoryRoutingRow
    {
#if HAVE_SELF_CATEGORIES
        internal sealed class DebuggerProxy
        {
            public int inside;
            public int aligned;
            public int selfAligned;
            public int selfReverseAligned;
            public int reverseAligned;
            public int outside;
            public DebuggerProxy(CategoryRoutingRow v)
            {
                inside = (int)v[0];
                aligned = (int)v[1];
                selfAligned = (int)v[2];
                selfReverseAligned = (int)v[3];
                reverseAligned = (int)v[4];
                outside = (int)v[5];
            }
        }
#else
        internal sealed class DebuggerProxy
        {
            public int inside;
            public int aligned;
            public int reverseAligned;
            public int outside;
            public DebuggerProxy(CategoryRoutingRow v)
            {
                inside = (int)v[0];
                aligned = (int)v[1];
                reverseAligned = (int)v[2];
                outside = (int)v[3];
            }
        }
#endif

#if HAVE_SELF_CATEGORIES
        const short Invalid            = (short)CategoryGroupIndex.Invalid;
        const short Inside             = (short)(CategoryGroupIndex)CategoryIndex.Inside;
        const short Aligned            = (short)(CategoryGroupIndex)CategoryIndex.Aligned;
        const short SelfAligned        = (short)(CategoryGroupIndex)CategoryIndex.SelfAligned;
        const short SelfReverseAligned = (short)(CategoryGroupIndex)CategoryIndex.SelfReverseAligned;
        const short ReverseAligned     = (short)(CategoryGroupIndex)CategoryIndex.ReverseAligned;
        const short Outside            = (short)(CategoryGroupIndex)CategoryIndex.Outside;

        public static readonly CategoryRoutingRow invalid               = new CategoryRoutingRow(Invalid, Invalid, Invalid, Invalid, Invalid, Invalid);
        public static readonly CategoryRoutingRow identity              = new CategoryRoutingRow(Inside, Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow selfAligned           = new CategoryRoutingRow(SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned);
        public readonly static CategoryRoutingRow selfReverseAligned    = new CategoryRoutingRow(SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned);
        public readonly static CategoryRoutingRow outside               = new CategoryRoutingRow(Outside, Outside, Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow inside                = new CategoryRoutingRow(Inside, Inside, Inside, Inside, Inside, Inside);
#else
        const short Invalid            = (short)CategoryGroupIndex.Invalid;
        const short Inside             = (short)(CategoryGroupIndex)CategoryIndex.Inside;
        const short Aligned            = (short)(CategoryGroupIndex)CategoryIndex.Aligned;
        const short ReverseAligned     = (short)(CategoryGroupIndex)CategoryIndex.ReverseAligned;
        const short Outside            = (short)(CategoryGroupIndex)CategoryIndex.Outside;

        public static readonly CategoryRoutingRow invalid               = new CategoryRoutingRow(Invalid, Invalid, Invalid, Invalid);
        public static readonly CategoryRoutingRow identity              = new CategoryRoutingRow(Inside, Aligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow selfAligned           = new CategoryRoutingRow(Aligned, Aligned, Aligned, Aligned);
        public readonly static CategoryRoutingRow selfReverseAligned    = new CategoryRoutingRow(ReverseAligned, ReverseAligned, ReverseAligned, ReverseAligned);
        public readonly static CategoryRoutingRow outside               = new CategoryRoutingRow(Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow inside                = new CategoryRoutingRow(Inside, Inside, Inside, Inside);
#endif

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
#if HAVE_SELF_CATEGORIES
#if !DEBUG_CATEGORIES
        fixed int	destination[Length];
#else
        // visual studio debugging bug work around
        struct IntArray
        {
            int A; int B; int C; int D; int E; int F;
            public unsafe int this[int index]
            {
                get
                {
                    fixed (int* ptr = &A)
                    {
                        return ptr[index];
                    }
                }
                set
                {
                    fixed (int* ptr = &A)
                    {
                        ptr[index] = value;
                    }
                }
            }

        }
        IntArray   destination;
#endif
#else
        fixed short destination[Length];
#endif

        #region Operation tables            
#if HAVE_SELF_CATEGORIES
            public static readonly short[] kOperationTables = // NOTE: burst supports static readonly tables like this
            {
                // Additive set operation on polygons: output = (left-node || right-node)
                // Defines final output from combination of categorization of left and right node
                //new CategoryRoutingRow[] // Additive Operation
                //{
	                //             	        right node                                                                                                              |
	                //                                                              self                  self                                                      |
	                //                      inside                aligned           aligned               reverse-aligned       reverse-aligned   outside           |     left-node       
	                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	                Inside,               Inside,           Inside,               Inside,               Inside,           Inside            , // inside
	                Inside,               Aligned,          SelfAligned,          Inside,               Inside,           Aligned           , // other-aligned
	                Inside,               Aligned,          SelfAligned,          Inside,               Inside,           SelfAligned       , // self-aligned
	                Inside,               Inside,           Inside,               SelfReverseAligned,   ReverseAligned,   SelfReverseAligned, // self-reverse-aligned
	                Inside,               Inside,           Inside,               SelfReverseAligned,   ReverseAligned,   ReverseAligned    , // other-reverse-aligned
	                Inside,               Aligned,          SelfAligned,          SelfReverseAligned,   ReverseAligned,   Outside           , // outside
                //},

                // Subtractive set operation on polygons: output = !(!left-node || right-node)
                // Defines final output from combination of categorization of left and right node
                //new CategoryRoutingRow[] // Subtractive Operation
                //{
	                //             	        right node                                                                                                              |
	                //                                                              self                  self                                                      |
	                //                      inside                aligned           aligned               reverse-aligned       reverse-aligned   outside           |     left-node       
	                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	                Outside,              ReverseAligned,   SelfReverseAligned,   SelfAligned,          Aligned,          Inside            , // inside
	                Outside,              Aligned,          Inside,               SelfAligned,          Aligned,          Aligned           , // other-aligned
	                Outside,              Aligned,          Inside,               SelfAligned,          Aligned,          SelfAligned       , // self-aligned
	                Outside,              ReverseAligned,   SelfReverseAligned,   Outside,              Outside,          SelfReverseAligned, // self-reverse-aligned
	                Outside,              ReverseAligned,   SelfReverseAligned,   Outside,              Outside,          ReverseAligned    , // other-reverse-aligned
	                Outside,              Outside,          Outside,              Outside,              Outside,          Outside           , // outside
                //},

                // Common set operation on polygons: output = !(!left-node || !right-node)
                // Defines final output from combination of categorization of left and right node
                //new CategoryRoutingRow[] // Intersection Operation
                //{
	                //             	        right node                                                                                                              |
	                //                                                              self                  self                                                      |
	                //                      inside                aligned           aligned               reverse-aligned       reverse-aligned   outside           |     left-node       
	                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	                Inside,               Aligned,          SelfAligned,          SelfReverseAligned,   ReverseAligned,   Outside           , // inside
	                Aligned,              Aligned,          SelfAligned,          Outside,              Outside,          Outside           , // other-aligned
	                SelfAligned,          Aligned,          SelfAligned,          Outside,              Outside,          Outside           , // self-aligned
	                SelfReverseAligned,   Outside,          Outside,              SelfReverseAligned,   ReverseAligned,   Outside           , // self-reverse-aligned
	                ReverseAligned,       Outside,          Outside,              SelfReverseAligned,   ReverseAligned,   Outside           , // other-reverse-aligned
	                Outside,              Outside,          Outside,              Outside,              Outside,          Outside           , // outside
                //},

                // Additive set operation on polygons: output = (left-node || right-node)
                // Defines final output from combination of categorization of left and right node
                //new CategoryRoutingRow[] // AdditiveKeepInside Operation
                //{
	                //             	        right node                                                                                                              |
	                //                                                              self                  self                                                      |
	                //                      inside                aligned           aligned               reverse-aligned       reverse-aligned   outside           |     left-node       
	                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------
	                Inside,               Inside,           Inside,               Inside,               Inside,           Inside            , // inside
	                Inside,               Aligned,          SelfAligned,          Inside,               Inside,           Aligned           , // other-aligned
	                Inside,               Aligned,          SelfAligned,          Inside,               Inside,           SelfAligned       , // self-aligned
	                Inside,               Inside,           Inside,               SelfReverseAligned,   ReverseAligned,   SelfReverseAligned, // self-reverse-aligned
	                Inside,               Inside,           Inside,               SelfReverseAligned,   ReverseAligned,   ReverseAligned    , // other-reverse-aligned
	                Inside,               Aligned,          SelfAligned,          SelfReverseAligned,   ReverseAligned,   Outside           , // outside
                //}
            };
            public const int OperationStride    = 6 * 6;
            public const int RowStride          = 6;
#else
        public readonly static short[] kOperationTables = // NOTE: burst supports static readonly tables like this
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
#endif
        //}
#endregion

        
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(int operationIndex, CategoryIndex left, in CategoryRoutingRow right)
        {
#if HAVE_SELF_CATEGORIES
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            var operationOffset = operationIndex * OperationStride;
            for (int i = 0; i < Length; i++)
            {
                var row     = (int)left;
                var column  = (int)right[i];
                destination[(int)i] = kOperationTables[operationOffset + (row * RowStride) + column];
            }
#else
            unchecked
            {
                var operationOffset = operationIndex * OperationStride + ((int)left * RowStride);
                destination[0] = kOperationTables[(int)(operationOffset + (int)right.destination[0])];
                destination[1] = kOperationTables[(int)(operationOffset + (int)right.destination[1])];
                destination[2] = kOperationTables[(int)(operationOffset + (int)right.destination[2])];
                destination[3] = kOperationTables[(int)(operationOffset + (int)right.destination[3])];
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CategoryRoutingRow operator +(CategoryRoutingRow a, int offset)
        {
#if HAVE_SELF_CATEGORIES
            var newRow = new CategoryRoutingRow();
#if DEBUG_CATEGORIES
            newRow.destination = new IntArray();
#endif
            for (int i = 0; i < Length; i++)
                newRow.destination[(int)i] = (int)a[i] + offset;
            return newRow;
#else
            unchecked
            {
                var newRow = new CategoryRoutingRow();
                newRow.destination[0] = (short)(a.destination[0] + offset);
                newRow.destination[1] = (short)(a.destination[1] + offset);
                newRow.destination[2] = (short)(a.destination[2] + offset);
                newRow.destination[3] = (short)(a.destination[3] + offset);
                return newRow;
            }
#endif
        }

#if HAVE_SELF_CATEGORIES
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(CategoryGroupIndex inside, CategoryGroupIndex aligned, CategoryGroupIndex selfAligned, CategoryGroupIndex selfReverseAligned, CategoryGroupIndex reverseAligned, CategoryGroupIndex outside)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (short)inside;
            destination[(int)CategoryIndex.Aligned]             = (short)aligned;
            destination[(int)CategoryIndex.SelfAligned]         = (short)selfAligned;
            destination[(int)CategoryIndex.SelfReverseAligned]  = (short)selfReverseAligned;
            destination[(int)CategoryIndex.ReverseAligned]      = (short)reverseAligned;
            destination[(int)CategoryIndex.Outside]             = (short)outside;
        }
        
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(CategoryGroupIndex value)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (short)value;
            destination[(int)CategoryIndex.Aligned]             = (short)value;
            destination[(int)CategoryIndex.SelfAligned]         = (short)value;
            destination[(int)CategoryIndex.SelfReverseAligned]  = (short)value;
            destination[(int)CategoryIndex.ReverseAligned]      = (short)value;
            destination[(int)CategoryIndex.Outside]             = (short)value;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        CategoryRoutingRow(short inside, short aligned, short reverseAligned, short outside)
        {
#if HAVE_SELF_CATEGORIES
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = inside;
            destination[(int)CategoryIndex.Aligned]             = aligned;
            destination[(int)CategoryIndex.ReverseAligned]      = reverseAligned;
            destination[(int)CategoryIndex.Outside]             = outside;
#else
            unchecked
            {
                destination[0] = inside;
                destination[1] = aligned;
                destination[2] = reverseAligned;
                destination[3] = outside;
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(CategoryGroupIndex inside, CategoryGroupIndex aligned, CategoryGroupIndex reverseAligned, CategoryGroupIndex outside)
        {
#if HAVE_SELF_CATEGORIES
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (short)inside;
            destination[(int)CategoryIndex.Aligned]             = (short)aligned;
            destination[(int)CategoryIndex.ReverseAligned]      = (short)reverseAligned;
            destination[(int)CategoryIndex.Outside]             = (short)outside;
#else
            unchecked
            {
                destination[0] = (short)inside;
                destination[1] = (short)aligned;
                destination[2] = (short)reverseAligned;
                destination[3] = (short)outside;
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(CategoryGroupIndex value)
        {
#if HAVE_SELF_CATEGORIES
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (short)value;
            destination[(int)CategoryIndex.Aligned]             = (short)value;
            destination[(int)CategoryIndex.ReverseAligned]      = (short)value;
            destination[(int)CategoryIndex.Outside]             = (short)value;
#else
            unchecked
            {
                destination[0] = (short)value;
                destination[1] = (short)value;
                destination[2] = (short)value;
                destination[3] = (short)value;
            }
#endif
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllTheSame()
        {
#if HAVE_SELF_CATEGORIES
            for (var i = 1; i < Length; i++) { if (destination[i - 1] != destination[i]) return false; }
            return true;
#else
            return destination[0] == destination[1] &&
                   destination[1] == destination[2] &&
                   destination[2] == destination[3];
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllValue(int value)
        {
#if HAVE_SELF_CATEGORIES
            for (var i = 0; i < Length; i++) { if (destination[i] != value) return false; }
            return true;
#else
            return (destination[0] == value &&
                    destination[1] == value &&
                    destination[2] == value &&
                    destination[3] == value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CategoryRoutingRow other)
        {
#if HAVE_SELF_CATEGORIES
            for (var i = 0; i < Length; i++) { if (other.destination[i] != destination[i]) return false; }
            return true;
#else
            return (destination[0] == other.destination[0] &&
                    destination[1] == other.destination[1] &&
                    destination[2] == other.destination[2] &&
                    destination[3] == other.destination[3]);
#endif
        }

        public CategoryGroupIndex this[CategoryIndex index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { unchecked { return (CategoryGroupIndex)destination[(int)index]; } }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { unchecked { destination[(int)index] = (short)value; } }
        }

        public CategoryGroupIndex this[CategoryGroupIndex index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { unchecked { return (CategoryGroupIndex)destination[(int)index]; } }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { unchecked { destination[(int)index] = (short)value; } }
        }

        public CategoryGroupIndex this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { unchecked { return (CategoryGroupIndex)destination[index]; } }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { unchecked { destination[index] = (short)value; } }
        }
    }
}