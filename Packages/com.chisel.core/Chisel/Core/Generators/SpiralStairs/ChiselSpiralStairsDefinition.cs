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
using Color   = UnityEngine.Color;

namespace Chisel.Core
{
    // https://www.archdaily.com/896537/how-to-calculate-spiral-staircase-dimensions-and-designs
    // http://www.zhitov.ru/en/spiral_stairs/
    // https://easystair.net/en/spiral-staircase.php
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DTs7Bc10UxKpWM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DPwskqkaN7e_wM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    [Serializable]
    public struct ChiselSpiralStairsDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Spiral Stairs";

        public const int kTreadTopSurface       = 0;
        public const int kTreadBottomSurface    = 1;
        public const int kTreadFrontSurface     = 2;
        public const int kTreadBackSurface      = 3;
        public const int kRiserFrontSurface     = 4;
        public const int kRiserBackSurface      = 5;
        public const int kInnerSurface          = 6;
        public const int kOuterSurface          = 7;


        public const float	kMinStepHeight			= 0.01f;
        public const float  kMinStairsDepth         = 0.1f;
        public const float  kMinRiserDepth          = 0.01f;
        public const float	kMinRotation			= 15;
        public const int    kMinSegments            = 3;
        public const float	kMinInnerDiameter		= 0.00f;
        public const float	kMinOuterDiameter		= 0.01f;
        
        public const float	kDefaultStepHeight      = 0.20f;
        public const float	kDefaultTreadHeight     = 0.02f;
        public const float	kDefaultNosingDepth     = 0.02f;
        public const float	kDefaultNosingWidth     = 0.01f;

        public const float	kDefaultInnerDiameter	= 0.25f;
        public const float	kDefaultOuterDiameter	= 2;
        public const float	kDefaultHeight          = 1;
        
        public const int	kDefaultInnerSegments	= 8;
        public const int	kDefaultOuterSegments	= 16;
        
        public const float	kDefaultStartAngle		= 0;
        public const float	kDefaultRotation		= 180;

        public const float  kDefaultRiserDepth      = 0.03f;
        
        [DistanceValue] public Vector3  origin;
        [DistanceValue] public float	height;
        [DistanceValue] public float    outerDiameter;
        [DistanceValue] public float    innerDiameter;
        [DistanceValue] public float    stepHeight;
        [DistanceValue] public float    treadHeight;
        [DistanceValue] public float    nosingDepth;
        [DistanceValue] public float    nosingWidth;
        [DistanceValue] public float    riserDepth;
        [AngleValue   ] public float    startAngle;
        [AngleValue   ] public float    rotation; // can be >360 degrees
        public int					    innerSegments;
        public int					    outerSegments;
        public StairsRiserType		    riserType;

        public uint					    bottomSmoothingGroup;

        public int StepCount
        {
            get
            {
                const float kSmudgeValue = 0.0001f;
                return math.max(1, (int)math.floor((math.abs(height) + kSmudgeValue) / stepHeight));
            }
        }
        
        public float AnglePerStep
        {
            get
            {
                return rotation / StepCount;
            }
        }

        public void Reset()
        {
            origin		    = Vector3.zero;

            stepHeight	    = kDefaultStepHeight;
        
            treadHeight     = kDefaultTreadHeight;
            nosingDepth	    = kDefaultNosingDepth;
            nosingWidth	    = kDefaultNosingWidth;
                    
            innerDiameter   = kDefaultInnerDiameter;
            outerDiameter   = kDefaultOuterDiameter;
            height		    = kDefaultHeight;

            startAngle	    = kDefaultStartAngle;
            rotation	    = kDefaultRotation;
            
            innerSegments   = kDefaultInnerSegments;
            outerSegments   = kDefaultOuterSegments;

            riserType	    = StairsRiserType.ThickRiser;
            riserDepth	    = kDefaultRiserDepth;

            bottomSmoothingGroup    = 0;
        }

