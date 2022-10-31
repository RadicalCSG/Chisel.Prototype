using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    unsafe struct PointerReference<T>
        where T : unmanaged
    {
        public PointerReference(ref T input) { ptr = (T*)UnsafeUtility.AddressOf(ref input); }
        [NativeDisableUnsafePtrRestriction, NoAlias] T* ptr;

        public ref T Value { get { return ref UnsafeUtility.AsRef<T>(ptr); } }
    }
}
