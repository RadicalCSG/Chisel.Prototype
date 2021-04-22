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
    public struct ChiselStadiumDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Stadium";

        internal const float kNoCenterEpsilon           = 0.0001f;

        public const float	kMinDiameter				= 0.01f;
        public const float	kMinLength					= 0.01f;
        public const float	kMinHeight					= 0.01f;
        
        public const float	kDefaultHeight				= 1.0f;
        public const float	kDefaultLength				= 1.0f;
        public const float	kDefaultTopLength			= 0.25f;
        public const float	kDefaultBottomLength		= 0.25f;
        public const float	kDefaultWidth			    = 1.0f;
        
        public const int	kDefaultTopSides			= 4;
        public const int	SidesVertices				= 4;
        
        public float                width;        
        public float                height;
        public float                length;
        public float                topLength;
        public float                bottomLength;
        
        // TODO: better naming
        public int                  topSides;
        public int                  bottomSides;
        
        public int					sides				{ get { return (haveCenter ? 2 : 0) + math.max(topSides, 1) + math.max(bottomSides, 1); } }
        public int					firstTopSide		{ get { return 0; } }
        public int					lastTopSide			{ get { return math.max(topSides, 1); } }
        public int					firstBottomSide		{ get { return lastTopSide + 1; } }
        public int					lastBottomSide		{ get { return sides - 1; } }

        public bool					haveRoundedTop		{ get { return (topLength    > 0) && (topSides    > 1); } }
        public bool					haveRoundedBottom	{ get { return (bottomLength > 0) && (bottomSides > 1); } }
        public bool					haveCenter			{ get { return (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= kNoCenterEpsilon; } }


        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            width			    = kDefaultWidth;

            height				= kDefaultHeight;

            length				= kDefaultLength;
            topLength			= kDefaultTopLength;
            bottomLength		= kDefaultBottomLength;
            
            topSides			= kDefaultTopSides;
            bottomSides			= SidesVertices;
        }

        int Sides { get { return 2 + math.max(topSides, 1) + math.max(bottomSides, 1); } }

        public int RequiredSurfaceCount { get { return 2 + Sides; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            topLength	    = math.max(topLength,    0);
            bottomLength	= math.max(bottomLength, 0);
            length			= math.max(math.abs(length), (haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0));
            length			= math.max(math.abs(length), kMinLength);
            
            height			= math.max(math.abs(height), kMinHeight);
            width			= math.max(math.abs(width), kMinDiameter);
            
            topSides		= math.max(topSides,	 1);
            bottomSides		= math.max(bottomSides, 1);
        }

        [BurstCompile(CompileSynchronously = true)]
        public JobHandle Generate(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, ref CSGTreeNode node, int userID, CSGOperationType operation)
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

            if (!BrushMeshFactory.GenerateStadium(width, height, length,
                                                    topLength, topSides,
                                                    bottomLength, bottomSides,
                                                    in surfaceDefinitionBlob, out var brushMesh, Allocator.Persistent))
            {
                brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                return default;
            }

            brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
            return default;
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
            var sides				= definition.sides;
            var topSides			= math.max(definition.topSides, 1) + 1;
            var bottomSides			= math.max(definition.bottomSides, 1) + 1;

            var haveRoundedTop		= definition.haveRoundedTop;
            var haveRoundedBottom	= definition.haveRoundedBottom;
            var haveCenter			= definition.haveCenter;
            //renderer.DrawLineLoop(vertices,     0, sides, lineMode: lineMode, thickness: kCapLineThickness);
            //renderer.DrawLineLoop(vertices, sides, sides, lineMode: lineMode, thickness: kCapLineThickness);

            var firstTopSide = definition.firstTopSide;
            var lastTopSide  = definition.lastTopSide;
            for (int k = firstTopSide; k <= lastTopSide; k++)
            {
                var sideLine	= !haveRoundedTop || (k == firstTopSide) || (k == lastTopSide);
                var thickness	= (sideLine ? kSideLineThickness : kVertLineThickness);
                var dashSize	= (sideLine ? 0                  : kLineDash);
                renderer.DrawLine(vertices[k], vertices[sides + k], lineMode: lineMode, thickness: thickness, dashSize: dashSize);
            }
            
            var firstBottomSide = definition.firstBottomSide;
            var lastBottomSide  = definition.lastBottomSide;
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

            var height		        = this.height;
            var length		        = this.length;
            var diameter	        = this.width;
            var sides		        = this.sides;
            
            var firstTopSide	    = this.firstTopSide;
            var lastTopSide		    = this.lastTopSide;
            var firstBottomSide     = this.firstBottomSide;
            var lastBottomSide      = this.lastBottomSide;

            var haveRoundedTop		= this.haveRoundedTop;
            var haveRoundedBottom	= this.haveRoundedBottom;
            var haveCenter			= this.haveCenter;
            var topLength			= this.topLength;
            var bottomLength		= this.bottomLength;
            

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
                    //if (this.haveRoundedTop)
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
                    //if (this.haveRoundedBottom)
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
                this.height		= topPoint.y - bottomPoint.y;
                this.length		= math.max(0, frontPoint.z - backPoint.z);
                this.width	= leftPoint.x - rightPoint.x;
                // TODO: handle sizing in some directions (needs to modify transformation?)
            }
        }
        #endregion

        public bool HasValidState()
        {
            return false;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}