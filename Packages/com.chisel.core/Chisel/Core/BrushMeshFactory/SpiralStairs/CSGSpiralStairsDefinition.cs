using System;
using System.Linq;
using System.Collections.Generic;
using Bounds  = UnityEngine.Bounds;
using Mathf   = UnityEngine.Mathf;
using Vector3 = UnityEngine.Vector3;
using UnitySceneExtensions;

namespace Chisel.Core
{
    // https://www.archdaily.com/896537/how-to-calculate-spiral-staircase-dimensions-and-designs
    // http://www.zhitov.ru/en/spiral_stairs/
    // https://easystair.net/en/spiral-staircase.php
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DTs7Bc10UxKpWM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    // https://www.google.com/imgres?imgurl=https%3A%2F%2Fwww.visualarq.com%2Fwp-content%2Fuploads%2Fsites%2F2%2F2014%2F07%2FSpiral-stair-landings.png&imgrefurl=https%3A%2F%2Fwww.visualarq.com%2Fsupport%2Ftips%2Fhow-can-i-create-spiral-stairs-can-i-add-landings%2F&docid=Tk82BDe0l2fZmM&tbnid=DTs7Bc10UxKpWM%3A&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880&h=656&client=firefox-b-ab&bih=625&biw=1649&q=spiral%20stairs%20parameters&ved=0ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg&iact=mrc&uact=8#h=656&imgdii=DPwskqkaN7e_wM:&vet=10ahUKEwiL8_nBtLXeAhWC-qQKHUO4CBQQMwhCKAIwAg..i&w=880
    [Serializable]
    public struct CSGSpiralStairsDefinition
    {
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

        public ChiselBrushMaterial[]    brushMaterials;
        public SurfaceDescription[]     surfaceDescriptions;

        public int StepCount
        {
            get
            {
                const float kSmudgeValue = 0.0001f;
                return Mathf.Max(1,
                          Mathf.FloorToInt((Mathf.Abs(height) + kSmudgeValue) / stepHeight));
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
            brushMaterials           = null;
            surfaceDescriptions     = null;
        }

        public void Validate()
        {
            stepHeight		= Mathf.Max(kMinStepHeight, stepHeight);
            
            innerDiameter	= Mathf.Min(outerDiameter - kMinStairsDepth,  innerDiameter);
            innerDiameter	= Mathf.Max(kMinInnerDiameter,  innerDiameter);
            outerDiameter	= Mathf.Max(innerDiameter + kMinStairsDepth,  outerDiameter);
            outerDiameter	= Mathf.Max(kMinOuterDiameter,  outerDiameter);
            height			= Mathf.Max(stepHeight, Mathf.Abs(height)) * (height < 0 ? -1 : 1);
            treadHeight		= Mathf.Max(0, treadHeight);
            nosingDepth		= Mathf.Max(0, nosingDepth);
            nosingWidth		= Mathf.Max(0, nosingWidth);

            riserDepth		= Mathf.Max(kMinRiserDepth, riserDepth);

            rotation		= Mathf.Max(kMinRotation, Mathf.Abs(rotation)) * (rotation < 0 ? -1 : 1);

            innerSegments	= Mathf.Max(kMinSegments, innerSegments);
            outerSegments	= Mathf.Max(kMinSegments, outerSegments);
            
            if (brushMaterials == null ||
                brushMaterials.Length != 6)
            {
                var defaultRenderMaterial  = CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial = CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new ChiselBrushMaterial[6];
                for (int i = 0; i < 6; i++) // Note: sides share same material
                    brushMaterials[i] = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
            }

            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != 6)
            {
                var surfaceFlags = CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[6];
                for (int i = 0; i < 6; i++)
                {
                    surfaceDescriptions[i] = new SurfaceDescription { surfaceFlags = surfaceFlags, UV0 = UVMatrix.centered, smoothingGroup = bottomSmoothingGroup };
                }
            } else
            {
                for (int i = 0; i < 6; i++)
                {
                    surfaceDescriptions[i].smoothingGroup = bottomSmoothingGroup;
                }
            }
        }
    }
}