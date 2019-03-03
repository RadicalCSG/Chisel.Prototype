using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;

namespace Chisel.Components
{
	// TODO: add properties for SurfaceDescription/SurfaceAssets
	// TODO: beveled edges
	[ExecuteInEditMode]
	public sealed class CSGBox : CSGGeneratorComponent
	{
		public override string NodeTypeName { get { return "Box"; } }

		public CSGBox() : base() {  }
		
		[SerializeField] Bounds					bounds				= new Bounds(Vector3.zero, Vector3.one);
		[SerializeField] CSGSurfaceAsset[]		surfaceAssets;
		[SerializeField] SurfaceDescription[]	surfaceDescriptions;
		
		public Bounds Bounds
		{
			get { return bounds; }
			set
			{
				if (value == bounds)
					return;

				bounds = value;

				OnValidateInternal();
			}
		}

		protected override void OnResetInternal()
		{
			bounds				= new Bounds(Vector3.zero, Vector3.one);
			surfaceAssets		= null;
			surfaceDescriptions = null;
			base.OnResetInternal();
		}

		protected override void UpdateGeneratorInternal()
		{
			if (surfaceAssets == null)
			{
				var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
				var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
				surfaceAssets = new CSGSurfaceAsset[6]
				{
					CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
					CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
					CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
					CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
					CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
					CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial)
				};
			}

			if (surfaceDescriptions == null ||
				surfaceDescriptions.Length != 6)
			{
				// TODO: make this independent on plane position somehow
				var surfaceFlags	= CSGDefaults.SurfaceFlags;
				surfaceDescriptions = new SurfaceDescription[6]
				{
					new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
					new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
					new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
					new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
					new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
					new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }
				};
			}

            if (BoundsExtensions.IsValid(bounds.min, bounds.max))
                BrushMeshAssetFactory.GenerateBoxAsset(brushMeshAsset, bounds, surfaceAssets, surfaceDescriptions);
            else
                brushMeshAsset.Clear();
        }
	}
}