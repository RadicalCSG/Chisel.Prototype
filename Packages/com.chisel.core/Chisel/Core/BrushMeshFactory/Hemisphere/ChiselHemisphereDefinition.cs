using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselHemisphereDefinition : IChiselGenerator
    {
        public const float				kMinDiameter				= 0.01f;
        public const float              kDefaultRotation            = 0.0f;
        public const int				kDefaultHorizontalSegments  = 8;
        public const int				kDefaultVerticalSegments    = 8;
        public static readonly Vector3	kDefaultDiameter			= new Vector3(1.0f, 0.5f, 1.0f);

        [DistanceValue] public Vector3	diameterXYZ;
        public float                rotation; // TODO: useless?
        public int					horizontalSegments;
        public int					verticalSegments;

        [NamedItems("Bottom", overflow = "Side {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            diameterXYZ			= kDefaultDiameter;
            rotation			= kDefaultRotation;
            horizontalSegments	= kDefaultHorizontalSegments;
            verticalSegments	= kDefaultVerticalSegments;
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            diameterXYZ.x = Mathf.Max(kMinDiameter, Mathf.Abs(diameterXYZ.x));
            diameterXYZ.y = Mathf.Max(0,            Mathf.Abs(diameterXYZ.y)) * (diameterXYZ.y < 0 ? -1 : 1);
            diameterXYZ.z = Mathf.Max(kMinDiameter, Mathf.Abs(diameterXYZ.z));

            horizontalSegments	= Mathf.Max(horizontalSegments, 3);
            verticalSegments	= Mathf.Max(verticalSegments, 1);
            
            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            Profiler.BeginSample("GenerateHemisphere");
            try
            {
                return BrushMeshFactory.GenerateHemisphere(ref brushContainer, ref this);
            }
            finally
            {
                Profiler.EndSample();
            }
        }
    }
}