using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnitySceneExtensions;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselPathedStairsDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Pathed Stairs";

        public const int				kDefaultCurveSegments	= 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D					shape;
        public int                      curveSegments;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        public ChiselLinearStairsDefinition stairs;

        public void Reset()
        {
            shape           = kDefaultShape;
            curveSegments   = kDefaultCurveSegments;
            stairs.Reset();
        }

        public void Validate()
        {
            curveSegments = Mathf.Max(curveSegments, 2);
            stairs.Validate();
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GeneratePathedStairs(ref brushContainer, ref this);
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoShapeHandle(ref shape);
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}