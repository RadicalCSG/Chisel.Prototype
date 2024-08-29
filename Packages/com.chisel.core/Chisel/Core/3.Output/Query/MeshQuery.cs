using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    /// <summary>Describes what surface parameters, and optionally which surface parameter, to query for in a model, to create a mesh.</summary>
    /// <remarks>See the [Create Unity Meshes](~/documentation/createUnityMesh.md) article for more information.
    /// <seealso cref="Chisel.Core.SurfaceDestinationParameters"/>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.SurfaceDestinationFlags"/>
    /// <seealso cref="Chisel.Core.SurfaceParameterIndex"/>
    /// <seealso cref="Chisel.Core.CSGTree.GetMeshDescriptions" />
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct MeshQuery
    {
        const int	BitShift	= 24;
        const uint	BitMask		= (uint)((uint)127 << BitShift);

        /// <summary>Constructs a <see cref="Chisel.Core.MeshQuery"/> to use to specify which surfaces should be combined into meshes, and should they be subdivided by a particular layer parameter index.</summary>
        /// <param name="query">Which layer combination would we like to look for and generate a mesh with.</param>
        /// <param name="mask">What layers do we ignore, and what layers do we include in our comparison. When this value is <see cref="Chisel.Core.SurfaceDestinationFlags.None"/>, <paramref name="mask"/> is set to be equal to <paramref name="query"/>.</param>
        /// <param name="parameterIndex">Which parameter index we use to, for example, differentiate between different [UnityEngine.Material](https://docs.unity3d.com/ScriptReference/Material.html)s</param>
        /// <param name="vertexChannels">Which vertex channels need to be used for the meshes we'd like to generate.</param>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters" />
        public MeshQuery(SurfaceDestinationFlags query, SurfaceDestinationFlags mask = SurfaceDestinationFlags.None, SurfaceParameterIndex parameterIndex = SurfaceParameterIndex.None, VertexChannelFlags vertexChannels = VertexChannelFlags.Position)
        {
            if (mask == SurfaceDestinationFlags.None) mask = query;
            this.layers				= ((uint)query & ~BitMask) | ((uint)parameterIndex << BitShift);
            this.maskAndChannels	= ((uint)mask  & ~BitMask) | ((uint)vertexChannels << BitShift);
        }

        private uint	layers;				// 24 bit layer-usage / 8 bit parameter-index
        private uint	maskAndChannels;    // 24 bit layer-mask  / 8 bit vertex-channels

        /// <value>Which layer combination would we like to look for and generate a mesh with</value>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters" />
        /// <seealso cref="Chisel.Core.BrushMesh.Polygon" />
        public SurfaceDestinationFlags		LayerQuery			{ readonly get { return (SurfaceDestinationFlags)((uint)layers          & ~BitMask); } set { layers          = ((uint)value & ~BitMask) | ((uint)layers          & BitMask); } }
        
        /// <value>What layers do we ignore, and what layers do we include in our comparison</value>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters" />
        /// <seealso cref="Chisel.Core.BrushMesh.Polygon" />
        public SurfaceDestinationFlags		LayerQueryMask		{ readonly get { return (SurfaceDestinationFlags)((uint)maskAndChannels & ~BitMask); } set { maskAndChannels = ((uint)value & ~BitMask) | ((uint)maskAndChannels & BitMask); } }
        
        /// <value>Which parameter index we use to, for example, differentiate between different [UnityEngine.Material](https://docs.unity3d.com/ScriptReference/Material.html)s.</value>
        /// <seealso cref="Chisel.Core.SurfaceParameterIndex" />
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters" />
        /// <seealso cref="Chisel.Core.BrushMesh.Polygon" />
        public SurfaceParameterIndex	    LayerParameterIndex  { readonly get { return (SurfaceParameterIndex)(((uint)layers          & BitMask) >> BitShift); } set { layers          = ((uint)layers          & ~BitMask) | ((uint)value << BitShift); } }
        
        /// <value>Which vertex channels need to be used for the meshes we'd like to generate</value>
        /// <seealso cref="Chisel.Core.CSGTree.GetMeshDescriptions" />
        public VertexChannelFlags	        UsedVertexChannels	 { readonly get { return (VertexChannelFlags )(((uint)maskAndChannels & BitMask) >> BitShift); } set { maskAndChannels = ((uint)maskAndChannels & ~BitMask) | ((uint)value << BitShift); } }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator == (MeshQuery left, MeshQuery right) { return left.layers == right.layers && left.maskAndChannels == right.maskAndChannels; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (MeshQuery left, MeshQuery right) { return left.layers != right.layers || left.maskAndChannels != right.maskAndChannels; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) { if (!(obj is MeshQuery)) return false; var type = (MeshQuery)obj; return layers == type.layers && maskAndChannels == type.maskAndChannels; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { var hashCode = -1385006369; hashCode = hashCode * -1521134295; hashCode = hashCode * -1521134295 + (int)layers; hashCode = hashCode * -1521134295 + (int)maskAndChannels; return hashCode; }
        #endregion


        #region ToString
        public override string ToString()
        {
            return $"(LayerQuery: {LayerQuery}, LayerQueryMask: {LayerQueryMask}, LayerParameterIndex: {LayerParameterIndex}, UsedVertexChannels: {UsedVertexChannels})";
        }
        #endregion

        public static readonly MeshQuery[] RenderOnly =
        {
            // Renderables
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.RenderReceiveCastShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.RenderCastShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.RenderReceiveShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.Renderable,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.CastShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
                
			// Helper surfaces
			new MeshQuery(query: SurfaceDestinationFlags.None,          mask: SurfaceDestinationFlags.Renderable),	// hidden surfaces
			new MeshQuery(query: SurfaceDestinationFlags.CastShadows	),
			new MeshQuery(query: SurfaceDestinationFlags.ReceiveShadows	),
			new MeshQuery(query: SurfaceDestinationFlags.Culled			)
        };

        public static readonly MeshQuery[] CollisionOnly =
        {
            // Colliders
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.PhysicsMaterial,
                query:          SurfaceDestinationFlags.Collidable,
                mask:           SurfaceDestinationFlags.Collidable,
                vertexChannels: VertexChannelFlags.Position
            ),
                
			// Helper surfaces
			new MeshQuery(query: SurfaceDestinationFlags.None,          mask: SurfaceDestinationFlags.Renderable),	// hidden surfaces
            new MeshQuery(query: SurfaceDestinationFlags.Culled         ) // removed by CSG algorithm
        };

        // TODO: do not make this hardcoded
        public static readonly MeshQuery[] DefaultQueries =
        {
            // Renderables
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.RenderReceiveCastShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.RenderCastShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.RenderReceiveShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.Renderable,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.RenderMaterial,
                query:          SurfaceDestinationFlags.CastShadows,
                mask:           SurfaceDestinationFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),

            // Colliders
            new MeshQuery(
                parameterIndex: SurfaceParameterIndex.PhysicsMaterial,
                query:          SurfaceDestinationFlags.Collidable,
                mask:           SurfaceDestinationFlags.Collidable,
                vertexChannels: VertexChannelFlags.Position
            ),
                
			// Helper surfaces
			new MeshQuery(query: SurfaceDestinationFlags.None,                  mask: SurfaceDestinationFlags.Renderable),	        // hidden surfaces
			new MeshQuery(query: SurfaceDestinationFlags.RenderCastShadows,     mask: SurfaceDestinationFlags.RenderCastShadows),
            new MeshQuery(query: SurfaceDestinationFlags.CastShadows,           mask: SurfaceDestinationFlags.RenderCastShadows),
			new MeshQuery(query: SurfaceDestinationFlags.RenderReceiveShadows,  mask: SurfaceDestinationFlags.RenderReceiveShadows),
			new MeshQuery(query: SurfaceDestinationFlags.Culled,                mask: SurfaceDestinationFlags.Culled)               // removed by CSG algorithm
        };
    }
}
 