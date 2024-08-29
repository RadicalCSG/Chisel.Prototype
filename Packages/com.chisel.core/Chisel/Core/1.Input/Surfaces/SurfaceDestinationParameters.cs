using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Chisel.Core
{
	/// <summary>Define which layers of <see cref="Chisel.Core.BrushMesh.Polygon"/>s of <seealso cref="Chisel.Core.BrushMesh"/>es that should be combined to create meshes.</summary>
	/// <remarks>
	/// <note>
	/// The CSG process has no concept of, for instance, <see cref="Chisel.Core.SurfaceDestinationFlags.Renderable"/> or <see cref="Chisel.Core.SurfaceDestinationFlags.Collidable"/> 
	/// flags and just compares the bits set on the <see cref="SurfaceDestinationFlags"/> of the <see cref="Chisel.Core.BrushMesh.Polygon"/>s with the bits set in the <see cref="Chisel.Core.MeshQuery"/>.
	/// </note>
	/// <note>
	/// Only bits 0-23 can be used for layers, the 24th bit is used to find <see cref="Chisel.Core.SurfaceDestinationFlags.Culled"/> polygons.
	/// </note>
	/// </remarks>
	/// <seealso cref="Chisel.Core.BrushMesh"/>
	/// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
	/// <seealso cref="Chisel.Core.MeshQuery"/>
	[Serializable, Flags] 
    public enum SurfaceDestinationFlags : int // 24 bits max
    {
        /// <summary>No layers, can be used to find <see cref="Chisel.Core.BrushMesh.Polygon"/>s that are not assigned to any layers.</summary>
        /// <remarks>Can be used to find all the polygons that have been set to be 'hidden'.</remarks>
        None						= 0,
        
        /// <summary>Find the polygons that: are visible</summary>
        Renderable					= (int)((uint)1 <<  0),

        /// <summary>Find the polygons that: cast shadows</summary>
        CastShadows					= (int)((uint)1 <<  1),

        /// <summary>Find the polygons that: receive shadows</summary>
        ReceiveShadows				= (int)((uint)1 <<  2),

        /// <summary>Find the polygons that: are part of a collider</summary>
        Collidable					= (int)((uint)1 <<  3),

        /// <summary>Find the polygons that: are visible and cast shadows.</summary>
        RenderCastShadows			= Renderable | CastShadows,

        /// <summary>Find the polygons that: are visible and receive shadows.</summary>
        RenderReceiveShadows		= Renderable | ReceiveShadows,

        /// <summary>
        /// Find the polygons that: are visible, cast shadows and receive shadows.
        /// </summary>
        RenderReceiveCastShadows	= Renderable | CastShadows | ReceiveShadows,

        /// <summary>
        /// Find the polygons that: are visible, cast shadows and receive shadows. and generate colliders
        /// </summary>
        Default         			= RenderReceiveCastShadows | Collidable,

		
		/// <summary>
		/// Find the polygons that: are visible, double sided, cast shadows and receive shadows. and generate colliders
		/// </summary>
		All							= Renderable | CastShadows | ReceiveShadows | Collidable,

        /// <summary>Find polygons that have been removed by the CSG process, this can be used for debugging.</summary>
        Culled						= (int)((uint)1 << 23)
    };

	/// <summary>Index into one of the parameters of <seealso cref="SurfaceDestinationParameters"/></summary>
	/// <remarks>Used to generate a mesh, by querying for a specific surface layer parameter index.
	/// For example, the first layer parameter could be an index to a specific Material to render, 
	/// which could be used by all surface types that are renderable.
	/// <note>The number of parameters is the same as in <see cref="Chisel.Core.SurfaceDestinationParameters"/>.</note>
	/// </remarks>
	/// <seealso cref="Chisel.Core.BrushMesh"/>
	/// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
	/// <seealso cref="Chisel.Core.SurfaceDestinationParameters"/>
	[Serializable]
    public enum SurfaceParameterIndex : byte
    {
        /// <summary>No parameter index is used</summary>
        None				= 0,

        /// <summary>Find polygons and create a mesh for each unique <see cref="Chisel.Core.SurfaceDestinationParameters.parameter1"/>.</summary>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters.parameter1"/>.
        Parameter1 = 1,

        /// <summary>Find polygons and create a mesh for each unique <see cref="Chisel.Core.SurfaceDestinationParameters.parameter2"/>.</summary>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters.parameter2"/>.
        Parameter2 = 2,


        // Human readable versions of the above categories


        /// <summary>Find polygons and create a mesh for each unique Material</summary>
        /// <remarks>alias of <see cref="Chisel.Core.SurfaceParameterIndex.Parameter1"/>.</remarks>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters.parameter1"/>.
        RenderMaterial = Parameter1,

        /// <summary>Find polygons and create a mesh for each unique PhysicMaterial</summary>
        /// <remarks>alias of <see cref="Chisel.Core.SurfaceParameterIndex.Parameter2"/>.</remarks>
        /// <seealso cref="Chisel.Core.SurfaceDestinationParameters.parameter2"/>.
        PhysicsMaterial = Parameter2,

        
        MaxParameterIndex = Parameter2
    };

	/// <summary>This struct describes what layers a surface is part of, and user set layer indices</summary>
	/// <remarks>Setting layer indices can be used to, for example, assign things like [Material](https://docs.unity3d.com/ScriptReference/Material.html)s and [PhysicMaterial](https://docs.unity3d.com/ScriptReference/PhysicMaterial.html)s to a surface.
	/// Currently only 3 layer indices are supported, more might be added in the future.
	/// See the [Create Unity Meshes](~/documentation/createUnityMesh.md) article for more information.
	/// <note>The number of parameters is the same as in <see cref="Chisel.Core.SurfaceParameterIndex"/>.</note>
	/// </remarks>
	/// <seealso cref="Chisel.Core.BrushMesh"/>
	/// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
	/// <seealso cref="Chisel.Core.MeshQuery"/>
	/// <seealso cref="Chisel.Core.SurfaceDestinationFlags"/>
	/// <seealso cref="Chisel.Core.SurfaceParameterIndex"/>
	[Serializable, StructLayout(LayoutKind.Sequential)]
	public struct SurfaceDestinationParameters
    {
        public const int ParameterCount = 2;
        public static readonly SurfaceDestinationFlags[] kSurfaceDestinationParameterFlagMask = new[]
        {
            SurfaceDestinationFlags.Renderable,
            SurfaceDestinationFlags.Collidable
        };
        public const int kRenderableLayer = 0;
        public const int kColliderLayer = 1;

        public static readonly SurfaceDestinationParameters Empty = new() { parameters = int2.zero };


		/// <value>First layer-parameter.</value>
		/// <remarks>Could be, for instance, an instanceID to a [Material](https://docs.unity3d.com/ScriptReference/Material.html), which can then be found using [EditorUtility.InstanceIDToObject](https://docs.unity3d.com/ScriptReference/EditorUtility.InstanceIDToObject.html)
		/// A value of 0 means that it's not set.
		/// <code>
		///	mySurfaceLayer.<paramref name="parameter1"/> = myMaterial.GetInstanceID();
		///	... generate your mesh ...
		///	Material myMaterial = EditorUtility.InstanceIDToObject(myGeneratedMeshContents.surfaceParameter);
		/// </code>
		/// </remarks>
		/// <seealso cref="Chisel.Core.SurfaceParameterIndex.Parameter1"/>.
		public Int32			parameter1 { get { return parameters[kRenderableLayer]; } set { parameters[kRenderableLayer] = value; } }

		/// <value>Second layer-parameter.</value>
		/// <remarks>Could be, for instance, an instanceID to a [PhysicMaterial](https://docs.unity3d.com/ScriptReference/PhysicMaterial.html), which can then be found using [EditorUtility.InstanceIDToObject](https://docs.unity3d.com/ScriptReference/EditorUtility.InstanceIDToObject.html)
		/// A value of 0 means that it's not set.
		/// <code>
		///	mySurfaceLayer.<paramref name="parameter2"/> = myPhysicMaterial.GetInstanceID();
		///	... generate your mesh ...
		///	PhysicMaterial myMaterial = EditorUtility.InstanceIDToObject(myGeneratedMeshContents.surfaceParameter);
		/// </code>
		/// </remarks>
		/// <seealso cref="Chisel.Core.SurfaceParameterIndex.Parameter2"/>.
		public Int32			parameter2 { get { return parameters[kColliderLayer]; } set { parameters[kColliderLayer] = value; } }

        // .. this could be extended in the future, when necessary
        public int2             parameters;
    }
}
