using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselTorusDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Torus";

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


        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselTorusDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;
            
            if (definition.totalAngle != 360)
                horzSegments++;
            
            var prevColor		= renderer.color;
            prevColor.a *= 0.8f;
            var color			= prevColor;
            color.a *= 0.6f;

            renderer.color = color;
            for (int i = 0, j = 0; i < horzSegments; i++, j += vertSegments)
                renderer.DrawLineLoop(vertices, j, vertSegments, lineMode: lineMode, thickness: kVertLineThickness);

            for (int k = 0; k < vertSegments; k++)
            {
                for (int i = 0, j = 0; i < horzSegments - 1; i++, j += vertSegments)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + vertSegments], lineMode: lineMode, thickness: kHorzLineThickness);
            }
            if (definition.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    renderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            renderer.color = prevColor;
        }

        public void OnEdit(IChiselHandles handles)
        {
            var normal			= Vector3.up;

            Vector3[] vertices = null;
            if (BrushMeshFactory.GenerateTorusVertices(this, ref vertices))
            {
                var baseColor = handles.color;
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);
                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }

            var outerRadius = this.outerDiameter * 0.5f;
            var innerRadius = this.innerDiameter * 0.5f;
            var topPoint	= normal * (this.tubeHeight * 0.5f);
            var bottomPoint	= normal * (-this.tubeHeight * 0.5f);

            handles.DoRadiusHandle(ref outerRadius, normal, Vector3.zero);
            handles.DoRadiusHandle(ref innerRadius, normal, Vector3.zero);
            handles.DoDirectionHandle(ref bottomPoint, -normal);
            handles.DoDirectionHandle(ref topPoint, normal);
            if (handles.modified)
            {
                this.outerDiameter	= outerRadius * 2.0f;
                this.innerDiameter	= innerRadius * 2.0f;
                this.tubeHeight		= (topPoint.y - bottomPoint.y);
                // TODO: handle sizing down
            }
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}