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
            return BrushMeshFactory.GenerateCylinder(ref brushContainer, ref this);
        }


        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        void Init(ref CylinderHandle handle)
        {
            handle.bottomDiameterX	= this.BottomDiameterX;
            handle.bottomDiameterZ	= this.isEllipsoid ? this.BottomDiameterZ : this.BottomDiameterX;

            handle.topDiameterX		= this.TopDiameterX;
            handle.topDiameterZ		= this.isEllipsoid ? this.TopDiameterZ : this.TopDiameterX;

            handle.rotate			= Quaternion.AngleAxis(this.rotation, Vector3.up);
            handle.topXVector		= handle.rotate * Vector3.right   * handle.topDiameterX * 0.5f;
            handle.topZVector		= handle.rotate * Vector3.forward * handle.topDiameterZ * 0.5f;
            handle.bottomXVector	= handle.rotate * Vector3.right   * handle.bottomDiameterX * 0.5f;
            handle.bottomZVector	= handle.rotate * Vector3.forward * handle.bottomDiameterZ * 0.5f;
            handle.topHeight		= Vector3.up * this.top.height;
            handle.bottomHeight		= Vector3.up * this.bottom.height;
            handle.normal			= Vector3.up;

            if (!this.isEllipsoid)
            {
                handle.bottomZVector	= handle.bottomZVector.normalized * handle.bottomXVector.magnitude;
                handle.topZVector		= handle.topZVector.normalized    * handle.topXVector.magnitude;
            }

            handle.prevBottomXVector	= handle.bottomXVector;
            handle.prevBottomZVector	= handle.bottomZVector;
            handle.prevTopXVector		= handle.topXVector;
            handle.prevTopZVector		= handle.topZVector;

            handle.topPoint	   = handle.topHeight;
            handle.bottomPoint = handle.bottomHeight;

            handle.vertices = new Vector3[this.sides * 2];
        }

        struct CylinderHandle
        {
            public float bottomDiameterX;
            public float bottomDiameterZ;

            public float topDiameterX;
            public float topDiameterZ;

            public Quaternion rotate;
            public Vector3 topXVector;
            public Vector3 topZVector;
            public Vector3 bottomXVector;
            public Vector3 bottomZVector;
            public Vector3 topHeight;
            public Vector3 bottomHeight;
            public Vector3 normal;

            public Vector3 prevBottomXVector;
            public Vector3 prevTopXVector;
            public Vector3 prevBottomZVector;
            public Vector3 prevTopZVector;

            public Vector3 topPoint;
            public Vector3 bottomPoint;

            public Vector3[] vertices;
            public Vector3[] dottedVertices;
        }

        void ShowInstance(IChiselHandles handles, ref CylinderHandle temp)
        {
            ref var top		= ref this.top;
            ref var bottom	= ref this.bottom;
            ref var sides	= ref this.sides;

            if (!this.isEllipsoid)
            { 
                top.diameterZ = top.diameterX; 
                bottom.diameterZ = bottom.diameterX; 
            }

            bool prevModified = handles.modified;
            {
                switch (this.type)
                {
                    case CylinderShapeType.Cylinder:
                    {
                        if (this.isEllipsoid)
                        {
                            handles.DoRadius2DHandle(ref temp.bottomXVector, ref temp.bottomZVector, temp.topPoint,     temp.normal, renderDisc: false);
                            handles.DoRadius2DHandle(ref temp.bottomXVector, ref temp.bottomZVector, temp.bottomPoint, -temp.normal, renderDisc: false);
                        } else
                        {
                            handles.DoRadius2DHandle(ref temp.bottomXVector, temp.topPoint,     temp.normal, renderDisc: false);
                            handles.DoRadius2DHandle(ref temp.bottomXVector, temp.bottomPoint, -temp.normal, renderDisc: false);

                            temp.bottomZVector = temp.bottomXVector;
                        }
                        temp.topXVector = temp.bottomXVector;
                        temp.topZVector = temp.bottomZVector;

                        temp.bottomDiameterX = temp.bottomXVector.magnitude * 2.0f;
                        temp.bottomDiameterZ = temp.bottomZVector.magnitude * 2.0f;
                        
                        bottom.diameterX = temp.bottomDiameterX;
                        bottom.diameterZ = temp.bottomDiameterZ;

                        top.diameterX = bottom.diameterX;
                        top.diameterZ = bottom.diameterZ;
                        break;
                    }
                    case CylinderShapeType.ConicalFrustum:
                    {
                        if (this.isEllipsoid)
                        {
                            handles.DoRadius2DHandle(ref temp.topXVector,    ref temp.topZVector,    temp.topPoint,     temp.normal, renderDisc: false);
                            handles.DoRadius2DHandle(ref temp.bottomXVector, ref temp.bottomZVector, temp.bottomPoint, -temp.normal, renderDisc: false);
                        } else
                        {
                            handles.DoRadius2DHandle(ref temp.topXVector,    temp.topPoint,     temp.normal, renderDisc: false);
                            handles.DoRadius2DHandle(ref temp.bottomXVector, temp.bottomPoint, -temp.normal, renderDisc: false);

                            temp.bottomZVector = temp.bottomXVector;
                        }
                        break;
                    }
                    case CylinderShapeType.Cone:
                    {
                        if (this.isEllipsoid)
                        {
                            handles.DoRadius2DHandle(ref temp.bottomXVector, ref temp.bottomZVector, temp.bottomPoint, -temp.normal, renderDisc: false);
                        } else
                        {
                            handles.DoRadius2DHandle(ref temp.bottomXVector, temp.bottomPoint, -temp.normal, renderDisc: false);
                            temp.bottomZVector = temp.bottomXVector;
                        }
                        temp.topXVector = temp.bottomXVector;
                        temp.topZVector = temp.bottomZVector;
                        top.diameterX = 0;
                        top.diameterZ = 0;
                        break;
                    }
                }


                // TODO: add cylinder horizon "side-lines"
            }
            if (prevModified != handles.modified)
            {
                temp.topZVector.y = 0;
                temp.topXVector.y = 0;

                temp.bottomZVector.y = 0;
                temp.bottomXVector.y = 0;

                if (!this.isEllipsoid)
                {
                    if (temp.prevBottomXVector != temp.bottomXVector)
                    {
                        temp.bottomZVector = Vector3.Cross(temp.normal, temp.bottomXVector.normalized) * temp.bottomXVector.magnitude;
                    }
                    if (temp.prevTopXVector != temp.topXVector)
                    {
                        temp.topZVector = Vector3.Cross(temp.normal, temp.topXVector.normalized) * temp.topXVector.magnitude;
                    }
                }

                if (temp.prevTopXVector != temp.topXVector)
                {
                    this.rotation = GeometryMath.SignedAngle(Vector3.right, temp.topXVector.normalized, Vector3.up);
                }
                else if (temp.prevBottomXVector != temp.bottomXVector)
                {
                    this.rotation = GeometryMath.SignedAngle(Vector3.right, temp.bottomXVector.normalized, Vector3.up);
                }

                if (this.isEllipsoid)
                {
                    temp.bottomDiameterX = temp.bottomXVector.magnitude * 2.0f;
                    temp.bottomDiameterZ = temp.bottomZVector.magnitude * 2.0f;

                    temp.topDiameterX = temp.topXVector.magnitude * 2.0f;
                    temp.topDiameterZ = temp.topZVector.magnitude * 2.0f;
                } else
                {
                    if (temp.prevBottomZVector != temp.bottomZVector)
                    {
                        temp.bottomDiameterX = temp.bottomZVector.magnitude * 2.0f;
                        temp.bottomDiameterZ = temp.bottomZVector.magnitude * 2.0f;
                    } else
                    {
                        temp.bottomDiameterX = temp.bottomXVector.magnitude * 2.0f;
                        temp.bottomDiameterZ = temp.bottomXVector.magnitude * 2.0f;
                    }

                    if (temp.prevTopZVector != temp.topZVector)
                    {
                        temp.topDiameterX = temp.topZVector.magnitude * 2.0f;
                        temp.topDiameterZ = temp.topZVector.magnitude * 2.0f;
                    } else
                    {
                        temp.topDiameterX = temp.topXVector.magnitude * 2.0f;
                        temp.topDiameterZ = temp.topXVector.magnitude * 2.0f;
                    }
                }
            }
                
            const float kLineDash					= 2.0f;
            const float kLineThickness				= 1.0f;
            const float kCircleThickness			= 1.5f;
            const float kCapLineThickness			= 2.0f;
            const float kCapLineThicknessSelected	= 2.5f;
                 
            const int kMaxOutlineSides	= 32;
            const int kMinimumSides		= 8;
                
            var baseColor				= handles.color;
                
            BrushMeshFactory.GetConicalFrustumVertices(bottom, top, this.rotation, sides, ref temp.vertices);

            if (this.top.height < this.bottom.height)
                temp.normal = -Vector3.up;
            else
                temp.normal = Vector3.up;

            var isTopBackfaced      = handles.IsSufaceBackFaced(temp.topPoint,     temp.normal);
            var isBottomBackfaced   = handles.IsSufaceBackFaced(temp.bottomPoint, -temp.normal);

            bool topHasFocus, bottomHasFocus;
            prevModified = handles.modified;
            {
                handles.backfaced = isBottomBackfaced;
                handles.DoDirectionHandle(ref temp.bottomPoint, -temp.normal);
                bottomHasFocus = handles.lastHandleHadFocus;

                handles.backfaced = isTopBackfaced;
                handles.DoDirectionHandle(ref temp.topPoint, temp.normal);
                topHasFocus = handles.lastHandleHadFocus;
                handles.backfaced = false;
            }
            if (prevModified != handles.modified)
            {
                this.top.height     = Vector3.Dot(Vector3.up, temp.topPoint);
                this.bottom.height  = Vector3.Dot(Vector3.up, temp.bottomPoint);
            }


            var topThickness	= topHasFocus ? kCapLineThicknessSelected : kCapLineThickness;                    
            var bottomThickness	= bottomHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

            handles.color = handles.GetStateColor(baseColor, bottomHasFocus, isBottomBackfaced);
            handles.DrawLineLoop(temp.vertices, 0, sides, thickness: bottomThickness);

            handles.color = handles.GetStateColor(baseColor, topHasFocus, isTopBackfaced);
            handles.DrawLineLoop(temp.vertices, sides, sides, thickness: topThickness);

            handles.color = handles.GetStateColor(baseColor, false, false);
            for (int i = 0; i < sides; i++)
                handles.DrawLine(temp.vertices[i], temp.vertices[i + sides], lineMode: LineMode.ZTest, thickness: kLineThickness);

            handles.color = handles.GetStateColor(baseColor, false, true);
            for (int i = 0; i < sides; i++)
                handles.DrawLine(temp.vertices[i], temp.vertices[i + sides], lineMode: LineMode.NoZTest, thickness: kLineThickness);

            /*
            var point0    = camera.WorldToScreenPoint(topPoint);
            var direction = camera.ScreenToWorldPoint(point0 - Vector3.right);
            var point1	  = camera.WorldToScreenPoint(point0 - (direction * tempTop.diameterX));
            var size	  = Mathf.Max(point1.x - point0.x, point1.y - point0.y);
            */
            // TODO: figure out how to reduce the sides of the circle depending on radius & distance
            int outlineSides =  kMaxOutlineSides;
            if (sides <= kMinimumSides)
            {
                BrushMeshFactory.GetConicalFrustumVertices(bottom, top, this.rotation, outlineSides, ref temp.dottedVertices);

                handles.color = handles.GetStateColor(baseColor, topHasFocus, false);
                handles.DrawLineLoop(temp.dottedVertices, outlineSides, outlineSides, lineMode: LineMode.ZTest,   thickness: kCircleThickness, dashSize: kLineDash);

                handles.color = handles.GetStateColor(baseColor, topHasFocus, true);
                handles.DrawLineLoop(temp.dottedVertices, outlineSides, outlineSides, lineMode: LineMode.NoZTest, thickness: kCircleThickness, dashSize: kLineDash);

                handles.color = handles.GetStateColor(baseColor, bottomHasFocus, false);
                handles.DrawLineLoop(temp.dottedVertices, 0, outlineSides, lineMode: LineMode.ZTest, thickness: kCircleThickness, dashSize: kLineDash);

                handles.color = handles.GetStateColor(baseColor, bottomHasFocus, true);
                handles.DrawLineLoop(temp.dottedVertices, 0, outlineSides, lineMode: LineMode.NoZTest, thickness: kCircleThickness, dashSize: kLineDash);
            }
        }
        
        public void OnEdit(IChiselHandles handles)
        {
            var temp = new CylinderHandle();
            Init(ref temp);
            ShowInstance(handles, ref temp);
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}