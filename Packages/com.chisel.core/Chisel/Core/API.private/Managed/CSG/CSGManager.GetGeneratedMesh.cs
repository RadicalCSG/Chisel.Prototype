using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public struct ChiselSurfaceRenderBuffer
    {
        public BlobArray<Int32>		indices;
        public BlobArray<float3>	vertices;
        public BlobArray<float3>	normals;
        public BlobArray<float2>    uv0;

        public uint             geometryHash;
        public uint             surfaceHash;

        public SurfaceLayers    surfaceLayers;
        public Int32		    surfaceIndex;
    };

    public struct ChiselBrushRenderBuffer
    {
        public BlobArray<ChiselSurfaceRenderBuffer> surfaces;
    };

    internal sealed class Outline
    {
        public Int32[] visibleOuterLines;
        public Int32[] visibleInnerLines;
        public Int32[] visibleTriangles;
        public Int32[] invisibleOuterLines;
        public Int32[] invisibleInnerLines;
        public Int32[] invalidLines;

        public void Reset()
        {
            visibleOuterLines   = new Int32[0];
            visibleInnerLines   = new Int32[0];
            visibleTriangles    = new Int32[0];
            invisibleOuterLines = new Int32[0];
            invisibleInnerLines = new Int32[0];
            invalidLines        = new Int32[0];
        }
    };

    internal sealed class BrushOutline
    {
        public Outline      brushOutline   = new Outline();
        public Outline[]    surfaceOutlines;
        public float3[]     vertices;

        public void Reset()
        {
            brushOutline.Reset();
            surfaceOutlines = new Outline[0];
            vertices = new float3[0];
        }
    };

    static partial class CSGManager
    {
        const int kMaxPhysicsIndexCount = 32000;


        internal struct SubMeshCounts
        {
            public MeshQuery meshQuery;
            public int		surfaceIdentifier;

            public int		meshIndex;
            public int		subMeshIndex;
            
            public uint	    surfaceHash;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
            public uint	    geometryHash;  // used to detect changes in vertex positions / indices

            public int		indexCount;
            public int		vertexCount;
            
            public int      surfacesOffset;
            public int      surfacesCount;
        };

        internal sealed class BrushInfo
        {
            public int					    brushMeshInstanceID;
            public UInt64                   brushOutlineGeneration;
            public bool                     brushOutlineDirty = true;

            public BrushOutline             brushOutline        = new BrushOutline();


            public void Reset() 
            {
                brushOutlineDirty = true;
                brushOutlineGeneration  = 0;
                brushOutline.Reset();
            }
        }

        static void Realloc<T>(ref NativeArray<T> oldArray, int newSize, Allocator allocator, bool arrayIsUsed = false) where T:struct
        {
            if (oldArray.IsCreated && !arrayIsUsed)
            {
                oldArray.Dispose();
                oldArray = default;
                return;
            }

            if (oldArray.IsCreated && oldArray.Length == newSize)
            {
                oldArray.ClearStruct();
                return;
            }
            
            if (oldArray.IsCreated)
                oldArray.Dispose();
            oldArray = new NativeArray<T>(newSize, allocator);
        }


        public static bool GetGeneratedMesh(int treeNodeID, ref GeneratedMeshDescription meshDescription, ref GeneratedMeshContents generatedMeshContents)
        {
            if (!GetGeneratedMesh(treeNodeID, ref meshDescription, ref generatedMeshContents, Allocator.Persistent, out JobHandle jobHandle))
                return false;
            jobHandle.Complete();
            return true;
        }

        public static bool GetGeneratedMesh(int treeNodeID, ref GeneratedMeshDescription meshDescription, ref GeneratedMeshContents generatedMeshContents, Allocator allocator, out JobHandle jobHandle, JobHandle dependencies = default)
        {
            jobHandle = default;
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return false;

            if (nodeHierarchies[treeNodeID - 1].treeInfo == null) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            TreeInfo treeInfo = nodeHierarchies[treeNodeID - 1].treeInfo;
            if (treeInfo == null) { Debug.LogError("GetGeneratedMesh: Invalid node index used"); return false; }
            if (!treeInfo.subMeshCounts.IsCreated) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            if (meshDescription.vertexCount < 3 ||
                meshDescription.indexCount < 3)
            {
                Debug.LogWarning(string.Format("{0} called with a {1} that isn't valid", typeof(CSGTree).Name, typeof(GeneratedMeshDescription).Name));
                return false;
            }

            var meshIndex		= meshDescription.meshQueryIndex;
            var subMeshIndex	= meshDescription.subMeshQueryIndex;
            if (meshIndex    < 0) { Debug.LogError("GetGeneratedMesh: MeshIndex cannot be negative"); return false; }
            if (subMeshIndex < 0) { Debug.LogError("GetGeneratedMesh: SubMeshIndex cannot be negative"); return false; }

            
            int subMeshCountSize = (int)treeInfo.subMeshCounts.Length;
            if (subMeshIndex >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: SubMeshIndex is higher than the number of generated meshes"); return false; }
            if (meshIndex    >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: MeshIndex is higher than the number of generated meshes"); return false; }

            int foundIndex = -1;
            for (int i = 0; i < subMeshCountSize; i++)
            {
                if (meshIndex    == treeInfo.subMeshCounts[i].meshIndex &&
                    subMeshIndex == treeInfo.subMeshCounts[i].subMeshIndex)
                {
                    foundIndex = i;
                    break;
                }
            }
            if (foundIndex < 0 || foundIndex >= subMeshCountSize) { Debug.LogError("GetGeneratedMesh: Could not find mesh associated with MeshIndex/SubMeshIndex pair"); return false; }
            
            var subMeshCount = treeInfo.subMeshCounts[foundIndex];
            if (subMeshCount.indexCount > meshDescription.indexCount) { Debug.LogError("GetGeneratedMesh: The destination indices array (" + meshDescription.indexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.indexCount + ")"); return false; }
            if (subMeshCount.vertexCount > meshDescription.vertexCount) { Debug.LogError("GetGeneratedMesh: The destination vertices array (" + meshDescription.vertexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.vertexCount + ")"); return false; }
            if (subMeshCount.indexCount == 0 || subMeshCount.vertexCount == 0) { Debug.LogWarning("GetGeneratedMesh: Mesh is empty"); return false; }


            var usedVertexChannels		= meshDescription.meshQuery.UsedVertexChannels;
            var vertexCount				= meshDescription.vertexCount;
            var indexCount				= meshDescription.indexCount;

            var surfacesOffset  = subMeshCount.surfacesOffset;
            var surfacesCount   = subMeshCount.surfacesCount;
            var subMeshSurfaces = treeInfo.subMeshSurfaces;
            if (!subMeshSurfaces.IsCreated ||
                surfacesCount == 0 ||
                subMeshCount.vertexCount != vertexCount ||
                subMeshCount.indexCount  != indexCount ||
                subMeshCount.vertexCount == 0 ||
                subMeshCount.indexCount == 0)
                return false;

            // create our arrays on the managed side with the correct size

            bool useTangents   = ((usedVertexChannels & VertexChannelFlags.Tangent) == VertexChannelFlags.Tangent);
            bool useNormals    = ((usedVertexChannels & VertexChannelFlags.Normal ) == VertexChannelFlags.Normal);
            bool useUV0        = ((usedVertexChannels & VertexChannelFlags.UV0    ) == VertexChannelFlags.UV0);
            
            Profiler.BeginSample("Alloc");
            Realloc(ref generatedMeshContents.tangents,     vertexCount,    allocator, useTangents);
            Realloc(ref generatedMeshContents.normals,      vertexCount,    allocator, useNormals);
            Realloc(ref generatedMeshContents.uv0,          vertexCount,    allocator, useUV0);
            Realloc(ref generatedMeshContents.positions,    vertexCount,    allocator);
            Realloc(ref generatedMeshContents.indices,      indexCount,     allocator);
            Realloc(ref generatedMeshContents.brushIndices, indexCount / 3, allocator);
            Profiler.EndSample();

            if (!generatedMeshContents.indices.IsCreated ||
                !generatedMeshContents.positions.IsCreated)
                return false;
            
            generatedMeshContents.vertexCount   = vertexCount;
            generatedMeshContents.indexCount    = indexCount;
            
            Profiler.BeginSample("Generate");
            var generateVertexBuffersJob = new GenerateVertexBuffersJob
            {   
                meshQuery                   = subMeshCount.meshQuery,
                 
                surfacesOffset              = surfacesOffset,
                surfacesCount               = surfacesCount,
                subMeshSurfaces             = subMeshSurfaces,
                
                subMeshIndexCount           = generatedMeshContents.indexCount, 
                subMeshVertexCount          = generatedMeshContents.vertexCount,

                generatedMeshIndices        = generatedMeshContents.indices,
                generatedMeshBrushIndices   = generatedMeshContents.brushIndices,
                generatedMeshPositions      = generatedMeshContents.positions,
                generatedMeshTangents       = generatedMeshContents.tangents,
                generatedMeshNormals        = generatedMeshContents.normals,
                generatedMeshUV0            = generatedMeshContents.uv0
            };
            jobHandle = generateVertexBuffersJob.Schedule(dependencies);
            Profiler.EndSample();
            return true;
        }

        
        public static bool GetGeneratedMeshPositionOnly(int treeNodeID, ref GeneratedMeshDescription meshDescription, ref GeneratedMeshContents generatedMeshContents, Allocator allocator, out JobHandle jobHandle, JobHandle dependencies = default)
        {
            jobHandle = default;
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return false;

            if (nodeHierarchies[treeNodeID - 1].treeInfo == null) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            TreeInfo treeInfo = nodeHierarchies[treeNodeID - 1].treeInfo;
            if (treeInfo == null) { Debug.LogError("GetGeneratedMesh: Invalid node index used"); return false; }
            if (!treeInfo.subMeshCounts.IsCreated) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            if (meshDescription.vertexCount < 3 ||
                meshDescription.indexCount < 3)
            {
                Debug.LogWarning(string.Format("{0} called with a {1} that isn't valid", typeof(CSGTree).Name, typeof(GeneratedMeshDescription).Name));
                return false;
            }

            var meshIndex		= meshDescription.meshQueryIndex;
            var subMeshIndex	= meshDescription.subMeshQueryIndex;
            if (meshIndex    < 0) { Debug.LogError("GetGeneratedMesh: MeshIndex cannot be negative"); return false; }
            if (subMeshIndex < 0) { Debug.LogError("GetGeneratedMesh: SubMeshIndex cannot be negative"); return false; }

            
            int subMeshCountSize = (int)treeInfo.subMeshCounts.Length;
            if (subMeshIndex >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: SubMeshIndex is higher than the number of generated meshes"); return false; }
            if (meshIndex    >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: MeshIndex is higher than the number of generated meshes"); return false; }

            int foundIndex = -1;
            for (int i = 0; i < subMeshCountSize; i++)
            {
                if (meshIndex    == treeInfo.subMeshCounts[i].meshIndex &&
                    subMeshIndex == treeInfo.subMeshCounts[i].subMeshIndex)
                {
                    foundIndex = i;
                    break;
                }
            }
            if (foundIndex < 0 || foundIndex >= subMeshCountSize) { Debug.LogError("GetGeneratedMesh: Could not find mesh associated with MeshIndex/SubMeshIndex pair"); return false; }
            
            var subMeshCount = treeInfo.subMeshCounts[foundIndex];
            if (subMeshCount.indexCount > meshDescription.indexCount) { Debug.LogError("GetGeneratedMesh: The destination indices array (" + meshDescription.indexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.indexCount + ")"); return false; }
            if (subMeshCount.vertexCount > meshDescription.vertexCount) { Debug.LogError("GetGeneratedMesh: The destination vertices array (" + meshDescription.vertexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.vertexCount + ")"); return false; }
            if (subMeshCount.indexCount == 0 || subMeshCount.vertexCount == 0) { Debug.LogWarning("GetGeneratedMesh: Mesh is empty"); return false; }


            var usedVertexChannels		= meshDescription.meshQuery.UsedVertexChannels;
            var vertexCount				= meshDescription.vertexCount;
            var indexCount				= meshDescription.indexCount;

            var surfacesOffset  = subMeshCount.surfacesOffset;
            var surfacesCount   = subMeshCount.surfacesCount;
            var subMeshSurfaces = treeInfo.subMeshSurfaces;
            if (!subMeshSurfaces.IsCreated ||
                surfacesCount == 0 ||
                subMeshCount.vertexCount != vertexCount ||
                subMeshCount.indexCount  != indexCount ||
                subMeshCount.vertexCount == 0 ||
                subMeshCount.indexCount == 0)
                return false;

            // create our arrays on the managed side with the correct size

            Profiler.BeginSample("Alloc");
            Realloc(ref generatedMeshContents.positions,    vertexCount,    allocator);
            Realloc(ref generatedMeshContents.indices,      indexCount,     allocator);
            Realloc(ref generatedMeshContents.brushIndices, indexCount / 3, allocator);
            Profiler.EndSample();

            if (!generatedMeshContents.indices.IsCreated ||
                !generatedMeshContents.positions.IsCreated)
                return false;
            
            generatedMeshContents.vertexCount   = vertexCount;
            generatedMeshContents.indexCount    = indexCount;
            
            Profiler.BeginSample("Generate");
            var generateVertexBuffersJob = new GenerateVertexBuffersPositionOnlyJob
            {   
                surfacesOffset              = surfacesOffset,
                surfacesCount               = surfacesCount,
                subMeshSurfaces             = subMeshSurfaces,
                
                subMeshIndexCount           = generatedMeshContents.indexCount, 
                subMeshVertexCount          = generatedMeshContents.vertexCount,

                generatedMeshIndices        = generatedMeshContents.indices,
                generatedMeshBrushIndices   = generatedMeshContents.brushIndices,
                generatedMeshPositions      = generatedMeshContents.positions
            };
            jobHandle = generateVertexBuffersJob.Schedule(dependencies);
            Profiler.EndSample();
            return true;
        }


        static List<SubMeshCounts> s_ValidMeshDescriptions = new List<SubMeshCounts>();
        public static bool GetGeneratedMeshes(int treeNodeID, GeneratedMeshDescription[] meshDescriptions, int startIndex, int endIndex, ref GeneratedMeshContents generatedMeshContents, Allocator allocator, out JobHandle jobHandle, JobHandle dependencies = default)
        {
            s_ValidMeshDescriptions.Clear();
            jobHandle = default;
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return false;

            if (nodeHierarchies[treeNodeID - 1].treeInfo == null) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            TreeInfo treeInfo = nodeHierarchies[treeNodeID - 1].treeInfo;
            if (treeInfo == null) { Debug.LogError("GetGeneratedMesh: Invalid node index used"); return false; }
            if (!treeInfo.subMeshCounts.IsCreated) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            int subMeshCountSize = (int)treeInfo.subMeshCounts.Length;
            var subMeshSurfaces = treeInfo.subMeshSurfaces;
            if (!subMeshSurfaces.IsCreated)
                return false;

            bool success = false;
            int totalVertexCount = 0;
            int totalIndexCount = 0;
            for (int d = startIndex; d < endIndex; d++)
            { 
                ref var meshDescription = ref meshDescriptions[d];
                if (meshDescription.vertexCount < 3 ||
                    meshDescription.indexCount < 3)
                {
                    Debug.LogWarning(string.Format("{0} called with a {1} that isn't valid", typeof(CSGTree).Name, typeof(GeneratedMeshDescription).Name));
                    continue;
                }

                var meshIndex		= meshDescription.meshQueryIndex;
                var subMeshIndex	= meshDescription.subMeshQueryIndex;
                if (meshIndex    < 0) { Debug.LogError("GetGeneratedMesh: MeshIndex cannot be negative"); continue; }
                if (subMeshIndex < 0) { Debug.LogError("GetGeneratedMesh: SubMeshIndex cannot be negative"); continue; }

            
                if (subMeshIndex >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: SubMeshIndex is higher than the number of generated meshes"); continue; }
                if (meshIndex    >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: MeshIndex is higher than the number of generated meshes"); continue; }

                int foundIndex = -1;
                for (int i = 0; i < subMeshCountSize; i++)
                {
                    if (meshIndex    == treeInfo.subMeshCounts[i].meshIndex &&
                        subMeshIndex == treeInfo.subMeshCounts[i].subMeshIndex)
                    {
                        foundIndex = i;
                        break;
                    }
                }
                if (foundIndex < 0 || foundIndex >= subMeshCountSize) { Debug.LogError("GetGeneratedMesh: Could not find mesh associated with MeshIndex/SubMeshIndex pair"); return false; }
            
                var subMeshCount = treeInfo.subMeshCounts[foundIndex];
                if (subMeshCount.indexCount > meshDescription.indexCount) { Debug.LogError("GetGeneratedMesh: The destination indices array (" + meshDescription.indexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.indexCount + ")"); continue; }
                if (subMeshCount.vertexCount > meshDescription.vertexCount) { Debug.LogError("GetGeneratedMesh: The destination vertices array (" + meshDescription.vertexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.vertexCount + ")"); continue; }
                if (subMeshCount.indexCount == 0 || subMeshCount.vertexCount == 0) { Debug.LogWarning("GetGeneratedMesh: Mesh is empty"); continue; }

                //var usedVertexChannels  = meshDescription.meshQuery.UsedVertexChannels;
                
                var surfacesCount       = subMeshCount.surfacesCount;
                if (surfacesCount == 0 ||
                    subMeshCount.vertexCount != meshDescription.vertexCount ||
                    subMeshCount.indexCount  != meshDescription.indexCount ||
                    subMeshCount.vertexCount == 0 ||
                    subMeshCount.indexCount == 0)
                    continue;

                s_ValidMeshDescriptions.Add(subMeshCount);
                totalVertexCount += subMeshCount.vertexCount;
                totalIndexCount += subMeshCount.indexCount;
                success = true;
            }

            var numberOfSubMeshes = s_ValidMeshDescriptions.Count;
            if (!success || numberOfSubMeshes == 0)
                return false;

                
            bool useTangents   = true;//((usedVertexChannels & VertexChannelFlags.Tangent) == VertexChannelFlags.Tangent);
            bool useNormals    = true;//((usedVertexChannels & VertexChannelFlags.Normal ) == VertexChannelFlags.Normal);
            bool useUV0        = true;//((usedVertexChannels & VertexChannelFlags.UV0    ) == VertexChannelFlags.UV0);
            
            Profiler.BeginSample("Alloc");
            Realloc(ref generatedMeshContents.subMeshes,    numberOfSubMeshes,   allocator);
            Realloc(ref generatedMeshContents.tangents,     totalVertexCount,    allocator, useTangents);
            Realloc(ref generatedMeshContents.normals,      totalVertexCount,    allocator, useNormals);
            Realloc(ref generatedMeshContents.uv0,          totalVertexCount,    allocator, useUV0);
            Realloc(ref generatedMeshContents.positions,    totalVertexCount,    allocator);
            Realloc(ref generatedMeshContents.indices,      totalIndexCount,     allocator);
            Realloc(ref generatedMeshContents.brushIndices, totalIndexCount / 3, allocator);
            Profiler.EndSample();


            if (!generatedMeshContents.indices.IsCreated ||
                !generatedMeshContents.positions.IsCreated)
                return false;

            ref var subMeshes = ref generatedMeshContents.subMeshes;

            int currentBaseVertex = 0;
            int currentBaseIndex = 0;

            for (int d = 0; d < s_ValidMeshDescriptions.Count; d++)
            {
                var subMeshCount        = s_ValidMeshDescriptions[d];
                var vertexCount		    = subMeshCount.vertexCount;
                var indexCount		    = subMeshCount.indexCount;
                var surfacesOffset      = subMeshCount.surfacesOffset;
                var surfacesCount       = subMeshCount.surfacesCount;

                subMeshes[d] = new GeneratedSubMesh
                { 
                    baseVertex          = currentBaseVertex,
                    baseIndex           = currentBaseIndex,
                    indexCount          = indexCount,
                    vertexCount         = vertexCount,
                    surfacesOffset      = surfacesOffset,
                    surfacesCount       = surfacesCount,
                };


                success = true;

                currentBaseVertex += vertexCount;
                currentBaseIndex += indexCount;
            }

            generatedMeshContents.vertexCount = totalVertexCount;
            generatedMeshContents.indexCount = totalIndexCount;

            Profiler.BeginSample("Generate");
            var generateVertexBuffersJob = new GenerateVertexBuffersSlicedJob
            {   
                subMeshes                   = subMeshes,
                subMeshSurfaces             = subMeshSurfaces.AsDeferredJobArray(),

                generatedMeshIndices        = generatedMeshContents.indices,
                generatedMeshBrushIndices   = generatedMeshContents.brushIndices,
                generatedMeshPositions      = generatedMeshContents.positions,
                generatedMeshTangents       = generatedMeshContents.tangents,
                generatedMeshNormals        = generatedMeshContents.normals,
                generatedMeshUV0            = generatedMeshContents.uv0
            };
            jobHandle = generateVertexBuffersJob.Schedule(dependencies);
            Profiler.EndSample();
            return success;
        }

        internal static GeneratedMeshDescription[] GetMeshDescriptions(Int32                treeNodeID,
                                                                       MeshQuery[]          meshQueries,
                                                                       VertexChannelFlags   vertexChannelMask)
        {
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return null;
            if (meshQueries == null)
                throw new ArgumentNullException("meshTypes");

            if (meshQueries.Length == 0)
            {
                Debug.Log("meshQueries.Length == 0");
                return null;
            }

            if (!IsValidNodeID(treeNodeID))
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return null;
            }

            var treeNodeIndex = treeNodeID - 1;
            var treeInfo = nodeHierarchies[treeNodeIndex].treeInfo;
            if (treeInfo == null)
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return null;
            }

            if (treeInfo.subMeshCounts.IsCreated)
                treeInfo.subMeshCounts.Clear();
            treeInfo.meshDescriptions.Clear();

            if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
            {
                UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMesh");
                try
                {
                    var handle = UpdateTreeMeshes(new int[] { treeNodeID });
                    handle.Complete();
                } finally { UnityEngine.Profiling.Profiler.EndSample(); }
            }

            UnityEngine.Profiling.Profiler.BeginSample("CombineSubMeshes");
            try
            {
                CombineSubMeshes(treeInfo, meshQueries, vertexChannelMask);
            } finally { UnityEngine.Profiling.Profiler.EndSample(); }


            {
                var flags = nodeFlags[treeNodeIndex];
                flags.UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                nodeFlags[treeNodeIndex] = flags;
            }

            if (!treeInfo.subMeshCounts.IsCreated ||
                treeInfo.subMeshCounts.Length <= 0)
                return null;

            for (int i = (int)treeInfo.subMeshCounts.Length - 1; i >= 0; i--)
            {
                var subMesh = treeInfo.subMeshCounts[i];

                // Make sure the meshDescription actually holds a mesh
                if (subMesh.vertexCount == 0 ||
                    subMesh.indexCount == 0)
                    continue;

                // Make sure the mesh is valid
                if (subMesh.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial &&
                    subMesh.vertexCount >= kMaxPhysicsIndexCount)
                {
                    Debug.LogError("Mesh has too many vertices (" + subMesh.vertexCount + " > " + kMaxPhysicsIndexCount + ")");
                    continue;
                }

                var description = new GeneratedMeshDescription
                {
                    meshQuery           = subMesh.meshQuery,
                    surfaceParameter    = subMesh.surfaceIdentifier,
                    meshQueryIndex      = subMesh.meshIndex,
                    subMeshQueryIndex   = subMesh.subMeshIndex,

                    geometryHashValue   = subMesh.geometryHash,
                    surfaceHashValue    = subMesh.surfaceHash,

                    vertexCount         = subMesh.vertexCount,
                    indexCount          = subMesh.indexCount
                };

                treeInfo.meshDescriptions.Add(description);
            }

            if (treeInfo.meshDescriptions == null ||
                treeInfo.meshDescriptions.Count == 0 ||
                treeInfo.meshDescriptions[0].vertexCount <= 0 ||
                treeInfo.meshDescriptions[0].indexCount <= 0)
            {
                return null;
            }

            return treeInfo.meshDescriptions.ToArray();
        }

        class MeshQuerySurfaces
        {
            public readonly List<SubMeshSurface> querySurfaces = new List<SubMeshSurface>();
        }
        
        struct SubMeshSurfaceComparer : IComparer<SubMeshSurface>
        {
            public int Compare(SubMeshSurface x, SubMeshSurface y)
            {
                return x.surfaceParameter.CompareTo(y.surfaceParameter);
            }
        }

        static MeshQuerySurfaces[]  meshQuerySurfaces   = null;
        internal static void CombineSubMeshes(TreeInfo treeInfo,
                                              MeshQuery[] meshQueries,
                                              VertexChannelFlags vertexChannelMask)
        {
            var treeBrushNodeIDs = treeInfo.treeBrushes;
            var treeBrushNodeCount = (Int32)(treeBrushNodeIDs.Count);
            if (treeBrushNodeCount <= 0)
            {
                if (treeInfo.subMeshCounts.IsCreated)
                    treeInfo.subMeshCounts.Clear();
                if (treeInfo.subMeshSurfaces.IsCreated)
                    treeInfo.subMeshSurfaces.Clear();
                return;
            }

            Profiler.BeginSample("Allocate 1");
            if (meshQuerySurfaces == null ||
                meshQuerySurfaces.Length < meshQueries.Length)
            {
                meshQuerySurfaces = new MeshQuerySurfaces[meshQueries.Length];
                for (int i = 0; i < meshQueries.Length; i++)
                    meshQuerySurfaces[i] = new MeshQuerySurfaces();
            } else
            {
                for (int i = 0; i < meshQueries.Length; i++)
                    meshQuerySurfaces[i].querySurfaces.Clear();
            }
            Profiler.EndSample();

            Profiler.BeginSample("Find Surfaces");
            int surfaceCount = 0;
            for (int b = 0, count_b = treeBrushNodeCount; b < count_b; b++)
            {
                var brushNodeID     = treeBrushNodeIDs[b];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                var brushNodeIndex  = brushNodeID - 1;
                var nodeType = nodeFlags[brushNodeIndex].nodeType;
                if (nodeType != CSGNodeType.Brush)
                    continue;

                var brushInfo = nodeHierarchies[brushNodeIndex].brushInfo;
                if (brushInfo == null)
                    continue;
            
                var treeNodeID          = nodeHierarchies[brushNodeIndex].treeNodeID;
                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeID - 1];

                if (!chiselLookupValues.brushRenderBufferCache.TryGetValue(brushNodeIndex, out var brushRenderBuffer) ||
                    !brushRenderBuffer.IsCreated)
                    continue;

                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;
                ref var surfaces = ref brushRenderBufferRef.surfaces;

                if (surfaces.Length == 0)
                    continue;

                for (int j = 0, count_j = (int)surfaces.Length; j < count_j; j++)
                {
                    ref var brushSurfaceBuffer = ref surfaces[j];
                    var surfaceVertexCount = brushSurfaceBuffer.vertices.Length;
                    var surfaceIndexCount = brushSurfaceBuffer.indices.Length;

                    if (surfaceVertexCount <= 0 || surfaceIndexCount <= 0)
                        continue;

                    ref var surfaceLayers = ref brushSurfaceBuffer.surfaceLayers;

                    for (int t = 0; t < meshQueries.Length; t++)
                    {
                        var meshQuery = meshQueries[t];
                        var core_surface_flags = surfaceLayers.layerUsage;
                        if ((core_surface_flags & meshQuery.LayerQueryMask) != meshQuery.LayerQuery)
                            continue;

                        int surfaceParameter = 0;
                        if (meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1 &&
                            meshQuery.LayerParameterIndex <= LayerParameterIndex.MaxLayerParameterIndex)
                        {
                            // TODO: turn this into array lookup
                            switch (meshQuery.LayerParameterIndex)
                            {
                                case LayerParameterIndex.LayerParameter1: surfaceParameter = surfaceLayers.layerParameter1; break;
                                case LayerParameterIndex.LayerParameter2: surfaceParameter = surfaceLayers.layerParameter2; break;
                                case LayerParameterIndex.LayerParameter3: surfaceParameter = surfaceLayers.layerParameter3; break;
                            }
                        }

                        var querySurfaces = meshQuerySurfaces[t].querySurfaces;
                        querySurfaces.Add(new SubMeshSurface
                        {
                            surfaceIndex        = j,
                            brushNodeID         = brushNodeID,
                            surfaceParameter    = surfaceParameter,
                            brushRenderBuffer   = brushRenderBuffer
                        });
                        surfaceCount++;
                    }
                }
            }
            Profiler.EndSample();

            Profiler.BeginSample("Allocate 2");
            var surfaceCapacity = surfaceCount * meshQueries.Length;
            if (treeInfo.subMeshSurfaces.IsCreated)
            {
                treeInfo.subMeshSurfaces.Clear();
                if (treeInfo.subMeshSurfaces.Capacity < surfaceCapacity)
                    treeInfo.subMeshSurfaces.Capacity = surfaceCapacity;
            } else
                treeInfo.subMeshSurfaces = new NativeList<SubMeshSurface>(surfaceCapacity, Allocator.Persistent);

            var subMeshCapacity = surfaceCount * meshQueries.Length;
            if (treeInfo.subMeshCounts.IsCreated)
            {
                treeInfo.subMeshCounts.Clear();
                if (treeInfo.subMeshCounts.Capacity < subMeshCapacity)
                    treeInfo.subMeshCounts.Capacity = subMeshCapacity;
            } else
                treeInfo.subMeshCounts = new NativeList<SubMeshCounts>(subMeshCapacity, Allocator.Persistent);
            var subMeshCounts = treeInfo.subMeshCounts;

            Profiler.EndSample();

            Profiler.BeginSample("Sort");
            var comparer = new SubMeshSurfaceComparer();
            for (int t = 0; t < meshQueries.Length; t++)
            {
                var querySurfaces = meshQuerySurfaces[t].querySurfaces;
                querySurfaces.Sort(comparer);
            }
            Profiler.EndSample();

            var subMeshSurfaces = treeInfo.subMeshSurfaces;
            Profiler.BeginSample("Assign");
            for (int t = 0, meshIndex = 0, surfacesOffset = 0; t < meshQueries.Length; t++)
            {
                var meshQuery       = meshQueries[t];
                var querySurfaces   = meshQuerySurfaces[t].querySurfaces;
                var isPhysics       = meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial;

                if (querySurfaces.Count == 0)
                    continue;

                for (int b = 0; b < querySurfaces.Count; b++)
                    subMeshSurfaces.AddNoResize(querySurfaces[b]);

                var currentSubMesh = new SubMeshCounts
                {
                    meshIndex           = meshIndex,
                    subMeshIndex        = 0,
                    meshQuery           = meshQuery,
                    surfaceIdentifier   = querySurfaces[0].surfaceParameter,
                    surfacesOffset      = surfacesOffset
                };
                for (int b = 0; b < querySurfaces.Count; b++)
                {
                    var subMeshSurface              = querySurfaces[b];
                    var surfaceParameter            = subMeshSurface.surfaceParameter;
                    ref var brushRenderBufferRef    = ref subMeshSurface.brushRenderBuffer.Value;
                    ref var brushSurfaceBuffer      = ref brushRenderBufferRef.surfaces[subMeshSurface.surfaceIndex];
                    var surfaceVertexCount          = brushSurfaceBuffer.vertices.Length;
                    var surfaceIndexCount           = brushSurfaceBuffer.indices.Length;

                    if (currentSubMesh.surfaceIdentifier != surfaceParameter || 
                        (isPhysics && currentSubMesh.indexCount >= kMaxPhysicsIndexCount))
                    {
                        // Store the previous subMeshCount
                        subMeshCounts.AddNoResize(currentSubMesh);
                        
                        // Create the new SubMeshCount
                        currentSubMesh.surfaceIdentifier   = surfaceParameter;
                        currentSubMesh.subMeshIndex++;
                        currentSubMesh.surfaceHash         = 0;
                        currentSubMesh.geometryHash        = 0;
                        currentSubMesh.indexCount          = 0;
                        currentSubMesh.vertexCount         = 0;
                        currentSubMesh.surfacesOffset      += currentSubMesh.surfacesCount;
                        currentSubMesh.surfacesCount       = 0;
                    } 

                    currentSubMesh.indexCount   += surfaceIndexCount;
                    currentSubMesh.vertexCount  += surfaceVertexCount;
                    currentSubMesh.surfaceHash  = math.hash(new uint2(currentSubMesh.surfaceHash, brushSurfaceBuffer.surfaceHash));
                    currentSubMesh.geometryHash = math.hash(new uint2(currentSubMesh.geometryHash, brushSurfaceBuffer.geometryHash));
                    currentSubMesh.surfacesCount++;
                    //surfacesOffset++;
                }
                // Store the last subMeshCount
                subMeshCounts.AddNoResize(currentSubMesh);
                surfacesOffset = currentSubMesh.surfacesOffset + currentSubMesh.surfacesCount;
                meshIndex++;
            }
            Profiler.EndSample();
        }

        private static void UpdateDelayedHierarchyModifications()
        {
            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;

                {
                    var flags = nodeFlags[branchNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.OperationNeedsUpdate);
                    nodeFlags[branchNodeIndex] = flags;
                }
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedPreviousSiblingsUpdate))
                    continue;

                // TODO: implement
                //operation.RebuildPreviousSiblings();
                {
                    var flags = nodeFlags[branchNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.NeedPreviousSiblingsUpdate);
                    nodeFlags[branchNodeIndex] = flags;
                }
            }

            // TODO: implement
            /*
            var foundOperations = new List<int>();
            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                foundOperations.Add(branchNodeIndex);
            }

            for (int i = 0; i < foundOperations.Count; i++)
            {
                //UpdateChildOperationTouching(foundOperations[i]);
            }
            */

            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                // TODO: implement
                //UpdateChildBrushTouching(branchNodeID);
                {
                    var flags = nodeFlags[branchNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                    nodeFlags[branchNodeIndex] = flags;
                }
            }
        }
    }
}