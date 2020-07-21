using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeBrush
    {
        private static bool					GenerateBrush(Int32 userID, out Int32 generatedNodeID)		{ return CSGManager.GenerateBrush(userID, out generatedNodeID); }
        private static CSGTreeBrushFlags	GetBrushFlags(Int32 brushNodeID)							{ return CSGManager.GetBrushFlags(brushNodeID); }
        private static bool					SetBrushFlags(Int32 brushNodeID, CSGTreeBrushFlags flags)	{ return CSGManager.SetBrushFlags(brushNodeID, flags); }
    }
}