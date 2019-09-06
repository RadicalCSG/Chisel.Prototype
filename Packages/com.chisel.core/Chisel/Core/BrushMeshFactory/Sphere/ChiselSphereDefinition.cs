using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselSphereDefinition : IChiselGenerator
    {
        public const float              kMinSphereDiameter          = 0.01f;
        public const float              kDefaultRotation            = 0.0f;
        public const int                kDefaultHorizontalSegments  = 12;
        public const int                kDefaultVerticalSegments    = 12;
        public const bool               kDefaultGenerateFromCenter  = false;
        public static readonly Vector3  kDefaultDiameter            = Vector3.one;

        [DistanceValue] public Vector3	diameterXYZ;
        public float    offsetY;
        public bool     generateFromCenter;
        public float    rotation; // TODO: useless?
        public int	    horizontalSegments;
        public int	    verticalSegments;

        [NamedItems(overflow = "Side {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            diameterXYZ		    = kDefaultDiameter;
            offsetY             = 0;
            rotation		    = kDefaultRotation;
            horizontalSegments  = kDefaultHorizontalSegments;
            verticalSegments    = kDefaultVerticalSegments;
            generateFromCenter  = kDefaultGenerateFromCenter;
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            diameterXYZ.x = Mathf.Max(kMinSphereDiameter, Mathf.Abs(diameterXYZ.x));
            diameterXYZ.y = Mathf.Max(0,                  Mathf.Abs(diameterXYZ.y)) * (diameterXYZ.y < 0 ? -1 : 1);
            diameterXYZ.z = Mathf.Max(kMinSphereDiameter, Mathf.Abs(diameterXYZ.z));

            horizontalSegments = Mathf.Max(horizontalSegments, 3);
            verticalSegments	= Mathf.Max(verticalSegments, 2);
            
            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateSphere(ref brushContainer, ref this);
        }
    }
}