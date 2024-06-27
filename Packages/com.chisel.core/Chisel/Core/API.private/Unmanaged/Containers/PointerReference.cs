using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

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
