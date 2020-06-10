#if DEBUG
//#define DEBUG_CATEGORIES // visual studio debugging bug work around
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    public enum CategoryGroupIndex : short
    {
        First = 0,
        Invalid = -1
    }

    public unsafe struct CategoryRoutingRow
    {
#if HAVE_SELF_CATEGORIES
        const CategoryGroupIndex Invalid            = CategoryGroupIndex.Invalid;
        const CategoryGroupIndex Inside             = (CategoryGroupIndex)CategoryIndex.Inside;
        const CategoryGroupIndex Aligned            = (CategoryGroupIndex)CategoryIndex.Aligned;
        const CategoryGroupIndex SelfAligned        = (CategoryGroupIndex)CategoryIndex.SelfAligned;
        const CategoryGroupIndex SelfReverseAligned = (CategoryGroupIndex)CategoryIndex.SelfReverseAligned;
        const CategoryGroupIndex ReverseAligned     = (CategoryGroupIndex)CategoryIndex.ReverseAligned;
        const CategoryGroupIndex Outside            = (CategoryGroupIndex)CategoryIndex.Outside;

        public static readonly CategoryRoutingRow invalid               = new CategoryRoutingRow(Invalid, Invalid, Invalid, Invalid, Invalid, Invalid);
        public static readonly CategoryRoutingRow identity              = new CategoryRoutingRow(Inside, Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow selfAligned           = new CategoryRoutingRow(SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned);
        public readonly static CategoryRoutingRow selfReverseAligned    = new CategoryRoutingRow(SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned);
        public readonly static CategoryRoutingRow outside               = new CategoryRoutingRow(Outside, Outside, Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow inside                = new CategoryRoutingRow(Inside, Inside, Inside, Inside, Inside, Inside);
#else
        const CategoryGroupIndex Invalid            = CategoryGroupIndex.Invalid;
        const CategoryGroupIndex Inside             = (CategoryGroupIndex)CategoryIndex.Inside;
        const CategoryGroupIndex Aligned            = (CategoryGroupIndex)CategoryIndex.Aligned;
        const CategoryGroupIndex ReverseAligned     = (CategoryGroupIndex)CategoryIndex.ReverseAligned;
        const CategoryGroupIndex Outside            = (CategoryGroupIndex)CategoryIndex.Outside;

        public static readonly CategoryRoutingRow invalid               = new CategoryRoutingRow(Invalid, Invalid, Invalid, Invalid);
        public static readonly CategoryRoutingRow identity              = new CategoryRoutingRow(Inside, Aligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow selfAligned           = new CategoryRoutingRow(Aligned, Aligned, Aligned, Aligned);
        public readonly static CategoryRoutingRow selfReverseAligned    = new CategoryRoutingRow(ReverseAligned, ReverseAligned, ReverseAligned, ReverseAligned);
        public readonly static CategoryRoutingRow outside               = new CategoryRoutingRow(Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow inside                = new CategoryRoutingRow(Inside, Inside, Inside, Inside);
#endif

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
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

        #region Operation tables
        //static class OperationTables
        //{
#if HAVE_SELF_CATEGORIES
            //const CategoryGroupIndex Inside             = (CategoryGroupIndex)CategoryIndex.Inside;
            //const CategoryGroupIndex Aligned            = (CategoryGroupIndex)CategoryIndex.Aligned;
            //const CategoryGroupIndex SelfAligned        = (CategoryGroupIndex)CategoryIndex.SelfAligned;
            //const CategoryGroupIndex SelfReverseAligned = (CategoryGroupIndex)CategoryIndex.SelfReverseAligned;
            //const CategoryGroupIndex ReverseAligned     = (CategoryGroupIndex)CategoryIndex.ReverseAligned;
            //const CategoryGroupIndex Outside            = (CategoryGroupIndex)CategoryIndex.Outside;

            // TODO: Burst might support reading this directly now?
            public static readonly CategoryGroupIndex[] OperationTables = 
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
            public readonly static CategoryGroupIndex[] OperationTables =
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

        public CategoryRoutingRow(int operationIndex, CategoryIndex left, in CategoryRoutingRow right)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            var operationOffset = operationIndex * OperationStride;
            for (int i = 0; i < Length; i++)
            {
                var row     = (int)left;
                var column  = (int)right[i];
                destination[(int)i] = (int)OperationTables[operationOffset + (row * RowStride) + column];
            }
        }

        public static CategoryRoutingRow operator +(CategoryRoutingRow a, int offset)
        {
            var newRow = new CategoryRoutingRow();
#if DEBUG_CATEGORIES
            newRow.destination = new IntArray();
#endif
            for (int i = 0; i < Length; i++)
                newRow.destination[(int)i] = (int)a[i] + offset;

            return newRow;
        }

#if HAVE_SELF_CATEGORIES
        public CategoryRoutingRow(CategoryGroupIndex inside, CategoryGroupIndex aligned, CategoryGroupIndex selfAligned, CategoryGroupIndex selfReverseAligned, CategoryGroupIndex reverseAligned, CategoryGroupIndex outside)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (int)inside;
            destination[(int)CategoryIndex.Aligned]             = (int)aligned;
            destination[(int)CategoryIndex.SelfAligned]         = (int)selfAligned;
            destination[(int)CategoryIndex.SelfReverseAligned]  = (int)selfReverseAligned;
            destination[(int)CategoryIndex.ReverseAligned]      = (int)reverseAligned;
            destination[(int)CategoryIndex.Outside]             = (int)outside;
        }

        public CategoryRoutingRow(CategoryGroupIndex value)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (int)value;
            destination[(int)CategoryIndex.Aligned]             = (int)value;
            destination[(int)CategoryIndex.SelfAligned]         = (int)value;
            destination[(int)CategoryIndex.SelfReverseAligned]  = (int)value;
            destination[(int)CategoryIndex.ReverseAligned]      = (int)value;
            destination[(int)CategoryIndex.Outside]             = (int)value;
        }
#else
        public CategoryRoutingRow(CategoryGroupIndex inside, CategoryGroupIndex aligned, CategoryGroupIndex reverseAligned, CategoryGroupIndex outside)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (int)inside;
            destination[(int)CategoryIndex.Aligned]             = (int)aligned;
            destination[(int)CategoryIndex.ReverseAligned]      = (int)reverseAligned;
            destination[(int)CategoryIndex.Outside]             = (int)outside;
        }

        public CategoryRoutingRow(CategoryGroupIndex value)
        {
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
            destination[(int)CategoryIndex.Inside]              = (int)value;
            destination[(int)CategoryIndex.Aligned]             = (int)value;
            destination[(int)CategoryIndex.ReverseAligned]      = (int)value;
            destination[(int)CategoryIndex.Outside]             = (int)value;
        }
#endif

        public bool AreAllTheSame()
        {
            for (var i = 1; i < Length; i++) { if (destination[i - 1] != destination[i]) return false; }
            return true;
        }

        public bool AreAllValue(int value)
        {
            for (var i = 0; i < Length; i++) { if (destination[i] != value) return false; }
            return true;
        }

        public bool Equals(CategoryRoutingRow other)
        {
            for (var i = 0; i < Length; i++) { if (other.destination[i] != destination[i]) return false; }
            return true;
        }

        public CategoryGroupIndex this[CategoryIndex index]
        {
            get { return (CategoryGroupIndex)destination[(int)index]; }
            set { destination[(int)index] = (int)value; }
        }

        public CategoryGroupIndex this[CategoryGroupIndex index]
        {
            get { return (CategoryGroupIndex)destination[(int)index]; }
            set { destination[(int)index] = (int)value; }
        }

        public CategoryGroupIndex this[int index]
        {
            get { return (CategoryGroupIndex)destination[index]; }
            set { destination[index] = (int)value; }
        }

        public override string ToString()
        {
            return ToString(lastNode: false);
        }

        public string ToString(bool lastNode)
        {
#if HAVE_SELF_CATEGORIES
            var inside              = (int)destination[(int)CategoryIndex.Inside];
            var aligned             = (int)destination[(int)CategoryIndex.Aligned];
            var selfAligned         = (int)destination[(int)CategoryIndex.SelfAligned];
            var selfReverseAligned  = (int)destination[(int)CategoryIndex.SelfReverseAligned];
            var reverseAligned      = (int)destination[(int)CategoryIndex.ReverseAligned];
            var outside             = (int)destination[(int)CategoryIndex.Outside];

            if (lastNode)
                return $"({(CategoryIndex)inside}, {(CategoryIndex)aligned}, {(CategoryIndex)selfAligned}, {(CategoryIndex)selfReverseAligned}, {(CategoryIndex)reverseAligned}, {(CategoryIndex)outside})";
            else
                return $"({(int)inside,3}, {(int)aligned}, {(int)selfAligned}, {(int)selfReverseAligned}, {(int)reverseAligned}, {(int)outside})";
#else
            var inside              = (int)destination[(int)CategoryIndex.Inside];
            var aligned             = (int)destination[(int)CategoryIndex.Aligned];
            var reverseAligned      = (int)destination[(int)CategoryIndex.ReverseAligned];
            var outside             = (int)destination[(int)CategoryIndex.Outside];
            
            if (lastNode)
                return $"({(CategoryIndex)inside}, {(CategoryIndex)aligned}, {(CategoryIndex)reverseAligned}, {(CategoryIndex)outside})";
            else
                return $"({(int)inside}, {(int)aligned}, {(int)reverseAligned}, {(int)outside})";
#endif
        }
    }
}