using System;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // TODO: maybe just have different generators + support converting between generators
    [Serializable]
    public enum CylinderShapeType : byte
    {
        Cylinder,
        Cone,
        ConicalFrustum
    }

    // TODO: in circle mode use max(radiusx,radiusz) instead of just radiusx => toggling from ellipsoid to circle will make more sense
    // TODO: make handles snappable so we can snap circles to the grid
    // TODO: can end up with non convex shape when top ellipsoid is scaled larger than bottom on one (or more?) axi
    //          the quad triangulation needs to be reversed (figure out how to detect this)
    [Serializable]
    public struct ChiselCylinderDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Cylinder";

        public CylinderShapeType type;
        public bool     isEllipsoid;
        public bool     fitToBounds;
        public int      sides;

        // Show the name for the top Diameter X as "Top Diameter X" or "Top Diameter" depending on isEllipsoid being true or false
        [ConditionalNamePart("{ellipsoid}", " X", "", nameof(isEllipsoid)),
         ConditionalName("Top Diameter{ellipsoid}"),
         // Show this field depending on the type being a ConicalFrustum
         ConditionalHide(nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder), DistanceValue] 
        public float    topDiameterX;

        // Only show this field if it's a ConicalFrustum and isEllipsoid is true
        [ConditionalHide(nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder),
         ConditionalHide(nameof(isEllipsoid)), DistanceValue] // (z-diameter is only used for ellipsoids)
        public float    topDiameterZ;

        // Show the name for the bottom Diameter X as "Bottom Diameter X", "Bottom Diameter" or "Diameter" depending on isEllipsoid being true or false 
        // and if the type is a ConicalFrustum or not
        [ConditionalNamePart("{bottom}", "Bottom ", "", nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder),
         ConditionalNamePart("{ellipsoid}", " X", "", nameof(isEllipsoid)),
         ConditionalName("{bottom}Diameter{ellipsoid}"), DistanceValue] 
        public float    bottomDiameterX;

        // Show the name for the bottom Diameter Z as "Bottom Diameter X" or "Diameter Z" depending on if the type is a ConicalFrustum or not
        [ConditionalNamePart("{bottom}", "Bottom ", "", nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder),
         ConditionalName("{bottom}Diameter Z"),
         // Only show this field if isEllipsoid is true
         ConditionalHide(nameof(isEllipsoid)), DistanceValue] // (z-diameter is only used for ellipsoids)
        public float    bottomDiameterZ;

        [DistanceValue] public float height;
        [DistanceValue] public float bottomOffset;

        // TODO: show this in scene somehow
        [NamedItems("Top", "Bottom", overflow = "Side {0}")]
        public ChiselSurfaceDefinition surfaceDefinition;

        public ChiselSurfaceDefinition SurfaceDefinition { get { return surfaceDefinition; } }


        [UnityEngine.HideInInspector]
        public uint smoothingGroup; // TODO: show when we actually have normal smoothing + have custom property drawer for this

        [UnityEngine.HideInInspector, AngleValue]
        public float rotation;      // TODO: just get rid of this


        public float TopDiameterX
        {
            get { return topDiameterX; }
            set
            {
                if (value == topDiameterX)
                    return;

                topDiameterX = value;
                if (!isEllipsoid)
                    topDiameterZ = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    bottomDiameterX = value;
                    if (!isEllipsoid)
                        bottomDiameterZ = value;
                }
            }
        }

        public float TopDiameterZ
        {
            get { return topDiameterZ; }
            set
            {
                if (value == topDiameterZ)
                    return;

                topDiameterZ = value;
                if (!isEllipsoid)
                    topDiameterX = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    bottomDiameterZ = value;
                    if (!isEllipsoid)
                        bottomDiameterX = value;
                }
            }
        }

        public float Diameter
        {
            get { return bottomDiameterX; }
            set
            {
                if (value == bottomDiameterX)
                    return;

                bottomDiameterX = value;
                topDiameterX = value;
                bottomDiameterZ = value;
                topDiameterZ = value;
            }
        }

        public float DiameterX
        {
            get { return bottomDiameterX; }
            set
            {
                if (value == bottomDiameterX)
                    return;

                bottomDiameterX = value;
                topDiameterX = value;
                if (!isEllipsoid)
                {
                    bottomDiameterZ = value;
                    topDiameterZ = value;
                }
            }
        }

        public float DiameterZ
        {
            get { return bottomDiameterZ; }
            set
            {
                if (value == bottomDiameterZ)
                    return;

                bottomDiameterZ = value;
                topDiameterZ = value;
                if (!isEllipsoid)
                {
                    bottomDiameterX = value;
                    topDiameterX = value;
                }
            }
        }

        public float BottomDiameterX
        {
            get { return bottomDiameterX; }
            set
            {
                if (value == bottomDiameterX)
                    return;

                bottomDiameterX = value;
                if (!isEllipsoid)
                    bottomDiameterZ = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    topDiameterX = value;
                    if (!isEllipsoid)
                        topDiameterZ = value;
                }
            }
        }

        public float BottomDiameterZ
        {
            get { return bottomDiameterZ; }
            set
            {
                if (value == bottomDiameterZ)
                    return;

                bottomDiameterZ = value;
                if (!isEllipsoid)
                    bottomDiameterX = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    topDiameterZ = value;
                    if (!isEllipsoid)
                        topDiameterX = value;
                }
            }
        }

        public void Reset()
        {
            topDiameterX = 1.0f;
            topDiameterZ = 1.0f;
            height = 1.0f;

            bottomDiameterX = 1.0f;
            bottomDiameterZ = 1.0f;
            bottomOffset = 0.0f;

            rotation = 0.0f;
            isEllipsoid = false;
            fitToBounds = true;
            sides = 16;
            smoothingGroup = 1;
            type = CylinderShapeType.Cylinder;

            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            topDiameterX = Mathf.Abs(topDiameterX);
            topDiameterZ = Mathf.Abs(topDiameterZ);
            bottomDiameterX = Mathf.Abs(bottomDiameterX);
            bottomDiameterZ = Mathf.Abs(bottomDiameterZ);

            sides = Mathf.Max(3, sides);

            if (surfaceDefinition.EnsureSize(2 + sides))
            {
                // Top plane
                surfaceDefinition.surfaces[0].surfaceDescription.UV0 = UVMatrix.centered;

                // Bottom plane
                surfaceDefinition.surfaces[1].surfaceDescription.UV0 = UVMatrix.centered;
                
                float radius = topDiameterX * 0.5f;
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

                var bottomDiameter  = new float2(bottomDiameterX, bottomDiameterZ);
                var topHeight       = height + bottomOffset;
                var bottomHeight    = bottomOffset;

                float2 topDiameter;
                const Allocator allocator = Allocator.Persistent;
                switch (type)
                {
                    case CylinderShapeType.ConicalFrustum:  topDiameter = new float2(topDiameterX, topDiameterZ); break;
                    case CylinderShapeType.Cylinder:        topDiameter = bottomDiameter; break;
                    case CylinderShapeType.Cone:            topDiameter = float2.zero; break; 
                    default: throw new NotImplementedException();
                }

                if (!isEllipsoid)
                {
                    topDiameter.y    = topDiameter.x;
                    bottomDiameter.y = bottomDiameter.x;
                }

                if (surfaceDefinition.surfaces.Length != sides + 2 ||
                    !BrushMeshFactory.GenerateConicalFrustumSubMesh(topDiameter, topHeight, bottomDiameter, bottomHeight, rotation, sides, fitToBounds, in surfaceDefinitionBlob, out var brushMesh, allocator))
                {
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                    return false;
                }

                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
                return true;
            }
        }


        //
        // TODO: code below needs to be cleaned up & simplified 
        //

        static void DraggableRadius(IChiselHandles handles, ref float diameterX, ref float diameterZ, Vector3 center, Vector3 up, Vector3 xVector, Vector3 zVector, IChiselHandle[] radiusHandles, bool isEllipsoid)
        {
            if (isEllipsoid)
            {
                Debug.Assert(radiusHandles.Length == 4);
                diameterX = -diameterX; handles.DoSlider1DHandle(ref diameterX, center, xVector, radiusHandles[1]); // right
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX);
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ);
                }
                diameterX = -diameterX; handles.DoSlider1DHandle(ref diameterX, center, xVector, radiusHandles[0]); // left
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX);
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ);
                }
                diameterZ = -diameterZ; handles.DoSlider1DHandle(ref diameterZ, center, zVector, radiusHandles[3]); // back
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX);
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ);
                }
                diameterZ = -diameterZ; handles.DoSlider1DHandle(ref diameterZ, center, zVector, radiusHandles[2]); // forward
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX);
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ);
                }

                diameterX = Mathf.Abs(diameterX);
                diameterZ = Mathf.Abs(diameterZ);
            } else
            {
                Debug.Assert(radiusHandles.Length == 1);

                handles.DoDistanceHandle(ref diameterX, center, up, radiusHandles[0]); // left
                diameterX       = Mathf.Abs(diameterX);
                var radiusX     = diameterX * 0.5f;

                if (handles.lastHandleHadFocus)
                {
                    if (handles.TryGetClosestPoint(radiusHandles, out var closestPoint, interpolate: false))
                    {
                        var vec = (closestPoint - center);
                        handles.RenderDistanceMeasurement(center, center + vec, radiusX);
                    }
                }
            }
        }

        class CylinderHandles
        {
            public Vector3 normal = Vector3.up;
            public IChiselEllipsoidHandle  fullBottomCircleHandle;
            public IChiselEllipsoidHandle fullTopCircleHandle;
            public IChiselLineHandle verticalHandle1;
            public IChiselLineHandle verticalHandle2;
            public IChiselNormalHandle topHandle;
            public IChiselNormalHandle bottomHandle;

            public IChiselHandle[] topHandles;
            public IChiselHandle[] bottomHandles;
            public IChiselEllipsoidHandle[] topRadiusHandles;
            public IChiselEllipsoidHandle[] bottomRadiusHandles;

            public float topY;
            public float bottomY;
            public Vector3 topPoint;
            public Vector3 bottomPoint;
            public Vector3 topXVector;
            public Vector3 topZVector;
            public Vector3 bottomXVector;
            public Vector3 bottomZVector;

            public void Init(IChiselHandleAllocation handles, ChiselCylinderDefinition definition)
            {   
                fullBottomCircleHandle  = handles.CreateEllipsoidHandle(Vector3.zero, Vector3.zero, 0, 0);
                fullTopCircleHandle     = handles.CreateEllipsoidHandle(Vector3.zero, Vector3.zero, 0, 0);

                verticalHandle1          = handles.CreateLineHandle(Vector3.zero, Vector3.zero, highlightOnly: true);
                verticalHandle2          = handles.CreateLineHandle(Vector3.zero, Vector3.zero, highlightOnly: true);

                topHandle               = handles.CreateNormalHandle(Vector3.zero, Vector3.zero);
                bottomHandle            = handles.CreateNormalHandle(Vector3.zero, Vector3.zero);
            }
        
            public void Update(IChiselHandles handles, ChiselCylinderDefinition definition)
            {
                var tempBottomDiameterX	= definition.BottomDiameterX;
                var tempBottomDiameterZ = definition.isEllipsoid ? definition.BottomDiameterZ : definition.BottomDiameterX;

                float tempTopDiameterX, tempTopDiameterZ;
                if (definition.type == CylinderShapeType.Cone)
                {
                    tempTopDiameterX     = 0;
                    tempTopDiameterZ     = 0;
                } else
                if (definition.type == CylinderShapeType.Cylinder)
                { 
                    tempTopDiameterX	= tempBottomDiameterX;
                    tempTopDiameterZ	= tempBottomDiameterZ;
                } else
                {
                    tempTopDiameterX	= definition.TopDiameterX;
                    tempTopDiameterZ	= definition.isEllipsoid ? definition.TopDiameterZ : definition.TopDiameterX;
                }
            
                topY            = (definition.height + definition.bottomOffset);
                bottomY         = definition.bottomOffset;
                var rotate		= Quaternion.AngleAxis(definition.rotation, Vector3.up);
                topXVector		= rotate * Vector3.right   * tempTopDiameterX * 0.5f;
                topZVector      = rotate * Vector3.forward * tempTopDiameterZ * 0.5f;
                bottomXVector   = rotate * Vector3.right   * tempBottomDiameterX * 0.5f;
                bottomZVector   = rotate * Vector3.forward * tempBottomDiameterZ * 0.5f;
                normal = Vector3.up;
                if (topY < bottomY) normal = -Vector3.up; else normal = Vector3.up;
                topPoint        = normal * topY;
                bottomPoint     = normal * bottomY;
            
            
                // Render vertical horizon of cylinder
                // TODO: make this work with math instead of "finding" it
                Vector3 bottomPointA, topPointA;
                Vector3 bottomPointB, topPointB;
                var camera = UnityEngine.Camera.current;
                var cameraTransform = camera.transform;
                var cameraPosition = handles.inverseMatrix.MultiplyPoint(cameraTransform.position);
                const float degreeStep = 5;
                var pointA      = fullTopCircleHandle.GetPointAtDegree(360 - degreeStep);
                var pointB      = fullBottomCircleHandle.GetPointAtDegree(360 - degreeStep);
                var camOrtho    = camera.orthographic;
                var camForward  = handles.inverseMatrix.MultiplyVector(cameraTransform.forward).normalized;
                var camDir      = camOrtho ? camForward : (pointA - cameraPosition).normalized;

            
                var delta       = (pointA - pointB).normalized;
                var normal3     = -Vector3.Cross(delta, Vector3.Cross((pointB - bottomPoint).normalized, delta)).normalized;
                var prevDot     = Vector3.Dot(normal3, camDir) < 0;

                bool renderHorizon = false;
                //*
                bottomPointA    = Vector3.zero;
                topPointA       = Vector3.zero;
                bottomPointB    = Vector3.zero;
                topPointB       = Vector3.zero;
                var lineCount = 0;
                for (float degree = 0; degree < 360; degree += degreeStep)
                {
                    pointA = fullTopCircleHandle.GetPointAtDegree(degree);
                    pointB = fullBottomCircleHandle.GetPointAtDegree(degree);

                    delta   = (pointA - pointB).normalized;
                    normal3 = -Vector3.Cross(delta, Vector3.Cross((pointB - bottomPoint).normalized, delta)).normalized;

                    camDir = camOrtho ? camForward : (pointB - cameraPosition).normalized;
                    var currDot = Vector3.Dot(normal3, camDir) < 0;

                    if (prevDot != currDot)
                    {
                        lineCount++;
                        if (lineCount == 1)
                        {
                            topPointA = pointA;
                            bottomPointA = pointB;
                        } else
                        //if (lineCount == 2)
                        {
                            topPointB = pointA;
                            bottomPointB = pointB;
                            renderHorizon = true;
                            break;
                        }
                    }
                    prevDot = currDot;
                }

    #if false
                {
                    var pointC      = (Vector3.right * (definition.topDiameterX * 0.5f)) + (Vector3.up * (definition.height + definition.bottomOffset));
                    var pointD      = (Vector3.right * (definition.bottomDiameterX * 0.5f)) + (Vector3.up * definition.bottomOffset);
                    //var deltar      = (pointC - pointD).normalized;
                    //var normala     = -Vector3.Cross(Vector3.forward, deltar).normalized;


                    var DT = (cameraPosition - topPoint);
                    var DB = (cameraPosition - bottomPoint);
                    var DmT = DT.magnitude;
                    var DmB = DB.magnitude;
                    //var Dv = D / Dm;

                    var RmT = definition.topDiameterX * 0.5f;
                    var RmB = definition.bottomDiameterX * 0.5f;

                    var cosAT = RmT / DmT;
                    var cosAB = RmB / DmB;
                    var AT = Mathf.Acos(cosAT) * Mathf.Rad2Deg;
                    var AB = Mathf.Acos(cosAB) * Mathf.Rad2Deg;
                    var RvT = (Quaternion.AngleAxis(AT, Vector3.up) * DT).normalized;
                    var RvB = (Quaternion.AngleAxis(AB, Vector3.up) * DB).normalized;
                    //var R = Rv * Rm;

                    var angleT = Vector3.SignedAngle(Vector3.right, RvT, Vector3.up);
                    var angleB = Vector3.SignedAngle(Vector3.right, RvB, Vector3.up);

                    var arotationT  = Quaternion.AngleAxis(angleT, Vector3.up);
                    var arotationB  = Quaternion.AngleAxis(angleB, Vector3.up);
                    var ptA = arotationT * pointC;
                    var ptB = arotationB * pointD;
                    var prevCol = handles.color;
                    handles.color = UnityEngine.Color.red;
                    handles.DrawLine(bottomPoint, bottomPoint + Vector3.right);
                    //handles.DrawLine(bottomPoint, bottomPoint + Vector3.forward);
                    //handles.DrawLine(bottomPoint, bottomPoint + normala);
                    //handles.DrawLine(bottomPoint, bottomPoint + deltar);
                    //handles.DrawLine(bottomPoint, bottomPoint + R);
                    handles.DrawLine(bottomPoint, bottomPoint + RvT);
                    handles.DrawLine(bottomPoint, bottomPoint + RvB);
                    //handles.DrawLine(bottomPoint, bottomPoint + desired);
                    handles.DrawLine(ptA, ptB);
                    handles.color = prevCol;
                }
    #endif


                /*/
                if (camera.orthographic)
                {
                    {
                        var radius = definition.bottomDiameterX * 0.5f;
                        var center = bottomPoint;
                        bottomPointA = center + (cameraTransform.right * radius);
                        bottomPointB = center - (cameraTransform.right * radius);
                    }
                    {
                        var radius = definition.topDiameterX * 0.5f;
                        var center = topPoint;
                        topPointA = center + (cameraTransform.right * radius);
                        topPointB = center - (cameraTransform.right * radius);
                    }
                } else
                {
                    var handleMatrix = handles.matrix;
                    renderHorizon = GeometryMath.FindCircleHorizon(handleMatrix, definition.bottomDiameterX, bottomPoint, -normal, out bottomPointB, out bottomPointA);
                    renderHorizon = GeometryMath.FindCircleHorizon(handleMatrix, definition.topDiameterX,    topPoint,     normal, out topPointA,    out topPointB) && renderHorizon;

                    if (renderHorizon && definition.bottomDiameterX != definition.topDiameterX)
                    {
                        renderHorizon = !(GeometryMath.PointInCameraCircle(handleMatrix, bottomPointA, definition.topDiameterX,    topPoint,     normal) ||
                                          GeometryMath.PointInCameraCircle(handleMatrix, topPointA,    definition.bottomDiameterX, bottomPoint, -normal) ||
                                          GeometryMath.PointInCameraCircle(handleMatrix, bottomPointB, definition.topDiameterX,    topPoint,     normal) ||
                                          GeometryMath.PointInCameraCircle(handleMatrix, topPointB,    definition.bottomDiameterX, bottomPoint, -normal));
                    }
                }
                //*/

                if (!renderHorizon)
                {
                    bottomPointA = Vector3.zero;
                    topPointA = Vector3.zero;
                    bottomPointB = Vector3.zero;
                    topPointB = Vector3.zero;
                }

                verticalHandle1.From = bottomPointA;
                verticalHandle1.To   = topPointA;
                verticalHandle2.From = bottomPointB;
                verticalHandle2.To   = topPointB;
                
                fullTopCircleHandle.Center       = topPoint;
                fullBottomCircleHandle.Center    = bottomPoint;
                
                fullTopCircleHandle.DiameterX    = tempTopDiameterX;
                fullTopCircleHandle.DiameterZ    = tempTopDiameterZ;
                fullBottomCircleHandle.DiameterX = tempBottomDiameterX;
                fullBottomCircleHandle.DiameterZ = tempBottomDiameterZ;

                topHandle   .Origin = topPoint;
                bottomHandle.Origin = bottomPoint;


                fullTopCircleHandle.Normal = normal;
                fullBottomCircleHandle.Normal = -normal;

                topHandle.Normal = normal;
                bottomHandle.Normal = -normal;

                
                if (definition.isEllipsoid)
                {
                    if (bottomRadiusHandles == null || bottomRadiusHandles.Length != 4)
                    {
                        bottomRadiusHandles = new IChiselEllipsoidHandle[]
                        {
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: +45f, angles: 90),        // left
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: +45f + 180f, angles: 90), // right
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: -45f, angles: 90),        // forward
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: -45f + 180f, angles: 90), // back
                        };
                    }

                    if (topRadiusHandles == null || topRadiusHandles.Length != 4)
                    {
                        topRadiusHandles = new IChiselEllipsoidHandle[]
                        {
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: +45f, angles: 90),          // left
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: +45f + 180f, angles: 90),   // right
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: -45f, angles: 90),          // forward
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: -45f + 180f, angles: 90),   // back
                        };
                    }

                    for (int i = 0; i < bottomRadiusHandles.Length; i++)
                    {
                        bottomRadiusHandles[i].Center = bottomPoint;
                        bottomRadiusHandles[i].Normal = -normal;
                        bottomRadiusHandles[i].DiameterX = tempBottomDiameterX;
                        bottomRadiusHandles[i].DiameterZ = tempBottomDiameterZ;
                        bottomRadiusHandles[i].Rotation = definition.rotation;
                    }

                    for (int i = 0; i < topRadiusHandles.Length; i++)
                    {
                        topRadiusHandles[i].Center = topPoint;
                        topRadiusHandles[i].Normal = normal;
                        topRadiusHandles[i].DiameterX = tempTopDiameterX;
                        topRadiusHandles[i].DiameterZ = tempTopDiameterZ;
                        topRadiusHandles[i].Rotation = definition.rotation;
                    }

                    if (bottomHandles == null || bottomHandles.Length != 4)
                        bottomHandles   = new IChiselHandle[] { bottomHandle, bottomRadiusHandles[0], bottomRadiusHandles[1], bottomRadiusHandles[2], bottomRadiusHandles[3] };
                    if (definition.type != CylinderShapeType.Cone)
                    {
                        if (topHandles == null || topHandles.Length != 5)
                            topHandles = new IChiselHandle[] { topHandle, topRadiusHandles[0], topRadiusHandles[1], topRadiusHandles[2], topRadiusHandles[3] };
                    } else
                    {
                        if (topHandles == null || topHandles.Length != 1)
                            topHandles = new IChiselHandle[] { topHandle };
                    }
                } else
                {
                    if (bottomRadiusHandles == null || bottomRadiusHandles.Length != 1) bottomRadiusHandles = new IChiselEllipsoidHandle[] { fullBottomCircleHandle };
                    if (topRadiusHandles == null || topRadiusHandles.Length    != 1) topRadiusHandles = new IChiselEllipsoidHandle[] { fullTopCircleHandle };

                    if (bottomHandles == null || bottomHandles.Length != 2) bottomHandles   = new IChiselHandle[] { bottomHandle, bottomRadiusHandles[0] };
                    if (definition.type != CylinderShapeType.Cone)
                    {
                        if (topHandles == null || topHandles.Length != 2)
                            topHandles = new IChiselHandle[] { topHandle, topRadiusHandles[0] };
                    } else
                    {
                        if (topHandles == null || topHandles.Length != 1)
                            topHandles = new IChiselHandle[] { topHandle };
                    }
                }
            }
        }

        public void OnEdit(IChiselHandles handles)
        {
            // Store our allocated handles in generatorState to avoid reallocating them every frame
            var cylinderHandles = handles.generatorState as CylinderHandles;
            if (cylinderHandles == null)
            {
                cylinderHandles = new CylinderHandles();
                cylinderHandles.Init(handles, this);
                handles.generatorState = cylinderHandles;
            }
            cylinderHandles.Update(handles, this);


            // Render vertical lines at the horizon of the cylinder
            handles.DoRenderHandles(new[] { cylinderHandles.verticalHandle1, cylinderHandles.verticalHandle2 });


            // Move the cylinder top/bottom up/down
            var prevModified = handles.modified;
            {
                handles.DoSlider1DHandle(ref cylinderHandles.bottomPoint, -cylinderHandles.normal, cylinderHandles.bottomHandles);
                var haveFocus = handles.lastHandleHadFocus;
                handles.DoSlider1DHandle(ref cylinderHandles.topPoint,     cylinderHandles.normal, cylinderHandles.topHandles);
                haveFocus = haveFocus || handles.lastHandleHadFocus;
                if (haveFocus)
                    handles.RenderDistanceMeasurement(cylinderHandles.topPoint, cylinderHandles.bottomPoint, Mathf.Abs(height));
            }
            if (prevModified != handles.modified)
            {
                cylinderHandles.topY    = Vector3.Dot(Vector3.up, cylinderHandles.topPoint);
                cylinderHandles.bottomY = Vector3.Dot(Vector3.up, cylinderHandles.bottomPoint);

                height = cylinderHandles.topY - cylinderHandles.bottomY;
                bottomOffset = cylinderHandles.bottomY;
            }


            // Resize the top/bottom circle by grabbing and dragging it
            prevModified = handles.modified;
            {
                // Make the bottom circle draggable
                DraggableRadius(handles, ref bottomDiameterX, ref bottomDiameterZ, cylinderHandles.bottomPoint, -cylinderHandles.normal, cylinderHandles.bottomXVector, cylinderHandles.bottomZVector, cylinderHandles.bottomRadiusHandles, this.isEllipsoid);
                // If we're a Cylinder, the top circle actually changes the bottom circle too
                if (type == CylinderShapeType.Cylinder)
                    DraggableRadius(handles, ref bottomDiameterX, ref bottomDiameterZ, cylinderHandles.topPoint, cylinderHandles.normal, cylinderHandles.topXVector, cylinderHandles.topZVector, cylinderHandles.topRadiusHandles, this.isEllipsoid);
                else
                // If we're a Conical Frustum, the top circle can be resized independently from the bottom circle
                if (type == CylinderShapeType.ConicalFrustum)
                    DraggableRadius(handles, ref topDiameterX, ref topDiameterZ, cylinderHandles.topPoint, cylinderHandles.normal, cylinderHandles.topXVector, cylinderHandles.topZVector, cylinderHandles.topRadiusHandles, this.isEllipsoid);
                // else; If we're a Cone, we ignore the top circle
            }
            if (prevModified != handles.modified)
            {
                // Ensure that when our shape is circular and we modify it, that when we convert back to an ellipsoid, it'll still be circular
                if (!this.isEllipsoid)
                {
                    topDiameterZ    = topDiameterX;
                    bottomDiameterZ = bottomDiameterX;
                }
            }
        }

        public void OnMessages(IChiselMessages messages)
        {
            // TODO: show a message when height = 0 or when shape is a line or flat b/c diameters are 0
        }
    }
}