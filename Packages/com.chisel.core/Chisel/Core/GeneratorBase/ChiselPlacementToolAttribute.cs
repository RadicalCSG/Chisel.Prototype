using System;
using UnityEngine;

namespace Chisel.Core
{
    /// Used in combination with <see cref="Chisel.Core.ChiselBoundsPlacementTool>/<see cref="Chisel.Core.ChiselShapePlacementTool>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ChiselPlacementToolAttribute : Attribute
    {
        public ChiselPlacementToolAttribute(string name, string group)
        {
            this.ToolName = name;
            this.Group = group;
        }
        public readonly string ToolName;
        public readonly string Group;
    }
}
