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
using AOT;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    [Serializable]
    [BurstCompile()]
    public struct ChiselTorus : IBranchGenerator
    {
        public readonly static ChiselTorus DefaultValues = new ChiselTorus
        {
            tubeWidth           = 0.5f,
            tubeHeight          = 0.5f,
            outerDiameter       = 1.0f,
            tubeRotation        = 0,
            startAngle          = 0.0f,
            totalAngle          = 360.0f,
            horizontalSegments  = 8,
            verticalSegments    = 8,
            fitCircle           = true
        };

        // TODO: add scale the tube in y-direction (use transform instead?)
        // TODO: add start/total angle of tube

        public float    outerDiameter;
        public float    tubeWidth;
        public float    tubeHeight;
        public float    tubeRotation;
        public float    startAngle;
        public float    totalAngle;
        public int      verticalSegments;
        public int      horizontalSegments;

        [MarshalAs(UnmanagedType.U1)]
        public bool     fitCircle;


        #region Properties

        const float kMinTubeDiameter = 0.1f;

        public float InnerDiameter { get { return math.max(0, outerDiameter - (tubeWidth * 2)); } set { tubeWidth = math.max(kMinTubeDiameter, (outerDiameter - InnerDiameter) * 0.5f); } }
        #endregion

        #region Generate
        [BurstCompile]
        public int PrepareAndCountRequiredBrushMeshes()
        {
            return horizontalSegments;
        }

        [BurstCompile]
        public bool GenerateNodes(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, Allocator allocator)
        {
            using (var generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp))
            {
                generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
                using var vertices = BrushMeshFactory.GenerateTorusVertices(outerDiameter,
                                                                        tubeWidth,
                                                                        tubeHeight,
                                                                        tubeRotation,
                                                                        startAngle,
                                                                        totalAngle,
                                                                        verticalSegments,
                                                                        horizontalSegments,
                                                                        fitCircle,
                                                                        Allocator.Temp);

                if (!BrushMeshFactory.GenerateTorus(generatedBrushMeshes,
                                                    in vertices,
                                                    verticalSegments,
                                                    horizontalSegments,
                                                    in surfaceDefinitionBlob,
                                                    allocator))
                {
                    for (int i = 0; i < generatedBrushMeshes.Length; i++)
                    {
                        if (generatedBrushMeshes[i].IsCreated)
                            generatedBrushMeshes[i].Dispose();
                    }
                    return false;
                }

                for (int i = 0; i < generatedBrushMeshes.Length; i++)
                    nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i]);
                return true;
            }
        }

        [BurstCompile]
        public void Dispose() { }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 6; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }
        #endregion

        #region Validation
        public void Validate()
        {
            tubeWidth			= math.max(tubeWidth,  kMinTubeDiameter);
            tubeHeight			= math.max(tubeHeight, kMinTubeDiameter);
            outerDiameter		= math.max(outerDiameter, tubeWidth * 2);

            horizontalSegments	= math.max(horizontalSegments, 3);
            verticalSegments	= math.max(verticalSegments, 3);

            totalAngle			= math.clamp(totalAngle, 1, 360); // TODO: constants
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultValues; }
        #endregion
    }

    [Serializable]
    public class ChiselTorusDefinition : SerializedBranchGenerator<ChiselTorus>
    {
        public const string kNodeTypeName = "Torus";

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

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
            var horzSegments	= definition.settings.horizontalSegments;
            var vertSegments	= definition.settings.verticalSegments;
            
            if (definition.settings.totalAngle != 360)
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
            if (definition.settings.totalAngle == 360)
            {
                for (int k = 0; k < vertSegments; k++)
                {
                    renderer.DrawLine(vertices[k], vertices[k + ((horzSegments - 1) * vertSegments)], lineMode: lineMode, thickness: kHorzLineThickness);
                }
            }
            renderer.color = prevColor;
        }

        public override void OnEdit(IChiselHandles handles)
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

            var outerRadius = settings.outerDiameter * 0.5f;
            var innerRadius = settings.InnerDiameter * 0.5f;
            var topPoint	= normal * (settings.tubeHeight * 0.5f);
            var bottomPoint	= normal * (-settings.tubeHeight * 0.5f);

            handles.DoRadiusHandle(ref outerRadius, normal, float3.zero);
            handles.DoRadiusHandle(ref innerRadius, normal, float3.zero);
            handles.DoDirectionHandle(ref bottomPoint, -normal);
            handles.DoDirectionHandle(ref topPoint, normal);
            if (handles.modified)
            {
                settings.outerDiameter	= outerRadius * 2.0f;
                settings.InnerDiameter	= innerRadius * 2.0f;
                settings.tubeHeight		= (topPoint.y - bottomPoint.y);
                // TODO: handle sizing down
            }
        }
        #endregion
    }
}