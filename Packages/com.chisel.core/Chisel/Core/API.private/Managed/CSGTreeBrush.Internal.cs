using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeBrush
    {
        private static bool					GenerateBrush(Int32 userID, out Int32 generatedNodeID)		{ return CSGManager.GenerateBrush(userID, out generatedNodeID); }
    }
}