        public int RequiredSurfaceCount { get { return 8; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            stepHeight		= math.max(kMinStepHeight, stepHeight);
            
            innerDiameter	= math.min(outerDiameter - kMinStairsDepth,  innerDiameter);
            innerDiameter	= math.max(kMinInnerDiameter,  innerDiameter);
            outerDiameter	= math.max(innerDiameter + kMinStairsDepth,  outerDiameter);
            outerDiameter	= math.max(kMinOuterDiameter,  outerDiameter);
            height			= math.max(stepHeight, math.abs(height)) * (height < 0 ? -1 : 1);
            treadHeight		= math.max(0, treadHeight);
            nosingDepth		= math.max(0, nosingDepth);
            nosingWidth		= math.max(0, nosingWidth);

            riserDepth		= math.max(kMinRiserDepth, riserDepth);

            rotation		= math.max(kMinRotation, math.abs(rotation)) * (rotation < 0 ? -1 : 1);

            innerSegments	= math.max(kMinSegments, innerSegments);
            outerSegments	= math.max(kMinSegments, outerSegments);
        }

        public JobHandle Generate(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, ref CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var branch = (CSGTreeBranch)node;
            if (!branch.Valid)
            {
                node = branch = CSGTreeBranch.Create(userID: userID, operation: operation);
            } else
            {
                if (branch.Operation != operation)
                    branch.Operation = operation;
            }

            // TODO: expose this to user
            var smoothSubDivisions		= 3;

            const float kEpsilon = 0.001f;
            var haveInnerCylinder	    = (innerDiameter >= kEpsilon);
            var haveTread		        = (nosingDepth < kEpsilon) ? false : (treadHeight >= kEpsilon);

            var cylinderSubMeshCount	= haveInnerCylinder ? 2 : 1;
            var subMeshPerRiser			= (riserType == StairsRiserType.None  ) ? 0 : 
                                          (riserType == StairsRiserType.Smooth) ? (2 * smoothSubDivisions)
                                                                                : 1;
            var riserSubMeshCount		= (StepCount * subMeshPerRiser) + ((riserType == StairsRiserType.None  ) ? 0 : cylinderSubMeshCount);
            var treadSubMeshCount		= (haveTread ? StepCount + cylinderSubMeshCount : 0);
            var requiredSubMeshCount    = (treadSubMeshCount + riserSubMeshCount);

            if (requiredSubMeshCount == 0)
            {
                this.ClearBrushes(branch);
                return default;
            }

            if (branch.Count != requiredSubMeshCount)
                this.BuildBrushes(branch, requiredSubMeshCount);
            
            var haveRiser		= riserType != StairsRiserType.None;
            var treadStart      = !haveRiser ? 0 : riserSubMeshCount;

            using (var brushMeshes = new NativeArray<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp))
            {
                if (!BrushMeshFactory.GenerateSpiralStairs(brushMeshes, ref this, in surfaceDefinitionBlob, Allocator.Persistent))
                {
                    this.ClearBrushes(branch);
                    return default;
                }

                for (int i = 0; i < requiredSubMeshCount; i++)
                {
                    var brush = (CSGTreeBrush)branch[i];
                    brush.LocalTransformation = float4x4.identity;
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshes[i]) };
                }

                {
                    var subMeshIndex = treadStart - cylinderSubMeshCount;
                    var brush = (CSGTreeBrush)branch[subMeshIndex];
                    brush.Operation = CSGOperationType.Intersecting;
            
                    subMeshIndex = requiredSubMeshCount - cylinderSubMeshCount;
                    brush = (CSGTreeBrush)branch[subMeshIndex];
                    brush.Operation = CSGOperationType.Intersecting;
                }

                if (haveInnerCylinder)
                {
                    var subMeshIndex = treadStart - 1;
                    var brush = (CSGTreeBrush)branch[subMeshIndex];
                    brush.Operation = CSGOperationType.Subtractive;

                    subMeshIndex = requiredSubMeshCount - 1;
                    brush = (CSGTreeBrush)branch[subMeshIndex];
                    brush.Operation = CSGOperationType.Subtractive;
                }

