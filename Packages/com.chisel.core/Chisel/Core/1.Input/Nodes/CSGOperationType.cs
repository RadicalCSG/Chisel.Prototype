using System;

namespace Chisel.Core
{
    /// <summary>Defines how the CSG operation is performed on the intersection between <see cref="Chisel.Core.CSGTreeBrush"/>es and/or <see cref="Chisel.Core.CSGTreeBranch"/>es.</summary>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    /// <seealso cref="Chisel.Core.CSGTreeBranch"/>
    [Serializable]
    public enum CSGOperationType : byte
    {
        /// <summary>The given <see cref="Chisel.Core.CSGTreeBrush"/> or <see cref="Chisel.Core.CSGTreeBranch"/> is added to the <see cref="Chisel.Core.CSGTree"/> and removes all the geometry inside it.</summary>
        Additive = 0,

        /// <summary>The given <see cref="Chisel.Core.CSGTreeBrush"/> or <see cref="Chisel.Core.CSGTreeBranch"/> removes all the geometry that are inside it.</summary>
        Subtractive = 1,

        /// <summary>The given <see cref="Chisel.Core.CSGTreeBrush"/> or <see cref="Chisel.Core.CSGTreeBranch"/> removes all the geometry that is outside it.</summary>
        Intersecting = 2,

        /// <summary>The given <see cref="Chisel.Core.CSGTreeBrush"/> or <see cref="Chisel.Core.CSGTreeBranch"/> is added to the <see cref="Chisel.Core.CSGTree"/>.</summary>
        Copy = 3,

        /// <summary>Invalid value.</summary>
        Invalid = 4
    }
}
