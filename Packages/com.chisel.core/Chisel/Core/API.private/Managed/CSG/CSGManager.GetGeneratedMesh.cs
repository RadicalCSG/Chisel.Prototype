using System;
using System.Collections.Generic;
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
        const int kMaxPhysicsVertexCount = 32000;


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

        private struct MeshID
        {
            public MeshQuery	meshQuery;
            public Int32		surfaceParameter;

            public static bool operator ==(MeshID left, MeshID right) { return (left.meshQuery == right.meshQuery && left.surfaceParameter == right.surfaceParameter); }
            public static bool operator !=(MeshID left, MeshID right) { return (left.meshQuery != right.meshQuery || left.surfaceParameter != right.surfaceParameter); }
            public override bool Equals(object obj) { if (ReferenceEquals(this, obj)) return true; if (!(obj is MeshID)) return false; var other = (MeshID)obj; return other == this; }
            public override int GetHashCode() { return surfaceParameter.GetHashCode() ^ meshQuery.GetHashCode(); }
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
            if (treeInfo.subMeshCounts == null) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

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

            
            int subMeshCountSize = (int)treeInfo.subMeshCounts.Count;
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
            if (treeInfo.subMeshCounts == null) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

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

            
            int subMeshCountSize = (int)treeInfo.subMeshCounts.Count;
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
            if (treeInfo.subMeshCounts == null) { Debug.LogWarning("Tree has not been initialized properly"); return false; }

            int subMeshCountSize = (int)treeInfo.subMeshCounts.Count;
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

            if (treeInfo.subMeshCounts.Count <= 0)
                return null;

            for (int i = (int)treeInfo.subMeshCounts.Count - 1; i >= 0; i--)
            {
                var subMesh = treeInfo.subMeshCounts[i];

                // Make sure the meshDescription actually holds a mesh
                if (subMesh.vertexCount == 0 ||
                    subMesh.indexCount == 0)
                    continue;

                // Make sure the mesh is valid
                if (subMesh.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial &&
                    subMesh.vertexCount >= kMaxPhysicsVertexCount)
                {
                    Debug.LogError("Mesh has too many vertices (" + subMesh.vertexCount + " > " + kMaxPhysicsVertexCount + ")");
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

        static Dictionary<MeshID, int>      uniqueMeshDescriptions  = new Dictionary<MeshID, int>();

        class MeshQuerySurfaces
        {
            public readonly List<SubMeshSurfaceQuery> querySurfaces = new List<SubMeshSurfaceQuery>();
        }
        
        internal struct SubMeshSurfaceQuery
        {
            public int              surfaceParameter;
            public SubMeshSurface   surface;
        }

        struct SubMeshSurfaceQueryComparer : IComparer<SubMeshSurfaceQuery>
        {
            public int Compare(SubMeshSurfaceQuery x, SubMeshSurfaceQuery y)
            {
                return x.surfaceParameter.CompareTo(y.surfaceParameter);
            }
        }

        static MeshQuerySurfaces[]  meshQuerySurfaces   = null;
        internal static void CombineSubMeshes(TreeInfo treeInfo,
                                              MeshQuery[] meshQueries,
                                              VertexChannelFlags vertexChannelMask)
        {
            var subMeshCounts = treeInfo.subMeshCounts;
            subMeshCounts.Clear();
            if (treeInfo.subMeshSurfaces.IsCreated)
                treeInfo.subMeshSurfaces.Clear();

            var treeBrushNodeIDs = treeInfo.treeBrushes;
            var treeBrushNodeCount = (Int32)(treeBrushNodeIDs.Count);
            if (treeBrushNodeCount <= 0)
                return;

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

            uniqueMeshDescriptions.Clear();
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
                        querySurfaces.Add(new SubMeshSurfaceQuery
                        {
                            surfaceParameter    = surfaceParameter,
                            surface             = new SubMeshSurface
                            {
                                surfaceIndex        = j,
                                brushNodeID         = brushNodeID,
                                brushRenderBuffer   = brushRenderBuffer
                            }
                        });
                        surfaceCount++;
                    }
                }
            }

            var surfaceCapacity = surfaceCount * meshQueries.Length;
            if (treeInfo.subMeshSurfaces.IsCreated)
            {
                treeInfo.subMeshSurfaces.Clear();
                if (treeInfo.subMeshSurfaces.Capacity < surfaceCapacity)
                    treeInfo.subMeshSurfaces.Capacity = surfaceCapacity;
            } else
                treeInfo.subMeshSurfaces = new NativeList<SubMeshSurface>(surfaceCapacity, Allocator.Persistent);            
            var subMeshSurfaces = treeInfo.subMeshSurfaces;

            var comparer = new SubMeshSurfaceQueryComparer();
            for (int t = 0; t < meshQueries.Length; t++)
            {
                var meshQuery       = meshQueries[t];
                var querySurfaces   = meshQuerySurfaces[t].querySurfaces;
                var isPhysics       = meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial;

                querySurfaces.Sort(comparer);

                for (int b = 0; b < querySurfaces.Count; b++)
                {
                    var subMeshSurfaceQuery         = querySurfaces[b];
                    var subMeshSurface              = subMeshSurfaceQuery.surface;
                    ref var brushRenderBufferRef    = ref subMeshSurface.brushRenderBuffer.Value;
                    ref var brushSurfaceBuffer      = ref brushRenderBufferRef.surfaces[subMeshSurface.surfaceIndex];
                    var surfaceVertexCount          = brushSurfaceBuffer.vertices.Length;
                    var surfaceIndexCount           = brushSurfaceBuffer.indices.Length;

                    ref var surfaceLayers           = ref brushSurfaceBuffer.surfaceLayers;


                    var meshID = new MeshID { meshQuery = meshQuery, surfaceParameter = subMeshSurfaceQuery.surfaceParameter };

                    if (!uniqueMeshDescriptions.TryGetValue(meshID, out int generatedMeshIndex))
                        generatedMeshIndex = -1;

                    SubMeshCounts currentSubMesh;
                    if (generatedMeshIndex == -1 
                        || (isPhysics &&
                            (subMeshCounts[generatedMeshIndex].vertexCount + surfaceVertexCount) >= kMaxPhysicsVertexCount))
                    {
                        int meshIndex, subMeshIndex;
                        if (generatedMeshIndex != -1)
                        {
                            var prevMeshCountIndex = generatedMeshIndex;
                            generatedMeshIndex = (int)subMeshCounts.Count;
                            subMeshIndex = subMeshCounts[prevMeshCountIndex].subMeshIndex + 1;
                            meshIndex = subMeshCounts[prevMeshCountIndex].meshIndex;
                        } else
                        {
                            generatedMeshIndex = (int)subMeshCounts.Count;
                            meshIndex = generatedMeshIndex;
                            subMeshIndex = 0;
                        }

                        uniqueMeshDescriptions[meshID] = generatedMeshIndex;
                        currentSubMesh = new SubMeshCounts
                        {
                            meshIndex           = meshIndex,
                            subMeshIndex        = subMeshIndex,
                            meshQuery           = meshID.meshQuery,
                            surfaceIdentifier   = subMeshSurfaceQuery.surfaceParameter,
                            surfacesOffset      = subMeshSurfaces.Length
                        };
                        subMeshCounts.Add(currentSubMesh);
                    } else
                        currentSubMesh = subMeshCounts[generatedMeshIndex];

                    currentSubMesh.indexCount   += surfaceIndexCount;
                    currentSubMesh.vertexCount  += surfaceVertexCount;
                    currentSubMesh.surfaceHash  = math.hash(new uint2(currentSubMesh.surfaceHash, brushSurfaceBuffer.surfaceHash));
                    currentSubMesh.geometryHash = math.hash(new uint2(currentSubMesh.geometryHash, brushSurfaceBuffer.geometryHash));
                    currentSubMesh.surfacesCount++;
                    subMeshSurfaces.AddNoResize(subMeshSurface);
                    subMeshCounts[generatedMeshIndex] = currentSubMesh;
                }
            }
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