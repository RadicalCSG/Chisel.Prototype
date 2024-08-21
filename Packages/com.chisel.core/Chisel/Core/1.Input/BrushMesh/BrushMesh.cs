using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>Flags that define how surfaces in a <see cref="Chisel.Core.BrushMesh"/> behave.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>	
    [Serializable, Flags]
    public enum SurfaceFlags : byte
    {
        /// <summary>The surface has no flags set</summary>
        None = 0,

        /// <summary>When set, the surface texture coordinates are calculated in world-space instead of brush-space</summary>
        TextureIsInWorldSpace = 1
    }

    // Separate struct so that we can create a property drawer for it
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct SmoothingGroup
    {
        public UInt32 value;

        public static implicit operator uint(SmoothingGroup smoothingGroup) { return smoothingGroup.value; }
        public static implicit operator SmoothingGroup(uint smoothingGroup) { return new SmoothingGroup() { value = smoothingGroup }; }
    }

    /// <summary>Defines the surface of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
    /// <seealso cref="Chisel.Core.ChiselBrushMaterial"/>
    /// <seealso cref="Chisel.Core.SurfaceDescription"/>
    [Serializable]
    public sealed class ChiselSurface
    {
        public const string kBrushMaterialName      = nameof(brushMaterial);
        public const string kSurfaceDescriptionName = nameof(surfaceDescription);

        public ChiselBrushMaterial  brushMaterial;
        public SurfaceDescription   surfaceDescription;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = HashExtensions.Hash(ref surfaceDescription);
                hash = math.hash(new uint2(hash, (uint)brushMaterial.GetHashCode()));
                return (int)hash;
            }
        }
    }

    /// <summary>Describes how the texture coordinates and normals are generated and if a surface is, for example, <see cref="Chisel.Core.LayerUsageFlags.Renderable"/> and/or <see cref="Chisel.Core.LayerUsageFlags.Collidable" /> etc.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SurfaceDescription
    {
        public const string kSmoothingGroupName = nameof(smoothingGroup);
        public const string kSurfaceFlagsName   = nameof(surfaceFlags);
        public const string kUV0Name            = nameof(UV0);

        /// <value>The current normal smoothing group, 0 means that the surface doesn't do any smoothing</value>
        /// <remarks><note>This is only used when normals are set to be generated using the <see cref="Chisel.Core.VertexChannelFlags"/>.</note></remarks>
        public SmoothingGroup   smoothingGroup;

        /// <value>Surface specific flags</value>
        [UnityEngine.HideInInspector]
        public SurfaceFlags     surfaceFlags;

        /// <value>2x4 matrix to calculate UV0 coordinates from vertex positions.</value>
        /// <remarks><note>This is only used when uv0 channels are set to be generated using the <see cref="Chisel.Core.VertexChannelFlags"/>.</note></remarks>
        public UVMatrix         UV0;


        // .. more UVMatrices can be added when more UV channels are supported

        public static SurfaceDescription Default = new SurfaceDescription()
        {
            smoothingGroup  = 0,
            surfaceFlags    = CSGDefaults.SurfaceFlags,
            UV0             = UVMatrix.centered
        };
    }

    /// <summary>Contains a shape that can be used to initialize and update a <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
    /// <remarks>See the [Brush Meshes](~/documentation/brushMesh.md) article for more information.
    /// <note>This struct is safe to serialize.</note>
    /// <seealso cref="Chisel.Core.BrushMeshInstance"/>
    /// <seealso cref="Chisel.Core.BrushMeshInstance.Create"/>
    /// <seealso cref="Chisel.Core.BrushMeshInstance.Set"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [Serializable]
    public sealed partial class BrushMesh
    {
        const int kLatestVersion = 1;
        [HideInInspector]
        [SerializeField] int version = kLatestVersion;  // Serialization will overwrite the version number 
                                                        // new instances will have the latest version
                                                        // This allows us to detect changes and support upgrades

        public BrushMesh() { }
        public BrushMesh(BrushMesh other)
        {
            version = other.version;
            if (other.vertices != null)
            {
                vertices = new float3[other.vertices.Length];
                Array.Copy(other.vertices, this.vertices, other.vertices.Length);
            }
            if (other.halfEdges != null)
            {
                halfEdges = new HalfEdge[other.halfEdges.Length];
                Array.Copy(other.halfEdges, this.halfEdges, other.halfEdges.Length);
            }
            if (other.halfEdgePolygonIndices != null)
            {
                halfEdgePolygonIndices = new int[other.halfEdgePolygonIndices.Length];
                Array.Copy(other.halfEdgePolygonIndices, this.halfEdgePolygonIndices, other.halfEdgePolygonIndices.Length);
            }
            if (other.polygons != null)
            {
                polygons = new Polygon[other.polygons.Length];
                Array.Copy(other.polygons, this.polygons, other.polygons.Length);
            }
            if (other.planes != null)
            {
                planes = new float4[other.planes.Length];
                Array.Copy(other.planes, this.planes, other.planes.Length);
            }
        }

        /// <summary>Defines the polygon of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
        /// <seealso cref="Chisel.Core.BrushMesh"/>
        /// <seealso cref="Chisel.Core.Surface"/>
        /// <seealso cref="Chisel.Core.ChiselSurface"/>
        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct Polygon
        {
            /// <value>The index to the first half edge that forms this <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
            public Int32 firstEdge;
            
            /// <value>The number or edges of this <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
            public Int32 edgeCount;
            
            public Int32 descriptionIndex;

            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString() { return $"{{ firstEdge = {firstEdge}, edgeCount = {edgeCount}, descriptionIndex = {descriptionIndex} }}"; }
        }

        /// <summary>Defines a half edge of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
        /// <seealso cref="Chisel.Core.BrushMesh"/>
        [Serializable, StructLayout(LayoutKind.Sequential)] 
        public struct HalfEdge
        {
            /// <value>The index to the vertex of this <seealso cref="Chisel.Core.BrushMesh.HalfEdge"/>.</value>
            public Int32 vertexIndex;

            /// <value>The index to the twin <seealso cref="Chisel.Core.BrushMesh.HalfEdge"/> of this <seealso cref="Chisel.Core.BrushMesh.HalfEdge"/>.</value>
            public Int32 twinIndex;

            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString() { return $"{{ twinIndex = {twinIndex}, vertexIndex = {vertexIndex} }}"; }
        }

        /// <value>The vertices of this <see cref="Chisel.Core.BrushMesh"/>.</value> 
        public float3[]	    vertices;

        /// <value>An array of <see cref="Chisel.Core.BrushMesh.HalfEdge"/> that define the edges of a <see cref="Chisel.Core.BrushMesh"/>.
        /// This array must be equal in length to <see cref="halfEdgePolygonIndices"/>s.</value>
        public HalfEdge[]	halfEdges;

        /// <value>An array of indices to <see cref="polygons"/>s that define which <see cref="Chisel.Core.BrushMesh.Polygon"/> each <see cref="halfEdges">halfEdge</see> belongs to.
        /// This array must be equal in length to <see cref="halfEdges"/>s.</value>
        public int[]        halfEdgePolygonIndices;

        /// <value>An array of <see cref="Chisel.Core.BrushMesh.Polygon"/> that define the polygons of a <see cref="Chisel.Core.BrushMesh"/>.
        /// This array must be equal in length to <see cref="brushMaterials"/>s.</value>
        /// <seealso cref="Chisel.Core.BrushMesh.BrushMaterial"/>
        public Polygon[]	polygons;

        /// <value>The surfaces of this <see cref="Chisel.Core.BrushMesh"/>.</value> 
        public float4[]	    planes;
    }
}