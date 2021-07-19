using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using Vector3 = UnityEngine.Vector3;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselCapsule : IBrushGenerator
    {
        public const float	kDefaultDiameter			= 1.0f;
        public const float	kDefaultHemisphereRatio	    = 0.25f;
        public const float	kDefaultHemisphereHeight	= kDefaultDiameter * kDefaultHemisphereRatio;
        
        public static readonly ChiselCapsule DefaultSettings = new ChiselCapsule
        {
            height				= 1.0f,
            topHeight			= kDefaultHemisphereHeight,
            bottomHeight		= kDefaultHemisphereHeight,
            offsetY             = 0,
            diameterX			= kDefaultDiameter,
            diameterZ			= kDefaultDiameter,
            rotation			= 0.0f,
            
            sides				= 8,
            topSegments			= 4,
            bottomSegments		= 4
        };

        public float    height;
        public float    topHeight;
        public float    bottomHeight;
        public float    offsetY;

        public float    diameterX;
        public float    diameterZ;
        public float    rotation;

        public int      sides;
        public int      topSegments;
        public int      bottomSegments;


        #region Properties
        const float kHeightEpsilon = 0.001f;

        public bool	        HaveRoundedTop		{ get { return topSegments > 0 && topHeight > kHeightEpsilon; } }
        public bool	        HaveRoundedBottom	{ get { return bottomSegments > 0 && bottomHeight > kHeightEpsilon; } }
        public bool	        HaveCylinder		{ get { return CylinderHeight > kHeightEpsilon; } }
        public float        CylinderHeight		{ get { return math.abs(height - (bottomHeight + topHeight)); } }


        internal int		BottomRingCount		{ get { return HaveRoundedBottom ? bottomSegments : 1; } }
        internal int		TopRingCount		{ get { return HaveRoundedTop    ? topSegments    : 1; } }
        internal int		RingCount			{ get { return BottomRingCount + TopRingCount - (HaveCylinder ? 0 : 1); } }
        internal int		Segments            { get { return math.max(1, RingCount); } }


        // TODO: store somewhere else
        internal int		ExtraVertexCount	{ get { return ((HaveRoundedTop) ? 1 : 0) + ((HaveRoundedBottom) ? 1 : 0); } }
        internal int		BottomVertex		{ get { return (0); } }
        internal int		TopVertex			{ get { return (HaveRoundedBottom) ? 1 : 0; } }


        internal int		VertexCount			{ get { return (sides * RingCount) + ExtraVertexCount; } }

        internal int		BottomRing			{ get { return (HaveRoundedBottom) ? (RingCount - bottomSegments) : RingCount - 1; } }
        internal int		TopRing				{ get { return (HaveRoundedTop   ) ? (topSegments - 1) : 0; } }

        internal float	    TopOffset			{ get { if (height < 0) return -topHeight; return height - topHeight; } }

        internal float	    BottomOffset		{ get { if (height < 0) return height + bottomHeight; return bottomHeight; } }

        internal int		TopVertexOffset		{ get { return ExtraVertexCount + ((TopRingCount - 1) * sides); } }
        internal int		BottomVertexOffset	{ get { return ExtraVertexCount + ((RingCount - BottomRingCount) * sides); } }
        #endregion

        #region Generate
        [BurstCompile]
        public ChiselBlobAssetReference<BrushMeshBlob> GenerateMesh(ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator)
        {
            if (!BrushMeshFactory.GenerateCapsule(in this,
                                                  in surfaceDefinitionBlob,
                                                  out var newBrushMesh,
                                                  allocator))
                return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2 + sides; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }
        #endregion

        #region Validation

        public const float kMinDiameter = 0.01f;

        [BurstDiscard]
        public void Validate()
        {
            topHeight		= math.max(topHeight, 0);
            bottomHeight	= math.max(bottomHeight, 0);
            height			= math.max(topHeight + bottomHeight, math.abs(height)) * (height < 0 ? -1 : 1);

            diameterX		= math.max(math.abs(diameterX), kMinDiameter);
            diameterZ		= math.max(math.abs(diameterZ), kMinDiameter);

            topSegments	    = math.max(topSegments, 0);
            bottomSegments	= math.max(bottomSegments, 0);
            sides			= math.max(sides, 3);
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages)
        {
        }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    [Serializable]
    public class ChiselCapsuleDefinition : SerializedBrushGenerator<ChiselCapsule>
    {
        public const string kNodeTypeName = "Capsule";

        //[NamedItems(overflow = "Side {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        
        #region OnEdit
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
            var sides			    = definition.settings.sides;
            
            // TODO: share this logic with GenerateCapsuleVertices
            
            var topHemisphere		= definition.settings.HaveRoundedTop;
            var bottomHemisphere	= definition.settings.HaveRoundedBottom;
            var topSegments			= topHemisphere    ? definition.settings.topSegments    : 0;
            var bottomSegments		= bottomHemisphere ? definition.settings.bottomSegments : 0;
            
            var extraVertices		= definition.settings.ExtraVertexCount;
            var bottomVertex		= definition.settings.BottomVertex;
            var topVertex			= definition.settings.TopVertex;
            
            var rings				= definition.settings.RingCount;
            var bottomRing			= (bottomHemisphere) ? (rings - bottomSegments) : rings - 1;
            var topRing				= (topHemisphere   ) ? (topSegments - 1) : 0;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                if ((!definition.settings.HaveRoundedTop && i == topRing) ||
                    (!definition.settings.HaveRoundedBottom && i == bottomRing))
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
        
        public override void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            if (BrushMeshFactory.GenerateCapsuleVertices(ref settings, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);

                handles.color = baseColor;
            }

            var topPoint	= normal * (settings.offsetY + settings.height);
            var bottomPoint = normal * (settings.offsetY);
            var middlePoint	= normal * (settings.offsetY + (settings.height * 0.5f));
            var radius2D	= new float2(settings.diameterX, settings.diameterZ) * 0.5f;

            var topHeight       = settings.topHeight;
            var bottomHeight    = settings.bottomHeight;

            var maxTopHeight    = settings.height - bottomHeight;
            var maxBottomHeight = settings.height - topHeight;

            if (settings.height < 0)
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
                    for (int j = settings.sides - 1, i = 0; i < settings.sides; j = i, i++)
                    {
                        var from = vertices[j + settings.TopVertexOffset];
                        var to = vertices[i + settings.TopVertexOffset];

                        if (handles.DoEdgeHandle1DOffset(out var edgeOffset, UnitySceneExtensions.Axis.Y, from, to, renderLine: false))
                            topHeight = math.clamp(topHeight - edgeOffset, 0, maxTopHeight);
                        topLoopHasFocus = topLoopHasFocus || handles.lastHandleHadFocus;
                    }


                    handles.color = baseColor;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    var topHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;

                    topLoopHasFocus = topLoopHasFocus || (topHasFocus && !settings.HaveRoundedTop);

                    var thickness = topLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    handles.color = handles.GetStateColor(baseColor, topLoopHasFocus, true);
                    handles.DrawLineLoop(vertices, settings.TopVertexOffset, settings.sides, lineMode: LineMode.NoZTest, thickness: thickness);

                    handles.color = handles.GetStateColor(baseColor, topLoopHasFocus, false);
                    handles.DrawLineLoop(vertices, settings.TopVertexOffset, settings.sides, lineMode: LineMode.ZTest,   thickness: thickness);
                }
                
                {
                    var isBottomBackfaced	= handles.IsSufaceBackFaced(bottomPoint, -normal);
                    var bottomLoopHasFocus = false;
                    handles.backfaced = isBottomBackfaced;
                    for (int j = settings.sides - 1, i = 0; i < settings.sides; j = i, i++)
                    {
                        var from    = vertices[j + settings.BottomVertexOffset];
                        var to      = vertices[i + settings.BottomVertexOffset];

                        if (handles.DoEdgeHandle1DOffset(out var edgeOffset, UnitySceneExtensions.Axis.Y, from, to, renderLine: false))
                            bottomHeight = math.clamp(bottomHeight + edgeOffset, 0, maxBottomHeight);
                        bottomLoopHasFocus = bottomLoopHasFocus || handles.lastHandleHadFocus;
                    }

                    handles.color = baseColor;
                    handles.DoDirectionHandle(ref bottomPoint, -normal);
                    var bottomHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;

                    bottomLoopHasFocus = bottomLoopHasFocus || (bottomHasFocus && !settings.HaveRoundedBottom);

                    var thickness = bottomLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    handles.color = handles.GetStateColor(baseColor, bottomLoopHasFocus, true);
                    handles.DrawLineLoop(vertices, settings.BottomVertexOffset, settings.sides, lineMode: LineMode.NoZTest, thickness: thickness);

                    handles.color = handles.GetStateColor(baseColor, bottomLoopHasFocus, false);
                    handles.DrawLineLoop(vertices, settings.BottomVertexOffset, settings.sides, lineMode: LineMode.ZTest,   thickness: thickness);
                }
            }
            if (prevModified != handles.modified)
            {
                settings.diameterX      = radius2D.x * 2.0f;
                settings.height         = topPoint.y - bottomPoint.y;
                settings.diameterZ      = radius2D.x * 2.0f;
                settings.offsetY        = bottomPoint.y;
                settings.topHeight      = topHeight;
                settings.bottomHeight   = bottomHeight;
                // TODO: handle sizing down (needs to modify transformation?)
            }
        }
        #endregion
    }
}