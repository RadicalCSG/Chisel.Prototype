using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselTorusDefinition : IChiselGenerator
    {
        public const float kMinTubeDiameter			   = 0.1f;
        public const int   kDefaultHorizontalSegments  = 8;
        public const int   kDefaultVerticalSegments    = 8;

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

        [NamedItems(overflow = "Surface {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;

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
            verticalSegments	= kDefaultVerticalSegments;

            fitCircle			= true;

            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            tubeWidth			= Mathf.Max(tubeWidth,  kMinTubeDiameter);
            tubeHeight			= Mathf.Max(tubeHeight, kMinTubeDiameter);
            outerDiameter		= Mathf.Max(outerDiameter, tubeWidth * 2);
            
            horizontalSegments	= Mathf.Max(horizontalSegments, 3);
            verticalSegments	= Mathf.Max(verticalSegments, 3);

            totalAngle			= Mathf.Clamp(totalAngle, 1, 360); // TODO: constants
            
            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateTorus(ref brushContainer, ref this);
        }
    }
}