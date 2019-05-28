using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>Flags that define how surfaces in a <see cref="Chisel.Core.BrushMesh"/> behave.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>	
    [Serializable, Flags]
    public enum SurfaceFlags : Int32
    {
        /// <summary>The surface has no flags set</summary>
        None = 0,

        /// <summary>When set, the surface texture coordinates are calculated in world-space instead of brush-space</summary>
        TextureIsInWorldSpace = 1
    }

    /// <summary>A 2x4 matrix to calculate the UV coordinates for the vertices of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct UVMatrix
    {
        public UVMatrix(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); }
        public UVMatrix(Vector4 u, Vector4 v) { U = u; V = v; }

        /// <value>Used to convert a vertex coordinate to a U texture coordinate</value>
        public Vector4 U;

        /// <value>Used to convert a vertex coordinate to a V texture coordinate</value>
        public Vector4 V;


        // TODO: add description
        public Matrix4x4 ToMatrix() { var W = Vector3.Cross(U, V); return new Matrix4x4(U, V, W, new Vector4(0, 0, 0, 1)).transpose; }

        // TODO: add description
        public UVMatrix Set(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); return this; }

        // TODO: add description
        public static implicit operator Matrix4x4(UVMatrix input) { return input.ToMatrix(); }
        public static implicit operator UVMatrix(Matrix4x4 input) { return new UVMatrix(input); }

        // TODO: add description
        public static readonly UVMatrix identity = new UVMatrix(new Vector4(1,0,0,0.0f), new Vector4(0,1,0,0.0f));
        public static readonly UVMatrix centered = new UVMatrix(new Vector4(1,0,0,0.5f), new Vector4(0,1,0,0.5f));
    }

    /// <summary>Describes how the texture coordinates and normals are generated and if a surface is, for example, <see cref="Chisel.Core.LayerUsageFlags.Renderable"/> and/or <see cref="Chisel.Core.LayerUsageFlags.Collidable" /> etc.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SurfaceDescription
    {
        /// <value>The current normal smoothing group, 0 means that the surface doesn't do any smoothing</value>
        /// <remarks><note>This is only used when normals are set to be generated using the <see cref="Chisel.Core.VertexChannelFlags"/>.</note></remarks>
        public UInt32           smoothingGroup;

        /// <value>Surface specific flags</value>
        [UnityEngine.HideInInspector]
        public SurfaceFlags     surfaceFlags;

        /// <value>2x4 matrix to calculate UV0 coordinates from vertex positions.</value>
        /// <remarks><note>This is only used when uv0 channels are set to be generated using the <see cref="Chisel.Core.VertexChannelFlags"/>.</note></remarks>
        public UVMatrix         UV0;


        // .. more UVMatrices can be added when more UV channels are supported

        public static readonly SurfaceDescription Default = new SurfaceDescription()
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
        public const int CurrentVersion = 1;

        public int version = 0;

        public BrushMesh() { }
        public BrushMesh(BrushMesh other)
        {
            version = other.version;
            if (other.vertices != null)
            {
                vertices = new Vector3[other.vertices.Length];
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
            if (other.surfaces != null)
            {
                surfaces = new Surface[other.surfaces.Length];
                Array.Copy(other.surfaces, this.surfaces, other.surfaces.Length);
            }
        }

        /// <summary>Defines the polygon of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
        /// <seealso cref="Chisel.Core.BrushMesh"/>
        /// <seealso cref="Chisel.Core.Surface"/>
        [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Polygon
        {
            /// <value>The index to the first half edge that forms this <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
            public Int32 firstEdge;
            
            /// <value>The number or edges of this <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
            public Int32 edgeCount;
            
            /// <value>An ID that can be used to identify the <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
            public Int32 surfaceID; // TODO: replace with surfaceID (leading to BrushMaterial uniqueID) and polygonIndex
            
            /// <value>Describes how normals and texture coordinates are created.</value>
            public SurfaceDescription description;

            /// <value>Describes the Material and PhysicMaterial that this <see cref="Chisel.Core.BrushMesh.Polygon"/> uses.</value>
            /// <seealso cref="ChiselBrushMaterial"/>
            public ChiselBrushMaterial brushMaterial;

            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString() { return string.Format("{{ firstEdge = {0}, edgeCount = {1}, surfaceID = {2} }}", firstEdge, edgeCount, surfaceID); }
        }

        /// <summary>Defines a half edge of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
        /// <seealso cref="Chisel.Core.BrushMesh"/>
        [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct HalfEdge
        {
            /// <value>The index to the vertex of this <seealso cref="Chisel.Core.BrushMesh.HalfEdge"/>.</value>
            public Int32 vertexIndex;

            /// <value>The index to the twin <seealso cref="Chisel.Core.BrushMesh.HalfEdge"/> of this <seealso cref="Chisel.Core.BrushMesh.HalfEdge"/>.</value>
            public Int32 twinIndex;

            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString() { return string.Format("{{ twinIndex = {0}, vertexIndex = {1} }}", twinIndex, vertexIndex); }
        }

        /// <summary>Defines a surface of a <see cref="Chisel.Core.BrushMesh"/>, multiple polygons may share the same surface.</summary>
        /// <seealso cref="Chisel.Core.Polygon"/>
        /// <seealso cref="Chisel.Core.BrushMesh"/>
        [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Surface
        {
            public Surface(Plane localPlane) { this.localPlane = localPlane; }
            public Plane localPlane;
        }

#if USE_MANAGED_CSG_IMPLEMENTATION
        
        /// <value>The axis aligned bounding box of this <see cref="Chisel.Core.BrushMesh"/>.</value> 
        public Bounds		localBounds;

        // TODO: add description
        public int[][]		surfacesAroundVertex;

#endif

        /// <value>The vertices of this <see cref="Chisel.Core.BrushMesh"/>.</value> 
        public Vector3[]	vertices;

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
        public Surface[]	surfaces;
    }
}