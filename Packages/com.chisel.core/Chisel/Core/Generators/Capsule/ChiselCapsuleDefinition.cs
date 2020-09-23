using System;
using Bounds = UnityEngine.Bounds;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselCapsuleDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Capsule";

        public const float	kMinDiameter				= 0.01f;

        public const float	kDefaultHeight				= 1.0f;
        public const float	kDefaultDiameter			= 1.0f;
        public const float	kDefaultHemisphereRatio	    = 0.25f;
        public const float	kDefaultHemisphereHeight	= kDefaultDiameter * kDefaultHemisphereRatio;
        public const float  kDefaultRotation            = 0.0f;
        public const int	kDefaultSides				= 8;
        public const int	kDefaultTopSegments			= 4;
        public const int	kDefaultBottomSegments		= 4;
        public const float  kHeightEpsilon              = 0.001f;
        
        public float                height;
        public float                topHeight;
        public float                bottomHeight;
        public float                offsetY;

        public float                diameterX;
        public float                diameterZ;
        public float                rotation;

        public int                  sides;
        public int                  topSegments;
        public int                  bottomSegments;

        [NamedItems(overflow = "Side {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;

        public bool					haveRoundedTop		{ get { return topSegments > 0 && topHeight > kHeightEpsilon; } }
        public bool					haveRoundedBottom	{ get { return bottomSegments > 0 && bottomHeight > kHeightEpsilon; } }
        public bool					haveCylinder		{ get { return cylinderHeight > kHeightEpsilon; } }
        public float				cylinderHeight		{ get { return Mathf.Abs(height - (bottomHeight + topHeight)); } }


        public int					bottomRingCount		{ get { return haveRoundedBottom ? bottomSegments : 1; } }
        public int					topRingCount		{ get { return haveRoundedTop    ? topSegments    : 1; } }
        public int					ringCount			{ get { return bottomRingCount + topRingCount - (haveCylinder ? 0 : 1); } }
        
        public int					segments            { get { return Mathf.Max(1, ringCount); } }


        // TODO: store somewhere else
        public int					extraVertexCount	{ get { return ((haveRoundedTop) ? 1 : 0) + ((haveRoundedBottom) ? 1 : 0); } }
        public int					bottomVertex		{ get { return (0); } }
        public int					topVertex			{ get { return (haveRoundedBottom) ? 1 : 0; } }

        
        public int					vertexCount			{ get { return (sides * ringCount) + extraVertexCount; } }

        public int					bottomRing			{ get { return (haveRoundedBottom) ? (ringCount - bottomSegments) : ringCount - 1; } }
        public int					topRing				{ get { return (haveRoundedTop   ) ? (topSegments - 1) : 0; } }
        
        public float				topOffset			{ get { if (height < 0) return -topHeight; return height - topHeight; } }

        public float				bottomOffset		{ get { if (height < 0) return height + bottomHeight; return bottomHeight; } }
        
        public int					topVertexOffset		{ get { return extraVertexCount + ((topRingCount - 1) * sides); } }
        public int					bottomVertexOffset	{ get { return extraVertexCount + ((ringCount - bottomRingCount) * sides); } }

        public void Reset()
        {
            height				= kDefaultHeight;
            topHeight			= kDefaultHemisphereHeight;
            bottomHeight		= kDefaultHemisphereHeight;
            offsetY             = 0;
            diameterX			= kDefaultDiameter;
            diameterZ			= kDefaultDiameter;
            rotation			= kDefaultRotation;
            
            sides				= kDefaultSides;
            topSegments			= kDefaultTopSegments;
            bottomSegments		= kDefaultBottomSegments;
            
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            topHeight			= Mathf.Max(topHeight, 0);
            bottomHeight		= Mathf.Max(bottomHeight, 0);
            height				= Mathf.Max(topHeight + bottomHeight, Mathf.Abs(height)) * (height < 0 ? -1 : 1);

            diameterX			= Mathf.Max(Mathf.Abs(diameterX), kMinDiameter);
            diameterZ			= Mathf.Max(Mathf.Abs(diameterZ), kMinDiameter);
            
            topSegments			= Mathf.Max(topSegments, 0);
            bottomSegments		= Mathf.Max(bottomSegments, 0);
            sides				= Mathf.Max(sides, 3);
            
            surfaceDefinition.EnsureSize(2 + sides);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateCapsule(ref brushContainer, ref this);
        }


        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselCapsuleDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides			= definition.sides;
            
            // TODO: share this logic with GenerateCapsuleVertices
            
            var topHemisphere		= definition.haveRoundedTop;
            var bottomHemisphere	= definition.haveRoundedBottom;
            var topSegments			= topHemisphere    ? definition.topSegments    : 0;
            var bottomSegments		= bottomHemisphere ? definition.bottomSegments : 0;
            
            var extraVertices		= definition.extraVertexCount;
            var bottomVertex		= definition.bottomVertex;
            var topVertex			= definition.topVertex;
            
            var rings				= definition.ringCount;
            var bottomRing			= (bottomHemisphere) ? (rings - bottomSegments) : rings - 1;
            var topRing				= (topHemisphere   ) ? (topSegments - 1) : 0;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                if ((!definition.haveRoundedTop && i == topRing) ||
                    (!definition.haveRoundedBottom && i == bottomRing))
                    continue;
                bool isCapRing = (i == topRing || i == bottomRing);
                if (isCapRing)
                    continue;
                renderer.color = (isCapRing ? prevColor : color);
                renderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: (isCapRing ? kCapLineThickness : kHorzLineThickness), dashSize: (isCapRing ? 0 : kLineDash));
            }

            renderer.color = color;
            for (int k = 0; k < sides; k++)
            {
                if (topHemisphere)
                    renderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (bottomHemisphere)
                    renderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            renderer.color = prevColor;
        }

        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?
        
        public void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            if (BrushMeshFactory.GenerateCapsuleVertices(ref this, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);

                handles.color = baseColor;
            }

            var topPoint	= normal * (this.offsetY + this.height);
            var bottomPoint = normal * (this.offsetY);
            var middlePoint	= normal * (this.offsetY + (this.height * 0.5f));
            var radius2D	= new Vector2(this.diameterX, this.diameterZ) * 0.5f;

            var topHeight       = this.topHeight;
            var bottomHeight    = this.bottomHeight;

            var maxTopHeight    = this.height - bottomHeight;
            var maxBottomHeight = this.height - topHeight;

            if (this.height < 0)
                normal = -normal;

            var prevModified = handles.modified;
            {
                handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                var radius2Dx = radius2D.x;
                handles.DoRadiusHandle(ref radius2Dx, normal, middlePoint);
                radius2D.x = radius2Dx;

                {
                    var isTopBackfaced = handles.IsSufaceBackFaced(topPoint, normal);
                    var topLoopHasFocus = false;
                    handles.backfaced = isTopBackfaced;
                    for (int j = this.sides - 1, i = 0; i < this.sides; j = i, i++)
                    {
                        var from = vertices[j + this.topVertexOffset];
                        var to = vertices[i + this.topVertexOffset];

                        if (handles.DoEdgeHandle1DOffset(out var edgeOffset, UnitySceneExtensions.Axis.Y, from, to, renderLine: false))
                            topHeight = Mathf.Clamp(topHeight - edgeOffset, 0, maxTopHeight);
                        topLoopHasFocus = topLoopHasFocus || handles.lastHandleHadFocus;
                    }


                    handles.color = baseColor;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    var topHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;

                    topLoopHasFocus = topLoopHasFocus || (topHasFocus && !this.haveRoundedTop);

                    var thickness = topLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    handles.color = handles.GetStateColor(baseColor, topLoopHasFocus, true);
                    handles.DrawLineLoop(vertices, this.topVertexOffset, this.sides, lineMode: LineMode.NoZTest, thickness: thickness);

                    handles.color = handles.GetStateColor(baseColor, topLoopHasFocus, false);
                    handles.DrawLineLoop(vertices, this.topVertexOffset, this.sides, lineMode: LineMode.ZTest,   thickness: thickness);
                }
                
                {
                    var isBottomBackfaced	= handles.IsSufaceBackFaced(bottomPoint, -normal);
                    var bottomLoopHasFocus = false;
                    handles.backfaced = isBottomBackfaced;
                    for (int j = this.sides - 1, i = 0; i < this.sides; j = i, i++)
                    {
                        var from    = vertices[j + this.bottomVertexOffset];
                        var to      = vertices[i + this.bottomVertexOffset];

                        if (handles.DoEdgeHandle1DOffset(out var edgeOffset, UnitySceneExtensions.Axis.Y, from, to, renderLine: false))
                            bottomHeight = Mathf.Clamp(bottomHeight + edgeOffset, 0, maxBottomHeight);
                        bottomLoopHasFocus = bottomLoopHasFocus || handles.lastHandleHadFocus;
                    }

                    handles.color = baseColor;
                    handles.DoDirectionHandle(ref bottomPoint, -normal);
                    var bottomHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;

                    bottomLoopHasFocus = bottomLoopHasFocus || (bottomHasFocus && !this.haveRoundedBottom);

                    var thickness = bottomLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    handles.color = handles.GetStateColor(baseColor, bottomLoopHasFocus, true);
                    handles.DrawLineLoop(vertices, this.bottomVertexOffset, this.sides, lineMode: LineMode.NoZTest, thickness: thickness);

                    handles.color = handles.GetStateColor(baseColor, bottomLoopHasFocus, false);
                    handles.DrawLineLoop(vertices, this.bottomVertexOffset, this.sides, lineMode: LineMode.ZTest,   thickness: thickness);
                }
            }
            if (prevModified != handles.modified)
            {
                this.diameterX      = radius2D.x * 2.0f;
                this.height         = topPoint.y - bottomPoint.y;
                this.diameterZ      = radius2D.x * 2.0f;
                this.offsetY        = bottomPoint.y;
                this.topHeight      = topHeight;
                this.bottomHeight   = bottomHeight;
                // TODO: handle sizing down (needs to modify transformation?)
            }
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}