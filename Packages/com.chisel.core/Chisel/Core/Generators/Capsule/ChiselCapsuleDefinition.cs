using System;
using Bounds = UnityEngine.Bounds;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnityEngine.Profiling;
using Unity.Burst;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Collections;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselCapsuleDefinition : IChiselGenerator, IBrushGenerator
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
        
        [Serializable]
        public struct Settings
        { 
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

            public bool	        HaveRoundedTop		{ get { return topSegments > 0 && topHeight > kHeightEpsilon; } }
            public bool	        HaveRoundedBottom	{ get { return bottomSegments > 0 && bottomHeight > kHeightEpsilon; } }
            public bool	        HaveCylinder		{ get { return CylinderHeight > kHeightEpsilon; } }
            public float        CylinderHeight		{ get { return Mathf.Abs(height - (bottomHeight + topHeight)); } }


            internal int		BottomRingCount		{ get { return HaveRoundedBottom ? bottomSegments : 1; } }
            internal int		TopRingCount		{ get { return HaveRoundedTop    ? topSegments    : 1; } }
            internal int		RingCount			{ get { return BottomRingCount + TopRingCount - (HaveCylinder ? 0 : 1); } }
            internal int		Segments            { get { return Mathf.Max(1, RingCount); } }


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
        }

        static readonly Settings kDefaultSettings = new Settings
        {
            height				= kDefaultHeight,
            topHeight			= kDefaultHemisphereHeight,
            bottomHeight		= kDefaultHemisphereHeight,
            offsetY             = 0,
            diameterX			= kDefaultDiameter,
            diameterZ			= kDefaultDiameter,
            rotation			= kDefaultRotation,
            
            sides				= kDefaultSides,
            topSegments			= kDefaultTopSegments,
            bottomSegments		= kDefaultBottomSegments
        };

        public Settings settings;


        [NamedItems(overflow = "Side {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;

        public ChiselSurfaceDefinition SurfaceDefinition { get { return surfaceDefinition; } }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe override int GetHashCode()
        {
            unchecked
            {
                fixed (Settings* settingsPtr = &settings)
                {
                    return (int)math.hash(new uint2(math.hash(settingsPtr, sizeof(Settings)),
                                                    (uint)surfaceDefinition.GetHashCode()));
                }
            }
        }

        public void Reset()
        {
            settings = kDefaultSettings;

            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            settings.topHeight		= Mathf.Max(settings.topHeight, 0);
            settings.bottomHeight	= Mathf.Max(settings.bottomHeight, 0);
            settings.height			= Mathf.Max(settings.topHeight + settings.bottomHeight, Mathf.Abs(settings.height)) * (settings.height < 0 ? -1 : 1);

            settings.diameterX		= Mathf.Max(Mathf.Abs(settings.diameterX), kMinDiameter);
            settings.diameterZ		= Mathf.Max(Mathf.Abs(settings.diameterZ), kMinDiameter);

            settings.topSegments	= Mathf.Max(settings.topSegments, 0);
            settings.bottomSegments	= Mathf.Max(settings.bottomSegments, 0);
            settings.sides			= Mathf.Max(settings.sides, 3);
            
            surfaceDefinition.EnsureSize(2 + settings.sides);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateCapsule(ref brushContainer, ref this);
        }


        [BurstCompile(CompileSynchronously = true)]
        public bool Generate(ref CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
            {
                node = brush = CSGTreeBrush.Create(userID: userID, operation: operation);
            } else
            {
                if (brush.Operation != operation)
                    brush.Operation = operation;
            }

            using (var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.Temp))
            {
                Validate();
                if (!BrushMeshFactory.GenerateCapsule(in settings,
                                                      in surfaceDefinitionBlob,
                                                      out var brushMesh,
                                                      Allocator.Persistent))
                {
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                    return false;
                }

                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
            }
            return true;
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

            var topPoint	= normal * (settings.offsetY + settings.height);
            var bottomPoint = normal * (settings.offsetY);
            var middlePoint	= normal * (settings.offsetY + (settings.height * 0.5f));
            var radius2D	= new Vector2(settings.diameterX, settings.diameterZ) * 0.5f;

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
                            topHeight = Mathf.Clamp(topHeight - edgeOffset, 0, maxTopHeight);
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
                            bottomHeight = Mathf.Clamp(bottomHeight + edgeOffset, 0, maxBottomHeight);
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

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}