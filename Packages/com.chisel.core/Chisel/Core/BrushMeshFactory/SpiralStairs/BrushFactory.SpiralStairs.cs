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
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        // TODO: create helper method to cut brushes, use that instead of intersection + subtraction brushes
        // TODO: create spiral sides support

        public static bool GenerateSpiralStairs(ref BrushMesh[] brushMeshes, ref CSGOperationType[] operations, ref ChiselSpiralStairsDefinition definition)
        {
            definition.Validate();
            ref readonly var surfaceDefinition = ref definition.surfaceDefinition;
            if (surfaceDefinition == null ||
                surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length != 6)
            {
                brushMeshes = null;
                operations = null;
                return false;
            }
            
            const float kEpsilon = 0.001f;

            var nosingDepth		= definition.nosingDepth;
            var treadHeight		= (nosingDepth < kEpsilon) ? 0 : definition.treadHeight;
            var haveTread		= (treadHeight >= kEpsilon);

            var innerDiameter	= definition.innerDiameter;
            var haveInnerCyl	= (innerDiameter >= kEpsilon);

            var riserType		= definition.riserType;
            var haveRiser		= riserType != StairsRiserType.None;

            if (!haveRiser && !haveTread)
            {
                brushMeshes = null;
                operations = null;
                return false;
            }
            

            var origin		    = definition.origin;

            var startAngle	    = definition.startAngle   * Mathf.Deg2Rad;
            var anglePerStep    = definition.AnglePerStep * Mathf.Deg2Rad;

            var nosingWidth	    = definition.nosingWidth;
            var outerDiameter   = definition.outerDiameter;

            var p0 = new Vector2(Mathf.Sin(0                  ), Mathf.Cos(0                  ));
            var p1 = new Vector2(Mathf.Sin(anglePerStep       ), Mathf.Cos(anglePerStep       ));
            var pm = new Vector2(Mathf.Sin(anglePerStep * 0.5f), Mathf.Cos(anglePerStep * 0.5f));
            var pn = (p0 + p1) * 0.5f;

            var stepOuterDiameter		= outerDiameter + ((pm.magnitude - pn.magnitude) * (outerDiameter * 1.25f)); // TODO: figure out why we need the 1.25 magic number to fit the step in the outerDiameter?
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
            
            if (brushMeshes == null || operations == null ||
                brushMeshes.Length != operations.Length ||
                brushMeshes.Length != subMeshCount)
            {
                operations = new CSGOperationType[subMeshCount];
                brushMeshes = new BrushMesh[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                    brushMeshes[i] = new BrushMesh();
            } 

            if (haveRiser)
            {
                if (riserType == StairsRiserType.ThinRiser)
                {
                    var minY = origin.y;
                    var maxY = origin.y + stepHeight - treadHeight;
                    Vector2 o0, o1;
                    float angle = startAngle;
                    var c1 = Mathf.Sin(angle) * stepOuterRadius;
                    var s1 = Mathf.Cos(angle) * stepOuterRadius;
                    for (int i = 0; i < stepCount; i++)
                    {
                        var c0 = c1;
                        var s0 = s1;
                        angle += anglePerStep;
                        c1 = Mathf.Sin(angle) * stepOuterRadius;
                        s1 = Mathf.Cos(angle) * stepOuterRadius;

                        o0 = new Vector2(origin.x + c0, origin.z + s0);
                        o1 = new Vector2(origin.x     , origin.z     );

                        var riserVector = (new Vector2((c0 - c1), (s0 - s1)).normalized) * riserDepth;

                        var i0 = o0 - riserVector;
                        var i1 = o1 - riserVector;

                        var vertices = new[] {
                                                new Vector3( i0.x, maxY, i0.y), // 0
                                                new Vector3( i1.x, maxY, i1.y), // 1
                                                new Vector3( o1.x, maxY, o1.y), // 2
                                                new Vector3( o0.x, maxY, o0.y), // 3  
                                            
                                                new Vector3( i0.x, minY, i0.y), // 4
                                                new Vector3( i1.x, minY, i1.y), // 5
                                                new Vector3( o1.x, minY, o1.y), // 6
                                                new Vector3( o0.x, minY, o0.y), // 7
                                            };

                        ref var brushMesh = ref brushMeshes[i];
                        if (i == 0)
                        {
                            brushMesh.polygons = BrushMeshFactory.CreateBoxPolygons(surfaceDefinition);
                            minY -= treadHeight;
                        } else
                        {
                            brushMesh.polygons = brushMeshes[0].polygons.ToArray();
                        }
                        brushMesh.halfEdges	= (anglePerStep > 0) ? invertedBoxHalfEdges.ToArray() : boxHalfEdges.ToArray();
                        brushMesh.vertices	= vertices;

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
                    var c1 = Mathf.Sin(angle);
                    var s1 = Mathf.Cos(angle);
                    angle += anglePerStep;
                    var c2 = Mathf.Sin(angle);
                    var s2 = Mathf.Cos(angle);

                    for (int i = 0; i < riserSubMeshCount; i += subMeshPerRiser)
                    {
                        var c0 = c1;
                        var s0 = s1;
                        c1 = c2;
                        s1 = s2;
                        angle += anglePerStep;
                        c2 = Mathf.Sin(angle);
                        s2 = Mathf.Cos(angle);

                        var c0o = c0 * stepOuterRadius;
                        var c1o = c1 * stepOuterRadius;
                        var s0o = s0 * stepOuterRadius;
                        var s1o = s1 * stepOuterRadius;
                        
                        var o0 = new Vector2(origin.x + c0o, origin.z + s0o);
                        var o1 = new Vector2(origin.x + c1o, origin.z + s1o);

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

                            i0 = new Vector2(origin.x + c0i, origin.z + s0i);
                            i1 = new Vector2(origin.x + c1i, origin.z + s1i);

                            {
                                var vertices = new[] {
                                                        new Vector3(  i0.x, maxY,  i0.y), // 0
                                                        new Vector3(  i0.x, minY,  i0.y), // 1
                                                        new Vector3(  o0.x, minY,  o0.y), // 2  
                                                        new Vector3(  o0.x, maxY,  o0.y), // 3  

                                                        new Vector3(  o1.x, maxY,  o1.y), // 4
                                                    };

                                ref var brushMesh = ref brushMeshes[subMeshIndex];
                                if (i == 0)
                                {
                                    brushMesh.polygons = BrushMeshFactory.CreateSquarePyramidAssetPolygons(surfaceDefinition);
                                } else
                                {
                                    brushMesh.polygons = brushMeshes[subMeshIndex - i].polygons.ToArray();
                                }
                                brushMesh.halfEdges = (anglePerStep > 0) ? invertedSquarePyramidHalfEdges.ToArray() : squarePyramidHalfEdges.ToArray();
                                brushMesh.vertices = vertices;
                                subMeshIndex++;
                            }

                            {
                                var vertices = new[] {
                                                        new Vector3(  i0.x, maxY,  i0.y), // 0
                                                        new Vector3(  i0.x, minY,  i0.y), // 1
                                                        new Vector3(  i1.x, maxY,  i1.y), // 2

                                                        new Vector3(  o1.x, maxY,  o1.y), // 3
                                                    };

                                ref var brushMesh = ref brushMeshes[subMeshIndex];
                                if (i == 0)
                                {
                                    brushMesh.polygons = BrushMeshFactory.CreateTriangularPyramidAssetPolygons(surfaceDefinition);
                                } else
                                {
                                    brushMesh.polygons = brushMeshes[subMeshIndex - i].polygons.ToArray();
                                }
                                brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                brushMesh.vertices = vertices;
                                subMeshIndex++;
                            }
                            
                            o0 = i0;
                            o1 = i1;
                        }

                        {
                            var vertices = new[] {
                                                    new Vector3(  i0.x, maxY,  i0.y), // 0
                                                    new Vector3(  i1.x, maxY,  i1.y), // 2
                                                    new Vector3(  i0.x, minY,  i0.y), // 1

                                                    new Vector3( origin.x, minY, origin.y), // 3
                                                };

                            ref var brushMesh = ref brushMeshes[subMeshIndex];
                            if (i == 0)
                            {
                                brushMesh.polygons = BrushMeshFactory.CreateTriangularPyramidAssetPolygons(surfaceDefinition);
                            } else
                            {
                                brushMesh.polygons = brushMeshes[subMeshIndex - i].polygons.ToArray();
                            }
                            brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                            brushMesh.vertices = vertices;
                            subMeshIndex++;
                        }

                        {
                            var vertices = new[] {
                                                    new Vector3(  i1.x, maxY,  i1.y), // 2
                                                    new Vector3(  i0.x, maxY,  i0.y), // 0
                                                    new Vector3( origin.x, maxY, origin.y), // 1

                                                    new Vector3( origin.x, minY, origin.y), // 3
                                                };

                            ref var brushMesh = ref brushMeshes[subMeshIndex];
                            if (i == 0)
                            {
                                brushMesh.polygons = BrushMeshFactory.CreateTriangularPyramidAssetPolygons(surfaceDefinition);
                            } else
                            {
                                brushMesh.polygons = brushMeshes[subMeshIndex - i].polygons.ToArray();
                            }
                            brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                            brushMesh.vertices = vertices;
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
                    Vector2 o0, o1;
                    float angle = startAngle;
                    var c1 = Mathf.Sin(angle) * stepOuterRadius;
                    var s1 = Mathf.Cos(angle) * stepOuterRadius;
                    for (int i = 0; i < stepCount; i++)
                    {
                        var c0 = c1;
                        var s0 = s1;
                        angle += anglePerStep;
                        c1 = Mathf.Sin(angle) * stepOuterRadius;
                        s1 = Mathf.Cos(angle) * stepOuterRadius;

                        o0 = new Vector2(origin.x + c0, origin.z + s0);
                        o1 = new Vector2(origin.x + c1, origin.z + s1);
                        var vertices = new[] {
                                                new Vector3( origin.x, maxY, origin.z), // 0
                                                new Vector3(  o1.x, maxY,  o1.y), // 1
                                                new Vector3(  o0.x, maxY,  o0.y), // 2  

                                                new Vector3( origin.x, minY, origin.z), // 3
                                                new Vector3(  o1.x, minY,  o1.y), // 4
                                                new Vector3(  o0.x, minY,  o0.y), // 5
                                            };

                        ref var brushMesh = ref brushMeshes[i];
                        if (i == 0)
                        {
                            brushMesh.polygons = BrushMeshFactory.CreateWedgeAssetPolygons(surfaceDefinition);
                            minY -= treadHeight;
                        } else
                        {
                            brushMesh.polygons = brushMeshes[0].polygons.ToArray();
                        }
                        brushMesh.halfEdges = (anglePerStep > 0) ? invertedWedgeHalfEdges.ToArray() : wedgeHalfEdges.ToArray();
                        brushMesh.vertices = vertices;
                        
                        if (riserType != StairsRiserType.FillDown)
                            minY += stepHeight;
                        maxY += stepHeight;
                    }
                }
                
                {
                    var subMeshIndex = treadStart - cylinderSubMeshCount;
                    var cylinderSurfaceDefinition   = new ChiselSurfaceDefinition();
                    cylinderSurfaceDefinition.EnsureSize(outerSides + 2);
                    cylinderSurfaceDefinition.surfaces[0] = surfaceDefinition.surfaces[0];
                    cylinderSurfaceDefinition.surfaces[1] = surfaceDefinition.surfaces[1];
                    for (int i = 2; i < cylinderSurfaceDefinition.surfaces.Length; i++)
                        cylinderSurfaceDefinition.surfaces[i] = surfaceDefinition.surfaces[2];
                    
                    BrushMeshFactory.GenerateCylinderSubMesh(ref brushMeshes[subMeshIndex], outerDiameter, origin.y, origin.y + height, 0, outerSides, cylinderSurfaceDefinition);
                    operations[subMeshIndex] = CSGOperationType.Intersecting;
                }

                if (haveInnerCyl)
                {
                    var subMeshIndex = treadStart - 1;
                    var cylinderSurfaceDefinition   = new ChiselSurfaceDefinition();
                    cylinderSurfaceDefinition.EnsureSize(innerSides + 2);
                    cylinderSurfaceDefinition.surfaces[0] = surfaceDefinition.surfaces[0];
                    cylinderSurfaceDefinition.surfaces[1] = surfaceDefinition.surfaces[1];
                    for (int i = 2; i < cylinderSurfaceDefinition.surfaces.Length; i++)
                        cylinderSurfaceDefinition.surfaces[i] = surfaceDefinition.surfaces[2];

                    BrushMeshFactory.GenerateCylinderSubMesh(ref brushMeshes[subMeshIndex], innerDiameter, origin.y, origin.y + height, 0, innerSides, cylinderSurfaceDefinition);
                    operations[subMeshIndex] = CSGOperationType.Subtractive;
                }

            }

            if (haveTread)
            {
                var minY = origin.y + stepHeight - treadHeight;
                var maxY = origin.y + stepHeight;
                Vector2 i0, i1, o0, o1;
                float angle = startAngle;
                var c1 = Mathf.Sin(angle);
                var s1 = Mathf.Cos(angle);
                var startIndex = treadStart;
                for (int n = 0, i = startIndex; n < stepCount; n++, i++)
                {
                    var c0 = c1;
                    var s0 = s1;
                    angle += anglePerStep;
                    c1 = Mathf.Sin(angle);
                    s1 = Mathf.Cos(angle);

                    i0 = new Vector2(origin.x + (c0 * (stepInnerRadius              )), origin.z + (s0 * (stepInnerRadius              )));
                    i1 = new Vector2(origin.x + (c1 * (stepInnerRadius              )), origin.z + (s1 * (stepInnerRadius              )));
                    o0 = new Vector2(origin.x + (c0 * (stepOuterRadius + nosingWidth)), origin.z + (s0 * (stepOuterRadius + nosingWidth)));
                    o1 = new Vector2(origin.x + (c1 * (stepOuterRadius + nosingWidth)), origin.z + (s1 * (stepOuterRadius + nosingWidth)));

                    var noseSizeDeep = (new Vector2((c0 - c1), (s0 - s1)).normalized) * nosingDepth;
                    i0 += noseSizeDeep;
                    o0 += noseSizeDeep;

                    var vertices = new[] {
                                            new Vector3( i1.x, maxY, i1.y), // 1
                                            new Vector3( i0.x, maxY, i0.y), // 0
                                            new Vector3( o0.x, maxY, o0.y), // 3
                                            new Vector3( o1.x, maxY, o1.y), // 2  
                                            
                                            new Vector3( i1.x, minY, i1.y), // 5
                                            new Vector3( i0.x, minY, i0.y), // 4
                                            new Vector3( o0.x, minY, o0.y), // 7
                                            new Vector3( o1.x, minY, o1.y), // 6
                                        };
                    ref var brushMesh = ref brushMeshes[i];
                    if (n == 0)
                    {
                        brushMesh.polygons = BrushMeshFactory.CreateBoxPolygons(surfaceDefinition);
                    } else
                        brushMesh.polygons = brushMeshes[startIndex].polygons.ToArray();

                    brushMesh.halfEdges = (anglePerStep > 0) ? invertedBoxHalfEdges.ToArray() : boxHalfEdges.ToArray();
                    brushMesh.vertices  = vertices;

                    minY += stepHeight;
                    maxY += stepHeight;
                }
            }


            {
                var subMeshIndex = subMeshCount - cylinderSubMeshCount;
                var cylinderSurfaceDefinition   = new ChiselSurfaceDefinition();
                cylinderSurfaceDefinition.EnsureSize(outerSides + 2);
                cylinderSurfaceDefinition.surfaces[0] = surfaceDefinition.surfaces[0];
                cylinderSurfaceDefinition.surfaces[1] = surfaceDefinition.surfaces[1];
                for (int i = 2; i < cylinderSurfaceDefinition.surfaces.Length; i++)
                    cylinderSurfaceDefinition.surfaces[i] = surfaceDefinition.surfaces[2];
                
                BrushMeshFactory.GenerateCylinderSubMesh(ref brushMeshes[subMeshIndex], outerDiameter + nosingWidth, origin.y, origin.y + height, 0, outerSides, cylinderSurfaceDefinition);
                operations[subMeshIndex] = CSGOperationType.Intersecting;
            }

            if (haveInnerCyl)
            {
                var subMeshIndex = subMeshCount - 1;
                var cylinderSurfaceDefinition   = new ChiselSurfaceDefinition();
                cylinderSurfaceDefinition.EnsureSize(innerSides + 2);
                cylinderSurfaceDefinition.surfaces[0] = surfaceDefinition.surfaces[0];
                cylinderSurfaceDefinition.surfaces[1] = surfaceDefinition.surfaces[1];
                for (int i = 2; i < cylinderSurfaceDefinition.surfaces.Length; i++)
                    cylinderSurfaceDefinition.surfaces[i] = surfaceDefinition.surfaces[2];

                BrushMeshFactory.GenerateCylinderSubMesh(ref brushMeshes[subMeshIndex], innerDiameter - nosingWidth, origin.y, origin.y + height, 0, innerSides, cylinderSurfaceDefinition);
                operations[subMeshIndex] = CSGOperationType.Subtractive;
            }
            return true;
        }
    }
}