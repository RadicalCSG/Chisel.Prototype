﻿using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct GeneratedSubMesh
    {
        public int baseIndex;
        public int indexCount;

        public int baseVertex;
        public int vertexCount;

        public AABB bounds;
    }

    /// <summary>Stores a mesh generated by calling <see cref="Chisel.Core.CSGTree.GetGeneratedMesh" />.</summary>
    /// <remarks>See the [Create Unity Meshes](~/documentation/createUnityMesh.md) article for more information. <see cref="Chisel.Core.GeneratedMeshContents"/> holds the binary mesh data from the managed side and can be turned into a [UnityEngine.Mesh](https://docs.unity3d.com/ScriptReference/Mesh.html) by calling <see cref="Chisel.Core.GeneratedMeshContents.CopyTo" />.
    /// See the [Create Unity Meshes](~/documentation/createUnityMesh.md) article for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTree" /><seealso cref="Chisel.Core.CSGTree.GetGeneratedMesh" />
    /// <seealso cref="Chisel.Core.GeneratedMeshDescription"/><seealso cref="Chisel.Core.SurfaceDescription"/>
    /// <seealso href="https://docs.unity3d.com/ScriptReference/Mesh.html">UnityEngine.Mesh</seealso>
    public struct GeneratedMeshContents// : IDisposable
    {
        /// <value>Number of indices in mesh.</value>
        public int           		    indexCount      { get { return indices.IsCreated ? indices.Length : 0; } }
        
        /// <value>Number of vertices in mesh.</value>
        public int           		    vertexCount     { get { return positions.IsCreated ? positions.Length : 0; } }

        public NativeList<GeneratedSubMesh> subMeshes;

        /// <value>Triplet indices to the vertices that make up the triangles in this mesh.</value>
        public NativeList<int> 		    indices;

        /// <value>A brush index per triangle.</value>
        public NativeList<int> 		    brushIndices;
        
        /// <value>Position for each vertex.</value>
        public NativeList<float3>	    positions;        

        /// <value>Tangent for each vertex.</value>
        /// <remarks><note>Can be null when the <see cref="description"/> has no tangents set in its <see cref="Chisel.Core.MeshQuery.UsedVertexChannels"/>.</note></remarks>
        public NativeList<float4>       tangents;
        
        /// <value>Normal for each vertex.</value>
        /// <remarks>Each <seealso cref="Chisel.Core.BrushMesh.Polygon"/> has a <seealso cref="Chisel.Core.SurfaceDescription.smoothingGroup"/> field in its <seealso cref="Chisel.Core.SurfaceDescription"/>.
        /// If the <seealso cref="Chisel.Core.SurfaceDescription.smoothingGroup"/> is set to something other than 0 the normal is calculated by; combining the normals of all the surfaces around each vertex that share the same <seealso cref="Chisel.Core.SurfaceDescription.smoothingGroup"/>.
        /// If the <seealso cref="Chisel.Core.SurfaceDescription.smoothingGroup"/> is set to 0 the normal of the polygon is used.
        /// <note>Can be null when the <see cref="description"/>  has no normals set in its <see cref="Chisel.Core.MeshQuery.UsedVertexChannels"/>.</note>
        /// </remarks>
        public NativeList<float3>       normals;

        /// <value>First uv channel for each vertex.</value>
        /// <remarks>These are created by multiplying the vertices of the <seealso cref="Chisel.Core.BrushMesh"/>, which was used to generate this geometry, 
        /// by the <seealso cref="Chisel.Core.SurfaceDescription.UV0" /> <seealso cref="Chisel.Core.UVMatrix"/> of the <seealso cref="Chisel.Core.SurfaceDescription"/> of the vertex.
        /// <note>Can be null when the <see cref="description"/> has no <seealso cref="Chisel.Core.VertexChannelFlags.UV0" /> set in its <see cref="Chisel.Core.MeshQuery.UsedVertexChannels"/>.</note>
        /// </remarks>
        public NativeList<float2>       uv0;

        public void Dispose()
        {
            if (subMeshes   .IsCreated) subMeshes.Dispose();
            if (indices     .IsCreated) indices.Dispose();
            if (brushIndices.IsCreated) brushIndices.Dispose();
            if (positions   .IsCreated) positions.Dispose();
            if (tangents    .IsCreated) tangents.Dispose();
            if (normals     .IsCreated) normals.Dispose();
            if (uv0         .IsCreated) uv0.Dispose();

            subMeshes       = default;
            indices         = default;
            brushIndices    = default;
            positions       = default;
            tangents        = default;
            normals         = default;
            uv0             = default;
        }
    };
}