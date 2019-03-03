using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;
using UnitySceneExtensions;

namespace Chisel.Components
{
	// TODO: add properties for SurfaceDescription/SurfaceAssets
	// TODO: beveled edges (for top and/or bottom)
	//			-> makes this a capsule
	[ExecuteInEditMode] 
	public sealed class CSGCylinder : CSGGeneratorComponent
	{
		public override string NodeTypeName { get { return "Cylinder"; } }

		public CSGCylinder() : base() {  }

		[SerializeField] CSGCylinderDefinition	top;
		[SerializeField] CSGCylinderDefinition  bottom;
		[SerializeField] [AngleValue] float     rotation		= 0;
		[SerializeField] bool                   isEllipsoid		= false;
		[SerializeField] CylinderShapeType      type			= CylinderShapeType.Cylinder;
		[SerializeField] uint                   smoothingGroup	= 0;
		[SerializeField] int					sides			= 16;
		[SerializeField] CSGSurfaceAsset[]		surfaceAssets;
		[SerializeField] SurfaceDescription[]	surfaceDescriptions;

		protected override void OnResetInternal()
		{
			top.height			= 1.0f;
			top.diameterX		= 1.0f;
			top.diameterZ		= 1.0f;
			bottom.height		= 0.0f;
			bottom.diameterX	= 1.0f;
			bottom.diameterZ	= 1.0f;
			rotation			= 0.0f;
			isEllipsoid			= false;
			sides				= 16;
			smoothingGroup		= 1;
			surfaceAssets		= null;
			surfaceDescriptions = null;
			base.OnResetInternal();
		}

		protected override void OnValidateInternal()
		{
			sides = Mathf.Max(3, sides);
			base.OnValidateInternal();
		}

		public CylinderShapeType Type
		{
			get { return type; }
			set
			{
				if (value == type)
					return;

				type = value;

				OnValidateInternal();
			}
		}

		public CSGCylinderDefinition Top	{ get { return top; } }
		public CSGCylinderDefinition Bottom	{ get { return bottom; } }

		public float Height
		{
			get { return top.height; }
			set
			{
				if (value == top.height)
					return;
				
				top.height = bottom.height + value;

				OnValidateInternal();
			}
		}

		public float TopHeight
		{
			get { return top.height; }
			set
			{
				if (value == top.height)
					return;
				
				top.height = value;

				OnValidateInternal();
			}
		}

		public float BottomHeight
		{
			get { return bottom.height; }
			set
			{
				if (value == bottom.height)
					return;
				
				bottom.height = value;

				OnValidateInternal();
			}
		}

		public float TopDiameterX
		{
			get { return top.diameterX; }
			set
			{
				if (value == top.diameterX)
					return;

				top.diameterX = value;
				if (!isEllipsoid)
					top.diameterZ = value;
				if (type == CylinderShapeType.Cylinder)
				{
					bottom.diameterX = value;
					if (!isEllipsoid)
						bottom.diameterZ = value;
				}

				OnValidateInternal();
			}
		}

		public float TopDiameterZ
		{
			get { return top.diameterZ; }
			set
			{
				if (value == top.diameterZ)
					return;

				top.diameterZ = value;
				if (!isEllipsoid)
					top.diameterX = value;
				if (type == CylinderShapeType.Cylinder)
				{
					bottom.diameterZ = value;
					if (!isEllipsoid)
						bottom.diameterX = value;
				}

				OnValidateInternal();
			}
		}
		
		public float Diameter
		{
			get { return bottom.diameterX; }
			set
			{
				if (value == bottom.diameterX)
					return;

				bottom.diameterX = value;
				top.diameterX = value;
				bottom.diameterZ = value;
				top.diameterZ = value;

				OnValidateInternal();
			}
		}
		
		public float DiameterX
		{
			get { return bottom.diameterX; }
			set
			{
				if (value == bottom.diameterX)
					return;

				bottom.diameterX = value;
				top.diameterX = value;
				if (!isEllipsoid)
				{
					bottom.diameterZ = value;
					top.diameterZ = value;
				}

				OnValidateInternal();
			}
		}
		
		public float DiameterZ
		{
			get { return bottom.diameterZ; }
			set
			{
				if (value == bottom.diameterZ)
					return;

				bottom.diameterZ = value;
				top.diameterZ = value;
				if (!isEllipsoid)
				{
					bottom.diameterX = value;
					top.diameterX = value;
				}

				OnValidateInternal();
			}
		}

