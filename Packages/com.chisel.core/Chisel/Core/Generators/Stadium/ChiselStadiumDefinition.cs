using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using Vector3 = UnityEngine.Vector3;

namespace Chisel.Core
{
    [Serializable]
    public struct StadiumSettings
    {
        internal const float kNoCenterEpsilon = 0.0001f;

        public float                width;        
        public float                height;
        public float                length;
        public float                topLength;
        public float                bottomLength;
        
        // TODO: better naming
        public int                  topSides;
        public int                  bottomSides;

        internal int				Sides				{ get { return (HaveCenter ? 2 : 0) + math.max(topSides, 1) + math.max(bottomSides, 1); } }
        internal int				FirstTopSide		{ get { return 0; } }
        internal int				LastTopSide			{ get { return math.max(topSides, 1); } }
        internal int				FirstBottomSide		{ get { return LastTopSide + 1; } }
        internal int				LastBottomSide		{ get { return Sides - 1; } }

        internal bool				HaveRoundedTop		{ get { return (topLength    > 0) && (topSides    > 1); } }
        internal bool				HaveRoundedBottom	{ get { return (bottomLength > 0) && (bottomSides > 1); } }
        internal bool				HaveCenter			{ get { return (length - ((HaveRoundedTop ? topLength : 0) + (HaveRoundedBottom ? bottomLength : 0))) >= kNoCenterEpsilon; } }
    }

    public struct ChiselStadiumGenerator : IChiselBrushTypeGenerator<StadiumSettings>
    {
        [BurstCompile(CompileSynchronously = true)]
        public BlobAssetReference<BrushMeshBlob> GenerateMesh(StadiumSettings settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, Allocator allocator)
        {
            if (!BrushMeshFactory.GenerateStadium(settings.width, settings.height, settings.length,
                                                  settings.topLength, settings.topSides,
                                                  settings.bottomLength, settings.bottomSides,
                                                  in surfaceDefinitionBlob,
                                                  out var newBrushMesh,
                                                  allocator))
                return default;
            return newBrushMesh;
        }
    }

    [Serializable]
    public struct ChiselStadiumDefinition : IChiselBrushGenerator<ChiselStadiumGenerator, StadiumSettings>
    {
        public const string kNodeTypeName = "Stadium";

        public const float	kMinDiameter				= 0.01f;
        public const float	kMinLength					= 0.01f;
        public const float	kMinHeight					= 0.01f;
        
        public const float	kDefaultHeight				= 1.0f;
        public const float	kDefaultLength				= 1.0f;
        public const float	kDefaultTopLength			= 0.25f;
        public const float	kDefaultBottomLength		= 0.25f;
        public const float	kDefaultWidth			    = 1.0f;
        
        public const int	kDefaultTopSides			= 4;
        public const int	kSidesVertices				= 4;

        [HideFoldout] public StadiumSettings settings;
        

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            settings.width			    = kDefaultWidth;

            settings.height				= kDefaultHeight;

            settings.length				= kDefaultLength;
            settings.topLength			= kDefaultTopLength;
            settings.bottomLength		= kDefaultBottomLength;

            settings.topSides			= kDefaultTopSides;
            settings.bottomSides		= kSidesVertices;
        }

