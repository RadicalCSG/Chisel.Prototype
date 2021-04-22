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
    public struct ChiselTorusDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Torus";

        public const float kMinTubeDiameter = 0.1f;
        public const int kDefaultHorizontalSegments = 8;
        public const int kDefaultVerticalSegments = 8;

        // TODO: add scale the tube in y-direction (use transform instead?)
        // TODO: add start/total angle of tube

        public float    outerDiameter;
        public float    innerDiameter { get { return math.max(0, outerDiameter - (tubeWidth * 2)); } set { tubeWidth = math.max(kMinTubeDiameter, (outerDiameter - innerDiameter) * 0.5f); } }
        public float    tubeWidth;
        public float    tubeHeight;
        public float    tubeRotation;
        public float    startAngle;
        public float    totalAngle;
        public int      verticalSegments;
        public int      horizontalSegments;

        public bool     fitCircle;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            // TODO: create constants
            tubeWidth = 0.5f;
            tubeHeight = 0.5f;
            outerDiameter = 1.0f;
            tubeRotation = 0;
            startAngle = 0.0f;
            totalAngle = 360.0f;
            horizontalSegments = kDefaultHorizontalSegments;
            verticalSegments = kDefaultVerticalSegments;

            fitCircle = true;
        }

        public int RequiredSurfaceCount { get { return 6; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            tubeWidth			= math.max(tubeWidth,  kMinTubeDiameter);
            tubeHeight			= math.max(tubeHeight, kMinTubeDiameter);
            outerDiameter		= math.max(outerDiameter, tubeWidth * 2);
            
            horizontalSegments	= math.max(horizontalSegments, 3);
            verticalSegments	= math.max(verticalSegments, 3);

            totalAngle			= math.clamp(totalAngle, 1, 360); // TODO: constants
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

            int requiredSubMeshCount = horizontalSegments;
            if (requiredSubMeshCount == 0)
            {
                this.ClearBrushes(branch);
                return default;
            }

            if (branch.Count != requiredSubMeshCount)
                this.BuildBrushes(branch, requiredSubMeshCount);

            using (var vertices = BrushMeshFactory.GenerateTorusVertices(outerDiameter, tubeWidth, tubeHeight, tubeRotation, startAngle, totalAngle, verticalSegments, horizontalSegments, fitCircle, Allocator.Temp))
            {
                using (var brushMeshes = new NativeArray<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp))
                {
                    if (!BrushMeshFactory.GenerateTorus(brushMeshes, in vertices, verticalSegments, horizontalSegments,
                                                in surfaceDefinitionBlob, Allocator.Persistent))
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
                    return default;
                }
            }
        }

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselTorusDefinition definition, float3[] vertices, LineMode lineMode)
        {
            var horzSegments	= definition.horizontalSegments;
            var vertSegments	= definition.verticalSegments;
            
            if (definition.totalAngle != 360)
                horzSegments++;
            
            var prevColor		= renderer.color;
            prevColor.a *= 0.8f;
            var color			= prevColor;
            color.a *= 0.6f;

            renderer.color = color;
            for (int i = 0, j = 0; i < horzSegments; i++, j += vertSegments)
                renderer.DrawLineLoop(vertices, j, vertSegments, lineMode: lineMode, thickness: kVertLineThickness);

            for (int k = 0; k < vertSegments; k++)
            {
                for (int i = 0, j = 0; i < horzSegments - 1; i++, j += vertSegments)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + vertSegments], lineMode: lineMode, thickness: kHorzLineThickness);
            }
            if (definition.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    renderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            renderer.color = prevColor;
        }

        public void OnEdit(IChiselHandles handles)
        {
            var normal			= Vector3.up;

            float3[] vertices = null;
            if (BrushMeshFactory.GenerateTorusVertices(this, ref vertices))
            {
                var baseColor = handles.color;
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);
                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }

            var outerRadius = this.outerDiameter * 0.5f;
            var innerRadius = this.innerDiameter * 0.5f;
            var topPoint	= normal * (this.tubeHeight * 0.5f);
            var bottomPoint	= normal * (-this.tubeHeight * 0.5f);

            handles.DoRadiusHandle(ref outerRadius, normal, float3.zero);
            handles.DoRadiusHandle(ref innerRadius, normal, float3.zero);
            handles.DoDirectionHandle(ref bottomPoint, -normal);
            handles.DoDirectionHandle(ref topPoint, normal);
            if (handles.modified)
            {
                this.outerDiameter	= outerRadius * 2.0f;
                this.innerDiameter	= innerRadius * 2.0f;
                this.tubeHeight		= (topPoint.y - bottomPoint.y);
                // TODO: handle sizing down
            }
        }
        #endregion

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}