                return default;
            }
        }

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        Vector3[] innerVertices;
        Vector3[] outerVertices;

        public void OnEdit(IChiselHandles handles)
        {
            var normal					= Vector3.up;
            var topDirection			= Vector3.forward;
            var lowDirection			= Vector3.forward;

            var originalOuterDiameter	= this.outerDiameter;
            var originalInnerDiameter	= this.innerDiameter;
            var originalStartAngle		= this.startAngle;
            var originalStepHeight		= this.stepHeight;
            var originalRotation		= this.rotation;
            var originalHeight			= this.height;
            var originalOrigin			= this.origin;
            var cylinderTop				= new BrushMeshFactory.ChiselCircleDefinition { diameterX = 1, diameterZ = 1, height = originalOrigin.y + originalHeight };
            var cylinderLow				= new BrushMeshFactory.ChiselCircleDefinition { diameterX = 1, diameterZ = 1, height = originalOrigin.y };
            var originalTopPoint		= normal * cylinderTop.height;
            var originalLowPoint		= normal * cylinderLow.height;
            var originalMidPoint		= (originalTopPoint + originalLowPoint) * 0.5f;
                    
            var outerDiameter		= originalOuterDiameter;
            var innerDiameter		= originalInnerDiameter;
            var topPoint			= originalTopPoint;
            var lowPoint			= originalLowPoint;
            var midPoint			= originalMidPoint;
            var startAngle			= originalStartAngle;
            var rotation			= originalRotation;

            {
                var currRotation = startAngle + rotation;
                handles.DoRotatableLineHandle(ref startAngle  , lowPoint, outerDiameter * 0.5f, normal, lowDirection, Vector3.Cross(normal, lowDirection));
                handles.DoRotatableLineHandle(ref currRotation, topPoint, outerDiameter * 0.5f, normal, topDirection, Vector3.Cross(normal, topDirection));
                if (handles.modified)
                    rotation = currRotation - startAngle;


                // TODO: properly show things as backfaced
                // TODO: temporarily show inner or outer diameter as disabled when resizing one or the other
                // TODO: FIXME: why aren't there any arrows?
                handles.DoDirectionHandle(ref topPoint, normal, snappingStep: originalStepHeight);
                topPoint.y		= math.max(lowPoint.y + originalStepHeight, topPoint.y);
                handles.DoDirectionHandle(ref lowPoint, -normal, snappingStep: originalStepHeight);
                lowPoint.y		= math.min(topPoint.y - originalStepHeight, lowPoint.y);

                float minOuterDiameter = innerDiameter + ChiselSpiralStairsDefinition.kMinStairsDepth;
                { 
                    var outerRadius = outerDiameter * 0.5f;
                    handles.DoRadiusHandle(ref outerRadius, Vector3.up, topPoint, renderDisc: false);
                    handles.DoRadiusHandle(ref outerRadius, Vector3.up, lowPoint, renderDisc: false);
                    outerDiameter = math.max(minOuterDiameter, outerRadius * 2.0f);
                }
                        
                float maxInnerDiameter = outerDiameter - ChiselSpiralStairsDefinition.kMinStairsDepth;
                { 
                    var innerRadius = innerDiameter * 0.5f;
                    handles.DoRadiusHandle(ref innerRadius, Vector3.up, midPoint, renderDisc: false);
                    innerDiameter = math.min(maxInnerDiameter, innerRadius * 2.0f);
                }



                // TODO: somehow put this into a separate renderer
                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalInnerDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, this.innerSegments, ref innerVertices);

                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalOuterDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, this.outerSegments, ref outerVertices);
                
                var originalColor	= handles.color;
                var color			= handles.color;
                var outlineColor	= Color.black;
                outlineColor.a = color.a;

                handles.color = outlineColor;
                {
                    var sides = this.outerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = outerVertices[i];
                        var t1 = outerVertices[j];
                        var b0 = outerVertices[i + sides];
                        var b1 = outerVertices[j + sides];

                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);
                    }
                }
                {
                    var sides = this.innerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = innerVertices[i];
                        var t1 = innerVertices[j];
                        var b0 = innerVertices[i + sides];
                        var b1 = innerVertices[j + sides];

                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);
                    }
                }

                handles.color = originalColor;
                {
                    var sides = this.outerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = outerVertices[i];
                        var t1 = outerVertices[j];
                        var b0 = outerVertices[i + sides];
                        var b1 = outerVertices[j + sides];

                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);
                    }
                }
                {
                    var sides = this.innerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = innerVertices[i];
                        var t1 = innerVertices[j];
                        var b0 = innerVertices[i + sides];
                        var b1 = innerVertices[j + sides];


                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);

                        var m0 = (t0 + b0) * 0.5f;
                        var m1 = (t1 + b1) * 0.5f;
                        handles.DrawLine(m0, m1, thickness: 2.0f);
                    }
                }
            }
            if (handles.modified)
            {
                this.outerDiameter = outerDiameter;
                this.innerDiameter = innerDiameter;
                this.startAngle    = startAngle;
                this.rotation	   = rotation;

                if (topPoint != originalTopPoint)
                    this.height = topPoint.y - lowPoint.y;

                if (lowPoint != originalLowPoint)
                {
                    this.height	= topPoint.y - lowPoint.y;
                    var newOrigin = originalOrigin;
                    newOrigin.y += lowPoint.y - originalLowPoint.y;
                    this.origin = newOrigin;
                }
            }
        }
        #endregion

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}