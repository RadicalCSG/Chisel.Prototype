using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    [Serializable]
    public enum CylinderShapeType : byte
    {
        Cylinder,
        Cone,
        ConicalFrustum
    }

    [Serializable]
    public struct ChiselCircleDefinition
    {
        public ChiselCircleDefinition(float diameterX, float diameterZ, float height) { this.diameterX = diameterX; this.diameterZ = diameterZ; this.height = height; }
        public ChiselCircleDefinition(float diameter, float height) { this.diameterX = diameter; this.diameterZ = diameter; this.height = height; }

        [DistanceValue] public float diameterX;
        [DistanceValue] public float diameterZ;
        [DistanceValue] public float height;

        public void Reset()
        {
            height = 0.0f;
            diameterX = 1.0f;
            diameterZ = 1.0f;
        }

        public void Validate()
        {
        }
    }

    [Serializable]
    public struct ChiselCylinderDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Cylinder";

        public ChiselCircleDefinition  top;
        public ChiselCircleDefinition  bottom;
        public bool                 isEllipsoid;
        public CylinderShapeType    type;
        public uint                 smoothingGroup;
        public int                  sides;

        [AngleValue]
        public float rotation;

        [NamedItems("Top", "Bottom", overflow = "Side {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;

        public float TopDiameterX
        {
            get { return top.diameterX; }
            set
            {
                if (value == top.diameterX)
                    return;

                top.diameterX = value;
                if (!isEllipsoid)
                    top.diameterZ = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    bottom.diameterX = value;
                    if (!isEllipsoid)
                        bottom.diameterZ = value;
                }
            }
        }

        public float TopDiameterZ
        {
            get { return top.diameterZ; }
            set
            {
                if (value == top.diameterZ)
                    return;

                top.diameterZ = value;
                if (!isEllipsoid)
                    top.diameterX = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    bottom.diameterZ = value;
                    if (!isEllipsoid)
                        bottom.diameterX = value;
                }
            }
        }

        public float Diameter
        {
            get { return bottom.diameterX; }
            set
            {
                if (value == bottom.diameterX)
                    return;

                bottom.diameterX = value;
                top.diameterX = value;
                bottom.diameterZ = value;
                top.diameterZ = value;
            }
        }

        public float DiameterX
        {
            get { return bottom.diameterX; }
            set
            {
                if (value == bottom.diameterX)
                    return;

                bottom.diameterX = value;
                top.diameterX = value;
                if (!isEllipsoid)
                {
                    bottom.diameterZ = value;
                    top.diameterZ = value;
                }
            }
        }

        public float DiameterZ
        {
            get { return bottom.diameterZ; }
            set
            {
                if (value == bottom.diameterZ)
                    return;

                bottom.diameterZ = value;
                top.diameterZ = value;
                if (!isEllipsoid)
                {
                    bottom.diameterX = value;
                    top.diameterX = value;
                }
            }
        }

        public float BottomDiameterX
        {
            get { return bottom.diameterX; }
            set
            {
                if (value == bottom.diameterX)
                    return;

                bottom.diameterX = value;
                if (!isEllipsoid)
                    bottom.diameterZ = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    top.diameterX = value;
                    if (!isEllipsoid)
                        top.diameterZ = value;
                }
            }
        }

        public float BottomDiameterZ
        {
            get { return bottom.diameterZ; }
            set
            {
                if (value == bottom.diameterZ)
                    return;

                bottom.diameterZ = value;
                if (!isEllipsoid)
                    bottom.diameterX = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    top.diameterZ = value;
                    if (!isEllipsoid)
                        top.diameterX = value;
                }
            }
        }

        public void Reset()
        {
            top.Reset();
            bottom.Reset();

            top.height = 1.0f;
            bottom.height = 0.0f;
            rotation = 0.0f;
            isEllipsoid = false;
            sides = 16;
            smoothingGroup = 1;
            type = CylinderShapeType.Cylinder;

            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            top.Validate();
            bottom.Validate();

            sides = Mathf.Max(3, sides);

            if (surfaceDefinition.EnsureSize(2 + sides))
            {
                // Top plane
                surfaceDefinition.surfaces[0].surfaceDescription.UV0 = UVMatrix.centered;

                // Bottom plane
                surfaceDefinition.surfaces[1].surfaceDescription.UV0 = UVMatrix.centered;
                
                float radius = top.diameterX * 0.5f;
                float angle = (360.0f / sides);
                float sideLength = (2 * Mathf.Sin((angle / 2.0f) * Mathf.Deg2Rad)) * radius;

                // Side planes
                for (int i = 2; i < 2 + sides; i++)
                {
                    var uv0 = UVMatrix.identity;
                    uv0.U.w = ((i - 2) + 0.5f) * sideLength;
                    // TODO: align with bottom
                    //uv0.V.w = 0.5f;
                    surfaceDefinition.surfaces[i].surfaceDescription.UV0 = uv0;
                    surfaceDefinition.surfaces[i].surfaceDescription.smoothingGroup = smoothingGroup;
                }
            }
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            Profiler.BeginSample("GenerateCylinder");
            try
            {
                return BrushMeshFactory.GenerateCylinder(ref brushContainer, ref this);
            }
            finally
            {
                Profiler.EndSample();
            }
        }
    }
}