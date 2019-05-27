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
    public struct CSGCapsuleDefinition
    {
        public const float	kMinDiameter				= 0.01f;

        public const float	kDefaultHeight				= 1.0f;
        public const float	kDefaultHemisphereHeight	= 0.25f;
        public const float	kDefaultDiameterX			= 1.0f;
        public const float	kDefaultDiameterZ			= 1.0f;
        public const float  kDefaultRotation            = 0.0f;
        public const int	kDefaultSides				= 8;
        public const int	kDefaultTopSegments			= 4;
        public const int	kDefaultBottomSegments		= 4;
        public const float  kHeightEpsilon              = 0.001f;
        
        public float                height;
        public float                topHeight;
        public float                bottomHeight;
        public float                offsetY;

        public float                diameterX;
        public float                diameterZ;
        public float                rotation;

        public int                  sides;
        public int                  topSegments;
        public int                  bottomSegments;
        
        public ChiselBrushMaterial[] brushMaterials;
        public SurfaceDescription[]	 surfaceDescriptions;

        public bool					haveRoundedTop		{ get { return topSegments > 0 && topHeight > kHeightEpsilon; } }
        public bool					haveRoundedBottom	{ get { return bottomSegments > 0 && bottomHeight > kHeightEpsilon; } }
        public bool					haveCylinder		{ get { return cylinderHeight > kHeightEpsilon; } }
        public float				cylinderHeight		{ get { return Mathf.Abs(height - (bottomHeight + topHeight)); } }


        public int					bottomRingCount		{ get { return haveRoundedBottom ? bottomSegments : 1; } }
        public int					topRingCount		{ get { return haveRoundedTop    ? topSegments    : 1; } }
        public int					ringCount			{ get { return bottomRingCount + topRingCount - (haveCylinder ? 0 : 1); } }
        
        public int					segments
        {
            get
            {
                return Mathf.Max(1, ringCount);
            }
        }


        // TODO: store somewhere else
        public int					extraVertexCount	{ get { return ((haveRoundedTop) ? 1 : 0) + ((haveRoundedBottom) ? 1 : 0); } }
        public int					bottomVertex		{ get { return (0); } }
        public int					topVertex			{ get { return (haveRoundedBottom) ? 1 : 0; } }

        
        public int					vertexCount			{ get { return (sides * ringCount) + extraVertexCount; } }

        public int					bottomRing			{ get { return (haveRoundedBottom) ? (ringCount - bottomSegments) : ringCount - 1; } }
        public int					topRing				{ get { return (haveRoundedTop   ) ? (topSegments - 1) : 0; } }
        
        public float				topOffset			{ get { if (height < 0) return -topHeight; return height - topHeight; } }

        public float				bottomOffset		{ get { if (height < 0) return height + bottomHeight; return bottomHeight; } }
        
        public int					topVertexOffset		{ get { return extraVertexCount + ((topRingCount - 1) * sides); } }
        public int					bottomVertexOffset	{ get { return extraVertexCount + ((ringCount - bottomRingCount) * sides); } }

        public void Reset()
        {
            height				= kDefaultHeight;
            topHeight			= kDefaultHemisphereHeight;
            bottomHeight		= kDefaultHemisphereHeight;
            offsetY             = 0;
            diameterX			= kDefaultDiameterX;
            diameterZ			= kDefaultDiameterZ;
            rotation			= kDefaultRotation;
            
            sides				= kDefaultSides;
            topSegments			= kDefaultTopSegments;
            bottomSegments		= kDefaultBottomSegments;

            brushMaterials		= null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            topHeight			= Mathf.Max(topHeight, 0);
            bottomHeight		= Mathf.Max(bottomHeight, 0);
            height				= Mathf.Max(topHeight + bottomHeight, Mathf.Abs(height)) * (height < 0 ? -1 : 1);

            diameterX			= Mathf.Max(Mathf.Abs(diameterX), kMinDiameter);
            diameterZ			= Mathf.Max(Mathf.Abs(diameterZ), kMinDiameter);
            
            topSegments			= Mathf.Max(topSegments, 0);
            bottomSegments		= Mathf.Max(bottomSegments, 0);
            sides				= Mathf.Max(sides, 3);
            
            var minBrushMaterials = 2 + sides;
            if (brushMaterials == null ||
                brushMaterials.Length != minBrushMaterials )
            {
                var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new ChiselBrushMaterial[minBrushMaterials ];
                for (int a = 0; a < minBrushMaterials ; a++)
                    brushMaterials[a] = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
            }

            var minSurfaceDescriptions = 2 + sides;
            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != minSurfaceDescriptions)
            {
                // TODO: make this independent on plane position somehow
                var surfaceFlags	= CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[minSurfaceDescriptions];
                for (int s = 0; s < minSurfaceDescriptions; s++)
                    surfaceDescriptions[s] = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 };
            }
        }
    }
}