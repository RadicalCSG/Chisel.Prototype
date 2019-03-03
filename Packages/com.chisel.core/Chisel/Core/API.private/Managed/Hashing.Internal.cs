using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
	public static class Hashing
	{
		// TODO: completely convert from C++ or find new hashing solution

		const ulong PRIME64_1 = 11400714785074694791UL;
		const ulong PRIME64_2 = 14029467366897019727UL;
		const ulong PRIME64_3 = 1609587929392839161UL;
		const ulong PRIME64_4 = 9650029242287828579UL;
		const ulong PRIME64_5 = 2870177450012600261UL;

		static ulong XXH_rotl64(ulong x, int r) { return ((x << r) | (x >> (64 - r))); }

		static ulong XXH64_round(ulong acc, ulong input)
		{
			acc += input * PRIME64_2;
			acc = XXH_rotl64(acc, 31);
			acc *= PRIME64_1;
			return acc;
		}

		public static ulong XXH64_mergeRound(ulong acc, ulong val)
		{
			val = XXH64_round(0, val);
			acc ^= val;
			acc = acc * PRIME64_1 + PRIME64_4;
			return acc;
		}
	}
}
