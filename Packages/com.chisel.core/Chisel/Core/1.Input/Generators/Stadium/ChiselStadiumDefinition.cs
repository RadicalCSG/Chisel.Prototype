using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselStadium : IBrushGenerator
    {
        public readonly static ChiselStadium DefaultValues = new ChiselStadium
        {
            width			= 1.0f,
            height			= 1.0f,
            length			= 1.0f,

            topLength		= 0.25f,
            bottomLength	= 0.25f,

            topSides		= 4,
            bottomSides		= 4
        };

        public float                width;        
        public float                height;
        public float                length;
        public float                topLength;
        public float                bottomLength;
        
        // TODO: better naming
        public int                  topSides;
        public int                  bottomSides;


        #region Properties
        internal const float kNoCenterEpsilon = 0.0001f;

        internal int				Sides				{ get { return (HaveCenter ? 2 : 0) + math.max(topSides, 1) + math.max(bottomSides, 1); } }
        internal int				FirstTopSide		{ get { return 0; } }
        internal int				LastTopSide			{ get { return math.max(topSides, 1); } }
        internal int				FirstBottomSide		{ get { return LastTopSide + 1; } }
        internal int				LastBottomSide		{ get { return Sides - 1; } }

        internal bool				HaveRoundedTop		{ get { return (topLength    > 0) && (topSides    > 1); } }
        internal bool				HaveRoundedBottom	{ get { return (bottomLength > 0) && (bottomSides > 1); } }
        internal bool				HaveCenter			{ get { return (length - ((HaveRoundedTop ? topLength : 0) + (HaveRoundedBottom ? bottomLength : 0))) >= kNoCenterEpsilon; } }
        #endregion

        #region Generate
        public BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator)
        {
            if (!BrushMeshFactory.GenerateStadium(width, height, length,
                                                  topLength, topSides,
                                                  bottomLength, bottomSides,
                                                  in surfaceDefinitionBlob,
                                                  out var newBrushMesh,
                                                  allocator))
                return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2 + Sides; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion

        #region Validation
        public const float	kMinDiameter    = 0.01f;
        public const float	kMinLength	    = 0.01f;
        public const float	kMinHeight	    = 0.01f;

        public bool Validate()
        {
            topLength	    = math.max(topLength,    0);
            bottomLength	= math.max(bottomLength, 0);
            length			= math.max(math.abs(length), (HaveRoundedTop ? topLength : 0) + (HaveRoundedBottom ? bottomLength : 0));
            length			= math.max(math.abs(length), kMinLength);

            height			= math.max(math.abs(height), kMinHeight);
            width			= math.max(math.abs(width), kMinDiameter);

            topSides		= math.max(topSides,	 1);
            bottomSides	    = math.max(bottomSides, 1);
            return true;
        }

        [BurstDiscard]
        public void GetMessages(IChiselMessageHandler messages)
        {
        }
        #endregion
        
        #region Reset
        public void Reset() { this = DefaultValues; }
        #endregion
    }

    [Serializable]
    public class ChiselStadiumDefinition : SerializedBrushGenerator<ChiselStadium>
    {
        public const string kNodeTypeName = "Stadium";

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

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

        public override void OnEdit(IChiselHandles handles)
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
    }
}