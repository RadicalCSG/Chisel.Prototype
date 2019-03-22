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
		public BrushMesh() { }
		public BrushMesh(BrushMesh other)
		{
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
			if (other.polygons != null)
			{
				polygons = new Polygon[other.polygons.Length];
				Array.Copy(other.polygons, this.polygons, other.polygons.Length);
			}
		}

		/// <summary>Defines the polygon of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
		/// <seealso cref="Chisel.Core.BrushMesh"/>
		[Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct Polygon
		{
			/// <value>The index to the first half edge that forms this <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
			public Int32 firstEdge;
			
			/// <value>The number or edges of this <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
			public Int32 edgeCount;
			
			/// <value>An ID that can be used to identify the <see cref="Chisel.Core.BrushMesh.Polygon"/>.</value>
			public Int32 surfaceID; // TODO: replace with surfaceID (leading to SurfaceAsset uniqueID) and polygonIndex on
			
			/// <value>Describes how normals and texture coordinates are created.</value>
			public SurfaceDescription description;

			/// <value>Describes the surface layers that this <see cref="Chisel.Core.BrushMesh.Polygon"/> is part of, and, for example, what Materials it uses.</value>
			/// <seealso cref="Chisel.Core.MeshQuery"/>
			public SurfaceLayers layers;
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

#if USE_MANAGED_CSG_IMPLEMENTATION
            // TODO: add description
            public Int32 polygonIndex;

            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString() { return string.Format("{{ twinIndex = {0}, vertexIndex = {1}, surfaceIndex = {2} }}", twinIndex, vertexIndex, polygonIndex); }
#else
			[EditorBrowsable(EditorBrowsableState.Never)]
			public override string ToString() { return string.Format("{{ twinIndex = {0}, vertexIndex = {1} }}", twinIndex, vertexIndex); }
#endif
		}

#if USE_MANAGED_CSG_IMPLEMENTATION
        // TODO: add descriptions
        [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct Surface
        {
            public Surface(Plane plane) { this.plane = plane; }
            public Plane plane;
        }
        
		// TODO: add description
		public Surface[]	surfaces;
		
		// TODO: add description
		public Bounds		localBounds;

		// TODO: add description
		public int[][]		surfacesAroundVertex;

#endif

        /// <value>The vertices of this <see cref="Chisel.Core.BrushMesh"/>.</value> 
        public Vector3[]	vertices;

		/// <value>An array of <see cref="Chisel.Core.BrushMesh.HalfEdge"/> that define the edges of a <see cref="Chisel.Core.BrushMesh"/>.</value>
		public HalfEdge[]	halfEdges;
		
		/// <value>An array of <see cref="Chisel.Core.BrushMesh.Polygon"/> that define the polygons of a <see cref="Chisel.Core.BrushMesh"/>.</value>
		public Polygon[]	polygons;
	}
}