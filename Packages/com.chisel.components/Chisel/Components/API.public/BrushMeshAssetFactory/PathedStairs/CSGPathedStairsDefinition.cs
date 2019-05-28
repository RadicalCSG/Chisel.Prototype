using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnitySceneExtensions;
using Chisel.Core;

namespace Chisel.Components
{
    [Serializable]
    public class CSGPathedStairsDefinition // TODO: make this a struct
    {
        public const int				kDefaultCurveSegments	= 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D					shape  = null; // TODO: make this a struct
        public int                      curveSegments;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        public CSGLinearStairsDefinition stairs = new CSGLinearStairsDefinition();

        public void Reset()
        {
            shape = kDefaultShape;
            curveSegments = kDefaultCurveSegments;
            stairs.Reset();
        }

        public void Validate()
        {
            curveSegments = Mathf.Max(curveSegments, 2);
            stairs.Validate();
        }
    }
}