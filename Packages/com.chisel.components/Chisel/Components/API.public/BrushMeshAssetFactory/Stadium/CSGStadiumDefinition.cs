using System;
using Chisel.Assets;
using Chisel.Core;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;

namespace Chisel.Components
{
    [Serializable]
    public struct CSGStadiumDefinition
    {
        const float         kNoCenterEpsilon            = 0.0001f;

        public const float	kMinDiameter				= 0.01f;
        public const float	kMinLength					= 0.01f;
        public const float	kMinHeight					= 0.01f;
        
        public const float	kDefaultHeight				= 1.0f;
        public const float	kDefaultLength				= 1.0f;
        public const float	kDefaultTopLength			= 0.25f;
        public const float	kDefaultBottomLength		= 0.25f;
        public const float	kDefaultDiameter			= 1.0f;
        
        public const int	kDefaultTopSides			= 4;
        public const int	SidesVertices				= 4;
        
        public float                diameter;
        
        public float                height;
        public float                length;
        public float                topLength;
        public float                bottomLength;
        
        // TODO: better naming
        public int                  topSides;
        public int                  bottomSides;
        
        public int					sides				{ get { return (haveCenter ? 2 : 0) + Mathf.Max(topSides, 1) + Mathf.Max(bottomSides, 1); } }
        public int					firstTopSide		{ get { return 0; } }
        public int					lastTopSide			{ get { return Mathf.Max(topSides, 1); } }
        public int					firstBottomSide		{ get { return lastTopSide + 1; } }
        public int					lastBottomSide		{ get { return sides - 1; } }

        public bool					haveRoundedTop		{ get { return (topLength    > 0) && (topSides    > 1); } }
        public bool					haveRoundedBottom	{ get { return (bottomLength > 0) && (bottomSides > 1); } }
        public bool					haveCenter			{ get { return (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= kNoCenterEpsilon; } }
        
        public CSGSurfaceAsset[]	surfaceAssets;
        public SurfaceDescription[]	surfaceDescriptions;

        public void Reset()
        {
            diameter			= kDefaultDiameter;

            height				= kDefaultHeight;

            length				= kDefaultLength;
            topLength			= kDefaultTopLength;
            bottomLength		= kDefaultBottomLength;
            
            topSides			= kDefaultTopSides;
            bottomSides			= SidesVertices;

            surfaceAssets		= null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            topLength			= Mathf.Max(topLength,    0);
            bottomLength		= Mathf.Max(bottomLength, 0);
            length				= Mathf.Max(Mathf.Abs(length), (haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0));
            length				= Mathf.Max(Mathf.Abs(length), kMinLength);
            
            height				= Mathf.Max(Mathf.Abs(height), kMinHeight);
            diameter			= Mathf.Max(Mathf.Abs(diameter), kMinDiameter);
            
            topSides			= Mathf.Max(topSides,	 1);
            bottomSides			= Mathf.Max(bottomSides, 1);

            var sides			= 2 + Mathf.Max(topSides,1) + Mathf.Max(bottomSides,1);
            int kMinSurfaceAssets = 2 + sides;
            if (surfaceAssets == null ||
                surfaceAssets.Length != kMinSurfaceAssets)
            {
                var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
                surfaceAssets = new CSGSurfaceAsset[kMinSurfaceAssets];
                for (int a = 0; a < kMinSurfaceAssets; a++)
                    surfaceAssets[a] = CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
            }

            int kMinSurfaceDescriptions = 2 + sides;
            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != kMinSurfaceDescriptions)
            {
                // TODO: make this independent on plane position somehow
                var surfaceFlags	= CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[kMinSurfaceDescriptions];
                for (int s = 0; s < kMinSurfaceDescriptions; s++)
                    surfaceDescriptions[s] = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 };
            }
        }
    }
}