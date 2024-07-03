using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    public static class HashExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetHashCode<T>(T[] array) where T : unmanaged
		{
			return Hash(array);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint GetHashCode<T>([NoAlias, ReadOnly] ref T value) where T : unmanaged
        {
            return Hash(ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetHashCode<T>([NoAlias, ReadOnly] ref BlobArray<T> value) where T : unmanaged
        {
            return Hash(ref value);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint Hash<T>(T[] array) where T : unmanaged
		{
			if (array == null)
				return 0;

			var length = array.Length;
			if (length == 0)
				return 0;

			fixed (void* ptr = &array[0])
			{
				return math.hash((byte*)ptr, length * System.Runtime.InteropServices.Marshal.SizeOf<T>());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint Hash<T>([NoAlias, ReadOnly] ref T input) where T : unmanaged
		{
			fixed (void* ptr = &input)
			{
				return math.hash((byte*)ptr, sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint Hash<T>([NoAlias, ReadOnly] ref BlobArray<T> input) where T : unmanaged
		{
			var ptr = input.GetUnsafePtr();
			if (ptr == null)
			{
				throw new System.NullReferenceException($"{nameof(input)} is null");
			}
			return math.hash((byte*)ptr, input.Length * sizeof(T));
		}
	}
}
