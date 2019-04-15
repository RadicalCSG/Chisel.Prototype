using System;
using Chisel.Assets;
using Chisel.Core;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;

namespace Chisel.Components
{
    [Serializable]
    public struct CSGTorusDefinition
    {
        public const float kMinTubeDiameter			= 0.1f;
        public const int   kDefaultHorizontalSegments  = 8;
        public const int   kVerticalHorizontalSegments = 8;

        // TODO: add scale the tube in y-direction (use transform instead?)
        // TODO: add start/total angle of tube

        public float                outerDiameter; 
        public float                innerDiameter	{ get { return CalcInnerDiameter(outerDiameter, tubeWidth); } set { tubeWidth = CalcTubeWidth(outerDiameter, value); } }
        public float                tubeWidth;
        public float                tubeHeight;
        public float                tubeRotation;
        public float                startAngle;
        public float                totalAngle;
        public int                  verticalSegments;
        public int                  horizontalSegments;

        public bool                 fitCircle;

        public CSGSurfaceAsset[]	surfaceAssets;
        public SurfaceDescription[]	surfaceDescriptions;

        public static float CalcInnerDiameter(float outerDiameter, float tubeWidth)
        {
            var innerDiameter = outerDiameter - (tubeWidth * 2);
            return Mathf.Max(0, innerDiameter);
        }

        public static float CalcTubeWidth(float outerDiameter, float innerDiameter)
        {
            var tubeWidth = (outerDiameter - innerDiameter) * 0.5f;
            return Mathf.Max(kMinTubeDiameter, tubeWidth);
        }

        public void Reset()
        {
            // TODO: create constants
            tubeWidth			= 0.5f;
            tubeHeight			= 0.5f;
            outerDiameter		= 1.0f;
            tubeRotation		= 0;
            startAngle			= 0.0f;
            totalAngle			= 360.0f;
            horizontalSegments	= kDefaultHorizontalSegments;
            verticalSegments	= kVerticalHorizontalSegments;

            fitCircle			= true;

            surfaceAssets		= null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            tubeWidth			= Mathf.Max(tubeWidth,  kMinTubeDiameter);
            tubeHeight			= Mathf.Max(tubeHeight, kMinTubeDiameter);
            outerDiameter		= Mathf.Max(outerDiameter, tubeWidth * 2);
            
            horizontalSegments	= Mathf.Max(horizontalSegments, 3);
            verticalSegments	= Mathf.Max(verticalSegments, 3);

            totalAngle			= Mathf.Clamp(totalAngle, 1, 360); // TODO: constants


            if (surfaceAssets == null ||
                surfaceAssets.Length != 6)
            {
                var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
                surfaceAssets = new CSGSurfaceAsset[6]
                {
                    CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    CSGSurfaceAsset.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial)
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