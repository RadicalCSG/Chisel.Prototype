using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselRevolvedShapeDefinition : IChiselGenerator// TODO: make this a struct
    {
        public const int				kDefaultCurveSegments	= 8;
        public const int                kDefaultRevolveSegments = 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D				shape;
        public int					curveSegments;
        public int					revolveSegments;
        public float				startAngle;
        public float				totalAngle;
        
        public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            // TODO: create constants
            shape				= kDefaultShape;
            startAngle			= 0.0f;
            totalAngle			= 360.0f;
            curveSegments		= kDefaultCurveSegments;
            revolveSegments		= kDefaultRevolveSegments;
            
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            curveSegments		= Mathf.Max(curveSegments, 2);
            revolveSegments		= Mathf.Max(revolveSegments, 1);

            totalAngle			= Mathf.Clamp(totalAngle, 1, 360); // TODO: constants

            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateRevolvedShape(ref brushContainer, ref this);
        }
    }
}