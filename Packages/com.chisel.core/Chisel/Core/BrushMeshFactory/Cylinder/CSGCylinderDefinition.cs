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

namespace Chisel.Core
{
    [Serializable]
    public enum CylinderShapeType
    {
        Cylinder,
        Cone,
        ConicalFrustum
    }

    [Serializable]
    public struct CSGCircleDefinition
    {
        public CSGCircleDefinition(float diameterX, float diameterZ, float height) { this.diameterX = diameterX; this.diameterZ = diameterZ; this.height = height; }
        public CSGCircleDefinition(float diameter, float height) { this.diameterX = diameter; this.diameterZ = diameter; this.height = height; }

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
    public struct CSGCylinderDefinition
    {
        public CSGCircleDefinition  top;
        public CSGCircleDefinition  bottom;
        public bool                 isEllipsoid;
        public CylinderShapeType    type;
        public uint                 smoothingGroup;
        public int                  sides;

        [AngleValue]
        public float rotation;

        public ChiselBrushMaterial[] brushMaterials;
        public SurfaceDescription[]  surfaceDescriptions;

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
            brushMaterials = null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            top.Validate();
            bottom.Validate();

            sides = Mathf.Max(3, sides);


            if (brushMaterials == null ||
               brushMaterials.Length != 3)
            {
                var defaultRenderMaterial = CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial = CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new ChiselBrushMaterial[3];
                for (int i = 0; i < 3; i++) // Note: sides share same material
                    brushMaterials[i] = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
            }

            // TODO: handle existing surfaces better
            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != (2 + sides))
            {
                var surfaceFlags = CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[2 + sides];

                UVMatrix uv0;
                // Top plane
                uv0 = UVMatrix.identity;
                uv0.U.w = 0.5f;
                uv0.V.w = 0.5f;
                surfaceDescriptions[0] = new SurfaceDescription { UV0 = uv0, surfaceFlags = surfaceFlags, smoothingGroup = 0 };

                // Bottom plane
                uv0 = UVMatrix.identity;
                uv0.U.w = 0.5f;
                uv0.V.w = 0.5f;
                surfaceDescriptions[1] = new SurfaceDescription { UV0 = uv0, surfaceFlags = surfaceFlags, smoothingGroup = 0 };


                float radius = top.diameterX * 0.5f;
                float angle = (360.0f / sides);
                float sideLength = (2 * Mathf.Sin((angle / 2.0f) * Mathf.Deg2Rad)) * radius;

                // Side planes
                for (int i = 2; i < 2 + sides; i++)
                {
                    uv0 = UVMatrix.identity;
                    uv0.U.w = ((i - 2) + 0.5f) * sideLength;
                    // TODO: align with bottom
                    //uv0.V.w = 0.5f;
                    surfaceDescriptions[i] = new SurfaceDescription { UV0 = uv0, surfaceFlags = surfaceFlags, smoothingGroup = smoothingGroup };
                }
            }
        }
    }
}