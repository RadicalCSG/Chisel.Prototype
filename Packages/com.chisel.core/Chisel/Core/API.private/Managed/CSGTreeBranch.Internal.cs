﻿using System;

namespace Chisel.Core
{
    partial struct CSGTreeBranch
    {
#if USE_MANAGED_CSG_IMPLEMENTATION
        private static bool	GenerateBranch(Int32 userID, out Int32 generatedBranchNodeID) { return CSGManager.GenerateBranch(userID, out generatedBranchNodeID); }
#endif
    }
}