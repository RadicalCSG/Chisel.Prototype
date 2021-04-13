using System;
using Bounds = UnityEngine.Bounds;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Mathf = UnityEngine.Mathf;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using UnityEngine.Profiling;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselSphereDefinition : IChiselGenerator, IBrushGenerator
    {
        public const string kNodeTypeName = "Sphere";

        public const float              kMinSphereDiameter          = 0.01f;
        public const float              kDefaultRotation            = 0.0f;
        public const int                kDefaultHorizontalSegments  = 12;
        public const int                kDefaultVerticalSegments    = 12;
        public const bool               kDefaultGenerateFromCenter  = false;
        public static readonly Vector3  kDefaultDiameter            = Vector3.one;

        [DistanceValue] public Vector3	diameterXYZ;
        public float    offsetY;
        public bool     generateFromCenter;
        public float    rotation; // TODO: useless?
        public int	    horizontalSegments;
        public int	    verticalSegments;

        [NamedItems(overflow = "Side {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;
        public ChiselSurfaceDefinition SurfaceDefinition { get { return surfaceDefinition; } }

        public void Reset()
        {
            diameterXYZ		    = kDefaultDiameter;
            offsetY             = 0;
            rotation		    = kDefaultRotation;
            horizontalSegments  = kDefaultHorizontalSegments;
            verticalSegments    = kDefaultVerticalSegments;
            generateFromCenter  = kDefaultGenerateFromCenter;
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            diameterXYZ.x = Mathf.Max(kMinSphereDiameter, Mathf.Abs(diameterXYZ.x));
            diameterXYZ.y = Mathf.Max(0,                  Mathf.Abs(diameterXYZ.y)) * (diameterXYZ.y < 0 ? -1 : 1);
            diameterXYZ.z = Mathf.Max(kMinSphereDiameter, Mathf.Abs(diameterXYZ.z));

            horizontalSegments = Mathf.Max(horizontalSegments, 3);
            verticalSegments	= Mathf.Max(verticalSegments, 2);
            
            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateSphere(ref brushContainer, ref this);
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

                if (!BrushMeshFactory.GenerateSphere(diameterXYZ, offsetY, rotation, generateFromCenter, horizontalSegments, verticalSegments, in surfaceDefinitionBlob,
                                                         out var brushMesh, Allocator.Persistent))
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


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselSphereDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides			= definition.horizontalSegments;
            
            var extraVertices	= 2;
            var bottomVertex	= 1;
            var topVertex		= 0;
            
            var rings			= (vertices.Length - extraVertices) / sides;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            renderer.color = color;
            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                renderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: kHorzLineThickness, dashSize: kLineDash);
            }

            for (int k = 0; k < sides; k++)
            {
                renderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                renderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            renderer.color = prevColor;
        }

        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?

        public void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            if (BrushMeshFactory.GenerateSphereVertices(this, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }

            Vector3 center, topPoint, bottomPoint;
            if (!this.generateFromCenter)
            {
                center      = normal * (this.offsetY + (this.diameterXYZ.y * 0.5f));
                topPoint    = normal * (this.offsetY + this.diameterXYZ.y);
                bottomPoint = normal * (this.offsetY);
            } else
            {
                center      = normal * (this.offsetY);
                topPoint    = normal * (this.offsetY + (this.diameterXYZ.y *  0.5f));
                bottomPoint = normal * (this.offsetY + (this.diameterXYZ.y * -0.5f));
            }

            if (this.diameterXYZ.y < 0)
                normal = -normal;

            var radius2D = new Vector2(this.diameterXYZ.x, this.diameterXYZ.z) * 0.5f;

            {
                // TODO: make it possible to (optionally) size differently in x & z
                var radiusX = radius2D.x;
                handles.DoRadiusHandle(ref radiusX, normal, center);
                radius2D.x = radiusX;

                {
                    var isBottomBackfaced	= false; // TODO: how to do this?
                    
                    handles.backfaced = isBottomBackfaced;
                    handles.DoDirectionHandle(ref bottomPoint, -normal);
                    handles.backfaced = false;
                }

                {
                    var isTopBackfaced		= false; // TODO: how to do this?
                    
                    handles.backfaced = isTopBackfaced;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    handles.backfaced = false;
                }
            }
            if (handles.modified)
            {
                var diameter = this.diameterXYZ;
                diameter.y = topPoint.y - bottomPoint.y;
                diameter.x = radius2D.x * 2.0f;
                diameter.z = radius2D.x * 2.0f;
                this.offsetY    = bottomPoint.y;
                this.diameterXYZ = diameter;
                // TODO: handle sizing down (needs to modify transformation?)
            }
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}