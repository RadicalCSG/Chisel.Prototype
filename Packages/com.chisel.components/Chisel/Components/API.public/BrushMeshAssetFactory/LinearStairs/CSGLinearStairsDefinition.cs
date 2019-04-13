using System;
using System.Linq;
using System.Collections.Generic;
using Chisel.Assets;
using Chisel.Core;
using Bounds  = UnityEngine.Bounds;
using Mathf   = UnityEngine.Mathf;
using Vector3 = UnityEngine.Vector3;
using UnitySceneExtensions;

namespace Chisel.Components
{
	// https://www.archdaily.com/892647/how-to-make-calculations-for-staircase-designs
	// https://inspectapedia.com/Stairs/2024s.jpg
	// https://landarchbim.com/2014/11/18/stair-nosing-treads-and-stringers/
	// https://en.wikipedia.org/wiki/Stairs
	[Serializable]
    public struct CSGLinearStairsDefinition
	{
		public enum SurfaceSides
		{
			Top,
			Bottom,
			Left,
			Right,
			Forward,
			Back,
			Tread,
			Step,

			TotalSides
		}

		public const float	kMinStepHeight			= 0.01f;
		public const float	kMinStepDepth			= 0.01f;
		public const float  kMinRiserDepth          = 0.01f;
		public const float  kMinSideWidth			= 0.01f;
		public const float	kMinWidth				= 0.0001f;

		public const float	kDefaultStepHeight		= 0.20f;
		public const float	kDefaultStepDepth		= 0.20f;
		public const float	kDefaultTreadHeight     = 0.02f;
		public const float	kDefaultNosingDepth     = 0.02f; 
		public const float	kDefaultNosingWidth     = 0.01f;

		public const float	kDefaultWidth			= 1;
		public const float	kDefaultHeight			= 1;
		public const float	kDefaultDepth			= 1;

		public const float	kDefaultPlateauHeight	= 0;

		public const float  kDefaultRiserDepth      = 0.03f;
		public const float  kDefaultSideDepth		= 0.0f;
		public const float  kDefaultSideWidth		= 0.03f;
		public const float  kDefaultSideHeight      = 0.03f;

		// TODO: add all spiral stairs improvements to linear stairs
		// TODO: make placement of stairs works properly

		[DistanceValue] public float	stepHeight;
		[DistanceValue] public float	stepDepth;
		[DistanceValue] public float	plateauHeight;
		[DistanceValue] public float	treadHeight;
		[DistanceValue] public float	nosingDepth;
		[DistanceValue] public float	nosingWidth;
		[DistanceValue] public float	riserDepth;
		[DistanceValue] public float	sideDepth;
		[DistanceValue] public float	sideWidth;
		[DistanceValue] public float	sideHeight;
		public Bounds					bounds;
		public StairsRiserType			riserType;
		public StairsSideType           leftSide;
		public StairsSideType           rightSide;
		
		[NonSerialized]
		public CSGSurfaceAsset[]		surfaceAssets;
		public SurfaceDescription[]		surfaceDescriptions;
		
		public CSGSurfaceAsset			topSurface;
		public CSGSurfaceAsset			bottomSurface;
		public CSGSurfaceAsset			leftSurface;
		public CSGSurfaceAsset			rightSurface;
		public CSGSurfaceAsset			forwardSurface;
		public CSGSurfaceAsset			backSurface;
		public CSGSurfaceAsset			treadSurface;
		public CSGSurfaceAsset			stepSurface;

		
		public float	width  { get { return bounds.size.x; } set { var size = bounds.size; size.x = value; bounds.size = size; } }
		public float	height { get { return bounds.size.y; } set { var size = bounds.size; size.y = value; bounds.size = size; } }
		public float	depth  { get { return bounds.size.z; } set { var size = bounds.size; size.z = value; bounds.size = size; } }
		public Vector3	size   { get { return bounds.size;   } set { bounds.size = value; } }

		public int StepCount
		{
			get
			{
				const float kSmudgeValue = 0.0001f;
				return Mathf.Max(1,
						  Mathf.FloorToInt((Mathf.Abs(height) - plateauHeight + kSmudgeValue) / stepHeight));
			}
		}

		public float StepDepthOffset
		{
			get { return Mathf.Max(0, Mathf.Abs(depth) - (StepCount * stepDepth)); }
		}

