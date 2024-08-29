using System.Runtime.CompilerServices;

using Unity.Entities;

namespace Chisel.Core
{
    // This describes a chisel surface for use within job systems 
    // (which cannot handle Materials since they're managed classes)
	public struct InternalChiselSurface
    {
        public static readonly InternalChiselSurface Default = new()
        {
			details          = SurfaceDetails.Default,
			parameters       = SurfaceDestinationParameters.Empty,
			destinationFlags = SurfaceDestinationFlags.None,
			outputFlags      = SurfaceOutputFlags.Default
		};

		public SurfaceDetails               details;
		public SurfaceDestinationParameters parameters;
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceOutputFlags           outputFlags;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static InternalChiselSurface Convert(ChiselSurface surface)
		{
			return new InternalChiselSurface
            {
                details		     = surface.surfaceDetails,
                parameters       = new SurfaceDestinationParameters
				                   {
										parameter1 = ChiselMaterialManager.GetID(surface.RenderMaterial),
										parameter2 = ChiselMaterialManager.GetID(surface.PhysicsMaterial)
				                   },
				destinationFlags = surface.DestinationFlags,
				outputFlags	     = surface.OutputFlags,
            };
		}
	}

	public struct InternalChiselSurfaceArray
	{
		public BlobArray<InternalChiselSurface> surfaces;
	}
}