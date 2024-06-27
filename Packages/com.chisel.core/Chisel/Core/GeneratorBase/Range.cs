using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    public struct Range
    {
        public int start;
        public int end;
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return end - start; }
        }

        public int Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return start + ((end - start) / 2); }
        }
    }
}