		public void Reset()
		{
			surfaceAssets		= null;
			surfaceDescriptions = null;

			// TODO: set defaults using attributes?
			stepHeight		= kDefaultStepHeight;
			stepDepth		= kDefaultStepDepth;
			treadHeight		= kDefaultTreadHeight;
			nosingDepth		= kDefaultNosingDepth;
			nosingWidth		= kDefaultNosingWidth;

			width			= kDefaultWidth;
			height			= kDefaultHeight;
			depth			= kDefaultDepth;

			plateauHeight	= kDefaultPlateauHeight;

			riserType		= StairsRiserType.ThinRiser;
			leftSide		= StairsSideType.None;
			rightSide		= StairsSideType.None;
			riserDepth		= kDefaultRiserDepth;
			sideDepth		= kDefaultSideDepth;
			sideWidth		= kDefaultSideWidth;
			sideHeight		= kDefaultSideHeight;
		}

		public void Validate()
		{
			if (surfaceAssets == null ||
				surfaceDescriptions.Length != (int)SurfaceSides.TotalSides)
			{
				var defaultRenderMaterial  = CSGMaterialManager.DefaultWallMaterial;
				var defaultPhysicsMaterial = CSGMaterialManager.DefaultPhysicsMaterial;
				surfaceAssets = new CSGSurfaceAsset[(int)SurfaceSides.TotalSides];

				surfaceAssets[(int)SurfaceSides.Top    ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultFloorMaterial, defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Bottom ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultFloorMaterial, defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Left   ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Right  ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Forward] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Back   ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultWallMaterial,  defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Tread  ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultTreadMaterial, defaultPhysicsMaterial);
				surfaceAssets[(int)SurfaceSides.Step   ] = CSGSurfaceAsset.CreateInstance(CSGMaterialManager.DefaultStepMaterial,  defaultPhysicsMaterial);

				for (int i = 0; i < surfaceAssets.Length; i++)
				{
					if (surfaceAssets[i] == null)
						surfaceAssets[i] = CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
				}
				
				topSurface		= surfaceAssets[(int)SurfaceSides.Top    ];
				bottomSurface	= surfaceAssets[(int)SurfaceSides.Bottom ];
				leftSurface		= surfaceAssets[(int)SurfaceSides.Left   ];
				rightSurface	= surfaceAssets[(int)SurfaceSides.Right  ];
				forwardSurface	= surfaceAssets[(int)SurfaceSides.Forward];
				backSurface		= surfaceAssets[(int)SurfaceSides.Back   ];
				treadSurface	= surfaceAssets[(int)SurfaceSides.Tread  ];
				stepSurface		= surfaceAssets[(int)SurfaceSides.Step   ];
			}

			if (surfaceDescriptions == null ||
				surfaceDescriptions.Length != 6)
			{
				var surfaceFlags = CSGDefaults.SurfaceFlags;
				surfaceDescriptions = new SurfaceDescription[6];
				for (int i = 0; i < 6; i++)
				{
					surfaceDescriptions[i] = new SurfaceDescription { surfaceFlags = surfaceFlags, UV0 = UVMatrix.centered };
				}
			}


			stepHeight		= Mathf.Max(kMinStepHeight, stepHeight);
			stepDepth		= Mathf.Clamp(stepDepth, kMinStepDepth, Mathf.Abs(depth));			
			treadHeight		= Mathf.Max(0, treadHeight);
			nosingDepth		= Mathf.Max(0, nosingDepth);
			nosingWidth		= Mathf.Max(0, nosingWidth);

			width			= Mathf.Max(kMinWidth,  Mathf.Abs(width)) * (width < 0 ? -1 : 1);
			depth			= Mathf.Max(stepDepth,  Mathf.Abs(depth)) * (depth < 0 ? -1 : 1); 

			riserDepth		= Mathf.Max(kMinRiserDepth, riserDepth);
			sideDepth		= Mathf.Max(0, sideDepth);
			sideWidth		= Mathf.Max(kMinSideWidth, sideWidth);
			sideHeight		= Mathf.Max(0, sideHeight);

            var absHeight   = Mathf.Max(stepHeight, Mathf.Abs(height));
			var maxPlateauHeight = absHeight - stepHeight;

			plateauHeight		= Mathf.Clamp(plateauHeight, 0, maxPlateauHeight);

			var totalSteps		= Mathf.Max(1, Mathf.FloorToInt((absHeight - plateauHeight) / stepHeight));
			var totalStepHeight = totalSteps * stepHeight;

			plateauHeight		= Mathf.Max(0, absHeight - totalStepHeight);
			stepDepth			= Mathf.Clamp(stepDepth, kMinStepDepth, Mathf.Abs(depth) / totalSteps);
		}
	}
}