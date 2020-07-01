using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions.Must;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateIntersectionLoopsJob : IJobParallelFor
    {
        const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;

        // Read
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>                    brushTreeSpacePlanes;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>              treeSpaceVerticesArray;
        
        // Read Write
        [NoAlias] public NativeArray<BlobAssetReference<BrushPairIntersection>>                             intersectingBrushes;

        // Write
        [NoAlias, WriteOnly] public NativeMultiHashMap<int, BlobAssetReference<BrushIntersectionLoop>>.ParallelWriter   outputSurfaces;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>                   localVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>                   usedVertexIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<PlaneIndexOffsetLength>   planeIndexOffsets;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>                   uniqueIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int2>                     sortedStack;        
        [NativeDisableContainerSafetyRestriction] NativeArray<PlaneVertexIndexPair>     foundIndices0;
        [NativeDisableContainerSafetyRestriction] NativeArray<PlaneVertexIndexPair>     foundIndices1;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>                   foundVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<IntersectionEdge>         foundEdges;
        [NativeDisableContainerSafetyRestriction] NativeArray<IntersectionPlanes>       foundIntersections;
        [NativeDisableContainerSafetyRestriction] HashedVertices                        hashedVertices;
        [NativeDisableContainerSafetyRestriction] HashedVertices                        snapHashedVertices;
                
        struct PlaneVertexIndexPair
        {
            public ushort planeIndex;
            public ushort vertexIndex;
        }

        struct PlaneIndexOffsetLength
        {
            public ushort length;
            public ushort offset;
            public ushort planeIndex;
        }
        
        bool IsOutsidePlanes(ref BlobArray<float4> planes, float4 localVertex)
        {
            int n = 0;
            for (; n + 4 < planes.Length; n+=4)
            {
                var distance = new float4(math.dot(planes[n+0], localVertex),
                                          math.dot(planes[n+1], localVertex),
                                          math.dot(planes[n+2], localVertex),
                                          math.dot(planes[n+3], localVertex));

                // will be 'false' when distance is NaN or Infinity
                if (!math.all(distance <= kFatPlaneWidthEpsilon))
                    return true;
            }
            for (; n < planes.Length; n ++)
            {
                var distance = math.dot(planes[n], localVertex);

                // will be 'false' when distance is NaN or Infinity
                if (!(distance <= kFatPlaneWidthEpsilon))
                    return true;
            }
            return false;
        }


        #region Sort
        static float3 FindPolygonCentroid(HashedVertices vertices, NativeArray<ushort> indices, int offset, int indicesCount)
        {
            var centroid = float3.zero;
            for (int i = 0; i < indicesCount; i++, offset++)
                centroid += vertices[indices[offset]];
            return centroid / indicesCount;
        }

        // TODO: sort by using plane information instead of unreliable floating point math ..
        static void SortIndices(HashedVertices vertices, NativeArray<int2> sortedStack, NativeArray<ushort> indices, int offset, int indicesCount, float3 normal)
        {
            // There's no point in trying to sort a point or a line 
            if (indicesCount < 3)
                return;

            float3 tangentX, tangentY;
            if (normal.x > normal.y)
            {
                if (normal.x > normal.z)
                {
                    tangentX = math.cross(normal, new float3(0, 1, 0));
                    tangentY = math.cross(normal, tangentX);
                } else
                {
                    tangentX = math.cross(normal, new float3(0, 0, 1));
                    tangentY = math.cross(normal, tangentX);
                }
            } else
            {
                if (normal.y > normal.z)
                {
                    tangentX = math.cross(normal, new float3(1, 0, 0));
                    tangentY = math.cross(normal, tangentX);
                } else
                {
                    tangentX = math.cross(normal, new float3(0, 1, 0));
                    tangentY = math.cross(normal, tangentX);
                }
            }

            var centroid = FindPolygonCentroid(vertices, indices, offset, indicesCount);
            var center = new float2(math.dot(tangentX, centroid), // distance in direction of tangentX
                                    math.dot(tangentY, centroid)); // distance in direction of tangentY


            var sortedStackLength = 1;
            sortedStack[0] = new int2(0, indicesCount - 1);
            while (sortedStackLength > 0)
            {
                var top = sortedStack[sortedStackLength - 1];
                sortedStackLength--;
                var l = top.x;
                var r = top.y;
                var left = l;
                var right = r;
                var va = vertices[indices[offset + (left + right) / 2]];
                while (true)
                {
                    var a_angle = math.atan2(math.dot(tangentX, va) - center.x, math.dot(tangentY, va) - center.y);

                    {
                        var vb = vertices[indices[offset + left]];
                        var b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        while (b_angle > a_angle)
                        {
                            left++;
                            vb = vertices[indices[offset + left]];
                            b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        }
                    }

                    {
                        var vb = vertices[indices[offset + right]];
                        var b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        while (a_angle > b_angle)
                        {
                            right--;
                            vb = vertices[indices[offset + right]];
                            b_angle = math.atan2(math.dot(tangentX, vb) - center.x, math.dot(tangentY, vb) - center.y);
                        }
                    }

                    if (left <= right)
                    {
                        if (left != right)
                        {
                            var t = indices[offset + left];
                            indices[offset + left] = indices[offset + right];
                            indices[offset + right] = t;
                        }

                        left++;
                        right--;
                    }
                    if (left > right)
                        break;
                }
                if (l < right)
                {
                    sortedStack[sortedStackLength] = new int2(l, right);
                    sortedStackLength++;
                }
                if (left < r)
                {
                    sortedStack[sortedStackLength] = new int2(left, r);
                    sortedStackLength++;
                }
            }
        }
        #endregion
        
        //[MethodImpl(MethodImplOptions.NoInlining)]
        void FindInsideVertices(ref BlobArray<float3>               usedVertices0,
                                ref BlobArray<ushort>               vertexIntersectionPlanes,
                                ref BlobArray<int2>                 vertexIntersectionSegments,
                                ref BlobArray<float4>               intersectingPlanes1,
                                float4x4                            nodeToTreeSpaceMatrix1,
                                float4x4                            vertexToLocal0,
                                ref HashedVertices                  hashedVertices,
                                ref HashedVertices                  snapHashedVertices,
                                NativeArray<PlaneVertexIndexPair>   foundIndices0,
                                ref int                             foundIndices0Length)
        {
            if (!localVertices.IsCreated || localVertices.Length < usedVertices0.Length)
            {
                if (localVertices.IsCreated) localVertices.Dispose();
                localVertices = new NativeArray<float4>(usedVertices0.Length, Allocator.Temp);
            }

            if (!usedVertexIndices.IsCreated || usedVertexIndices.Length < usedVertices0.Length)
            {
                if (usedVertexIndices.IsCreated) usedVertexIndices.Dispose();
                usedVertexIndices = new NativeArray<ushort>(usedVertices0.Length, Allocator.Temp);
            }

            //var localVertices       = stackalloc float4[usedVertices0.Length];
            //var usedVertexIndices   = stackalloc ushort[usedVertices0.Length];
            var foundVertexCount = 0;

            for (int j = 0; j < usedVertices0.Length; j++)
            {
                var brushVertex1 = new float4(usedVertices0[j], 1);
                localVertices[j] = math.mul(vertexToLocal0, brushVertex1);
                usedVertexIndices[j] = (ushort)j;
            }

            foundVertexCount = usedVertices0.Length;
            for (int j = foundVertexCount - 1; j >= 0; j--)
            {
                if (IsOutsidePlanes(ref intersectingPlanes1, localVertices[j]))
                {
                    if (j < foundVertexCount - 1)
                    {
                        localVertices[j] = localVertices[foundVertexCount - 1];
                        usedVertexIndices[j] = usedVertexIndices[foundVertexCount - 1];
                    }
                    foundVertexCount--;
                }
            }

            for (int j = 0; j < foundVertexCount; j++)
            {
                var usedVertexIndex     = usedVertexIndices[j];
                var segment             = vertexIntersectionSegments[usedVertexIndex];
                if (segment.y == 0)
                    continue;

                var treeSpaceVertex         = math.mul(nodeToTreeSpaceMatrix1, localVertices[j]).xyz;
                treeSpaceVertex             = snapHashedVertices[snapHashedVertices.AddNoResize(treeSpaceVertex)];
                var treeSpaceVertexIndex    = hashedVertices.AddNoResize(treeSpaceVertex);
                for (int i = segment.x; i < segment.x + segment.y; i++)
                {
                    var planeIndex = vertexIntersectionPlanes[i];
                    foundIndices0[foundIndices0Length] = new PlaneVertexIndexPair { planeIndex = (ushort)planeIndex, vertexIndex = (ushort)treeSpaceVertexIndex };
                    foundIndices0Length++;
                }
            }
        }
        
        struct IntersectionPlanes
        {
            public float4       plane0;
            public float4       plane1;
            public float4       plane2;
            public int          planeIndex0;
            public int          planeIndex1;
            public int          planeIndex2;
        }

        struct IntersectionEdge
        {
            public float4       edgeVertex0;
            public float4       edgeVertex1;
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        void FindIntersectionVertices(ref BlobArray<float4>             intersectingPlanes0,
                                      ref BlobArray<float4>             intersectingPlanes1,
                                      ref BlobArray<PlanePair>          usedPlanePairs1,
                                      ref BlobArray<int>                intersectingPlaneIndices0,
                                      float4x4                          nodeToTreeSpaceMatrix0,
                                      ref HashedVertices                hashedVertices,
                                      ref HashedVertices                snapHashedVertices,
                                      NativeArray<PlaneVertexIndexPair> foundIndices0,
                                      ref int                           foundIndices0Length,
                                      NativeArray<PlaneVertexIndexPair> foundIndices1,
                                      ref int                           foundIndices1Length)
        {
            int foundVerticesCount = usedPlanePairs1.Length * intersectingPlanes0.Length;
            if (!foundVertices.IsCreated || foundVertices.Length < foundVerticesCount)
            {
                if (foundVertices.IsCreated) foundVertices.Dispose();
                foundVertices = new NativeArray<float4>(foundVerticesCount, Allocator.Temp);
            }
            if (!foundEdges.IsCreated || foundEdges.Length < foundVerticesCount)
            {
                if (foundEdges.IsCreated) foundEdges.Dispose();
                foundEdges = new NativeArray<IntersectionEdge>(foundVerticesCount, Allocator.Temp);
            }
            if (!foundIntersections.IsCreated || foundIntersections.Length < foundVerticesCount)
            {
                if (foundIntersections.IsCreated) foundIntersections.Dispose();
                foundIntersections = new NativeArray<IntersectionPlanes>(foundVerticesCount, Allocator.Temp);
            }

            var n = 0;
            for (int i = 0; i < usedPlanePairs1.Length; i++)
            {
                for (int j = 0; j < intersectingPlanes0.Length; j++)
                { 
                    foundIntersections[n] = new IntersectionPlanes
                    { 
                        plane0      = usedPlanePairs1[i].plane0,
                        plane1      = usedPlanePairs1[i].plane1,
                        plane2      = intersectingPlanes0[j],
                        planeIndex0 = usedPlanePairs1[i].planeIndex0,
                        planeIndex1 = usedPlanePairs1[i].planeIndex1,
                        planeIndex2 = intersectingPlaneIndices0[j]
                    };

                    foundEdges[n] = new IntersectionEdge
                    {
                        edgeVertex0 = usedPlanePairs1[i].edgeVertex0,
                        edgeVertex1 = usedPlanePairs1[i].edgeVertex1
                    };
                    
                    var plane0      = usedPlanePairs1[i].plane0;
                    var plane1      = usedPlanePairs1[i].plane1;
                    var plane2      = intersectingPlanes0[j];

                    foundVertices[n] = new float4((float3)PlaneExtensions.Intersection(plane2, plane0, plane1), 1);
                    n++;
                }
            }

            for (int k = n - 1; k >= 0; k--)
            {
                var edgeVertex0 = foundEdges[k].edgeVertex0;
                var edgeVertex1 = foundEdges[k].edgeVertex1;
                var plane2      = foundIntersections[k].plane2;

                if (math.abs(math.dot(plane2, edgeVertex0)) <= kFatPlaneWidthEpsilon &&
                    math.abs(math.dot(plane2, edgeVertex1)) <= kFatPlaneWidthEpsilon)
                {
                    if (k < n - 1)
                    {
                        foundIntersections[k] = foundIntersections[n - 1];
                        foundVertices[k] = foundVertices[n - 1];
                    }
                    n--;
                }
            }

            // TODO: since we're using a pair in the outer loop, we could also determine which 
            //       2 planes it intersects at both ends and just check those two planes ..

            // NOTE: for brush2, the intersection will always be only on two planes
            //       UNLESS it's a corner vertex along that edge (we can compare to the two vertices)
            //       in which case we could use a pre-calculated list of planes ..
            //       OR when the intersection is outside of the edge ..

            for (int k = n - 1; k >= 0; k--)
            {
                if (IsOutsidePlanes(ref intersectingPlanes0, foundVertices[k]) ||
                    IsOutsidePlanes(ref intersectingPlanes1, foundVertices[k]))
                {
                    if (k < n - 1)
                    {
                        foundIntersections[k] = foundIntersections[n - 1];
                        foundVertices[k] = foundVertices[n - 1];
                    }
                    n--;
                }
            }

            for (int k = 0; k < n; k++)
            {
                var planeIndex0 = (ushort)foundIntersections[k].planeIndex0;
                var planeIndex1 = (ushort)foundIntersections[k].planeIndex1;
                var planeIndex2 = (ushort)foundIntersections[k].planeIndex2;

                var localVertex = foundVertices[k];

                // TODO: should be having a Loop for each plane that intersects this vertex, and add that vertex 
                //       to ensure they are identical
                var treeSpaceVertex = math.mul(nodeToTreeSpaceMatrix0, localVertex).xyz;
                treeSpaceVertex     = snapHashedVertices[snapHashedVertices.AddNoResize(treeSpaceVertex)];
                var vertexIndex     = hashedVertices.AddNoResize(treeSpaceVertex);

                foundIndices0[foundIndices0Length] = new PlaneVertexIndexPair { planeIndex = planeIndex2, vertexIndex = vertexIndex };
                foundIndices0Length++;

                foundIndices1[foundIndices1Length] = new PlaneVertexIndexPair { planeIndex = planeIndex0, vertexIndex = vertexIndex };
                foundIndices1Length++;

                foundIndices1[foundIndices1Length] = new PlaneVertexIndexPair { planeIndex = planeIndex1, vertexIndex = vertexIndex };
                foundIndices1Length++;
            }
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        void GenerateLoop(IndexOrder                        brushIndexOrder0,
                          IndexOrder                        brushIndexOrder1,
                          ref BlobArray<SurfaceInfo>        surfaceInfos,
                          ref BrushTreeSpacePlanes          brushTreeSpacePlanes,
                          NativeArray<PlaneVertexIndexPair> foundIndices0,
                          ref int                           foundIndices0Length,
                          ref HashedVertices                hashedVertices,
                          NativeMultiHashMap<int, BlobAssetReference<BrushIntersectionLoop>>.ParallelWriter outputSurfaces)
        {
            // Why is the unity NativeSort slower than bubble sort?
            for (int i = 0; i < foundIndices0Length - 1; i++)
            {
                for (int j = i + 1; j < foundIndices0Length; j++)
                {
                    var x = foundIndices0[i];
                    var y = foundIndices0[j];
                    if (x.planeIndex > y.planeIndex)
                        continue;
                    if (x.planeIndex == y.planeIndex)
                    {
                        if (x.vertexIndex <= y.vertexIndex)
                            continue;
                    }

                    var t = x;
                    foundIndices0[i] = foundIndices0[j];
                    foundIndices0[j] = t;
                }
            }


            if (!planeIndexOffsets.IsCreated || planeIndexOffsets.Length < foundIndices0Length)
            {
                if (planeIndexOffsets.IsCreated) planeIndexOffsets.Dispose();
                planeIndexOffsets = new NativeArray<PlaneIndexOffsetLength>(foundIndices0Length, Allocator.Temp);
            }
            if (!uniqueIndices.IsCreated || uniqueIndices.Length < foundIndices0Length)
            {
                if (uniqueIndices.IsCreated) uniqueIndices.Dispose();
                uniqueIndices = new NativeArray<ushort>(foundIndices0Length, Allocator.Temp);
            }

            var planeIndexOffsetsLength = 0;
            //var planeIndexOffsets       = stackalloc PlaneIndexOffsetLength[foundIndices0Length];
            var uniqueIndicesLength     = 0;
            //var uniqueIndices           = stackalloc ushort[foundIndices0Length];

            // Now that our indices are sorted by planeIndex, we can segment them by start/end offset
            var previousPlaneIndex  = foundIndices0[0].planeIndex;
            var previousVertexIndex = foundIndices0[0].vertexIndex;
            uniqueIndices[uniqueIndicesLength] = previousVertexIndex;
            uniqueIndicesLength++;
            var loopStart = 0;
            for (int i = 1; i < foundIndices0Length; i++)
            {
                var indices     = foundIndices0[i];

                var planeIndex  = indices.planeIndex;
                var vertexIndex = indices.vertexIndex;

                // TODO: why do we have soooo many duplicates sometimes?
                if (planeIndex  == previousPlaneIndex &&
                    vertexIndex == previousVertexIndex)
                    continue;

                if (planeIndex != previousPlaneIndex)
                {
                    var currLength = (uniqueIndicesLength - loopStart);
                    if (currLength > 2)
                    {
                        planeIndexOffsets[planeIndexOffsetsLength] = new PlaneIndexOffsetLength
                        {
                            length = (ushort)currLength,
                            offset = (ushort)loopStart,
                            planeIndex = previousPlaneIndex
                        };
                        planeIndexOffsetsLength++;
                    }
                    loopStart = uniqueIndicesLength;
                }

                uniqueIndices[uniqueIndicesLength] = vertexIndex;
                uniqueIndicesLength++;
                previousVertexIndex = vertexIndex;
                previousPlaneIndex = planeIndex;
            }
            {
                var currLength = (uniqueIndicesLength - loopStart);
                if (currLength > 2)
                {
                    planeIndexOffsets[planeIndexOffsetsLength] = new PlaneIndexOffsetLength
                    {
                        length = (ushort)currLength,
                        offset = (ushort)loopStart,
                        planeIndex = previousPlaneIndex
                    };
                    planeIndexOffsetsLength++;
                }
            }

            var maxLength = 0;
            for (int i = 0; i < planeIndexOffsetsLength; i++)
                maxLength = math.max(maxLength, planeIndexOffsets[i].length);

            if (!sortedStack.IsCreated || sortedStack.Length < maxLength * 2)
            {
                if (sortedStack.IsCreated) sortedStack.Dispose();
                sortedStack = new NativeArray<int2>(maxLength * 2, Allocator.Temp);
            }

            // For each segment, we now sort our vertices within each segment, 
            // making the assumption that they are convex
            //var sortedStack = stackalloc int2[maxLength * 2];
            var vertices = hashedVertices;//.GetUnsafeReadOnlyPtr();
            for (int n = planeIndexOffsetsLength - 1; n >= 0; n--)
            {
                var planeIndexOffset    = planeIndexOffsets[n];
                var length              = planeIndexOffset.length;
                var offset              = planeIndexOffset.offset;
                var planeIndex          = planeIndexOffset.planeIndex;
                    
                // TODO: use plane information instead
                SortIndices(vertices, sortedStack, uniqueIndices, offset, length, brushTreeSpacePlanes.treeSpacePlanes[planeIndex].xyz);
            }

            
            var totalLoopsSize          = 16 + (planeIndexOffsetsLength * UnsafeUtility.SizeOf<BrushIntersectionLoop>());
            var totalSize               = totalLoopsSize;
            for (int j = 0; j < planeIndexOffsetsLength; j++)
            {
                var planeIndexLength = planeIndexOffsets[j];
                var loopLength = planeIndexLength.length;
                totalSize += (loopLength * UnsafeUtility.SizeOf<float3>()); 
            }

            var srcVertices = hashedVertices;//.GetUnsafeReadOnlyPtr();
            for (int j = 0; j < planeIndexOffsetsLength; j++)
            { 
                var planeIndexLength    = planeIndexOffsets[j];
                var offset              = planeIndexLength.offset;
                var loopLength          = planeIndexLength.length;
                var basePlaneIndex      = planeIndexLength.planeIndex;
                var surfaceInfo         = surfaceInfos[basePlaneIndex];

                var builder = new BlobBuilder(Allocator.Temp, totalSize);
                ref var root = ref builder.ConstructRoot<BrushIntersectionLoop>();

                root.indexOrder = brushIndexOrder1;
                root.surfaceInfo = surfaceInfo;
                var dstVertices = builder.Allocate(ref root.loopVertices, loopLength);
                for (int d = 0; d < loopLength; d++)
                    dstVertices[d] = srcVertices[uniqueIndices[offset + d]];

                outputSurfaces.Add(brushIndexOrder0.nodeOrder, 
                                   builder.CreateBlobAssetReference<BrushIntersectionLoop>(Allocator.TempJob));
            }

            //builder.Dispose(); // Allocated with Temp, so don't need dispose
        }

        public void Execute(int index)
        {
            if (index >= intersectingBrushes.Length)
                return;

            // Note: although is a BlobAssetReference, it only exists during a single frame and 
            //       its IndexOrder's are always correct
            var intersectionAsset               = intersectingBrushes[index];
            intersectingBrushes[index] = default;
            ref var intersection                = ref intersectionAsset.Value;
            ref var brushPairIntersection0      = ref intersection.brushes[0];
            ref var brushPairIntersection1      = ref intersection.brushes[1];
            var brushIndexOrder0                = brushPairIntersection0.brushIndexOrder;
            var brushIndexOrder1                = brushPairIntersection1.brushIndexOrder;
            
            int insideVerticesStream0Capacity   = math.max(1, brushPairIntersection0.usedVertices.Length);
            int insideVerticesStream1Capacity   = math.max(1, brushPairIntersection1.usedVertices.Length);
            int intersectionStream0Capacity     = math.max(1, brushPairIntersection1.usedPlanePairs.Length) * brushPairIntersection0.localSpacePlanes0.Length;
            int intersectionStream1Capacity     = math.max(1, brushPairIntersection0.usedPlanePairs.Length) * brushPairIntersection1.localSpacePlanes0.Length;
            int foundIndices0Capacity           = intersectionStream0Capacity + (2 * intersectionStream1Capacity) + (brushPairIntersection0.localSpacePlanes0.Length * insideVerticesStream0Capacity);
            int foundIndices1Capacity           = intersectionStream1Capacity + (2 * intersectionStream0Capacity) + (brushPairIntersection1.localSpacePlanes0.Length * insideVerticesStream1Capacity);

            if (!foundIndices0.IsCreated || foundIndices0.Length < foundIndices0Capacity)
            {
                if (foundIndices0.IsCreated) foundIndices0.Dispose();
                foundIndices0 = new NativeArray<PlaneVertexIndexPair>(foundIndices0Capacity, Allocator.Temp);
            }
            if (!foundIndices1.IsCreated || foundIndices1.Length < foundIndices1Capacity)
            {
                if (foundIndices1.IsCreated) foundIndices1.Dispose();
                foundIndices1 = new NativeArray<PlaneVertexIndexPair>(foundIndices1Capacity, Allocator.Temp);
            }

            var foundIndices0Length     = 0;
            var foundIndices1Length     = 0;

            var desiredVertexCapacity = math.max(foundIndices0Capacity, foundIndices1Capacity);
            if (!hashedVertices.IsCreated)
            {
                hashedVertices = new HashedVertices(desiredVertexCapacity, Allocator.Temp);
                snapHashedVertices = new HashedVertices(desiredVertexCapacity, Allocator.Temp);
            } else
            {
                if (hashedVertices.Capacity < desiredVertexCapacity)
                {
                    hashedVertices.Dispose();
                    hashedVertices = new HashedVertices(desiredVertexCapacity, Allocator.Temp);

                    snapHashedVertices.Dispose();
                    snapHashedVertices = new HashedVertices(desiredVertexCapacity, Allocator.Temp);
                } else
                {
                    hashedVertices.Clear();
                    snapHashedVertices.Clear();
                }
            }

            // TODO: fill them with original brush vertices so that they're always snapped to these
            
            if (brushIndexOrder0.nodeOrder < brushIndexOrder1.nodeOrder)
            {
                snapHashedVertices.AddUniqueVertices(ref treeSpaceVerticesArray[brushIndexOrder0.nodeOrder].Value.treeSpaceVertices);
                snapHashedVertices.ReplaceIfExists(ref treeSpaceVerticesArray[brushIndexOrder1.nodeOrder].Value.treeSpaceVertices);
            } else
            {
                snapHashedVertices.AddUniqueVertices(ref treeSpaceVerticesArray[brushIndexOrder1.nodeOrder].Value.treeSpaceVertices);
                snapHashedVertices.ReplaceIfExists(ref treeSpaceVerticesArray[brushIndexOrder0.nodeOrder].Value.treeSpaceVertices);
            }



            // First find vertices from other brush that are inside the other brush, so that any vertex we 
            // find during the intersection part will be snapped to those vertices and not the other way around

            // TODO: when all vertices of a polygon are inside the other brush, don't bother intersecting it.
            //       same when two planes overlap each other ...

            // Now find all the intersection vertices
            if (intersection.type == IntersectionType.Intersection)
            { 
                if (brushPairIntersection1.usedPlanePairs.Length > 0)
                {
                    FindIntersectionVertices(ref intersection.brushes[0].localSpacePlanes0,
                                             ref intersection.brushes[1].localSpacePlanes0,
                                             ref intersection.brushes[1].usedPlanePairs,
                                             ref intersection.brushes[0].localSpacePlaneIndices0,
                                             intersection.brushes[0].nodeToTreeSpace,
                                             ref hashedVertices,
                                             ref snapHashedVertices,
                                             foundIndices0, ref foundIndices0Length,
                                             foundIndices1, ref foundIndices1Length);
                }

                if (brushPairIntersection0.usedPlanePairs.Length > 0)
                {
                    FindIntersectionVertices(ref intersection.brushes[1].localSpacePlanes0,
                                             ref intersection.brushes[0].localSpacePlanes0,
                                             ref intersection.brushes[0].usedPlanePairs,
                                             ref intersection.brushes[1].localSpacePlaneIndices0,
                                             intersection.brushes[0].nodeToTreeSpace,
                                             ref hashedVertices,
                                             ref snapHashedVertices,
                                             foundIndices1, ref foundIndices1Length,
                                             foundIndices0, ref foundIndices0Length);
                }
            }

            // Find all vertices of brush0 that are inside brush1, and put their intersections into the appropriate loops
            if (foundIndices0Length > 0 &&
                brushPairIntersection0.usedVertices.Length > 0)
            {
                FindInsideVertices(ref intersection.brushes[0].usedVertices,
                                   ref intersection.brushes[0].vertexIntersectionPlanes,
                                   ref intersection.brushes[0].vertexIntersectionSegments,
                                   ref intersection.brushes[1].localSpacePlanes0,
                                   intersection.brushes[0].nodeToTreeSpace,
                                   float4x4.identity,
                                   ref hashedVertices,
                                   ref snapHashedVertices,
                                   foundIndices0, ref foundIndices0Length);
            }

            // Find all vertices of brush1 that are inside brush0, and put their intersections into the appropriate loops
            if (foundIndices1Length > 0 && 
                brushPairIntersection1.usedVertices.Length > 0)
            {
                FindInsideVertices(ref intersection.brushes[1].usedVertices,
                                   ref intersection.brushes[1].vertexIntersectionPlanes,
                                   ref intersection.brushes[1].vertexIntersectionSegments,
                                   ref intersection.brushes[0].localSpacePlanes0,
                                   intersection.brushes[0].nodeToTreeSpace,
                                   intersection.brushes[1].toOtherBrushSpace,
                                   ref hashedVertices,
                                   ref snapHashedVertices,
                                   foundIndices1, ref foundIndices1Length);
            }


            if (foundIndices0Length >= 3)
            {
                ref var brushTreeSpacePlanes0 = ref brushTreeSpacePlanes[brushIndexOrder0.nodeOrder].Value;
                GenerateLoop(brushIndexOrder0,
                             brushIndexOrder1,
                             ref intersection.brushes[0].surfaceInfos,
                             ref brushTreeSpacePlanes0,
                             foundIndices0, ref foundIndices0Length,
                             ref hashedVertices,
                             outputSurfaces);
            }

            if (foundIndices1Length >= 3)
            {
                ref var brushTreeSpacePlanes1 = ref brushTreeSpacePlanes[brushIndexOrder1.nodeOrder].Value;
                GenerateLoop(brushIndexOrder1,
                             brushIndexOrder0,
                             ref intersection.brushes[1].surfaceInfos,
                             ref brushTreeSpacePlanes1,
                             foundIndices1, 
                             ref foundIndices1Length,
                             ref hashedVertices,
                             outputSurfaces);
            }

            //foundIndices0.Dispose();
            //foundIndices1.Dispose();

            //hashedVertices.Dispose(); // Allocated with Temp, so do not need to dispose

            intersectionAsset.Dispose();
        }
    }
}
