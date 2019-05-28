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
    public class CSGExtrudedShapeDefinition
    {
        public const int                kDefaultCurveSegments   = 8;
        public static readonly Curve2D  kDefaultShape           = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D                  shape;
        public Path                     path;
        public int                      curveSegments   = kDefaultCurveSegments;

        public ChiselBrushMaterial[]    brushMaterials;
        public SurfaceDescription[]     surfaceDescriptions;
        
        public void Reset()
        {
            curveSegments   = kDefaultCurveSegments;
            path			= new Path(Path.Default);
            shape			= new Curve2D(kDefaultShape);

            brushMaterials      = null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            if (brushMaterials == null ||
                brushMaterials.Length != 3)
            {
                var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new ChiselBrushMaterial[3];
                for (int i = 0; i < 3; i++) // Note: sides share same material
                    brushMaterials[i] = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
            }

            if (shape == null)
                shape = new Curve2D(kDefaultShape);

            int sides = shape.controlPoints.Length;
            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != 2 + sides)
            {
                var surfaceFlags	= CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[2 + sides];
                for (int i = 0; i < 2 + sides; i++) 
                {
                    surfaceDescriptions[i] = new SurfaceDescription { surfaceFlags = surfaceFlags, UV0 = UVMatrix.centered };
                }
            }
        }
    }
}
