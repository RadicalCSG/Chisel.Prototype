using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    struct PlanePair
    {
        public float4   plane0;
        public float4   plane1;
        public float4   edgeVertex0;
        public float4   edgeVertex1;
        public int      planeIndex0;
        public int      planeIndex1;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareBrushPairIntersectionsJob : IJobParallelForDefer
    {
        const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;
        const float kPlaneWAlignEpsilon         = CSGConstants.kPlaneDAlignEpsilon;
        const float kNormalDotAlignEpsilon      = CSGConstants.kNormalDotAlignEpsilon;

        // Read
        [NoAlias, ReadOnly] public NativeList<BrushPair2>                         uniqueBrushPairs;
        [NoAlias, ReadOnly] public NativeList<NodeTransformations>                transformationCache;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshLookup;

        // Write
        [NoAlias, WriteOnly] public NativeStream.Writer                     intersectingBrushesStream;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<int>          intersectingPlaneIndices0;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>          intersectingPlaneIndices1;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>  surfaceInfos0;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>  surfaceInfos1;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       localSpacePlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       localSpacePlanes1;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       intersectingLocalSpacePlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       intersectingLocalSpacePlanes1;
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>       usedVertices0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>       usedVertices1;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>          vertexUsed;
        [NativeDisableContainerSafetyRestriction] NativeArray<PlanePair>    usedPlanePairs0;
        [NativeDisableContainerSafetyRestriction] NativeArray<PlanePair>    usedPlanePairs1;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>       vertexIntersectionPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>       vertexIntersectionPlanes1;
        [NativeDisableContainerSafetyRestriction] NativeArray<int2>         vertexIntersectionSegments0;
        [NativeDisableContainerSafetyRestriction] NativeArray<int2>         vertexIntersectionSegments1;
        [NativeDisableContainerSafetyRestriction] NativeBitArray            planeAvailable;
        

        // TODO: turn into job
        static void GetIntersectingPlanes(IntersectionType                  type,
                                          [NoAlias] ref BlobArray<float4>   localPlanes,
                                          int                               localPlaneCount,
                                          [NoAlias] ref BlobArray<float3>   vertices,
										  MinMaxAABB                        selfBounds, 
                                          float4x4                          treeToNodeSpaceInverseTransposed, 
                                          [NoAlias] ref NativeArray<int>    intersectingPlaneIndices, 
                                          [NoAlias] out int                 intersectingPlaneLength,
                                          [NoAlias] out int                 intersectingPlanesAndEdgesLength)
        {
            NativeCollectionHelpers.EnsureMinimumSize(ref intersectingPlaneIndices, localPlanes.Length);
            if (type != IntersectionType.Intersection)
            {
                intersectingPlaneLength = localPlaneCount;
                intersectingPlanesAndEdgesLength = localPlanes.Length;
                for (int i = 0; i < intersectingPlaneLength; i++) intersectingPlaneIndices[i] = i;
                return;
            }

            var min = selfBounds.Min;
            var max = selfBounds.Max;

            //Debug.Log($"{localPlanes.Length}");

            intersectingPlaneLength = 0;
            intersectingPlanesAndEdgesLength = 0;
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
                    intersectingPlanesAndEdgesLength = 0;
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

                if (i < localPlaneCount)
                {
                    intersectingPlaneIndices[intersectingPlaneLength] = i;
                    intersectingPlaneLength++;
                    intersectingPlanesAndEdgesLength++;
                } else
                {
                    intersectingPlaneIndices[intersectingPlanesAndEdgesLength] = i;
                    intersectingPlanesAndEdgesLength++;
                }
            }
        }
        
        // TODO: turn into job
        void FindPlanePairs(IntersectionType                        type,
                            [NoAlias] ref BrushMeshBlob             mesh,
                            [NoAlias] NativeArray<int>              intersectingPlanes,
                            int                                     intersectingPlanesLength,
                            [NoAlias] NativeArray<float4>           localSpacePlanesPtr,
                            [NoAlias] ref NativeArray<int>          vertexUsed,
                            float4x4                                vertexTransform,
                            bool                                    needTransform,
                            [NoAlias] ref NativeArray<PlanePair>    usedPlanePairs,
                            [NoAlias] ref NativeArray<float3>       usedVertices,
                            out int     usedPlanePairsLength,
                            out int     usedVerticesLength)
        {
            NativeCollectionHelpers.EnsureMinimumSize(ref usedVertices, mesh.localVertices.Length);
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref usedPlanePairs, mesh.halfEdges.Length);
            
            if (type != IntersectionType.Intersection)
            {
                usedPlanePairsLength = 0;
                usedVerticesLength = mesh.localVertices.Length;
                for (int i = 0; i < mesh.localVertices.Length; i++)
                    usedVertices[i] = mesh.localVertices[i];
                return;
            }
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref vertexUsed, mesh.localVertices.Length);
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref planeAvailable, mesh.localPlanes.Length);

            // TODO: this can be partially stored in brushmesh 
            // TODO: optimize

            ref var halfEdgePolygonIndices  = ref mesh.halfEdgePolygonIndices;
            ref var halfEdges               = ref mesh.halfEdges;


            usedVerticesLength = 0;
            usedPlanePairsLength = 0;
            
            for (int p = 0; p < intersectingPlanesLength; p++)
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

                var vertex0 = new float4(mesh.localVertices[vertexIndex0], 1);
                var vertex1 = new float4(mesh.localVertices[vertexIndex1], 1);

                if (needTransform)
                {
                    vertex0 = math.mul(vertexTransform, vertex0);
                    vertex1 = math.mul(vertexTransform, vertex1);
                }

                if (vertexUsed[vertexIndex0] == 0) { vertexUsed[vertexIndex0] = vertexIndex0 + 1; usedVerticesLength++; }
                if (vertexUsed[vertexIndex1] == 0) { vertexUsed[vertexIndex1] = vertexIndex1 + 1; usedVerticesLength++; }
                usedPlanePairs[usedPlanePairsLength] = new PlanePair
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
            if (usedVerticesLength > 0)
            {
                if (usedVerticesLength < mesh.localVertices.Length)
                {
                    for (int i = 0, n = 0; i < mesh.localVertices.Length; i++)
                        if (vertexUsed[i] != 0) { usedVertices[n] = mesh.localVertices[vertexUsed[i] - 1]; n++; }
                } else
                {
                    for (int i = 0; i < mesh.localVertices.Length; i++)
                        usedVertices[i] = mesh.localVertices[i];
                }
            }
        }


        // work around this, because this can fail hard
        static void FindAlignedPlanes(IntersectionType type,
                                      ref NativeArray<int>          intersectingPlaneIndices0, int intersectingPlanesLength0,
                                      ref NativeArray<float4>       localSpacePlanes0, int localSpacePlanes0Length,
                                      ref NativeArray<SurfaceInfo>  surfaceInfos0,
                                      ref NativeArray<int>          intersectingPlaneIndices1, int intersectingPlanesLength1,
                                      ref NativeArray<float4>       localSpacePlanes1, int localSpacePlanes1Length,
                                      ref NativeArray<SurfaceInfo> surfaceInfos1)
        {
            NativeCollectionHelpers.EnsureMinimumSize(ref surfaceInfos0, localSpacePlanes0Length);
            for (int i = 0; i < localSpacePlanes0Length; i++)
                surfaceInfos0[i] = new SurfaceInfo { basePlaneIndex = (ushort)i, interiorCategory = (byte)CategoryIndex.Inside };

            NativeCollectionHelpers.EnsureMinimumSize(ref surfaceInfos1, localSpacePlanes1Length);
            for (int i = 0; i < localSpacePlanes1Length; i++)
                surfaceInfos1[i] = new SurfaceInfo { basePlaneIndex = (ushort)i, interiorCategory = (byte)CategoryIndex.Inside };

            if (type == IntersectionType.Intersection)
            {
                // decide which planes of brush1 align with brush2
                // TODO: optimize
                // TODO: should do this as a separate pass
                for (int i1 = 0; i1 < intersectingPlanesLength0; i1++)
                {
                    var p1 = intersectingPlaneIndices0[i1];
                    var localPlane1 = localSpacePlanes0[p1];
                    for (int i2 = 0; i2 < intersectingPlanesLength1; i2++)
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
                            surfaceInfo0.interiorCategory = (byte)CategoryIndex.ReverseAligned;
                            surfaceInfos0[p1] = surfaceInfo0;
                            var surfaceInfo1 = surfaceInfos1[p2];
                            surfaceInfo1.interiorCategory = (byte)CategoryIndex.ReverseAligned;
                            surfaceInfos1[p2] = surfaceInfo1;
                        } else
                        {
                            var surfaceInfo0 = surfaceInfos0[p1];
                            surfaceInfo0.interiorCategory = (byte)CategoryIndex.Aligned;
                            surfaceInfos0[p1] = surfaceInfo0;
                            var surfaceInfo1 = surfaceInfos1[p2];
                            surfaceInfo1.interiorCategory = (byte)CategoryIndex.Aligned;
                            surfaceInfos1[p2] = surfaceInfo1;
                        }
                    }
                }
            }
        }

        
        static void GetLocalAndIndirectPlanes(ref BlobArray<float4>     inLocalSpacePlanes0,
                                              ref NativeArray<float4>   outLocalSpacePlanes0, int localSpacePlanes0Length,
                                              ref BlobArray<float4>     inLocalSpacePlanes1, float4x4 inversedNode1ToNode0,
                                              ref NativeArray<float4>   outLocalSpacePlanes1, int localSpacePlanes1Length,
                                              ref NativeArray<int>      intersectingPlaneIndices0, int intersectingPlanesLength0, 
                                              ref NativeArray<float4>   intersectingLocalSpacePlanes0,
                                              ref NativeArray<int>      intersectingPlaneIndices1, int intersectingPlanesLength1,
                                              ref NativeArray<float4>   intersectingLocalSpacePlanes1)
        {

            NativeCollectionHelpers.EnsureMinimumSize(ref outLocalSpacePlanes0, localSpacePlanes0Length);
            for (int p = 0; p < localSpacePlanes0Length; p++)
                outLocalSpacePlanes0[p] = inLocalSpacePlanes0[p];

            NativeCollectionHelpers.EnsureMinimumSize(ref outLocalSpacePlanes1, localSpacePlanes1Length);
            for (int p = 0; p < localSpacePlanes1Length; p++)
            {
                var transformedPlane = math.mul(inversedNode1ToNode0, inLocalSpacePlanes1[p]);
                outLocalSpacePlanes1[p] = transformedPlane / math.length(transformedPlane.xyz);
            }

            NativeCollectionHelpers.EnsureMinimumSize(ref intersectingLocalSpacePlanes0, localSpacePlanes0Length);
            if (intersectingPlanesLength0 == outLocalSpacePlanes0.Length)
            {
                for (int i = 0; i < intersectingPlanesLength0; i++)
                    intersectingLocalSpacePlanes0[i] = outLocalSpacePlanes0[i];
            } else
            {
                for (int i = 0; i < intersectingPlanesLength0; i++)
                    intersectingLocalSpacePlanes0[i] = outLocalSpacePlanes0[intersectingPlaneIndices0[i]];
            }

            NativeCollectionHelpers.EnsureMinimumSize(ref intersectingLocalSpacePlanes1, localSpacePlanes1Length);
            if (intersectingPlanesLength1 == outLocalSpacePlanes1.Length)
            {
                for (int i = 0; i < intersectingPlanesLength1; i++)
                    intersectingLocalSpacePlanes1[i] = outLocalSpacePlanes1[i];
            } else
            {
                for (int i = 0; i < intersectingPlanesLength1; i++)
                    intersectingLocalSpacePlanes1[i] = outLocalSpacePlanes1[intersectingPlaneIndices1[i]];
            }
        }

        static void FindPlanesIntersectingVertices(ref NativeArray<float3> usedVertices0, int usedVerticesLength0,
                                                   ref NativeArray<int> intersectingPlaneIndices0, int intersectingPlanesLength0,
                                                   ref BlobArray<float4> localPlanes,
                                                   ref NativeArray<ushort> vertexIntersectionPlanes0, ref NativeArray<int2> vertexIntersectionSegments0, out int vertexIntersectionPlaneCount0)
        {
            vertexIntersectionPlaneCount0 = 0;
            var vertexIntersectionPlaneMax0 = math.max(1, usedVerticesLength0 * intersectingPlanesLength0);
            NativeCollectionHelpers.EnsureMinimumSize(ref vertexIntersectionPlanes0, vertexIntersectionPlaneMax0);
            NativeCollectionHelpers.EnsureMinimumSize(ref vertexIntersectionSegments0, usedVerticesLength0);
            for (int i = 0; i < usedVerticesLength0; i++)
            {
                var segment = new int2(vertexIntersectionPlaneCount0, 0);
                for (int j = 0; j < intersectingPlanesLength0; j++)
                {
                    var planeIndex = intersectingPlaneIndices0[j];
                    var distance = math.dot(localPlanes[planeIndex], new float4(usedVertices0[i], 1));
                    if (distance >= -kPlaneWAlignEpsilon && distance <= kPlaneWAlignEpsilon) // Note: this is false on NaN/Infinity, so don't invert
                    {
                        vertexIntersectionPlanes0[vertexIntersectionPlaneCount0] = (ushort)planeIndex;
                        vertexIntersectionPlaneCount0++;
                    }
                }
                segment.y = vertexIntersectionPlaneCount0 - segment.x;
                vertexIntersectionSegments0[i] = segment;
            }
            if (vertexIntersectionPlaneCount0 <= 0)
            {
                vertexIntersectionPlaneCount0 = 1;
                vertexIntersectionPlanes0[0] = 0;
            }
        }

        public void Execute(int index)
        {
            if (index >= uniqueBrushPairs.Length)
                goto Fail;

            var brushPair = uniqueBrushPairs[index];

            if (brushPair.type == IntersectionType.InvalidValue)
                goto Fail;

            var brushIndexOrder0 = brushPair.brushIndexOrder0;
            var brushIndexOrder1 = brushPair.brushIndexOrder1;
            int brushNodeOrder0 = brushIndexOrder0.nodeOrder;
            int brushNodeOrder1 = brushIndexOrder1.nodeOrder;

            var blobMesh0 = brushMeshLookup[brushNodeOrder0];
            var blobMesh1 = brushMeshLookup[brushNodeOrder1];


            var type = brushPair.type;
            if (type != IntersectionType.Intersection &&
                type != IntersectionType.AInsideB &&
                type != IntersectionType.BInsideA) 
                goto Fail;


            ref var mesh0 = ref blobMesh0.Value;
            ref var mesh1 = ref blobMesh1.Value;

            var transformations0 = transformationCache[brushNodeOrder0];
            var transformations1 = transformationCache[brushNodeOrder1];


            var node1ToNode0 = math.mul(transformations0.treeToNode, transformations1.nodeToTree);
            var inversedNode0ToNode1 = math.transpose(node1ToNode0);
            GetIntersectingPlanes(type, ref mesh0.localPlanes, mesh0.localPlaneCount, ref mesh1.localVertices, mesh1.localBounds, inversedNode0ToNode1, ref intersectingPlaneIndices0, out int intersectingPlanesLength0, out int intersectingPlanesAndEdgesLength0);
            if (intersectingPlanesLength0 == 0) goto Fail;

            var node0ToNode1 = math.mul(transformations1.treeToNode, transformations0.nodeToTree);
            var inversedNode1ToNode0 = math.transpose(node0ToNode1);
            GetIntersectingPlanes(type, ref mesh1.localPlanes, mesh1.localPlaneCount, ref mesh0.localVertices, mesh0.localBounds, inversedNode1ToNode0, ref intersectingPlaneIndices1, out int intersectingPlanesLength1, out int intersectingPlanesAndEdgesLength1);
            if (intersectingPlanesLength1 == 0) goto Fail;

            // TODO: for each edge of each polygon that is considered an intersecting plane, find the plane that's in between both planes on each side of the edge, and add these planes.
            //       this is to avoid very sharp plane intersections accepting vertices that are obviously outside of the convex polytope

            GetLocalAndIndirectPlanes(ref mesh0.localPlanes,                       ref localSpacePlanes0, mesh0.localPlanes.Length,
                                      ref mesh1.localPlanes, inversedNode1ToNode0, ref localSpacePlanes1, mesh1.localPlanes.Length,
                                      ref intersectingPlaneIndices0, intersectingPlanesAndEdgesLength0, ref intersectingLocalSpacePlanes0,
                                      ref intersectingPlaneIndices1, intersectingPlanesAndEdgesLength1, ref intersectingLocalSpacePlanes1);

            FindPlanePairs(type, ref mesh0, intersectingPlaneIndices0, intersectingPlanesLength0, localSpacePlanes0, ref vertexUsed, float4x4.identity, false, ref usedPlanePairs0, ref usedVertices0, out int usedPlanePairsLength0, out int usedVerticesLength0);
            FindPlanePairs(type, ref mesh1, intersectingPlaneIndices1, intersectingPlanesLength1, localSpacePlanes1, ref vertexUsed, node1ToNode0,      true,  ref usedPlanePairs1, ref usedVertices1, out int usedPlanePairsLength1, out int usedVerticesLength1);
            
            FindAlignedPlanes(type, ref intersectingPlaneIndices0, intersectingPlanesLength0, ref localSpacePlanes0, mesh0.localPlaneCount, ref surfaceInfos0,
                                    ref intersectingPlaneIndices1, intersectingPlanesLength1, ref localSpacePlanes1, mesh1.localPlaneCount, ref surfaceInfos1);

            FindPlanesIntersectingVertices(ref usedVertices0, usedVerticesLength0,
                                           ref intersectingPlaneIndices0, intersectingPlanesLength0,
                                           ref mesh0.localPlanes,
                                           ref vertexIntersectionPlanes0, ref vertexIntersectionSegments0, out int vertexIntersectionPlaneCount0);

            FindPlanesIntersectingVertices(ref usedVertices1, usedVerticesLength1,
                                           ref intersectingPlaneIndices1, intersectingPlanesLength1,
                                           ref mesh1.localPlanes,
                                           ref vertexIntersectionPlanes1, ref vertexIntersectionSegments1, out int vertexIntersectionPlaneCount1);
            
            // TODO: mix writing to stream with filling the lists, this would remove allocation + copy for those lists
            intersectingBrushesStream.BeginForEachIndex(index);
            intersectingBrushesStream.Write(type);
            
            intersectingBrushesStream.Write(brushIndexOrder0);
            intersectingBrushesStream.Write(transformations0.nodeToTree);
            intersectingBrushesStream.Write(node0ToNode1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref usedVertices0, usedVerticesLength0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref usedPlanePairs0, usedPlanePairsLength0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref intersectingLocalSpacePlanes0, intersectingPlanesAndEdgesLength0);
            intersectingBrushesStream.Write(intersectingPlanesLength0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref intersectingPlaneIndices0, intersectingPlanesLength0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref vertexIntersectionPlanes0, vertexIntersectionPlaneCount0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref vertexIntersectionSegments0, usedVerticesLength0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref surfaceInfos0, mesh0.localPlanes.Length);

            intersectingBrushesStream.Write(brushIndexOrder1);
            intersectingBrushesStream.Write(transformations1.nodeToTree);
            intersectingBrushesStream.Write(node1ToNode0);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref usedVertices1, usedVerticesLength1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref usedPlanePairs1, usedPlanePairsLength1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref intersectingLocalSpacePlanes1, intersectingPlanesAndEdgesLength1);
            intersectingBrushesStream.Write(intersectingPlanesLength1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref intersectingPlaneIndices1, intersectingPlanesLength1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref vertexIntersectionPlanes1, vertexIntersectionPlaneCount1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref vertexIntersectionSegments1, usedVerticesLength1);
            NativeStreamExtensions.WriteArray(ref intersectingBrushesStream, ref surfaceInfos1, mesh1.localPlanes.Length);

            intersectingBrushesStream.EndForEachIndex();
            return;
Fail:
            intersectingBrushesStream.BeginForEachIndex(index);
            intersectingBrushesStream.Write(IntersectionType.InvalidValue);
            intersectingBrushesStream.EndForEachIndex();
            return;
        }
    }
}
