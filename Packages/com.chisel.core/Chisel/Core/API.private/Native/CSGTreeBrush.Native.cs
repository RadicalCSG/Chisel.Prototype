using System;
using System.Runtime.InteropServices;
using Vector3 = UnityEngine.Vector3;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Bounds = UnityEngine.Bounds;

namespace Chisel.Core
{
    partial struct CSGTreeBrush
    {
#if !USE_MANAGED_CSG_IMPLEMENTATION
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool	GenerateBrush(Int32 userID, out Int32 generatedNodeID);
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern CSGTreeBrushFlags GetBrushFlags(Int32 brushNodeID);
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool	SetBrushFlags(Int32 brushNodeID, CSGTreeBrushFlags flags);
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern Int32	GetBrushMeshID(Int32 brushNodeID);
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool	SetBrushMeshID(Int32 brushNodeID, Int32 brushMeshID);
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool GetBrushBounds(Int32 brushNodeID, ref AABB bounds);
#endif
    }
}