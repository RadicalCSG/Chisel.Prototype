using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeBrush
    {
#if USE_INTERNAL_IMPLEMENTATION
        private static bool					GenerateBrush(Int32 userID, out Int32 generatedNodeID)		{ return CSGManager.GenerateBrush(userID, out generatedNodeID); }
        private static CSGTreeBrushFlags	GetBrushFlags(Int32 brushNodeID)							{ return CSGManager.GetBrushFlags(brushNodeID); }
        private static bool					SetBrushFlags(Int32 brushNodeID, CSGTreeBrushFlags flags)	{ return CSGManager.SetBrushFlags(brushNodeID, flags); }
        private static Int32				GetBrushMeshID(Int32 brushNodeID)							{ return CSGManager.GetBrushMeshID(brushNodeID); }
        private static bool					SetBrushMeshID(Int32 brushNodeID, Int32 brushMeshID)		{ return CSGManager.SetBrushMeshID(brushNodeID, brushMeshID); }
        private static bool					GetBrushBounds(Int32 brushNodeID, ref Bounds bounds)		{ return CSGManager.GetBrushBounds(brushNodeID, ref bounds); }
#endif
    }
}