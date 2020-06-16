using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    public struct PlanePair
    {
        public float4 plane0;
        public float4 plane1;
        public float4 edgeVertex0;
        public float4 edgeVertex1;
        public int planeIndex0;
        public int planeIndex1;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FindBrushPairsJob : IJob
    {
        public struct Empty { }

        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                       treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>                                              nodeIndexToNodeOrder;
        [NoAlias, ReadOnly] public int                                                           nodeIndexToNodeOrderOffset;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;
        [NoAlias, WriteOnly] public NativeList<BrushPair>                                        uniqueBrushPairs;

        public void Execute()
        {
            var maxPairs = GeometryMath.GetTriangleArraySize(treeBrushIndexOrders.Length);
            var brushPairMap = new NativeHashMap<BrushPair, FindBrushPairsJob.Empty>(maxPairs, Allocator.Temp);
            var empty = new Empty();
            for (int b0 = 0; b0 < treeBrushIndexOrders.Length; b0++)
            {
                var brushIndexOrder0        = treeBrushIndexOrders[b0];
                int brushNodeIndex0         = brushIndexOrder0.nodeIndex;
                int brushNodeOrder0         = brushIndexOrder0.nodeOrder;
                //var brushesTouchedByBrush = touchedBrushesByTreeBrushes[b0];
                if (!brushesTouchedByBrushes.TryGetValue(brushNodeIndex0, out BlobAssetReference<BrushesTouchedByBrush> brushesTouchedByBrush))
                    continue;
                    
                ref var intersections = ref brushesTouchedByBrush.Value.brushIntersections;
                if (intersections.Length == 0)
                    continue;

                // Find all intersections between brushes
                for (int i = 0; i < intersections.Length; i++)
                {
                    var intersection        = intersections[i];
                    int brushIndex1         = intersection.nodeIndex;
                    int brushOrder1         = nodeIndexToNodeOrder[brushIndex1 - nodeIndexToNodeOrderOffset];
                    var brushNodeOrder1     = new IndexOrder { nodeIndex = brushIndex1, nodeOrder = brushOrder1 };

                    var brushPair       = new BrushPair
                    {
                        type             = intersection.type,
                        brushIndexOrder0 = brushIndexOrder0,
                        brushIndexOrder1 = brushNodeOrder1
                    };

                    if (brushNodeOrder0 > brushOrder1) // ensures we do calculations exactly the same for each brush pair
                        brushPair.Flip();

                    if (brushPairMap.TryAdd(brushPair, empty))
                    {
                        uniqueBrushPairs.AddNoResize(brushPair);
                    }
                }
            }
            brushPairMap.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareBrushPairIntersectionsJob : IJobParallelFor
    {
        const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;
        const float kPlaneWAlignEpsilon         = CSGConstants.kPlaneDAlignEpsilon;
        const float kNormalDotAlignEpsilon      = CSGConstants.kNormalDotAlignEpsilon;

        [NoAlias, ReadOnly] public NativeArray<BrushPair>                           uniqueBrushPairs;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>   brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                 transformations;
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushPairIntersection>>.ParallelWriter intersectingBrushes;

        // TODO: turn into job
        static unsafe void GetIntersectingPlanes(ref BlobArray<float4> localPlanes, ref BlobArray<float3> vertices, Bounds selfBounds, float4x4 treeToNodeSpaceInverseTransposed, int* intersectingPlanesPtr, out int intersectingPlaneLength)
        {
            var min = (float3)selfBounds.min;
            var max = (float3)selfBounds.max;

            intersectingPlaneLength = 0;
            var verticesLength = vertices.Length;
            for (int i = 0; i < localPlanes.Length; i++)
            {
                // bring plane into local space of mesh, the same space as the bounds of the mesh

                var localPlane = localPlanes[i];

                // note: a transpose is part of this transformation
                var transformedPlane = math.mul(treeToNodeSpaceInverseTransposed, localPlane);
                //var normal            = transformedPlane.xyz; // only need the signs, so don't care about normalization
                //transformedPlane /= math.length(normal);      // we don't have to normalize the plane

                var corner = new float4((transformedPlane.x < 0) ? max.x : min.x,
                                        (transformedPlane.y < 0) ? max.y : min.y,
                                        (transformedPlane.z < 0) ? max.z : min.z,
                                        1.0f);
                float forward = math.dot(transformedPlane, corner);
                if (forward > kFatPlaneWidthEpsilon) // closest point is outside
                {
                    intersectingPlaneLength = 0;
                    return;
                }

                // do a bounds check
                corner = new float4((transformedPlane.x >= 0) ? max.x : min.x,
                                    (transformedPlane.y >= 0) ? max.y : min.y,
                                    (transformedPlane.z >= 0) ? max.z : min.z,
                                    1.0f);
                float backward = math.dot(transformedPlane, corner);
                if (backward < -kFatPlaneWidthEpsilon) // closest point is inside
                    continue;

                float minDistance = float.PositiveInfinity;
                float maxDistance = float.NegativeInfinity;
                int onCount = 0;
                for (int v = 0; v < verticesLength; v++)
                {
                    float distance = math.dot(transformedPlane, new float4(vertices[v], 1));
                    minDistance = math.min(distance, minDistance);
                    maxDistance = math.max(distance, maxDistance);
                    onCount += (distance >= -kFatPlaneWidthEpsilon && distance <= kFatPlaneWidthEpsilon) ? 1 : 0;
                }

                // if all vertices are 'inside' this plane, then we're not truly intersecting with it
                if ((minDistance > kFatPlaneWidthEpsilon || maxDistance < -kFatPlaneWidthEpsilon))
                    continue;

                intersectingPlanesPtr[intersectingPlaneLength] = i;
                intersectingPlaneLength++;
            }
        }
        
        // TODO: turn into job
        unsafe static void FindPlanePairs(ref BrushMeshBlob         mesh,
                                          ref BlobBuilderArray<int> intersectingPlanes,
                                          float4*                   localSpacePlanesPtr,
                                          int*                      vertexUsedPtr,
                                          float4x4                  vertexTransform,
                                          PlanePair*                usedPlanePairsPtr,
                                          out int                   usedPlanePairsLength,
                                          out int                   usedVerticesLength)
        {
            //using (new ProfileSample("FindPlanePairs"))
            {
                // TODO: this can be partially stored in brushmesh 
                // TODO: optimize
                
                ref var halfEdgePolygonIndices  = ref mesh.halfEdgePolygonIndices;
                ref var halfEdges               = ref mesh.halfEdges;

                usedVerticesLength = 0;
                usedPlanePairsLength = 0;
                var planeAvailable  = stackalloc byte[mesh.localPlanes.Length];
                {
                    {
                        for (int p = 0; p < intersectingPlanes.Length; p++)
                        {
                            planeAvailable[intersectingPlanes[p]] = 1;
                        }
                    }

                    for (int e = 0; e < halfEdges.Length; e++)
                    {
                        var twinIndex = halfEdges[e].twinIndex;
                        if (twinIndex < e)
                            continue;

                        var planeIndex0 = halfEdgePolygonIndices[e];
                        var planeIndex1 = halfEdgePolygonIndices[twinIndex];

                        //Debug.Assert(planeIndex0 != planeIndex1);

                        if (planeAvailable[planeIndex0] == 0 ||
                            planeAvailable[planeIndex1] == 0)
                            continue;

                        var plane0 = localSpacePlanesPtr[planeIndex0];
                        var plane1 = localSpacePlanesPtr[planeIndex1];

                        var vertexIndex0 = halfEdges[e].vertexIndex;
                        var vertexIndex1 = halfEdges[twinIndex].vertexIndex;

                        var vertex0 = math.mul(vertexTransform, new float4(mesh.localVertices[vertexIndex0], 1));
                        var vertex1 = math.mul(vertexTransform, new float4(mesh.localVertices[vertexIndex1], 1));

                        if (vertexUsedPtr[vertexIndex0] == 0) { vertexUsedPtr[vertexIndex0] = vertexIndex0 + 1; usedVerticesLength++; }
                        if (vertexUsedPtr[vertexIndex1] == 0) { vertexUsedPtr[vertexIndex1] = vertexIndex1 + 1; usedVerticesLength++; }
                        usedPlanePairsPtr[usedPlanePairsLength] = new PlanePair
                        {
                            plane0 = plane0,
                            plane1 = plane1,
                            edgeVertex0 = vertex0,
                            edgeVertex1 = vertex1,
                            planeIndex0 = planeIndex0,
                            planeIndex1 = planeIndex1
                        };
                        usedPlanePairsLength++;
                    }
                }
            }
        }

        public unsafe void Execute(int index)
        {
            if (index >= uniqueBrushPairs.Length)
                return;

            var brushPair           = uniqueBrushPairs[index];
            var brushIndexOrder0    = brushPair.brushIndexOrder0;
            var brushIndexOrder1    = brushPair.brushIndexOrder1;
            int brushNodeIndex0     = brushIndexOrder0.nodeIndex;
            int brushNodeOrder0     = brushIndexOrder0.nodeOrder;
            int brushNodeIndex1     = brushIndexOrder1.nodeIndex;
            int brushNodeOrder1     = brushIndexOrder1.nodeOrder;

            var blobMesh0 = brushMeshLookup[brushNodeOrder0];
            var blobMesh1 = brushMeshLookup[brushNodeOrder1];


            var type = brushPair.type;
            if (type != IntersectionType.Intersection &&
                type != IntersectionType.AInsideB &&
                type != IntersectionType.BInsideA)
                return;


            var transformations0 = transformations[brushNodeOrder0];
            var transformations1 = transformations[brushNodeOrder1];

            var node1ToNode0            = math.mul(transformations0.treeToNode, transformations1.nodeToTree);
            var node0ToNode1            = math.mul(transformations1.treeToNode, transformations0.nodeToTree);
            var inversedNode1ToNode0    = math.transpose(node0ToNode1);
            var inversedNode0ToNode1    = math.transpose(node1ToNode0);

            ref var mesh0 = ref blobMesh0.Value;
            ref var mesh1 = ref blobMesh1.Value;
            
            var builder = new BlobBuilder(Allocator.Temp, 4096);
            ref var root = ref builder.ConstructRoot<BrushPairIntersection>();
            root.type = type;

            var brushIntersections = builder.Allocate(ref root.brushes, 2);
            brushIntersections[0] = new BrushIntersectionInfo
            {
                brushIndexOrder     = brushIndexOrder0,
                nodeToTreeSpace     = transformations0.nodeToTree,
                toOtherBrushSpace   = node0ToNode1
            };
            brushIntersections[1] = new BrushIntersectionInfo
            {
                brushIndexOrder     = brushIndexOrder1,
                nodeToTreeSpace     = transformations1.nodeToTree,
                toOtherBrushSpace   = node1ToNode0
            };

            BlobBuilderArray<int> intersectingPlaneIndices0, intersectingPlaneIndices1;
            if (type == IntersectionType.Intersection)
            {
                {
                    int intersectingPlanesLength = 0;
                    var intersectingPlanesPtr = stackalloc int[mesh0.localPlanes.Length];
                    GetIntersectingPlanes(ref mesh0.localPlanes, ref mesh1.localVertices, mesh1.localBounds, inversedNode0ToNode1, intersectingPlanesPtr, out intersectingPlanesLength);
                    if (intersectingPlanesLength == 0) { builder.Dispose(); return; }
                    intersectingPlaneIndices0 = builder.Construct(ref brushIntersections[0].localSpacePlaneIndices0, intersectingPlanesPtr, intersectingPlanesLength);
                }

                {
                    int intersectingPlanesLength = 0;
                    var intersectingPlanesPtr = stackalloc int[mesh1.localPlanes.Length];
                    GetIntersectingPlanes(ref mesh1.localPlanes, ref mesh0.localVertices, mesh0.localBounds, inversedNode1ToNode0, intersectingPlanesPtr, out intersectingPlanesLength);
                    if (intersectingPlanesLength == 0) { builder.Dispose(); return; }
                    intersectingPlaneIndices1 = builder.Construct(ref brushIntersections[1].localSpacePlaneIndices0, intersectingPlanesPtr, intersectingPlanesLength);
                }
            } else
            //if (type == IntersectionType.AInsideB || type == IntersectionType.BInsideA)
            {
                intersectingPlaneIndices0 = builder.Allocate(ref brushIntersections[0].localSpacePlaneIndices0, mesh0.localPlanes.Length);
                intersectingPlaneIndices1 = builder.Allocate(ref brushIntersections[1].localSpacePlaneIndices0, mesh1.localPlanes.Length);
                for (int i = 0; i < intersectingPlaneIndices0.Length; i++) intersectingPlaneIndices0[i] = i;
                for (int i = 0; i < intersectingPlaneIndices1.Length; i++) intersectingPlaneIndices1[i] = i;
            }



            var inverseNodeToTreeSpaceMatrix0 = math.transpose(transformations0.treeToNode);
            var inverseNodeToTreeSpaceMatrix1 = math.transpose(transformations1.treeToNode);


            var surfaceInfos0 = builder.Allocate(ref brushIntersections[0].surfaceInfos, mesh0.localPlanes.Length);
            var surfaceInfos1 = builder.Allocate(ref brushIntersections[1].surfaceInfos, mesh1.localPlanes.Length);
            for (int i = 0; i < surfaceInfos0.Length; i++)
            {
                surfaceInfos0[i] = new SurfaceInfo
                {
                    interiorCategory    = (CategoryGroupIndex)CategoryIndex.Inside,
                    basePlaneIndex      = (ushort)i,
                    brushIndex          = brushIndexOrder1.nodeIndex
                };
            }
            for (int i = 0; i < surfaceInfos1.Length; i++)
            {
                surfaceInfos1[i] = new SurfaceInfo
                {
                    interiorCategory    = (CategoryGroupIndex)CategoryIndex.Inside,
                    basePlaneIndex      = (ushort)i,
                    brushIndex          = brushIndexOrder0.nodeIndex
                };
            }


            // TODO: we don't actually use ALL of these planes .. Optimize this
            var localSpacePlanes0Length = mesh0.localPlanes.Length;
            var localSpacePlanes0 = stackalloc float4[localSpacePlanes0Length];
            for (int p = 0; p < localSpacePlanes0Length; p++)
                localSpacePlanes0[p] = mesh0.localPlanes[p];

            // TODO: we don't actually use ALL of these planes .. Optimize this
            var localSpacePlanes1Length = mesh1.localPlanes.Length;
            var localSpacePlanes1 = stackalloc float4[localSpacePlanes1Length];
            for (int p = 0; p < localSpacePlanes1Length; p++)
            {
                var transformedPlane = math.mul(inversedNode1ToNode0, mesh1.localPlanes[p]);
                localSpacePlanes1[p] = transformedPlane / math.length(transformedPlane.xyz);
            }


            BlobBuilderArray<float3> usedVertices0;
            BlobBuilderArray<float3> usedVertices1;
            if (type != IntersectionType.Intersection)
            {
                var intersectingPlanes0 = builder.Construct(ref brushIntersections[0].localSpacePlanes0, localSpacePlanes0, localSpacePlanes0Length);
                var intersectingPlanes1 = builder.Construct(ref brushIntersections[1].localSpacePlanes0, localSpacePlanes1, localSpacePlanes1Length);

                builder.Allocate(ref brushIntersections[0].usedPlanePairs, 0);
                builder.Allocate(ref brushIntersections[1].usedPlanePairs, 0);

                usedVertices0 = builder.Allocate(ref brushIntersections[0].usedVertices, mesh0.localVertices.Length);
                usedVertices1 = builder.Allocate(ref brushIntersections[1].usedVertices, mesh1.localVertices.Length);
                for (int i = 0; i < usedVertices0.Length; i++) usedVertices0[i] = mesh0.localVertices[i];
                for (int i = 0; i < usedVertices1.Length; i++) usedVertices1[i] = mesh1.localVertices[i];
            } else
            {
                var intersectingPlanes0 = builder.Allocate(ref brushIntersections[0].localSpacePlanes0, intersectingPlaneIndices0.Length);
                var intersectingPlanes1 = builder.Allocate(ref brushIntersections[1].localSpacePlanes0, intersectingPlaneIndices1.Length);
                for (int i = 0; i < intersectingPlaneIndices0.Length; i++)
                    intersectingPlanes0[i] = localSpacePlanes0[intersectingPlaneIndices0[i]];
                for (int i = 0; i < intersectingPlaneIndices1.Length; i++)
                    intersectingPlanes1[i] = localSpacePlanes1[intersectingPlaneIndices1[i]];

                {
                    int usedVerticesLength;
                    var vertexUsedPtr = stackalloc int[mesh0.localVertices.Length];
                    {
                        var usedPlanePairsPtr = stackalloc PlanePair[mesh0.halfEdges.Length];
                        FindPlanePairs(ref mesh0, ref intersectingPlaneIndices0, localSpacePlanes0, vertexUsedPtr, float4x4.identity, usedPlanePairsPtr, out int usedPlanePairsLength, out usedVerticesLength);
                        builder.Construct(ref brushIntersections[0].usedPlanePairs, usedPlanePairsPtr, usedPlanePairsLength);
                    }
                    usedVertices0 = builder.Allocate(ref brushIntersections[0].usedVertices, usedVerticesLength);
                    if (usedVerticesLength > 0)
                    {
                        for (int i = 0, n = 0; i < mesh0.localVertices.Length; i++)
                        {
                            if (vertexUsedPtr[i] == 0)
                                continue;
                            usedVertices0[n] = mesh0.localVertices[vertexUsedPtr[i] - 1];
                            n++;
                        }
                    }
                }
                {
                    int usedVerticesLength;
                    var vertexUsedPtr = stackalloc int[mesh1.localVertices.Length];
                    {
                        var usedPlanePairsPtr = stackalloc PlanePair[mesh1.halfEdges.Length];
                        FindPlanePairs(ref mesh1, ref intersectingPlaneIndices1, localSpacePlanes1, vertexUsedPtr, node1ToNode0, usedPlanePairsPtr, out int usedPlanePairsLength, out usedVerticesLength);
                        builder.Construct(ref brushIntersections[1].usedPlanePairs, usedPlanePairsPtr, usedPlanePairsLength);
                    }
                    usedVertices1 = builder.Allocate(ref brushIntersections[1].usedVertices, usedVerticesLength);
                    if (usedVerticesLength > 0)
                    {
                        for (int i = 0, n = 0; i < mesh1.localVertices.Length; i++)
                        {
                            if (vertexUsedPtr[i] == 0)
                                continue;
                            usedVertices1[n] = mesh1.localVertices[vertexUsedPtr[i] - 1];
                            n++;
                        }
                    }
                }



                // decide which planes of brush1 align with brush2
                // TODO: optimize
                // TODO: should do this as a separate pass

                for (int i1 = 0; i1 < intersectingPlaneIndices0.Length; i1++)
                {
                    var p1          = intersectingPlaneIndices0[i1];
                    var localPlane1 = localSpacePlanes0[p1];
                    for (int i2 = 0; i2 < intersectingPlaneIndices1.Length; i2++)
                    {
                        var p2          = intersectingPlaneIndices1[i2];
                        var localPlane2 = localSpacePlanes1[p2];
                        if (math.abs(localPlane1.w - localPlane2.w) >= kPlaneWAlignEpsilon ||
                            math.dot(localPlane1.xyz, localPlane2.xyz) < kNormalDotAlignEpsilon)
                        {
                            localPlane2 = -localPlane2;
                            if (math.abs(localPlane1.w - localPlane2.w) >= kPlaneWAlignEpsilon ||
                                math.dot(localPlane1.xyz, localPlane2.xyz) < kNormalDotAlignEpsilon)
                                continue;

                            var surfaceInfo0 = surfaceInfos0[p1];
                            surfaceInfo0.interiorCategory = (CategoryGroupIndex)CategoryIndex.ReverseAligned;
                            surfaceInfos0[p1] = surfaceInfo0;
                            var surfaceInfo1 = surfaceInfos1[p2];
                            surfaceInfo1.interiorCategory = (CategoryGroupIndex)CategoryIndex.ReverseAligned;
                            surfaceInfos1[p2] = surfaceInfo1;
                        } else
                        {
                            var surfaceInfo0 = surfaceInfos0[p1];
                            surfaceInfo0.interiorCategory = (CategoryGroupIndex)CategoryIndex.Aligned;
                            surfaceInfos0[p1] = surfaceInfo0;
                            var surfaceInfo1 = surfaceInfos1[p2];
                            surfaceInfo1.interiorCategory = (CategoryGroupIndex)CategoryIndex.Aligned;
                            surfaceInfos1[p2] = surfaceInfo1;
                        }
                    }
                }
            }


            {
                var vertexIntersectionPlanes0       = stackalloc ushort[usedVertices0.Length * localSpacePlanes0Length];
                var vertexIntersectionSegments0     = stackalloc int2[usedVertices0.Length];
                var vertexIntersectionPlaneCount    = 0;

                for (int i = 0; i < usedVertices0.Length; i++)
                {
                    var segment = new int2(vertexIntersectionPlaneCount, 0);
                    for (int j = 0; j < intersectingPlaneIndices0.Length; j++)
                    {
                        var planeIndex = intersectingPlaneIndices0[j];
                        var distance = math.dot(mesh0.localPlanes[planeIndex], new float4(usedVertices0[i], 1));
                        if (distance >= -kPlaneWAlignEpsilon && distance <= kPlaneWAlignEpsilon) // Note: this is false on NaN/Infinity, so don't invert
                        {
                            vertexIntersectionPlanes0[vertexIntersectionPlaneCount] = (ushort)planeIndex;
                            vertexIntersectionPlaneCount++;
                        }
                    }
                    segment.y = vertexIntersectionPlaneCount - segment.x;
                    vertexIntersectionSegments0[i] = segment;
                }
                if (vertexIntersectionPlaneCount > 0)
                {
                    builder.Construct(ref brushIntersections[0].vertexIntersectionPlanes, vertexIntersectionPlanes0, vertexIntersectionPlaneCount);
                    builder.Construct(ref brushIntersections[0].vertexIntersectionSegments, vertexIntersectionSegments0, usedVertices0.Length);
                } else
                {
                    var vertexIntersectionPlanes = builder.Allocate(ref brushIntersections[0].vertexIntersectionPlanes, 1);
                    vertexIntersectionPlanes[0] = 0;
                    var vertexIntersectionSegments = builder.Allocate(ref brushIntersections[0].vertexIntersectionSegments, usedVertices0.Length);
                    for (int i = 0; i < vertexIntersectionSegments.Length;i++)
                        vertexIntersectionSegments[i] = int2.zero;
                }
            }


            {
                var vertexIntersectionPlanes1       = stackalloc ushort[usedVertices1.Length * localSpacePlanes1Length];
                var vertexIntersectionSegments1     = stackalloc int2[usedVertices1.Length];
                var vertexIntersectionPlaneCount    = 0;

                for (int i = 0; i < usedVertices1.Length; i++)
                {
                    var segment = new int2(vertexIntersectionPlaneCount, 0);
                    for (int j = 0; j < intersectingPlaneIndices1.Length; j++)
                    {
                        var planeIndex = intersectingPlaneIndices1[j];
                        var distance = math.dot(mesh1.localPlanes[planeIndex], new float4(usedVertices1[i], 1));
                        if (distance >= -kPlaneWAlignEpsilon && distance <= kPlaneWAlignEpsilon) // Note: this is false on NaN/Infinity, so don't invert
                        {
                            vertexIntersectionPlanes1[vertexIntersectionPlaneCount] = (ushort)planeIndex;
                            vertexIntersectionPlaneCount++;
                        }
                    }
                    segment.y = vertexIntersectionPlaneCount - segment.x;
                    vertexIntersectionSegments1[i] = segment;
                }
                if (vertexIntersectionPlaneCount > 0)
                {
                    builder.Construct(ref brushIntersections[1].vertexIntersectionPlanes, vertexIntersectionPlanes1, vertexIntersectionPlaneCount);
                    builder.Construct(ref brushIntersections[1].vertexIntersectionSegments, vertexIntersectionSegments1, usedVertices1.Length);
                } else
                {
                    var vertexIntersectionPlanes = builder.Allocate(ref brushIntersections[1].vertexIntersectionPlanes, 1);
                    vertexIntersectionPlanes[0] = 0;
                    var vertexIntersectionSegments = builder.Allocate(ref brushIntersections[1].vertexIntersectionSegments, usedVertices1.Length);
                    for (int i = 0; i < vertexIntersectionSegments.Length; i++)
                        vertexIntersectionSegments[i] = int2.zero;
                }
            }


            var result = builder.CreateBlobAssetReference<BrushPairIntersection>(Allocator.TempJob);
            builder.Dispose();

            intersectingBrushes.AddNoResize(result);
        }
    }
}