		public float BottomDiameterX
		{
			get { return bottom.diameterX; }
			set
			{
				if (value == bottom.diameterX)
					return;

				bottom.diameterX = value;
				if (!isEllipsoid)
					bottom.diameterZ = value;
				if (type == CylinderShapeType.Cylinder)
				{
					top.diameterX = value;
					if (!isEllipsoid)
						top.diameterZ = value;
				}

				OnValidateInternal();
			}
		}

		public float BottomDiameterZ
		{
			get { return bottom.diameterZ; }
			set
			{
				if (value == bottom.diameterZ)
					return;

				bottom.diameterZ = value;
				if (!isEllipsoid)
					bottom.diameterX = value;
				if (type == CylinderShapeType.Cylinder)
				{
					top.diameterZ = value;
					if (!isEllipsoid)
						top.diameterX = value;
				}

				OnValidateInternal();
			}
		}

		public float Rotation
		{
			get { return rotation; }
			set
			{
				if (value == rotation)
					return;

				rotation = value;

				OnValidateInternal();
			}
		}
		
		public bool IsEllipsoid
		{
			get { return isEllipsoid; }
			set
			{
				if (value == isEllipsoid)
					return;
				
				isEllipsoid = value;

				OnValidateInternal();
			}
		}
		
		public uint SmoothingGroup
		{
			get { return smoothingGroup; }
			set
			{
				if (value == smoothingGroup)
					return;
				
				smoothingGroup = value;

				OnValidateInternal();
			}
		}

		public int Sides
		{
			get { return sides; }
			set
			{
				var newValue = value;
				if (newValue < 3)
					newValue = 3;
				if (newValue == sides)
					return;
				
				sides = newValue;

				OnValidateInternal();
			}
		}

		protected override void UpdateGeneratorInternal()
		{
			if (surfaceAssets == null ||
				surfaceAssets.Length != 3)
			{
				var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
				var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
				surfaceAssets = new CSGSurfaceAsset[3];
				for (int i = 0; i < 3; i++) // Note: sides share same material
					surfaceAssets[i] = CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
			}

			// TODO: handle existing surfaces better
			if (surfaceDescriptions == null ||
				surfaceDescriptions.Length != (2 + sides))
			{
				var surfaceFlags	= CSGDefaults.SurfaceFlags;
				surfaceDescriptions = new SurfaceDescription[2 + sides];

				UVMatrix uv0;
				// Top plane
				uv0 = UVMatrix.identity;
				uv0.U.w = 0.5f;
				uv0.V.w = 0.5f;
				surfaceDescriptions[0] = new SurfaceDescription { UV0 = uv0, surfaceFlags = surfaceFlags, smoothingGroup = 0 };
				
				// Bottom plane
				uv0 = UVMatrix.identity;
				uv0.U.w = 0.5f;
				uv0.V.w = 0.5f;
				surfaceDescriptions[1] = new SurfaceDescription { UV0 = uv0, surfaceFlags = surfaceFlags, smoothingGroup = 0 };


				float	radius		= top.diameterX * 0.5f;
				float	angle		= (360.0f / sides);
				float	sideLength	= (2 * Mathf.Sin((angle / 2.0f) * Mathf.Deg2Rad)) * radius;

				// Side planes
				for (int i = 2; i < 2 + sides; i++) 
				{
					uv0 = UVMatrix.identity;
					uv0.U.w = ((i - 2) + 0.5f) * sideLength;
					// TODO: align with bottom
					//uv0.V.w = 0.5f;
					surfaceDescriptions[i] = new SurfaceDescription { UV0 = uv0, surfaceFlags = surfaceFlags, smoothingGroup = smoothingGroup };
				}
			}

			// TODO: render all caps/lines using CSGRenderer ...

			var tempTop		= top;
			var tempBottom	= bottom;

			if (!isEllipsoid)
			{
				tempTop.diameterZ = tempTop.diameterX;
				tempBottom.diameterZ = tempBottom.diameterX;
			}

			switch (type)
			{
				case CylinderShapeType.Cylinder:		BrushMeshAssetFactory.GenerateCylinderAsset(brushMeshAsset, tempBottom, tempTop.height, rotation, sides, surfaceAssets, surfaceDescriptions); break;
				case CylinderShapeType.ConicalFrustum:	BrushMeshAssetFactory.GenerateConicalFrustumAsset(brushMeshAsset, tempBottom, tempTop, rotation, sides, surfaceAssets, surfaceDescriptions); break;
				case CylinderShapeType.Cone:			BrushMeshAssetFactory.GenerateConeAsset(brushMeshAsset, tempBottom, tempTop.height, rotation, sides, surfaceAssets, surfaceDescriptions); break;					
			}
		}
	}
}