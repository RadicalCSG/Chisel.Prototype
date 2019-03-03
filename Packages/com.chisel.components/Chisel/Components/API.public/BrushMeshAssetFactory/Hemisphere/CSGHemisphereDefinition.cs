using System;
using Chisel.Assets;
using Chisel.Core;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;

namespace Chisel.Components
{
	[Serializable]
    public struct CSGHemisphereDefinition
	{
		public const float				kMinDiameter				= 0.01f;
		public const float              kDefaultRotation            = 0.0f;
		public const int				kDefaultHorizontalSegments  = 8;
		public const int				kVerticalHorizontalSegments = 8;
		public static readonly Vector3	kDefaultDiameter			= new Vector3(1.0f, 0.5f, 1.0f);

		[DistanceValue] public Vector3	diameterXYZ;
		public float                rotation; // TODO: useless?
		public int					horizontalSegments;
		public int					verticalSegments;
		
		public CSGSurfaceAsset[]	surfaceAssets;
		public SurfaceDescription[]	surfaceDescriptions;

		public void Reset()
		{
			diameterXYZ			= kDefaultDiameter;
			rotation			= kDefaultRotation;
			horizontalSegments	= kDefaultHorizontalSegments;
			verticalSegments	= kVerticalHorizontalSegments;
			surfaceAssets		= null;
			surfaceDescriptions = null;
		}

		public void Validate()
		{
			diameterXYZ.x = Mathf.Max(diameterXYZ.x, kMinDiameter);
			diameterXYZ.y = Mathf.Max(diameterXYZ.y, kMinDiameter);
			diameterXYZ.z = Mathf.Max(diameterXYZ.z, kMinDiameter);
			
			horizontalSegments	= Mathf.Max(horizontalSegments, 3);
			verticalSegments	= Mathf.Max(verticalSegments, 1);

			if (surfaceAssets == null ||
				surfaceAssets.Length != 6)
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
		}
	}
}