using System;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    partial struct CSGTreeBranch
    {
#if !USE_INTERNAL_IMPLEMENTATION
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool	GenerateBranch(Int32 userID, out Int32 generatedBranchNodeID);
#endif
    }
}