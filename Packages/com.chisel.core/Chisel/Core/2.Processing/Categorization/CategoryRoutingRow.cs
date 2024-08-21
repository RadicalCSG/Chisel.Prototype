#define HAVE_SELF_CATEGORIES
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    [System.Diagnostics.DebuggerTypeProxy(typeof(CategoryRoutingRow.DebuggerProxy))]
    [StructLayout(LayoutKind.Explicit)]
    readonly unsafe struct CategoryRoutingRow
    {
        internal sealed class DebuggerProxy
        {
#if HAVE_SELF_CATEGORIES
			public int inside;
			public int aligned;
			public int selfAligned;
			public int selfReverseAligned;
			public int reverseAligned;
			public int outside;
			public DebuggerProxy(CategoryRoutingRow v)
			{
				inside = (int)v.inside;
				aligned = (int)v.aligned;
				selfAligned = (int)v.selfAligned;
				selfReverseAligned = (int)v.selfReverseAligned;
				reverseAligned = (int)v.reverseAligned;
				outside = (int)v.outside;
			}
#else
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
#endif
		}

#if HAVE_SELF_CATEGORIES
		const byte Invalid            = (byte)255;
        const byte Inside             = (byte)CategoryIndex.Inside;
        const byte Aligned            = (byte)CategoryIndex.Aligned;
        const byte SelfAligned        = (byte)CategoryIndex.SelfAligned;
        const byte SelfReverseAligned = (byte)CategoryIndex.SelfReverseAligned;
        const byte ReverseAligned     = (byte)CategoryIndex.ReverseAligned;
        const byte Outside            = (byte)CategoryIndex.Outside;
        
        public readonly static CategoryRoutingRow Identity              = new(Inside,  Aligned, SelfAligned, SelfReverseAligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow AllInvalid            = new(Invalid, Invalid, Invalid, Invalid, Invalid, Invalid);
        public readonly static CategoryRoutingRow AllSelfAligned        = new(SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned, SelfAligned);
        public readonly static CategoryRoutingRow AllSelfReverseAligned = new(SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned, SelfReverseAligned);
        public readonly static CategoryRoutingRow AllOutside            = new(Outside, Outside, Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow AllInside             = new(Inside,  Inside, Inside, Inside, Inside, Inside);

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
        [FieldOffset(0)] public readonly byte inside;
        [FieldOffset(1)] public readonly byte aligned;
		[FieldOffset(2)] public readonly byte selfAligned;
		[FieldOffset(3)] public readonly byte selfReverseAligned;
		[FieldOffset(4)] public readonly byte reverseAligned;
        [FieldOffset(5)] public readonly byte outside;
#else
		const byte Invalid            = (byte)255;
        const byte Inside             = (byte)CategoryIndex.Inside;
        const byte Aligned            = (byte)CategoryIndex.Aligned;
        const byte ReverseAligned     = (byte)CategoryIndex.ReverseAligned;
        const byte Outside            = (byte)CategoryIndex.Outside;
        
        public readonly static CategoryRoutingRow Identity              = new(Inside,  Aligned, ReverseAligned, Outside);
        public readonly static CategoryRoutingRow AllInvalid            = new(Invalid, Invalid, Invalid, Invalid);
        public readonly static CategoryRoutingRow AllSelfAligned        = new(Aligned, Aligned, Aligned, Aligned);
        public readonly static CategoryRoutingRow AllSelfReverseAligned = new(ReverseAligned, ReverseAligned, ReverseAligned, ReverseAligned);
        public readonly static CategoryRoutingRow AllOutside            = new(Outside, Outside, Outside, Outside);
        public readonly static CategoryRoutingRow AllInside             = new(Inside,  Inside, Inside, Inside);

        public const int Length = (int)CategoryIndex.LastCategory + 1;

        // Is PolygonGroupIndex instead of int, but C# doesn't like that
        //[FieldOffset(0)] fixed byte destination[Length];
        [FieldOffset(0)] readonly uint destination;
        [FieldOffset(0)] public readonly byte inside;
        [FieldOffset(1)] public readonly byte aligned;
        [FieldOffset(2)] public readonly byte reverseAligned;
        [FieldOffset(3)] public readonly byte outside;
#endif

		#region Operation tables           
#if HAVE_SELF_CATEGORIES
            public static readonly byte[] kOperationTables = // NOTE: burst supports static readonly tables like this
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

            public const int OperationStride         = 6 * 6;
            public const int RowStride               = 6;
#else
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
#endif
#endregion

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(int operationIndex, CategoryIndex left, in CategoryRoutingRow right)
		{
#if HAVE_SELF_CATEGORIES
#if DEBUG_CATEGORIES
            destination = new IntArray();
#endif
			var operationOffset = operationIndex * OperationStride;

            // left = row, right = column
            var row = (byte)left;
			//this.destination        = 0;
			this.inside             = kOperationTables[operationOffset + (row * RowStride) + right.inside];
			this.aligned            = kOperationTables[operationOffset + (row * RowStride) + right.aligned];
			this.selfAligned        = kOperationTables[operationOffset + (row * RowStride) + right.selfAligned];
			this.selfReverseAligned = kOperationTables[operationOffset + (row * RowStride) + right.reverseAligned];
			this.reverseAligned     = kOperationTables[operationOffset + (row * RowStride) + right.selfReverseAligned];
			this.outside            = kOperationTables[operationOffset + (row * RowStride) + right.outside];

#else
			var operationOffset = operationIndex * OperationStride + ((int)left * RowStride);
            this.destination    = 0;
            this.inside         = kOperationTables[(int)(operationOffset + (int)right.inside)];
            this.aligned        = kOperationTables[(int)(operationOffset + (int)right.aligned)];
            this.reverseAligned = kOperationTables[(int)(operationOffset + (int)right.reverseAligned)];
            this.outside        = kOperationTables[(int)(operationOffset + (int)right.outside)];
#endif
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CategoryRoutingRow operator +(CategoryRoutingRow oldRow, int offset)
        {
#if HAVE_SELF_CATEGORIES
            return new CategoryRoutingRow
            (
                inside              : (byte)(oldRow.inside + offset),
                aligned             : (byte)(oldRow.aligned + offset),
                selfAligned         : (byte)(oldRow.selfAligned + offset),
                selfReverseAligned  : (byte)(oldRow.selfReverseAligned + offset),
                reverseAligned      : (byte)(oldRow.reverseAligned + offset),
                outside             : (byte)(oldRow.outside + offset)
            );
#else
            return new CategoryRoutingRow
            (
                inside          : (byte)(oldRow.inside + offset),
                aligned         : (byte)(oldRow.aligned + offset),
                reverseAligned  : (byte)(oldRow.reverseAligned + offset),
                outside         : (byte)(oldRow.outside + offset)
            );
#endif
		}

#if HAVE_SELF_CATEGORIES
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(byte inside, byte aligned, byte selfAligned, byte selfReverseAligned, byte reverseAligned, byte outside)
        {
            //this.destination        = 0;
            this.inside             = inside;
            this.aligned            = aligned;
			this.selfAligned        = selfAligned;
			this.selfReverseAligned = selfReverseAligned;
			this.reverseAligned     = reverseAligned;
            this.outside            = outside;
		}
#else
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(byte inside, byte aligned, byte reverseAligned, byte outside)
        {
            this.destination    = 0;
            this.inside         = inside;
            this.aligned        = aligned;
            this.reverseAligned = reverseAligned;
            this.outside        = outside;
		}
#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CategoryRoutingRow(byte value)
		{
#if HAVE_SELF_CATEGORIES
			//this.destination        = 0;
            this.inside             = (byte)value;
            this.aligned            = (byte)value;
			this.selfAligned        = (byte)value;
			this.selfReverseAligned = (byte)value;
			this.reverseAligned     = (byte)value;
            this.outside            = (byte)value;
#else
			this.destination    = 0;
            this.inside         = (byte)value;
            this.aligned        = (byte)value;
            this.reverseAligned = (byte)value;
            this.outside        = (byte)value;
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllTheSame()
		{
#if HAVE_SELF_CATEGORIES
			return inside == aligned &&
                   inside == selfAligned &&
				   inside == selfReverseAligned &&
                   inside == reverseAligned &&
				   inside == outside;
#else
			return inside == aligned &&
                   inside == reverseAligned &&
                   inside == outside;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllValue(int value)
		{
#if HAVE_SELF_CATEGORIES
			return (inside             == value &&
                    aligned            == value &&
					selfAligned        == value &&
					selfReverseAligned == value &&
					reverseAligned     == value &&
                    outside            == value);
#else
			return (inside          == value &&
                    aligned         == value &&
                    reverseAligned  == value &&
                    outside         == value);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CategoryRoutingRow other)
        {
#if HAVE_SELF_CATEGORIES
            return (inside             == other.inside &&
                    aligned            == other.aligned &&
                    selfAligned        == other.selfAligned &&
                    selfReverseAligned == other.selfReverseAligned &&
                    reverseAligned     == other.reverseAligned &&
                    outside            == other.outside);
#else
            return (inside          == other.inside &&
                    aligned         == other.aligned &&
                    reverseAligned  == other.reverseAligned &&
                    outside         == other.outside);
#endif
		}

		public byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                UnityEngine.Debug.Assert(index >= 0 && index < CategoryRoutingRow.Length);
#if HAVE_SELF_CATEGORIES
				switch(index)
                {
                    default:
                    case 0: return inside;
					case 1: return aligned;
					case 2: return selfAligned;
					case 3: return selfReverseAligned;
					case 4: return reverseAligned;
					case 5: return outside;
				}
#else
				return (byte)((destination << (index * 8)) & 255);
#endif
			}
		}
    }
}