using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
#if USE_INTERNAL_IMPLEMENTATION
	internal struct CSGSurfaceRenderBuffer
    {
        public Int32[]		indices;
        public Vector3[]	vertices;
        public Vector3[]	normals;
        public Vector2[]	uv0;

        public UInt64		geometryHash;
        public UInt64		surfaceHash;

        public MeshQuery	meshQuery;
        public Int32		surfaceParameter;
        public Int32		surfaceIndex;
    };
#endif

	static partial class CSGManager
    {
#if USE_INTERNAL_IMPLEMENTATION

        const int kMaxVertexCount = 65000;
        internal sealed class CSGBrushRenderBuffer
        {
            public readonly List<CSGSurfaceRenderBuffer> surfaceRenderBuffers = new List<CSGSurfaceRenderBuffer>();
        };

        internal sealed class SubMeshCounts
        {
            public MeshQuery meshQuery;
            public int		surfaceIdentifier;

            public int		meshIndex;
            public int		subMeshIndex;
            
            public ulong	surfaceHash;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
            public ulong	geometryHash;  // used to detect changes in vertex positions / indices

            public int		indexCount;
            public int		vertexCount;

            public readonly List<CSGSurfaceRenderBuffer> surfaces = new List<CSGSurfaceRenderBuffer>();
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

        internal sealed class BrushOutput
        {
            public int					brushMeshInstanceID;
            public OutputLoops			brushOutputLoops	= new OutputLoops();
            public CSGBrushRenderBuffer renderBuffers		= new CSGBrushRenderBuffer();

            public void Reset()
            {
                brushOutputLoops.Reset();
                renderBuffers.surfaceRenderBuffers.Clear();
            }
        }




        internal static void CombineSubMeshes(TreeInfo				treeInfo,
                                              MeshQuery[]			meshQueries,
                                              VertexChannelFlags	vertexChannelMask) 
        {
            var subMeshCounts = treeInfo.subMeshCounts;
            subMeshCounts.Clear();

            var treeBrushNodeIDs	= treeInfo.treeBrushes;
            var treeBrushNodeCount	= (Int32)(treeBrushNodeIDs.Count);
            if (treeBrushNodeCount <= 0)
                return;

            var uniqueMeshDescriptions = new Dictionary<MeshID, int>();
            for (Int32 b = 0, count_b = treeBrushNodeCount; b < count_b; b++)
            {
                var nodeID				= treeBrushNodeIDs[b];
                var nodeIndex			= nodeID - 1;
                var nodeType			= nodeFlags[nodeIndex].nodeType;
                if (nodeType != CSGNodeType.Brush)
                    continue;

                var brushOutput			= nodeHierarchies[nodeIndex].brushOutput;
                //var operation_type_bits = GetNodeOperationByIndex(nodeIndex);
                if (brushOutput == null //||
                    //brush.triangleMesh == null //||
                    //((int)operation_type_bits & InfiniteBrushBits) == InfiniteBrushBits 
                    )
                    continue;


                var renderBuffers	= brushOutput.renderBuffers;
                if (renderBuffers.surfaceRenderBuffers.Count == 0)
                    continue;

                var surfaceRenderBuffers = renderBuffers.surfaceRenderBuffers;
                for (int j = 0, count_j = (int)renderBuffers.surfaceRenderBuffers.Count; j < count_j; j++)
                {
                    var brushSurfaceBuffer	= surfaceRenderBuffers[j];
                    var surfaceVertexCount	= (brushSurfaceBuffer.vertices == null) ? 0 : brushSurfaceBuffer.vertices.Length;
                    var surfaceIndexCount	= (brushSurfaceBuffer.indices  == null) ? 0 : brushSurfaceBuffer.indices.Length;

                    if (surfaceVertexCount <= 0 || surfaceIndexCount <= 0) 
                        continue;

                    var surfaceParameter	= (brushSurfaceBuffer.meshQuery.LayerParameterIndex == LayerParameterIndex.None) ? 0 : brushSurfaceBuffer.surfaceParameter;
                    var meshID				= new MeshID (){ meshQuery = brushSurfaceBuffer.meshQuery, surfaceParameter = surfaceParameter };

                    int generatedMeshIndex;
                    if (!uniqueMeshDescriptions.TryGetValue(meshID, out generatedMeshIndex))
                        generatedMeshIndex = -1;
                    if (generatedMeshIndex == -1 ||
                        (subMeshCounts[generatedMeshIndex].vertexCount + surfaceVertexCount) >= kMaxVertexCount)
                    {
                        int meshIndex, subMeshIndex;
                        if (generatedMeshIndex != -1)
                        {
                            generatedMeshIndex = (int)subMeshCounts.Count;
                            var prevMeshCountIndex = generatedMeshIndex;
                            subMeshIndex		= subMeshCounts[prevMeshCountIndex].subMeshIndex + 1;
                            meshIndex			= subMeshCounts[prevMeshCountIndex].meshIndex;
                        } else
                        { 
                            generatedMeshIndex	= (int)subMeshCounts.Count;
                            meshIndex			= generatedMeshIndex;
                            subMeshIndex		= 0;
                        }

                        uniqueMeshDescriptions[meshID] = generatedMeshIndex;
                        SubMeshCounts newSubMesh = new SubMeshCounts();
                        newSubMesh.meshIndex			= meshIndex;
                        newSubMesh.subMeshIndex			= subMeshIndex;
                        newSubMesh.meshQuery			= meshID.meshQuery;
                        newSubMesh.surfaceIdentifier	= surfaceParameter;
                        newSubMesh.indexCount			= surfaceIndexCount;
                        newSubMesh.vertexCount			= surfaceVertexCount;
                        newSubMesh.surfaceHash			= brushSurfaceBuffer.surfaceHash;
                        newSubMesh.geometryHash			= brushSurfaceBuffer.geometryHash;
                        newSubMesh.surfaces.Add(brushSurfaceBuffer);

                        subMeshCounts.Add(newSubMesh);
                        continue;
                    } 
            
                    var currentSubMesh = subMeshCounts[generatedMeshIndex];
                    currentSubMesh.indexCount		+= surfaceIndexCount;
                    currentSubMesh.vertexCount		+= surfaceVertexCount;
                    currentSubMesh.surfaceHash		= Hashing.XXH64_mergeRound(currentSubMesh.surfaceHash,  brushSurfaceBuffer.surfaceHash);
                    currentSubMesh.geometryHash		= Hashing.XXH64_mergeRound(currentSubMesh.geometryHash, brushSurfaceBuffer.geometryHash);
                    currentSubMesh.surfaces.Add(brushSurfaceBuffer);
                }
            }

            for (int i = (int)subMeshCounts.Count - 1; i >= 0; i--)
            {
                if (subMeshCounts[i].vertexCount != 0 &&
                    subMeshCounts[i].indexCount != 0)
                    continue;
                subMeshCounts.RemoveAt(i);
            }
        }


        internal static GeneratedMeshContents GetGeneratedMesh(int treeNodeID, GeneratedMeshDescription meshDescription, GeneratedMeshContents previousGeneratedMeshContents)
        {
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return null;
            if (meshDescription.vertexCount <= 0 ||
                meshDescription.indexCount <= 0)
            {
                Debug.LogWarning(string.Format("{0} called with a {1} that isn't valid", typeof(CSGTree).Name, typeof(GeneratedMeshDescription).Name));
                return null;
            }

            var meshIndex		= meshDescription.meshQueryIndex;
            var subMeshIndex	= meshDescription.subMeshQueryIndex;
            if (meshIndex    < 0) { Debug.LogError("GetGeneratedMesh: MeshIndex cannot be negative"); return null; }
            if (subMeshIndex < 0) { Debug.LogError("GetGeneratedMesh: SubMeshIndex cannot be negative"); return null; }

            if (nodeHierarchies[treeNodeID - 1].treeInfo == null) { Debug.LogWarning("Tree has not been initialized properly"); return null;			}

            TreeInfo tree = nodeHierarchies[treeNodeID - 1].treeInfo;
            if (tree == null) { Debug.LogError("GetGeneratedMesh: Invalid node index used"); return null; }
            if (tree.subMeshCounts == null) { Debug.LogWarning("Tree has not been initialized properly"); return null; }
            



            int subMeshCountSize = (int)tree.subMeshCounts.Count;
            if (subMeshIndex >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: SubMeshIndex is higher than the number of generated meshes"); return null; }
            if (meshIndex    >= (int)subMeshCountSize) { Debug.LogError("GetGeneratedMesh: MeshIndex is higher than the number of generated meshes"); return null; }

            int foundIndex = -1;
            for (int i = 0; i < subMeshCountSize; i++)
            {
                if (meshIndex    == tree.subMeshCounts[i].meshIndex &&
                    subMeshIndex == tree.subMeshCounts[i].subMeshIndex)
                {
                    foundIndex = i;
                    break;
                }
            }
            if (foundIndex < 0 || foundIndex >= subMeshCountSize) { Debug.LogError("GetGeneratedMesh: Could not find mesh associated with MeshIndex/SubMeshIndex pair"); return null; }
            
            var subMeshCount = tree.subMeshCounts[foundIndex];
            if (subMeshCount.indexCount > meshDescription.indexCount) { Debug.LogError("GetGeneratedMesh: The destination indices array (" + meshDescription.indexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.indexCount + ")"); return null; }
            if (subMeshCount.vertexCount > meshDescription.vertexCount) { Debug.LogError("GetGeneratedMesh: The destination vertices array (" + meshDescription.vertexCount + ") is smaller than the size of the source data (" + (int)subMeshCount.vertexCount + ")"); return null; }
            if (subMeshCount.indexCount == 0 || subMeshCount.vertexCount == 0) { Debug.LogWarning("GetGeneratedMesh: Mesh is empty"); return null; }



            var generatedMesh			= (previousGeneratedMeshContents != null) ? previousGeneratedMeshContents : new GeneratedMeshContents();
            var usedVertexChannels		= meshDescription.meshQuery.UsedVertexChannels;
            var vertexCount				= meshDescription.vertexCount;
            var indexCount				= meshDescription.indexCount;
            generatedMesh.description	= meshDescription;
            
            // create our arrays on the managed side with the correct size
            generatedMesh.tangents		= ((usedVertexChannels & VertexChannelFlags.Tangent) == 0) ? null : (generatedMesh.tangents != null && generatedMesh.tangents.Length == vertexCount) ? generatedMesh.tangents : new Vector4[vertexCount];
            generatedMesh.normals		= ((usedVertexChannels & VertexChannelFlags.Normal ) == 0) ? null : (generatedMesh.normals  != null && generatedMesh.normals .Length == vertexCount) ? generatedMesh.normals  : new Vector3[vertexCount];
            generatedMesh.uv0			= ((usedVertexChannels & VertexChannelFlags.UV0    ) == 0) ? null : (generatedMesh.uv0      != null && generatedMesh.uv0     .Length == vertexCount) ? generatedMesh.uv0      : new Vector2[vertexCount];
            generatedMesh.positions		= (generatedMesh.positions != null && generatedMesh.positions .Length == vertexCount) ? generatedMesh.positions : new Vector3[vertexCount];
            generatedMesh.indices		= (generatedMesh.indices   != null && generatedMesh.indices   .Length == indexCount ) ? generatedMesh.indices   : new int    [indexCount ];

            generatedMesh.bounds = new Bounds();		

        
            bool result = CSGManager.GenerateVertexBuffers(subMeshCount, generatedMesh);

            if (!result)
                return null;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0, count = subMeshCount.indexCount; i < count; i++)
            {
                var position = generatedMesh.positions[generatedMesh.indices[i]];
                min.x = Mathf.Min(min.x, position.x);
                min.y = Mathf.Min(min.y, position.y);
                min.z = Mathf.Min(min.z, position.z);

                max.x = Mathf.Max(max.x, position.x);
                max.y = Mathf.Max(max.y, position.y);
                max.z = Mathf.Max(max.z, position.z);
            }

            var boundsCenter = (max + min) * 0.5f;
            var boundsSize	 = (max - min);

            if (float.IsInfinity(boundsSize.x) || float.IsInfinity(boundsSize.y) || float.IsInfinity(boundsSize.z) ||
                float.IsNaN(boundsSize.x) || float.IsNaN(boundsSize.y) || float.IsNaN(boundsSize.z))
                return null;

            generatedMesh.bounds = new Bounds(boundsCenter, boundsSize);
            return generatedMesh;
        }


        static bool GenerateVertexBuffers(SubMeshCounts subMeshCount, GeneratedMeshContents generatedMesh)
        {
            var submeshVertexCount	= subMeshCount.vertexCount;
            var submeshIndexCount	= subMeshCount.indexCount;
            var subMeshSurfaces		= subMeshCount.surfaces;

            if (subMeshSurfaces == null ||
                submeshVertexCount != generatedMesh.positions.Length ||
                submeshIndexCount  != generatedMesh.indices.Length ||
                generatedMesh.indices == null ||
                generatedMesh.positions == null)
                return false;

            bool needTangents		= generatedMesh.tangents != null && (((Int32)subMeshCount.meshQuery.UsedVertexChannels & (Int32)VertexChannelFlags.Tangent) != 0);
            bool needNormals		= generatedMesh.normals  != null && (((Int32)subMeshCount.meshQuery.UsedVertexChannels & (Int32)VertexChannelFlags.Normal) != 0);
            bool needUV0s			= generatedMesh.uv0      != null && (((Int32)subMeshCount.meshQuery.UsedVertexChannels & (Int32)VertexChannelFlags.UV0) != 0);
            bool needTempNormals	= needTangents && !needNormals;
            bool needTempUV0		= needTangents && !needUV0s;

            var normals	= !needTempNormals ? generatedMesh.normals : new Vector3[submeshVertexCount];
            var uv0s	= !needTempUV0     ? generatedMesh.uv0     : new Vector2[submeshVertexCount];

            // double snap_size = 1.0 / ants.SnapDistance();

            // copy all the vertices & indices to the sub-meshes for each material
            for (int surfaceIndex = 0, indexOffset = 0, vertexOffset = 0, surfaceCount = (int)subMeshSurfaces.Count;
                 surfaceIndex < surfaceCount;
                 ++surfaceIndex)
            {
                var sourceBuffer = subMeshSurfaces[surfaceIndex];
                if (sourceBuffer.indices == null ||
                    sourceBuffer.vertices == null ||
                    sourceBuffer.indices.Length == 0 ||
                    sourceBuffer.vertices.Length == 0)
                    continue;
                for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i++)
                {
                    generatedMesh.indices[indexOffset] = (int)(sourceBuffer.indices[i] + vertexOffset);
                    indexOffset++;
                }

                var sourceVertexCount = sourceBuffer.vertices.Length;
                Array.Copy(sourceBuffer.vertices, 0, generatedMesh.positions, vertexOffset, sourceVertexCount);

                if (needUV0s    || needTangents) Array.Copy(sourceBuffer.uv0,     0, uv0s,    vertexOffset, sourceVertexCount);
                if (needNormals || needTangents) Array.Copy(sourceBuffer.normals, 0, normals, vertexOffset, sourceVertexCount);
                vertexOffset += sourceVertexCount;
            }

            if (needTangents)
            {
                ComputeTangents(generatedMesh.indices,
                                generatedMesh.positions,
                                uv0s,
                                normals,
                                generatedMesh.tangents);
            }
            return true;
        }
        

        static void ComputeTangents(int[]		meshIndices,
                                    Vector3[]	positions,
                                    Vector2[]	uvs,
                                    Vector3[]	normals,
                                    Vector4[]	tangents) 
        {
            if (meshIndices == null ||
                positions == null ||
                uvs == null ||
                tangents == null ||
                meshIndices.Length == 0 ||
                positions.Length == 0)
                return;

            var tangentU = new Vector3[positions.Length];
            var tangentV = new Vector3[positions.Length];

            for (int i = 0; i < meshIndices.Length; i+=3) 
            {
                int i0 = meshIndices[i + 0];
                int i1 = meshIndices[i + 1];
                int i2 = meshIndices[i + 2];

                var v1 = positions[i0];
                var v2 = positions[i1];
                var v3 = positions[i2];
        
                var w1 = uvs[i0];
                var w2 = uvs[i1];
                var w3 = uvs[i2];

                var edge1 = v2 - v1;
                var edge2 = v3 - v1;

                var uv1 = w2 - w1;
                var uv2 = w3 - w1;
        
                var r = 1.0f / (uv1.x * uv2.y - uv1.y * uv2.x);
                if (float.IsNaN(r) || float.IsInfinity(r))
                    r = 0.0f;

                var udir = new Vector3(
                    ((edge1.x * uv2.y) - (edge2.x * uv1.y)) * r,
                    ((edge1.y * uv2.y) - (edge2.y * uv1.y)) * r,
                    ((edge1.z * uv2.y) - (edge2.z * uv1.y)) * r
                );

                var vdir = new Vector3(
                    ((edge1.x * uv2.x) - (edge2.x * uv1.x)) * r,
                    ((edge1.y * uv2.x) - (edge2.y * uv1.x)) * r,
                    ((edge1.z * uv2.x) - (edge2.z * uv1.x)) * r
                );

                tangentU[i0] += udir;
                tangentU[i1] += udir;
                tangentU[i2] += udir;

                tangentV[i0] += vdir;
                tangentV[i1] += vdir;
                tangentV[i2] += vdir;
            }

            for (int i = 0; i < positions.Length; i++) 
            {
                var n	= normals[i];
                var t0	= tangentU[i];
                var t1	= tangentV[i];

                var t = t0 - (n * Vector3.Dot(n, t0));
                t.Normalize();

                var c = Vector3.Cross(n, t0);
                float w = (Vector3.Dot(c, t1) < 0) ? 1.0f : -1.0f;
                tangents[i] = new Vector4(t.x, t.y, t.z, w);
                normals[i] = n;
            }
        }

#endif
    }
}