        public int RequiredSurfaceCount { get { return 2 + settings.Sides; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            settings.topLength	    = math.max(settings.topLength,    0);
            settings.bottomLength	= math.max(settings.bottomLength, 0);
            settings.length			= math.max(math.abs(settings.length), (settings.HaveRoundedTop ? settings.topLength : 0) + (settings.HaveRoundedBottom ? settings.bottomLength : 0));
            settings.length			= math.max(math.abs(settings.length), kMinLength);

            settings.height			= math.max(math.abs(settings.height), kMinHeight);
            settings.width			= math.max(math.abs(settings.width), kMinDiameter);

            settings.topSides		= math.max(settings.topSides,	 1);
            settings.bottomSides	= math.max(settings.bottomSides, 1);
        }

        public StadiumSettings GenerateSettings()
        {
            return settings;
        }


        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kSideLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselStadiumDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides				= definition.settings.Sides;
            var topSides			= math.max(definition.settings.topSides, 1) + 1;
            var bottomSides			= math.max(definition.settings.bottomSides, 1) + 1;

            var haveRoundedTop		= definition.settings.HaveRoundedTop;
            var haveRoundedBottom	= definition.settings.HaveRoundedBottom;
            var haveCenter			= definition.settings.HaveCenter;
            //renderer.DrawLineLoop(vertices,     0, sides, lineMode: lineMode, thickness: kCapLineThickness);
            //renderer.DrawLineLoop(vertices, sides, sides, lineMode: lineMode, thickness: kCapLineThickness);

            var firstTopSide = definition.settings.FirstTopSide;
            var lastTopSide  = definition.settings.LastTopSide;
            for (int k = firstTopSide; k <= lastTopSide; k++)
            {
                var sideLine	= !haveRoundedTop || (k == firstTopSide) || (k == lastTopSide);
                var thickness	= (sideLine ? kSideLineThickness : kVertLineThickness);
                var dashSize	= (sideLine ? 0                  : kLineDash);
                renderer.DrawLine(vertices[k], vertices[sides + k], lineMode: lineMode, thickness: thickness, dashSize: dashSize);
            }
            
            var firstBottomSide = definition.settings.FirstBottomSide;
            var lastBottomSide  = definition.settings.LastBottomSide;
            for (int k = firstBottomSide; k <= lastBottomSide; k++)
            {
                var sideLine	= haveCenter && (!haveRoundedBottom || (k == firstBottomSide) || (k == lastBottomSide));
                var thickness	= (sideLine ? kSideLineThickness : kVertLineThickness);
                var dashSize	= (sideLine ? 0                  : kLineDash);
                renderer.DrawLine(vertices[k], vertices[sides + k], lineMode: lineMode, thickness: thickness, dashSize: dashSize);
            }

            //renderer.DrawLine(vertices[firstBottomSide], vertices[lastBottomSide], lineMode: lineMode, thickness: kVertLineThickness);
            //renderer.DrawLine(vertices[firstTopSide   ], vertices[lastTopSide   ], lineMode: lineMode, thickness: kVertLineThickness);

            //renderer.DrawLine(vertices[sides + firstBottomSide], vertices[sides + lastBottomSide], lineMode: lineMode, thickness: kVertLineThickness);
            //renderer.DrawLine(vertices[sides + firstTopSide   ], vertices[sides + lastTopSide   ], lineMode: lineMode, thickness: kVertLineThickness);
        }

