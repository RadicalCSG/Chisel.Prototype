using System;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    // Separate struct so that we can create a property drawer for it
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct SmoothingGroup // TODO: should be part of "postprocessing" data
    {
        public UInt32 value;

        public static implicit operator uint(SmoothingGroup smoothingGroup) { return smoothingGroup.value; }
        public static implicit operator SmoothingGroup(uint smoothingGroup) { return new SmoothingGroup() { value = smoothingGroup }; }
	}


	/// <summary>Flags that define how surfaces in a <see cref="Chisel.Core.BrushMesh"/> behave.</summary>
	/// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
	/// <seealso cref="Chisel.Core.BrushMesh"/>	
	[Serializable, Flags]
    public enum SurfaceDetailFlags : byte
    {
        /// <summary>The surface has no flags set</summary>
        None = 0,

        /// <summary>When set, the surface texture coordinates are calculated in world-space instead of brush-space</summary>
        TextureIsInWorldSpace	= (int)((uint)1 << 1),

		VertexPainted			= (int)((uint)1 << 2), // subdivide surfaces to be able to vertex paint it

		Default = None
    }

	/// <summary>
    /// Describes part of a surface that is specific for each surface individually, 
    /// such as how the texture coordinates and normals are calculated, and if a surface is, 
    /// for example, <see cref="Chisel.Core.SurfaceDestinationFlags.Renderable"/> and/or 
	/// <see cref="Chisel.Core.SurfaceDestinationFlags.Collidable" /> etc.
    /// </summary>
	/// <seealso cref="Chisel.Core.SurfaceDestinationFlags"/>
	/// <seealso cref="Chisel.Core.SurfaceDetailFlags"/>
	/// <seealso cref="Chisel.Core.SmoothingGroup"/>
	/// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
	/// <seealso cref="Chisel.Core.BrushMesh"/>
	[Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SurfaceDetails
    {
        public const string kSmoothingGroupName = nameof(smoothingGroup);
		public const string kDetailFlagsName    = nameof(detailFlags);
		public const string kUV0Name            = nameof(UV0);

		/// <value>The current normal smoothing group, 0 means that the surface doesn't do any smoothing</value>
		/// <remarks><note>This is only used when normals are set to be generated using the <see cref="Chisel.Core.VertexChannelFlags"/>.</note></remarks>
		public SmoothingGroup       smoothingGroup;

        /// <value>Surface specific flags</value>
        [UnityEngine.HideInInspector]
        public SurfaceDetailFlags   detailFlags;

		/// <value>2x4 matrix to calculate UV0 coordinates from vertex positions.</value>
		/// <remarks><note>This is only used when uv0 channels are set to be generated using the <see cref="Chisel.Core.VertexChannelFlags"/>.</note></remarks>
		public UVMatrix             UV0;

        // .. more UVMatrices can be added when more UV channels are supported


        public static SurfaceDetails Default = new()
        {
            smoothingGroup = 0,
            detailFlags    = SurfaceDetailFlags.Default,
            UV0            = UVMatrix.centered
        };
	}
}