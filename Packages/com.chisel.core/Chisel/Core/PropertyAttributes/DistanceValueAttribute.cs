using UnityEngine;

namespace Chisel.Core
{
    public class DistanceValueAttribute : PropertyAttribute
    {
        public DistanceValueAttribute(UnitType type = UnitType.World) { this.Type = type; }
        public UnitType Type;
    }
}
