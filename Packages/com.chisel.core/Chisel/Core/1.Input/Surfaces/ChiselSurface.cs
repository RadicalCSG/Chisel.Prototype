using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

using UnityEngine;

namespace Chisel.Core
{
	// Encapsulated data so we can more easily make a propertydrawer for it
	//TODO: make a property drawer for it
	[Serializable]
	public struct ChiselMaterial
	{
        public const string kMaterialFieldName		 = nameof(material);
		public const string kSurfaceMetadataFieldName = nameof(surfaceMetadata);

		public Material				 material;
		public ChiselSurfaceMetadata surfaceMetadata;
	}

	/// <summary>Defines a surface on a <see cref="Chisel.Core.BrushMesh"/>. 
	/// Some of a surface is specific to a particular surface (for example: uvs),
	/// other parts are shared between surfaces (for example: the material).</summary>
	/// <seealso cref="Chisel.Core.ChiselBrushMaterial"/>
	/// <seealso cref="Chisel.Core.ChiselSurfaceArray"/>
	/// <seealso cref="Chisel.Core.SurfaceDetails"/>
	[Serializable]
    public sealed class ChiselSurface
	{
		public const string kChiselMaterialName = nameof(chiselMaterial);
		public const string kSurfaceDetailsName	= nameof(surfaceDetails);

		[SerializeField]
		internal ChiselMaterial chiselMaterial = new();
		public SurfaceDetails surfaceDetails = SurfaceDetails.Default;

		public bool HasMaterial { get { return chiselMaterial.material != null; } }

		public Material				   RenderMaterial   { get { return (chiselMaterial.material != null) ? chiselMaterial.material : ChiselDefaultMaterials.DefaultMaterial; } set { SetMaterial(value); } }
		public PhysicMaterial		   PhysicsMaterial  { get { return (chiselMaterial.surfaceMetadata != null) ? chiselMaterial.surfaceMetadata.physicsMaterial : ChiselDefaultMaterials.DefaultPhysicsMaterial; } }
		public SurfaceDestinationFlags DestinationFlags { get { return (chiselMaterial.surfaceMetadata != null) ? chiselMaterial.surfaceMetadata.destinationFlags : SurfaceDestinationFlags.Default; } }
		public SurfaceOutputFlags      OutputFlags      { get { return (chiselMaterial.surfaceMetadata != null) ? chiselMaterial.surfaceMetadata.outputFlags : SurfaceOutputFlags.Default; } }

		public void SetMaterial(Material material)
		{
			chiselMaterial.material = material;
			chiselMaterial.surfaceMetadata = (material == null) ? null : chiselMaterial.material.GetMetadataOfType<ChiselSurfaceMetadata>();
		}

		public static ChiselSurface Create(Material material, SurfaceDetails details) 
		{ 
			return new ChiselSurface
			{
				chiselMaterial = new ChiselMaterial
				{
					material        = material,
					surfaceMetadata = (material == null) ? null : material.GetMetadataOfType<ChiselSurfaceMetadata>(),
				},
				surfaceDetails	= details
			};
		}

		public static ChiselSurface Create(Material material)
		{
			return Create(material, SurfaceDetails.Default);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
				uint materialHashcode = (chiselMaterial.material != null) ? (uint)chiselMaterial.material.GetHashCode() : 0u;
				uint surfaceMetadataHashcode = (chiselMaterial.surfaceMetadata != null) ? (uint)chiselMaterial.surfaceMetadata.GetHashCode() : 0;

				uint hash = HashExtensions.Hash(ref surfaceDetails);				
                hash = math.hash(new uint3(hash, materialHashcode, surfaceMetadataHashcode));
                return (int)hash;
            }
        }
    }
}