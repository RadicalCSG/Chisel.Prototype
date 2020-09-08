using System;
using UnityEngine;

namespace Chisel.Core
{

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