        public void OnEdit(IChiselHandles handles)
        {
            var baseColor       = handles.color;
            var upVector		= Vector3.up;
            var rightVector		= Vector3.right;
            var forwardVector	= Vector3.forward;

            Vector3[] vertices = null;
            if (BrushMeshFactory.GenerateStadiumVertices(this, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }

            var height		        = settings.height;
            var length		        = settings.length;
            var diameter	        = settings.width;
            var sides		        = settings.Sides;
            
            var firstTopSide	    = settings.FirstTopSide;
            var lastTopSide		    = settings.LastTopSide;
            var firstBottomSide     = settings.FirstBottomSide;
            var lastBottomSide      = settings.LastBottomSide;

            var haveRoundedTop		= settings.HaveRoundedTop;
            var haveRoundedBottom	= settings.HaveRoundedBottom;
            var haveCenter			= settings.HaveCenter;
            var topLength			= settings.topLength;
            var bottomLength		= settings.bottomLength;
            

            var midY		= height * 0.5f;
            var halfLength	= length * 0.5f;
            var midZ		= ((halfLength - (haveRoundedTop ? topLength : 0)) - (halfLength - (haveRoundedBottom ? bottomLength : 0))) * -0.5f;
            //	haveCenter ? ((vertices[firstTopSide].z + vertices[firstBottomSide].z) * 0.5f) : 0;

            var topPoint	= new Vector3(0, height			, midZ);
            var bottomPoint = new Vector3(0, 0				, midZ);
            var frontPoint	= new Vector3(0, midY,  halfLength);
            var backPoint	= new Vector3(0, midY, -halfLength);
            var leftPoint	= new Vector3(diameter *  0.5f, midY, midZ);
            var rightPoint	= new Vector3(diameter * -0.5f, midY, midZ);

            {
                {
                    var isTopBackfaced		= handles.IsSufaceBackFaced(topPoint, upVector);

                    handles.backfaced = isTopBackfaced;
                    handles.DoDirectionHandle(ref topPoint, upVector);
                    var topHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;
                    //if (settings.haveRoundedTop)
                    {
                        var thickness = topHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                        handles.color = handles.GetStateColor(handles.color, topHasFocus, true);
                        handles.DrawLineLoop(vertices, sides, sides, lineMode: LineMode.NoZTest, thickness: thickness);
                        if (haveRoundedTop)
                            handles.DrawLine(vertices[sides + firstTopSide   ], vertices[sides + lastTopSide   ], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            handles.DrawLine(vertices[sides + firstBottomSide], vertices[sides + lastBottomSide], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);

                        handles.color = handles.GetStateColor(handles.color, topHasFocus, false);
                        handles.DrawLineLoop(vertices, sides, sides, lineMode: LineMode.ZTest,   thickness: thickness);
                        if (haveRoundedTop)
                            handles.DrawLine(vertices[sides + firstTopSide   ], vertices[sides + lastTopSide   ], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            handles.DrawLine(vertices[sides + firstBottomSide], vertices[sides + lastBottomSide], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                    }
                }
                
                {
                    var isBottomBackfaced	= handles.IsSufaceBackFaced(bottomPoint, -upVector);

                    handles.backfaced = isBottomBackfaced;
                    handles.DoDirectionHandle(ref bottomPoint, -upVector);
                    var bottomHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;
                    //if (settings.haveRoundedBottom)
                    {
                        var thickness = bottomHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                        handles.color = handles.GetStateColor(baseColor, bottomHasFocus, true);
                        handles.DrawLineLoop(vertices,     0, sides, lineMode: LineMode.NoZTest, thickness: thickness);
                        if (haveRoundedTop)
                            handles.DrawLine(vertices[firstTopSide   ], vertices[lastTopSide   ], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            handles.DrawLine(vertices[firstBottomSide], vertices[lastBottomSide], lineMode: LineMode.NoZTest, thickness: kVertLineThickness);

                        handles.color = handles.GetStateColor(baseColor, bottomHasFocus, false);
                        handles.DrawLineLoop(vertices,     0, sides, lineMode: LineMode.ZTest,   thickness: thickness);
                        if (haveRoundedTop)
                            handles.DrawLine(vertices[firstTopSide   ], vertices[lastTopSide   ], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                        if (haveRoundedBottom && haveCenter)
                            handles.DrawLine(vertices[firstBottomSide], vertices[lastBottomSide], lineMode: LineMode.ZTest, thickness: kVertLineThickness);
                    }
                }

                {
                    var isTopBackfaced		= handles.IsSufaceBackFaced(frontPoint, forwardVector);
                    
                    handles.backfaced = isTopBackfaced;
                    handles.DoDirectionHandle(ref frontPoint, forwardVector);
                    handles.backfaced = false;
                }
                
                {
                    var isBottomBackfaced	= handles.IsSufaceBackFaced(backPoint, -forwardVector);
                    
                    handles.backfaced = isBottomBackfaced;
                    handles.DoDirectionHandle(ref backPoint, -forwardVector);
                    handles.backfaced = false;
                }

                {
                    var isTopBackfaced		= handles.IsSufaceBackFaced(leftPoint, rightVector);
                    
                    handles.backfaced = isTopBackfaced;                    
                    handles.DoDirectionHandle(ref leftPoint, rightVector);
                    handles.backfaced = false;
                }
                
                {
                    var isBottomBackfaced	= handles.IsSufaceBackFaced(rightPoint, -rightVector);
                    
                    handles.backfaced = isBottomBackfaced;
                    handles.DoDirectionHandle(ref rightPoint, -rightVector);
                    handles.backfaced = false;
                }
            }
            if (handles.modified)
            {
                settings.height		= topPoint.y - bottomPoint.y;
                settings.length		= math.max(0, frontPoint.z - backPoint.z);
                settings.width	= leftPoint.x - rightPoint.x;
                // TODO: handle sizing in some directions (needs to modify transformation?)
            }
        }
        #endregion

        public bool HasValidState()
        {
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}