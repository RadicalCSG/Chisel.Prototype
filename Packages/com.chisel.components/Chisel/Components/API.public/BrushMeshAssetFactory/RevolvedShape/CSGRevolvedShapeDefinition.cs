using System;
using Chisel.Assets;
using Chisel.Core;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [Serializable]
    public class CSGRevolvedShapeDefinition // TODO: make this a struct
    {
        public const int				kDefaultCurveSegments	= 8;
        public const int                kDefaultRevolveSegments = 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D				shape  = null; // TODO: make this a struct
        public int					curveSegments;
        public int					revolveSegments;
        public float				startAngle;
        public float				totalAngle;

        public ChiselBrushMaterial[]	brushMaterials;
        public SurfaceDescription[]	surfaceDescriptions;

        public void Reset()
        {
            // TODO: create constants
            shape				= kDefaultShape;
            startAngle			= 0.0f;
            totalAngle			= 360.0f;
            curveSegments		= kDefaultCurveSegments;
            revolveSegments		= kDefaultRevolveSegments;

            brushMaterials		= null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            curveSegments		= Mathf.Max(curveSegments, 2);
            revolveSegments		= Mathf.Max(revolveSegments, 1);

            totalAngle			= Mathf.Clamp(totalAngle, 1, 360); // TODO: constants
            
            if (brushMaterials == null ||
                brushMaterials.Length != 6)
            {
                var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new ChiselBrushMaterial[6]
                {
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial)
                };
            }

            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != 6)
            {
                // TODO: make this independent on plane position somehow
                var surfaceFlags	= CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[6]
                {
                    new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
                    new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
                    new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
                    new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
                    new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
                    new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 }
                };
            }
        }
    }
}