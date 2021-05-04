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
    [Serializable]
    public struct ChiselSpiralStairs : IBranchGenerator
    {
        public readonly static ChiselSpiralStairs DefaultValues = new ChiselSpiralStairs
        {
            origin		    = float3.zero,

            stepHeight	    = 0.20f,                          

            treadHeight     = 0.02f,
            nosingDepth	    = 0.02f,
            nosingWidth	    = 0.01f,

            innerDiameter   = 0.25f,
            outerDiameter   = 2,
            height		    = 1,

            startAngle	    = 0,
            rotation	    = 180,

            innerSegments   = 8,
            outerSegments   = 8,

            riserType	    = StairsRiserType.ThickRiser,
            riserDepth	    = 0.03f,

            bottomSmoothingGroup    = 0
        };

        // TODO: expose this to user
        const int smoothSubDivisions = 3;

        [DistanceValue] public float3   origin;
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

        #region Properties

        const float kSmudgeValue = 0.0001f;

        public int StepCount
        {
            get
            {
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

        const float kEpsilon = 0.001f;

        internal bool HaveInnerCylinder => (innerDiameter >= kEpsilon);
        internal bool HaveTread => (nosingDepth < kEpsilon) ? false : (treadHeight >= kEpsilon);

        internal int CylinderSubMeshCount => HaveInnerCylinder ? 2 : 1;
        internal int SubMeshPerRiser => (riserType == StairsRiserType.None) ? 0 :
                                               (riserType == StairsRiserType.Smooth) ? (2 * smoothSubDivisions)
                                                                            : 1;
        internal int RiserSubMeshCount => (StepCount * SubMeshPerRiser) + ((riserType == StairsRiserType.None) ? 0 : CylinderSubMeshCount);
        internal int TreadSubMeshCount => (HaveTread ? StepCount + CylinderSubMeshCount : 0);
        internal int RequiredSubMeshCount => (TreadSubMeshCount + RiserSubMeshCount);


        internal bool HaveRiser => riserType != StairsRiserType.None;
        internal int TreadStart => !HaveRiser ? 0 : RiserSubMeshCount;
        #endregion

        #region Generate
        [BurstCompile]
        public int PrepareAndCountRequiredBrushMeshes()
        {
            return RequiredSubMeshCount;
        }

        [BurstCompile]
        public bool GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            if (!BrushMeshFactory.GenerateSpiralStairs(brushMeshes,
                                                       ref this,
                                                       in surfaceDefinitionBlob,
                                                       Allocator.Persistent))
            {
                for (int i = 0; i < brushMeshes.Length; i++)
                {
                    if (brushMeshes[i].IsCreated)
                        brushMeshes[i].Dispose();
                }
                return false;
            }
            return true;
        }

        public void Dispose() {}

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch)
        {
            // TODO: somehow make this possible to set up from within the job without requiring the treeBranches/treeBrushes
            {
                var subMeshIndex = TreadStart - CylinderSubMeshCount;
                var brush = (CSGTreeBrush)branch[subMeshIndex];
                brush.Operation = CSGOperationType.Intersecting;

                subMeshIndex = RequiredSubMeshCount - CylinderSubMeshCount;
                brush = (CSGTreeBrush)branch[subMeshIndex];
                brush.Operation = CSGOperationType.Intersecting;
            }

            if (HaveInnerCylinder)
            {
                var subMeshIndex = TreadStart - 1;
                var brush = (CSGTreeBrush)branch[subMeshIndex];
                brush.Operation = CSGOperationType.Subtractive;

                subMeshIndex = RequiredSubMeshCount - 1;
                brush = (CSGTreeBrush)branch[subMeshIndex];
                brush.Operation = CSGOperationType.Subtractive;
            }
        }
        #endregion

        #region Surfaces
        public enum SurfaceSides : byte
        { 
            TreadTopSurface       = 0,
            TreadBottomSurface    = 1,
            TreadFrontSurface     = 2,
            TreadBackSurface      = 3,
            RiserFrontSurface     = 4,
            RiserBackSurface      = 5,
            InnerSurface          = 6,
            OuterSurface          = 7,

            TotalSides
        }

        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 8; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }
        #endregion
        
        #region Validation
        public const float	kMinStepHeight			= 0.01f;
        public const float  kMinStairsDepth         = 0.1f;
        public const float  kMinRiserDepth          = 0.01f;
        public const float	kMinRotation			= 15;
        public const int    kMinSegments            = 3;
        public const float	kMinInnerDiameter		= 0.00f;
        public const float	kMinOuterDiameter		= 0.01f;

        public void Validate()
        {
            stepHeight      = math.max(kMinStepHeight, stepHeight);

            innerDiameter   = math.min(outerDiameter - kMinStairsDepth, innerDiameter);
            innerDiameter   = math.max(kMinInnerDiameter, innerDiameter);
            outerDiameter   = math.max(innerDiameter + kMinStairsDepth, outerDiameter);
            outerDiameter   = math.max(kMinOuterDiameter, outerDiameter);
            height          = math.max(stepHeight, math.abs(height)) * (height < 0 ? -1 : 1);
            treadHeight     = math.max(0, treadHeight);
            nosingDepth     = math.max(0, nosingDepth);
            nosingWidth     = math.max(0, nosingWidth);

            riserDepth      = math.max(kMinRiserDepth, riserDepth);

            rotation        = math.max(kMinRotation, math.abs(rotation)) * (rotation < 0 ? -1 : 1);

            innerSegments   = math.max(kMinSegments, innerSegments);
            outerSegments   = math.max(kMinSegments, outerSegments);
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultValues; }
        #endregion
    }

    // https://www.archdaily.com/896537/how-to-calculate-spiral-staircase-dimensions-and-designs
    // http://www.zhitov.ru/en/spiral_stairs/
    // https://easystair.net/en/spiral-staircase.php
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DTs7Bc10UxKpWM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DPwskqkaN7e_wM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    [Serializable]
    public class ChiselSpiralStairsDefinition : SerializedBranchGenerator<ChiselSpiralStairs>
    {
        public const string kNodeTypeName = "Spiral Stairs";

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        static Vector3[] s_InnerVertices;
        static Vector3[] s_OuterVertices;

        public override void OnEdit(IChiselHandles handles)
        {
            var normal					= Vector3.up;
            var topDirection			= Vector3.forward;
            var lowDirection			= Vector3.forward;

            var originalOuterDiameter	= settings.outerDiameter;
            var originalInnerDiameter	= settings.innerDiameter;
            var originalStartAngle		= settings.startAngle;
            var originalStepHeight		= settings.stepHeight;
            var originalRotation		= settings.rotation;
            var originalHeight			= settings.height;
            var originalOrigin			= settings.origin;
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

                float minOuterDiameter = innerDiameter + ChiselSpiralStairs.kMinStairsDepth;
                { 
                    var outerRadius = outerDiameter * 0.5f;
                    handles.DoRadiusHandle(ref outerRadius, Vector3.up, topPoint, renderDisc: false);
                    handles.DoRadiusHandle(ref outerRadius, Vector3.up, lowPoint, renderDisc: false);
                    outerDiameter = math.max(minOuterDiameter, outerRadius * 2.0f);
                }
                        
                float maxInnerDiameter = outerDiameter - ChiselSpiralStairs.kMinStairsDepth;
                { 
                    var innerRadius = innerDiameter * 0.5f;
                    handles.DoRadiusHandle(ref innerRadius, Vector3.up, midPoint, renderDisc: false);
                    innerDiameter = math.min(maxInnerDiameter, innerRadius * 2.0f);
                }



                // TODO: somehow put this into a separate renderer
                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalInnerDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, settings.innerSegments, ref s_InnerVertices);

                cylinderTop.diameterZ = cylinderTop.diameterX = cylinderLow.diameterZ = cylinderLow.diameterX = originalOuterDiameter;
                BrushMeshFactory.GetConicalFrustumVertices(cylinderLow, cylinderTop, 0, settings.outerSegments, ref s_OuterVertices);
                
                var originalColor	= handles.color;
                var color			= handles.color;
                var outlineColor	= Color.black;
                outlineColor.a = color.a;

                handles.color = outlineColor;
                {
                    var sides = settings.outerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = s_OuterVertices[i];
                        var t1 = s_OuterVertices[j];
                        var b0 = s_OuterVertices[i + sides];
                        var b1 = s_OuterVertices[j + sides];

                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);
                    }
                }
                {
                    var sides = settings.innerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = s_InnerVertices[i];
                        var t1 = s_InnerVertices[j];
                        var b0 = s_InnerVertices[i + sides];
                        var b1 = s_InnerVertices[j + sides];

                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);
                    }
                }

                handles.color = originalColor;
                {
                    var sides = settings.outerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = s_OuterVertices[i];
                        var t1 = s_OuterVertices[j];
                        var b0 = s_OuterVertices[i + sides];
                        var b1 = s_OuterVertices[j + sides];

                        handles.DrawLine(t0, b0, thickness: 1.0f);
                        handles.DrawLine(t0, t1, thickness: 1.0f);
                        handles.DrawLine(b0, b1, thickness: 1.0f);
                    }
                }
                {
                    var sides = settings.innerSegments;
                    for (int i = 0, j = sides - 1; i < sides; j = i, i++)
                    {
                        var t0 = s_InnerVertices[i];
                        var t1 = s_InnerVertices[j];
                        var b0 = s_InnerVertices[i + sides];
                        var b1 = s_InnerVertices[j + sides];


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
                settings.outerDiameter = outerDiameter;
                settings.innerDiameter = innerDiameter;
                settings.startAngle    = startAngle;
                settings.rotation	   = rotation;

                if (topPoint != originalTopPoint)
                    settings.height = topPoint.y - lowPoint.y;

                if (lowPoint != originalLowPoint)
                {
                    settings.height	= topPoint.y - lowPoint.y;
                    var newOrigin = originalOrigin;
                    newOrigin.y += lowPoint.y - originalLowPoint.y;
                    settings.origin = newOrigin;
                }
            }
        }
        #endregion
    }
}