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
using Unity.Mathematics;
using UnityEngine.Profiling;
using Unity.Collections;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;


namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static BlobAssetReference<BrushMeshBlob> CreateBrushBlob(BrushMesh brushMesh, in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob)
        {
            // TODO: eventually remove when it's more battle tested
            if (!brushMesh.Validate(logErrors: true))
                return BlobAssetReference<BrushMeshBlob>.Null;
            brushMesh.CalculatePlanes();
            brushMesh.UpdateHalfEdgePolygonIndices();
            return BrushMeshManager.ConvertToBrushMeshBlob(brushMesh, in surfaceDefinitionBlob, Allocator.Persistent);
        }

        // TODO: create helper method to cut brushes, use that instead of intersection + subtraction brushes
        // TODO: create spiral sides support
        [BurstCompile]
        public static bool GenerateSpiralStairs(NativeList<BlobAssetReference<BrushMeshBlob>>        brushMeshes, 
                                                ref ChiselSpiralStairs                               definition, 
                                                in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                Allocator                                            allocator)
        {
            const bool fitToBounds = false;
            const float kEpsilon = 0.001f;

            var nosingDepth		= definition.nosingDepth;
            var treadHeight		= (nosingDepth < kEpsilon) ? 0 : definition.treadHeight;
            var haveTread		= (treadHeight >= kEpsilon);

            var innerDiameter	= definition.innerDiameter;
            var haveInnerCyl	= (innerDiameter >= kEpsilon);

            var riserType		= definition.riserType;
            var haveRiser		= riserType != StairsRiserType.None;

            if (!haveRiser && !haveTread)
                return false;
            

            var origin		    = definition.origin;

            var startAngle	    = math.radians(definition.startAngle);
            var anglePerStep    = math.radians(definition.AnglePerStep);

            var nosingWidth	    = definition.nosingWidth;
            var outerDiameter   = definition.outerDiameter;

            var p0 = new float2(math.sin(0                  ), math.cos(0                  ));
            var p1 = new float2(math.sin(anglePerStep       ), math.cos(anglePerStep       ));
            var pm = new float2(math.sin(anglePerStep * 0.5f), math.cos(anglePerStep * 0.5f));
            var pn = (p0 + p1) * 0.5f;

            var stepOuterDiameter		= outerDiameter + ((math.length(pm) - math.length(pn)) * (outerDiameter * 1.25f)); // TODO: figure out why we need the 1.25 magic number to fit the step in the outerDiameter?
            var stepOuterRadius			= stepOuterDiameter * 0.5f;
            var stepInnerRadius			= 0.0f;
            var stepHeight				= definition.stepHeight;
            var height					= definition.height;
            var stepCount				= definition.StepCount;

            if (height < 0)
            {
                origin.y += height;
                height = -height;
            }

            // TODO: expose this to user
            var smoothSubDivisions		= 3;

            var cylinderSubMeshCount	= haveInnerCyl ? 2 : 1;
            var subMeshPerRiser			= (riserType == StairsRiserType.None  ) ? 0 : 
                                          (riserType == StairsRiserType.Smooth) ? (2 * smoothSubDivisions)
                                                                                : 1;
            var riserSubMeshCount		= (stepCount * subMeshPerRiser) + ((riserType == StairsRiserType.None  ) ? 0 : cylinderSubMeshCount);
            var treadSubMeshCount		= (haveTread ? stepCount + cylinderSubMeshCount : 0);
            var subMeshCount			= (treadSubMeshCount + riserSubMeshCount);

            var treadStart		= !haveRiser ? 0 : riserSubMeshCount;
            var innerSides		= definition.innerSegments;
            var outerSides		= definition.outerSegments;
            var riserDepth		= definition.riserDepth;

            using (var outerCylinderSurfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(outerSides + 2, Allocator.Temp))
            using (var innerCylinderSurfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(innerSides + 2, Allocator.Temp))
            {
                ref var outerCylinderSurfaces = ref outerCylinderSurfaceDefinitionBlob.Value.surfaces;
                for (int i = 0; i < outerCylinderSurfaces.Length; i++)
                    outerCylinderSurfaces[i] = surfaceDefinitionBlob.Value.surfaces[(int)ChiselSpiralStairs.SurfaceSides.OuterSurface];

                ref var innerCylinderSurfaces = ref innerCylinderSurfaceDefinitionBlob.Value.surfaces;
                for (int i = 0; i < innerCylinderSurfaces.Length; i++)
                    innerCylinderSurfaces[i] = surfaceDefinitionBlob.Value.surfaces[(int)ChiselSpiralStairs.SurfaceSides.InnerSurface];


                if (haveRiser)
                {
                    if (riserType == StairsRiserType.ThinRiser)
                    {
                        var minY = origin.y;
                        var maxY = origin.y + stepHeight - treadHeight;
                        float2 o0, o1;
                        float angle = startAngle;
                        var c1 = math.sin(angle) * stepOuterRadius;
                        var s1 = math.cos(angle) * stepOuterRadius;
                        for (int i = 0; i < stepCount; i++)
                        {
                            var c0 = c1;
                            var s0 = s1;
                            angle += anglePerStep;
                            c1 = math.sin(angle) * stepOuterRadius;
                            s1 = math.cos(angle) * stepOuterRadius;

                            o0 = new float2(origin.x + c0, origin.z + s0);
                            o1 = new float2(origin.x     , origin.z     );

                            var riserVector = math.normalize(new float2((c0 - c1), (s0 - s1))) * riserDepth;

                            var i0 = o0 - riserVector;
                            var i1 = o1 - riserVector;

                            //brushMesh.halfEdges = (anglePerStep > 0) ? invertedBoxHalfEdges.ToArray() : boxHalfEdges.ToArray();
                            brushMeshes[i] = CreateBox(new float3(i0.x, maxY, i0.y), // 0
                                                       new float3(i1.x, maxY, i1.y), // 1
                                                       new float3(o1.x, maxY, o1.y), // 2
                                                       new float3(o0.x, maxY, o0.y), // 3  

                                                       new float3(i0.x, minY, i0.y), // 4
                                                       new float3(i1.x, minY, i1.y), // 5
                                                       new float3(o1.x, minY, o1.y), // 6
                                                       new float3(o0.x, minY, o0.y), // 7
                                                       in surfaceDefinitionBlob,
                                                       allocator);
                            if (i == 0)
                                minY -= treadHeight;

                            minY += stepHeight;
                            maxY += stepHeight;
                        }
                    } else
                    if (riserType == StairsRiserType.Smooth)
                    {
                        //var stepY = stepHeight;
                        var minY  = origin.y;
                        var maxY  = origin.y + stepHeight - treadHeight;
                        var maxY2 = origin.y + (stepHeight * 2) - treadHeight;
                        float angle = startAngle;
                        var c1 = math.sin(angle);
                        var s1 = math.cos(angle);
                        angle += anglePerStep;
                        var c2 = math.sin(angle);
                        var s2 = math.cos(angle);

                        for (int i = 0; i < riserSubMeshCount; i += subMeshPerRiser)
                        {
                            var c0 = c1;
                            var s0 = s1;
                            c1 = c2;
                            s1 = s2;
                            angle += anglePerStep;
                            c2 = math.sin(angle);
                            s2 = math.cos(angle);

                            var c0o = c0 * stepOuterRadius;
                            var c1o = c1 * stepOuterRadius;
                            var s0o = s0 * stepOuterRadius;
                            var s1o = s1 * stepOuterRadius;
                        
                            var o0 = new float2(origin.x + c0o, origin.z + s0o);
                            var o1 = new float2(origin.x + c1o, origin.z + s1o);

                            var i0 = o0;
                            var i1 = o1;

                            int subMeshIndex = i;
                            for (int subDiv = 1; subDiv < smoothSubDivisions; subDiv++)
                            {
                                // TODO: need to space the subdivisions from smallest spaces to bigger spaces
                                float stepMidRadius;
                                stepMidRadius = (((outerDiameter * 0.5f) * (1.0f / (smoothSubDivisions + 1))) * ((smoothSubDivisions - 1) - (subDiv - 1)));
                                if (subDiv == (smoothSubDivisions - 1))
                                {
                                    var innerRadius = (innerDiameter * 0.5f) - 0.1f;
                                    stepMidRadius = (innerRadius < 0.1f) ? stepMidRadius : innerRadius;
                                }

                                var c0i = c0 * stepMidRadius;
                                var c1i = c1 * stepMidRadius;
                                var s0i = s0 * stepMidRadius;
                                var s1i = s1 * stepMidRadius;

                                i0 = new float2(origin.x + c0i, origin.z + s0i);
                                i1 = new float2(origin.x + c1i, origin.z + s1i);

                                {                  
                                    //brushMesh.halfEdges = (anglePerStep > 0) ? invertedSquarePyramidHalfEdges.ToArray() : squarePyramidHalfEdges.ToArray();
                                    brushMeshes[subMeshIndex] = CreateSquarePyramidAssetPolygons(new float3(i0.x, maxY, i0.y), // 0
                                                                                                 new float3(i0.x, minY, i0.y), // 1
                                                                                                 new float3(o0.x, minY, o0.y), // 2  
                                                                                                 new float3(o0.x, maxY, o0.y), // 3
                                                                                                 new float3(o1.x, maxY, o1.y), // 4
                                                                                                 in surfaceDefinitionBlob,
                                                                                                 allocator);
                                    subMeshIndex++;
                                }

                                {
                                    //brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                    brushMeshes[subMeshIndex] = CreateTriangularPyramidAssetPolygons(new float3(i0.x, maxY, i0.y), // 0
                                                                                                     new float3(i0.x, minY, i0.y), // 1
                                                                                                     new float3(i1.x, maxY, i1.y), // 2
                                                                                                     new float3(o1.x, maxY, o1.y), // 3,
                                                                                                     in surfaceDefinitionBlob, 
                                                                                                     allocator);
                                    subMeshIndex++;
                                }
                            
                                o0 = i0;
                                o1 = i1;
                            }

                            {
                                //brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                brushMeshes[subMeshIndex] = CreateTriangularPyramidAssetPolygons(new float3(     i0.x, maxY,     i0.y), // 0
                                                                                                 new float3(     i1.x, maxY,     i1.y), // 2
                                                                                                 new float3(     i0.x, minY,     i0.y), // 1
                                                                                                 new float3( origin.x, minY, origin.y), // 3
                                                                                                 in surfaceDefinitionBlob, 
                                                                                                 allocator);
                                subMeshIndex++;
                            }

                            {
                                //brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                brushMeshes[subMeshIndex] = CreateTriangularPyramidAssetPolygons(new float3(     i1.x, maxY,     i1.y), // 2
                                                                                                 new float3(     i0.x, maxY,     i0.y), // 0
                                                                                                 new float3( origin.x, maxY, origin.y), // 1
                                                                                                 new float3( origin.x, minY, origin.y), // 3
                                                                                                 in surfaceDefinitionBlob, 
                                                                                                 allocator);
                                subMeshIndex++;
                            }

                            if (i == 0)
                                minY -= treadHeight;

                            minY += stepHeight;
                            maxY += stepHeight;
                            maxY2 += stepHeight;
                        }
                    } else
                    {
                        var minY = origin.y;
                        var maxY = origin.y + stepHeight - treadHeight;
                        float2 o0, o1;
                        float angle = startAngle;
                        var c1 = math.sin(angle) * stepOuterRadius;
                        var s1 = math.cos(angle) * stepOuterRadius;
                        for (int i = 0; i < stepCount; i++)
                        {
                            var c0 = c1;
                            var s0 = s1;
                            angle += anglePerStep;
                            c1 = math.sin(angle) * stepOuterRadius;
                            s1 = math.cos(angle) * stepOuterRadius;

                            o0 = new float2(origin.x + c0, origin.z + s0);
                            o1 = new float2(origin.x + c1, origin.z + s1);

                            //brushMesh.halfEdges = (anglePerStep > 0) ? invertedWedgeHalfEdges.ToArray() : wedgeHalfEdges.ToArray();
                            brushMeshes[i] = CreateWedgeAssetPolygons(new float3( origin.x, minY, origin.z), // 0
                                                                      new float3(     o1.x, minY,     o1.y), // 1
                                                                      new float3(     o0.x, minY,     o0.y), // 2  

                                                                      new float3( origin.x, maxY, origin.z), // 3
                                                                      new float3(     o1.x, maxY,     o1.y), // 4
                                                                      new float3(     o0.x, maxY,     o0.y), // 5
                                                                      in surfaceDefinitionBlob, 
                                                                      allocator);
                            if (i == 0)
                                minY -= treadHeight;

                            if (riserType != StairsRiserType.FillDown)
                                minY += stepHeight;
                            maxY += stepHeight;
                        }
                    }
                
                    {
                        var subMeshIndex = treadStart - cylinderSubMeshCount;


                        if (!BrushMeshFactory.GenerateCylinderSubMesh(outerDiameter, origin.y, origin.y + height, 0, outerSides, fitToBounds: fitToBounds,
                                                                        in outerCylinderSurfaceDefinitionBlob,
                                                                        out var brushMeshBlob,
                                                                        allocator))
                            return false;
                        brushMeshes[subMeshIndex] = brushMeshBlob;
                        //brushContainer.operations[subMeshIndex] = CSGOperationType.Intersecting;
                    }

                    if (haveInnerCyl)
                    {
                        var subMeshIndex = treadStart - 1;



                        if (!BrushMeshFactory.GenerateCylinderSubMesh(innerDiameter, origin.y, origin.y + height, 0, innerSides, fitToBounds: fitToBounds,
                                                                        in innerCylinderSurfaceDefinitionBlob,
                                                                        out var brushMeshBlob,
                                                                        allocator))
                            return false;
                        brushMeshes[subMeshIndex] = brushMeshBlob;
                        //brushContainer.operations[subMeshIndex] = CSGOperationType.Subtractive;
                    }

                }

                if (haveTread)
                {
                    var minY = origin.y + stepHeight - treadHeight;
                    var maxY = origin.y + stepHeight;
                    float2 i0, i1, o0, o1;
                    float angle = startAngle;
                    var c1 = math.sin(angle);
                    var s1 = math.cos(angle);
                    var startIndex = treadStart;
                    for (int n = 0, i = startIndex; n < stepCount; n++, i++)
                    {
                        var c0 = c1;
                        var s0 = s1;
                        angle += anglePerStep;
                        c1 = math.sin(angle);
                        s1 = math.cos(angle);

                        i0 = new float2(origin.x + (c0 * (stepInnerRadius              )), origin.z + (s0 * (stepInnerRadius              )));
                        i1 = new float2(origin.x + (c1 * (stepInnerRadius              )), origin.z + (s1 * (stepInnerRadius              )));
                        o0 = new float2(origin.x + (c0 * (stepOuterRadius + nosingWidth)), origin.z + (s0 * (stepOuterRadius + nosingWidth)));
                        o1 = new float2(origin.x + (c1 * (stepOuterRadius + nosingWidth)), origin.z + (s1 * (stepOuterRadius + nosingWidth)));

                        var noseSizeDeep = math.normalize(new float2((c0 - c1), (s0 - s1))) * nosingDepth;
                        i0 += noseSizeDeep;
                        o0 += noseSizeDeep;

                        //brushMesh.halfEdges = (anglePerStep > 0) ? invertedBoxHalfEdges.ToArray() : boxHalfEdges.ToArray();
                        brushMeshes[i] = CreateBox(new float3(i1.x, maxY, i1.y), // 1
                                                   new float3(i0.x, maxY, i0.y), // 0
                                                   new float3(o0.x, maxY, o0.y), // 3
                                                   new float3(o1.x, maxY, o1.y), // 2  
                                            
                                                   new float3(i1.x, minY, i1.y), // 5
                                                   new float3(i0.x, minY, i0.y), // 4
                                                   new float3(o0.x, minY, o0.y), // 7
                                                   new float3(o1.x, minY, o1.y), // 6
                                                   in surfaceDefinitionBlob,
                                                   allocator);

                        minY += stepHeight;
                        maxY += stepHeight;
                    }
                }


                {
                    var subMeshIndex = subMeshCount - cylinderSubMeshCount;
                
                    if (!BrushMeshFactory.GenerateCylinderSubMesh(outerDiameter + nosingWidth, origin.y, origin.y + height, 0, outerSides, fitToBounds: fitToBounds,
                                                                    in outerCylinderSurfaceDefinitionBlob,
                                                                    out var brushMeshBlob,
                                                                    allocator))
                        return false;
                    brushMeshes[subMeshIndex] = brushMeshBlob;
                    //brushContainer.operations[subMeshIndex] = CSGOperationType.Intersecting;
                }

                if (haveInnerCyl)
                {
                    var subMeshIndex = subMeshCount - 1;
                    if (!BrushMeshFactory.GenerateCylinderSubMesh(innerDiameter - nosingWidth, origin.y, origin.y + height, 0, innerSides, fitToBounds: fitToBounds,
                                                                    in innerCylinderSurfaceDefinitionBlob,
                                                                    out var brushMeshBlob,
                                                                    allocator))
                        return false;
                    brushMeshes[subMeshIndex] = brushMeshBlob;
                    //brushContainer.operations[subMeshIndex] = CSGOperationType.Subtractive;
                }
                return true;
            }
        }
    }
}