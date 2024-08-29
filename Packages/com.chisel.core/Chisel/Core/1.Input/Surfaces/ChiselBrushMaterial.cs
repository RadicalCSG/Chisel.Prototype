using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
#if false
    // TODO: replace this with Material + ChiselSurfaceMetadata
    [Serializable]
    public sealed class ChiselBrushMaterial
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kDestinationFlagsFieldName = nameof(destinationFlags);
        public const string kOutputFlagsName           = nameof(outputFlags);
        public const string kRenderMaterialFieldName   = nameof(renderMaterial);
        public const string kPhysicsMaterialFieldName  = nameof(physicsMaterial);

        [SerializeField] public SurfaceDestinationFlags destinationFlags = SurfaceDestinationFlags.Default;
        [SerializeField] public SurfaceOutputFlags      outputFlags      = SurfaceOutputFlags.Default;

        [SerializeField] public Material       renderMaterial;
        [SerializeField] public PhysicMaterial physicsMaterial;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked { return (int)math.hash(new uint4(
                (uint)destinationFlags,   (uint)outputFlags,
                (uint)ChiselMaterialManager.GetID(physicsMaterial), 
                (uint)ChiselMaterialManager.GetID(renderMaterial))); }
        }
    }
#endif
}
