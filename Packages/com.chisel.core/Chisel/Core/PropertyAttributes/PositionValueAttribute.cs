using UnityEngine;

namespace Chisel.Core
{
    public class PositionValueAttribute : PropertyAttribute
    {
        public PositionValueAttribute(UnitType type = UnitType.World) { this.Type = type; }
        public UnitType Type;
    }
}
