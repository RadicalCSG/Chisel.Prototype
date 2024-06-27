using UnityEngine;

namespace Chisel.Core
{
    public sealed class ToggleFlagsAttribute : PropertyAttribute
    {
        public ToggleFlagsAttribute(bool showPrefix = true, int includeFlags = ~0, int excludeFlags = 0)
        {
            if ((includeFlags & (int)PlacementFlags.HeightEqualsXZ) != 0 &&
                (excludeFlags & (int)PlacementFlags.HeightEqualsXZ) == 0)
            {
                includeFlags &= ~(int)PlacementFlags.HeightEqualsHalfXZ;
                excludeFlags |= (int)PlacementFlags.HeightEqualsHalfXZ;
            }

            this.ShowPrefix = showPrefix;
            this.IncludeFlags = includeFlags;
            this.ExcludeFlags = excludeFlags;
        }
        public readonly bool ShowPrefix;
        public readonly int IncludeFlags;
        public readonly int ExcludeFlags;
    }
}
