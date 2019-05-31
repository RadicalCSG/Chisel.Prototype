using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselExtrudedShapeDefinition : IChiselGenerator
    {
        public const int                kDefaultCurveSegments   = 8;
        public static readonly Curve2D  kDefaultShape           = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D                  shape;
        public ChiselPath               path;
        public int                      curveSegments;
        
        public ChiselSurfaceDefinition  surfaceDefinition;
        
        public void Reset()
        {
            curveSegments   = kDefaultCurveSegments;
            path			= new ChiselPath(ChiselPath.Default);
            shape			= new Curve2D(kDefaultShape);
            
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();
            
            if (shape == null)
                shape = new Curve2D(kDefaultShape);

            int sides = shape.controlPoints.Length;
            surfaceDefinition.EnsureSize(2 + sides);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateExtrudedShape(ref brushContainer, ref this);
        }
    }
}
