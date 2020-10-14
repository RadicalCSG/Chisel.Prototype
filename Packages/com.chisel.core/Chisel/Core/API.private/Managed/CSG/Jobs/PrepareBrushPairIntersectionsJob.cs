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
        public float4   plane0;
        public float4   plane1;
        public float4   edgeVertex0;
        public float4   edgeVertex1;
        public int      planeIndex0;
        public int      planeIndex1;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareBrushPairIntersectionsJob : IJobParallelFor
    {
        const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;
        const float kPlaneWAlignEpsilon         = CSGConstants.kPlaneDAlignEpsilon;
        const float kNormalDotAlignEpsilon      = CSGConstants.kNormalDotAlignEpsilon;

        // Read
        [NoAlias, ReadOnly] public NativeArray<BrushPair2>                                  uniqueBrushPairs;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                         transformationCache;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly  brushMeshLookup;

        // Write
        [NoAlias, WriteOnly] public NativeStream.Writer                                     intersectingBrushesStream;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<int>       intersectingPlanes;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>    localSpacePlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>    localSpacePlanes1;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>       vertexUsed;
        [NativeDisableContainerSafetyRestriction] NativeArray<PlanePair> usedPlanePairs;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>    vertexIntersectionPlanes;
        [NativeDisableContainerSafetyRestriction] NativeBitArray         planeAvailable;


        // TODO: turn into job
        static void GetIntersectingPlanes([NoAlias] ref BlobArray<float4> localPlanes, 
                                          [NoAlias] ref BlobArray<float3> vertices, 
                                          Bounds selfBounds, float4x4 treeToNodeSpaceInverseTransposed, 
                                          [NoAlias] ref NativeArray<int> intersectingPlanes, 
                                          [NoAlias] out int intersectingPlaneLength)
        {
            var min = (float3)selfBounds.min;
            var max = (float3)selfBounds.max;

            //Debug.Log($"{localPlanes.Length}");

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

                intersectingPlanes[intersectingPlaneLength] = i;
                intersectingPlaneLength++;
            }
        }
        
        // TODO: turn into job
        void FindPlanePairs([NoAlias] ref BrushMeshBlob         mesh,
                            [NoAlias] ref BlobBuilderArray<int> intersectingPlanes,
                            [NoAlias] NativeArray<float4>       localSpacePlanesPtr,
                            [NoAlias] NativeArray<int>          vertexUsed,
                            float4x4                            vertexTransform,
                            [NoAlias] NativeArray<PlanePair>    usedPlanePairsPtr,
                            out int     usedPlanePairsLength,
                            out int     usedVerticesLength)
        {
            // TODO: this can be partially stored in brushmesh 
            // TODO: optimize
                
            ref var halfEdgePolygonIndices  = ref mesh.halfEdgePolygonIndices;
            ref var halfEdges               = ref mesh.halfEdges;


            if (!planeAvailable.IsCreated || planeAvailable.Length < mesh.localPlanes.Length)
            {
                if (planeAvailable.IsCreated) planeAvailable.Dispose();
                planeAvailable = new NativeBitArray(mesh.localPlanes.Length, Allocator.Temp);
            } else
                planeAvailable.Clear();

            usedVerticesLength = 0;
            usedPlanePairsLength = 0;
            
            for (int p = 0; p < intersectingPlanes.Length; p++)
                planeAvailable.Set(intersectingPlanes[p], true);

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var twinIndex = halfEdges[e].twinIndex;
                if (twinIndex < e)
                    continue;

                var planeIndex0 = halfEdgePolygonIndices[e];
                var planeIndex1 = halfEdgePolygonIndices[twinIndex];

                //Debug.Assert(planeIndex0 != planeIndex1);

                if (!planeAvailable.IsSet(planeIndex0) ||
                    !planeAvailable.IsSet(planeIndex1))
                    continue;

                var plane0 = localSpacePlanesPtr[planeIndex0];
                var plane1 = localSpacePlanesPtr[planeIndex1];

                var vertexIndex0 = halfEdges[e].vertexIndex;
                var vertexIndex1 = halfEdges[twinIndex].vertexIndex;

                var vertex0 = math.mul(vertexTransform, new float4(mesh.localVertices[vertexIndex0], 1));
                var vertex1 = math.mul(vertexTransform, new float4(mesh.localVertices[vertexIndex1], 1));

                if (vertexUsed[vertexIndex0] == 0) { vertexUsed[vertexIndex0] = vertexIndex0 + 1; usedVerticesLength++; }
                if (vertexUsed[vertexIndex1] == 0) { vertexUsed[vertexIndex1] = vertexIndex1 + 1; usedVerticesLength++; }
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


        BlobAssetReference<BrushPairIntersection> Create(int index)
        {
            if (index >= uniqueBrushPairs.Length)
                return BlobAssetReference<BrushPairIntersection>.Null;

            var brushPair           = uniqueBrushPairs[index];

            if (brushPair.type == IntersectionType.InvalidValue)
                return BlobAssetReference<BrushPairIntersection>.Null;

            var brushIndexOrder0    = brushPair.brushIndexOrder0;
            var brushIndexOrder1    = brushPair.brushIndexOrder1;
            int brushNodeOrder0     = brushIndexOrder0.nodeOrder;
            int brushNodeOrder1     = brushIndexOrder1.nodeOrder;

            var blobMesh0 = brushMeshLookup[brushNodeOrder0];
            var blobMesh1 = brushMeshLookup[brushNodeOrder1];


            var type = brushPair.type;
            if (type != IntersectionType.Intersection &&
                type != IntersectionType.AInsideB &&
                type != IntersectionType.BInsideA)
                return BlobAssetReference<BrushPairIntersection>.Null;


            var transformations0 = transformationCache[brushNodeOrder0];
            var transformations1 = transformationCache[brushNodeOrder1];

            var node1ToNode0 = math.mul(transformations0.treeToNode, transformations1.nodeToTree);
            var node0ToNode1 = math.mul(transformations1.treeToNode, transformations0.nodeToTree);
            var inversedNode1ToNode0 = math.transpose(node0ToNode1);
            var inversedNode0ToNode1 = math.transpose(node1ToNode0);

            ref var mesh0 = ref blobMesh0.Value;
            ref var mesh1 = ref blobMesh1.Value;

            var builder = new BlobBuilder(Allocator.Temp, 2048);
            ref var root = ref builder.ConstructRoot<BrushPairIntersection>();
            root.type = type;

            root.brush0 = new BrushIntersectionInfo
            {
                brushIndexOrder     = brushIndexOrder0,
                nodeToTreeSpace     = transformations0.nodeToTree,
                toOtherBrushSpace   = node0ToNode1
            };
            root.brush1 = new BrushIntersectionInfo
            {
                brushIndexOrder     = brushIndexOrder1,
                nodeToTreeSpace     = transformations1.nodeToTree,
                toOtherBrushSpace   = node1ToNode0
            };

            BlobBuilderArray<int> intersectingPlaneIndices0, intersectingPlaneIndices1;
            if (type == IntersectionType.Intersection)
            {
                {
                    if (!intersectingPlanes.IsCreated || intersectingPlanes.Length < mesh0.localPlanes.Length)
                    {
                        if (intersectingPlanes.IsCreated) intersectingPlanes.Dispose();
                        intersectingPlanes = new NativeArray<int>(mesh0.localPlanes.Length, Allocator.Temp);
                    }
                    GetIntersectingPlanes(ref mesh0.localPlanes, ref mesh1.localVertices, mesh1.localBounds, inversedNode0ToNode1, ref intersectingPlanes, out int intersectingPlanesLength);
                    if (intersectingPlanesLength == 0) { builder.Dispose(); return BlobAssetReference<BrushPairIntersection>.Null; }
                    intersectingPlaneIndices0 = builder.Construct(ref root.brush0.localSpacePlaneIndices0, intersectingPlanes, intersectingPlanesLength);
                }

                {
                    if (!intersectingPlanes.IsCreated || intersectingPlanes.Length < mesh1.localPlanes.Length)
                    {
                        if (intersectingPlanes.IsCreated) intersectingPlanes.Dispose();
                        intersectingPlanes = new NativeArray<int>(mesh1.localPlanes.Length, Allocator.Temp);
                    }
                    GetIntersectingPlanes(ref mesh1.localPlanes, ref mesh0.localVertices, mesh0.localBounds, inversedNode1ToNode0, ref intersectingPlanes, out int intersectingPlanesLength);
                    if (intersectingPlanesLength == 0) { builder.Dispose(); return BlobAssetReference<BrushPairIntersection>.Null; }
                    intersectingPlaneIndices1 = builder.Construct(ref root.brush1.localSpacePlaneIndices0, intersectingPlanes, intersectingPlanesLength);
                }
            } else
            //if (type == IntersectionType.AInsideB || type == IntersectionType.BInsideA)
            {
                intersectingPlaneIndices0 = builder.Allocate(ref root.brush0.localSpacePlaneIndices0, mesh0.localPlanes.Length);
                intersectingPlaneIndices1 = builder.Allocate(ref root.brush1.localSpacePlaneIndices0, mesh1.localPlanes.Length);
                for (int i = 0; i < intersectingPlaneIndices0.Length; i++) intersectingPlaneIndices0[i] = i;
                for (int i = 0; i < intersectingPlaneIndices1.Length; i++) intersectingPlaneIndices1[i] = i;
            }



            //var inverseNodeToTreeSpaceMatrix0 = math.transpose(transformations0.treeToNode);
            //var inverseNodeToTreeSpaceMatrix1 = math.transpose(transformations1.treeToNode);


            var surfaceInfos0 = builder.Allocate(ref root.brush0.surfaceInfos, mesh0.localPlanes.Length);
            var surfaceInfos1 = builder.Allocate(ref root.brush1.surfaceInfos, mesh1.localPlanes.Length);
            for (int i = 0; i < surfaceInfos0.Length; i++)
            {
                surfaceInfos0[i] = new SurfaceInfo
                {
                    basePlaneIndex = (ushort)i,
                    interiorCategory = (CategoryGroupIndex)CategoryIndex.Inside,
                    //nodeIndex = brushIndexOrder0.nodeIndex
                };
            }
            for (int i = 0; i < surfaceInfos1.Length; i++)
            {
                surfaceInfos1[i] = new SurfaceInfo
                {
                    basePlaneIndex = (ushort)i,
                    interiorCategory = (CategoryGroupIndex)CategoryIndex.Inside,
                    //nodeIndex = brushIndexOrder1.nodeIndex
                };
            }

            var localSpacePlanes0Length = mesh0.localPlanes.Length;
            if (!localSpacePlanes0.IsCreated || localSpacePlanes0.Length < localSpacePlanes0Length)
            {
                if (localSpacePlanes0.IsCreated) localSpacePlanes0.Dispose();
                localSpacePlanes0 = new NativeArray<float4>(localSpacePlanes0Length, Allocator.Temp);
            }

            var localSpacePlanes1Length = mesh1.localPlanes.Length;
            if (!localSpacePlanes1.IsCreated || localSpacePlanes1.Length < localSpacePlanes1Length)
            {
                if (localSpacePlanes1.IsCreated) localSpacePlanes1.Dispose();
                localSpacePlanes1 = new NativeArray<float4>(localSpacePlanes1Length, Allocator.Temp);
            }


            // TODO: we don't actually use ALL of these planes .. Optimize this
            //var localSpacePlanes0 = stackalloc float4[localSpacePlanes0Length];
            for (int p = 0; p < localSpacePlanes0Length; p++)
                localSpacePlanes0[p] = mesh0.localPlanes[p];

            // TODO: we don't actually use ALL of these planes .. Optimize this
            //var localSpacePlanes1 = stackalloc float4[localSpacePlanes1Length];
            for (int p = 0; p < localSpacePlanes1Length; p++)
            {
                var transformedPlane = math.mul(inversedNode1ToNode0, mesh1.localPlanes[p]);
                localSpacePlanes1[p] = transformedPlane / math.length(transformedPlane.xyz);
            }


            BlobBuilderArray<float3> usedVertices0;
            BlobBuilderArray<float3> usedVertices1;
            if (type != IntersectionType.Intersection)
            {
                builder.Construct(ref root.brush0.localSpacePlanes0, localSpacePlanes0, localSpacePlanes0Length);
                builder.Construct(ref root.brush1.localSpacePlanes0, localSpacePlanes1, localSpacePlanes1Length);

                builder.Allocate(ref root.brush0.usedPlanePairs, 0);
                builder.Allocate(ref root.brush1.usedPlanePairs, 0);

                usedVertices0 = builder.Allocate(ref root.brush0.usedVertices, mesh0.localVertices.Length);
                usedVertices1 = builder.Allocate(ref root.brush1.usedVertices, mesh1.localVertices.Length);
                for (int i = 0; i < usedVertices0.Length; i++) usedVertices0[i] = mesh0.localVertices[i];
                for (int i = 0; i < usedVertices1.Length; i++) usedVertices1[i] = mesh1.localVertices[i];
            } else
            {
                var intersectingPlanes0 = builder.Allocate(ref root.brush0.localSpacePlanes0, intersectingPlaneIndices0.Length);
                var intersectingPlanes1 = builder.Allocate(ref root.brush1.localSpacePlanes0, intersectingPlaneIndices1.Length);
                for (int i = 0; i < intersectingPlaneIndices0.Length; i++)
                    intersectingPlanes0[i] = localSpacePlanes0[intersectingPlaneIndices0[i]];
                for (int i = 0; i < intersectingPlaneIndices1.Length; i++)
                    intersectingPlanes1[i] = localSpacePlanes1[intersectingPlaneIndices1[i]];

                {
                    if (!vertexUsed.IsCreated || vertexUsed.Length < mesh0.localVertices.Length)
                    {
                        if (vertexUsed.IsCreated) vertexUsed.Dispose();
                        vertexUsed = new NativeArray<int>(mesh0.localVertices.Length, Allocator.Temp);
                    } else
                        vertexUsed.ClearValues();
                    if (!usedPlanePairs.IsCreated || usedPlanePairs.Length < mesh0.halfEdges.Length)
                    {
                        if (usedPlanePairs.IsCreated) usedPlanePairs.Dispose();
                        usedPlanePairs = new NativeArray<PlanePair>(mesh0.halfEdges.Length, Allocator.Temp);
                    } else
                        usedPlanePairs.ClearValues();

                    int usedVerticesLength;
                    {
                        FindPlanePairs(ref mesh0, ref intersectingPlaneIndices0, localSpacePlanes0, vertexUsed, float4x4.identity, usedPlanePairs, out int usedPlanePairsLength, out usedVerticesLength);
                        builder.Construct(ref root.brush0.usedPlanePairs, usedPlanePairs, usedPlanePairsLength);
                    }
                    usedVertices0 = builder.Allocate(ref root.brush0.usedVertices, usedVerticesLength);
                    if (usedVerticesLength > 0)
                    {
                        for (int i = 0, n = 0; i < mesh0.localVertices.Length; i++)
                        {
                            if (vertexUsed[i] == 0)
                                continue;
                            var srcIndex = vertexUsed[i] - 1;
                            var srcVertex = mesh0.localVertices[srcIndex];
                            usedVertices0[n] = srcVertex;
                            n++;
                        }
                    }
                }
                {
                    if (!vertexUsed.IsCreated || vertexUsed.Length < mesh1.localVertices.Length)
                    {
                        if (vertexUsed.IsCreated) vertexUsed.Dispose();
                        vertexUsed = new NativeArray<int>(mesh1.localVertices.Length, Allocator.Temp);
                    } else
                        vertexUsed.ClearValues();
                    if (!usedPlanePairs.IsCreated || usedPlanePairs.Length < mesh1.halfEdges.Length)
                    {
                        if (usedPlanePairs.IsCreated) usedPlanePairs.Dispose();
                        usedPlanePairs = new NativeArray<PlanePair>(mesh1.halfEdges.Length, Allocator.Temp);
                    } else
                        usedPlanePairs.ClearValues();

                    int usedVerticesLength;
                    {
                        FindPlanePairs(ref mesh1, ref intersectingPlaneIndices1, localSpacePlanes1, vertexUsed, node1ToNode0, usedPlanePairs, out int usedPlanePairsLength, out usedVerticesLength);
                        builder.Construct(ref root.brush1.usedPlanePairs, usedPlanePairs, usedPlanePairsLength);
                    }
                    usedVertices1 = builder.Allocate(ref root.brush1.usedVertices, usedVerticesLength);
                    if (usedVerticesLength > 0)
                    {
                        for (int i = 0, n = 0; i < mesh1.localVertices.Length; i++)
                        {
                            if (vertexUsed[i] == 0)
                                continue;
                            usedVertices1[n] = mesh1.localVertices[vertexUsed[i] - 1];
                            n++;
                        }
                    }
                }



                // decide which planes of brush1 align with brush2
                // TODO: optimize
                // TODO: should do this as a separate pass

                for (int i1 = 0; i1 < intersectingPlaneIndices0.Length; i1++)
                {
                    var p1 = intersectingPlaneIndices0[i1];
                    var localPlane1 = localSpacePlanes0[p1];
                    for (int i2 = 0; i2 < intersectingPlaneIndices1.Length; i2++)
                    {
                        var p2 = intersectingPlaneIndices1[i2];
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
                var vertexIntersectionPlaneMax = usedVertices0.Length * localSpacePlanes0Length;
                if (!vertexIntersectionPlanes.IsCreated || vertexIntersectionPlanes.Length < vertexIntersectionPlaneMax)
                {
                    if (vertexIntersectionPlanes.IsCreated) vertexIntersectionPlanes.Dispose();
                    vertexIntersectionPlanes = new NativeArray<ushort>(vertexIntersectionPlaneMax, Allocator.Temp);
                }

                var vertexIntersectionPlaneCount = 0;
                var vertexIntersectionSegments = builder.Allocate(ref root.brush0.vertexIntersectionSegments, usedVertices0.Length);
                for (int i = 0; i < usedVertices0.Length; i++)
                {
                    var segment = new int2(vertexIntersectionPlaneCount, 0);
                    for (int j = 0; j < intersectingPlaneIndices0.Length; j++)
                    {
                        var planeIndex = intersectingPlaneIndices0[j];
                        var distance = math.dot(mesh0.localPlanes[planeIndex], new float4(usedVertices0[i], 1));
                        if (distance >= -kPlaneWAlignEpsilon && distance <= kPlaneWAlignEpsilon) // Note: this is false on NaN/Infinity, so don't invert
                        {
                            vertexIntersectionPlanes[vertexIntersectionPlaneCount] = (ushort)planeIndex;
                            vertexIntersectionPlaneCount++;
                        }
                    }
                    segment.y = vertexIntersectionPlaneCount - segment.x;
                    vertexIntersectionSegments[i] = segment;
                }
                if (vertexIntersectionPlaneCount > 0)
                {
                    builder.Construct(ref root.brush0.vertexIntersectionPlanes, vertexIntersectionPlanes, vertexIntersectionPlaneCount);
                } else
                {
                    var vertexIntersectionPlanes = builder.Allocate(ref root.brush0.vertexIntersectionPlanes, 1);
                    vertexIntersectionPlanes[0] = 0;
                }
            }


            {
                var vertexIntersectionPlaneMax = usedVertices1.Length * localSpacePlanes1Length;
                if (!vertexIntersectionPlanes.IsCreated || vertexIntersectionPlanes.Length < vertexIntersectionPlaneMax)
                {
                    if (vertexIntersectionPlanes.IsCreated) vertexIntersectionPlanes.Dispose();
                    vertexIntersectionPlanes = new NativeArray<ushort>(vertexIntersectionPlaneMax, Allocator.Temp);
                }
                
                var vertexIntersectionPlaneCount = 0;
                var vertexIntersectionSegments = builder.Allocate(ref root.brush1.vertexIntersectionSegments, usedVertices1.Length);
                for (int i = 0; i < usedVertices1.Length; i++)
                {
                    var segment = new int2(vertexIntersectionPlaneCount, 0);
                    for (int j = 0; j < intersectingPlaneIndices1.Length; j++)
                    {
                        var planeIndex = intersectingPlaneIndices1[j];
                        var distance = math.dot(mesh1.localPlanes[planeIndex], new float4(usedVertices1[i], 1));
                        if (distance >= -kPlaneWAlignEpsilon && distance <= kPlaneWAlignEpsilon) // Note: this is false on NaN/Infinity, so don't invert
                        {
                            vertexIntersectionPlanes[vertexIntersectionPlaneCount] = (ushort)planeIndex;
                            vertexIntersectionPlaneCount++;
                        }
                    }
                    segment.y = vertexIntersectionPlaneCount - segment.x;
                    vertexIntersectionSegments[i] = segment;
                }
                if (vertexIntersectionPlaneCount > 0)
                {
                    builder.Construct(ref root.brush1.vertexIntersectionPlanes, vertexIntersectionPlanes, vertexIntersectionPlaneCount);
                } else
                {
                    var vertexIntersectionPlanes = builder.Allocate(ref root.brush1.vertexIntersectionPlanes, 1);
                    vertexIntersectionPlanes[0] = 0;
                }
            }


            var result = builder.CreateBlobAssetReference<BrushPairIntersection>(Allocator.TempJob);
            builder.Dispose();
            return result;
        }

        public void Execute(int index)
        {
            var finalBrushPairIntersection = Create(index);
            //if (!finalBrushPairIntersection.IsCreated)
            //    return;

            intersectingBrushesStream.BeginForEachIndex(index);
            intersectingBrushesStream.Write(finalBrushPairIntersection);
            intersectingBrushesStream.EndForEachIndex();
        }
    }
}
