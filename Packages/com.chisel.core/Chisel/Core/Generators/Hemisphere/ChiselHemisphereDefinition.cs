using System;
using Bounds = UnityEngine.Bounds;
using Vector2 = UnityEngine.Vector2;
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
        public const string kNodeTypeName = "Hemisphere";

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
            return BrushMeshFactory.GenerateHemisphere(ref brushContainer, ref this);
        }


        //
        // TODO: code below needs to be cleaned up & simplified 
        //

        
        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselHemisphereDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides			= definition.horizontalSegments;

            var topSegments		= Mathf.Max(definition.verticalSegments,    0);
            var bottomCap		= false;
            var topCap			= (topSegments    != 0);
            var extraVertices	= ((topCap) ? 1 : 0) + ((bottomCap) ? 1 : 0);
            var bottomVertex	= 0;
            //var topVertex		= (bottomCap) ? 1 : 0;
            
            var rings			= (vertices.Length - extraVertices) / sides;
            var bottomRing		= 0;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                renderer.color = ((i == bottomRing) ? prevColor : color);
                renderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: ((i == bottomRing) ? kCapLineThickness : kHorzLineThickness), dashSize: ((i == bottomRing) ? 0 : kLineDash));
            }

            renderer.color = color;
            for (int k = 0; k < sides; k++)
            {
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (topCap)
                    renderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            renderer.color = prevColor;
        }
        

        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?

        public void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            if (BrushMeshFactory.GenerateHemisphereVertices(ref this, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }
            

            var topPoint	= normal * this.diameterXYZ.y;
            var radius2D	= new Vector2(this.diameterXYZ.x, this.diameterXYZ.z) * 0.5f;

            if (this.diameterXYZ.y < 0)
                normal = -normal;
            bool previousModified;
            previousModified = handles.modified;
            {
                handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                handles.DoRadiusHandle(ref radius2D.x, normal, Vector3.zero);

                {
                    var isTopBackfaced		= false; // TODO: how to do this?
                    
                    handles.backfaced = isTopBackfaced;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    handles.backfaced = false;
                }
            }
            if (previousModified != handles.modified)
            {
                var diameter = this.diameterXYZ;
                diameter.y = topPoint.y;
                diameter.x = radius2D.x * 2.0f;
                diameter.z = radius2D.x * 2.0f;
                this.diameterXYZ = diameter;
            }
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}