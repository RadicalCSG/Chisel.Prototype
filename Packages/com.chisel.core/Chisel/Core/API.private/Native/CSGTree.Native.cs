using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3	= UnityEngine.Vector3;
using Vector4	= UnityEngine.Vector4;
using Vector2	= UnityEngine.Vector2;
using Bounds	= UnityEngine.Bounds;
using Plane		= UnityEngine.Plane;
using Debug		= UnityEngine.Debug;

namespace Chisel.Core
{
    partial struct CSGTree
    {
#if !USE_INTERNAL_IMPLEMENTATION
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool	GenerateTree(Int32 userID, out Int32	generatedTreeNodeID);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern Int32	GetNumberOfBrushesInTree(Int32 nodeID);
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern bool	DoesTreeContainBrush(Int32 nodeID, Int32 brushID);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)] private static extern Int32	FindTreeByUserID(Int32 userID);

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GenerateMeshDescriptions(Int32				treeNodeID, 
                                                            Int32				meshTypeCount,
                                                            [In]IntPtr			meshTypes,
                                                            VertexChannelFlags	vertexChannelMask,
                                                            [Out]out Int32		meshDescriptionCount);

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GetMeshDescriptions(Int32 treeNodeID, Int32 meshDescriptionCount, [Out] IntPtr meshDescriptions);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool RayCastMultiGet(int objectCount, [Out] IntPtr outputBrushIntersection);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern Int32 RayCastIntoTreeMultiCount(Int32					treeNodeID,
            
                                                              // TODO: clean this up and be consistent w/ matrices
                                                              [In] ref Vector3		worldRayStart,
                                                              [In] ref Vector3		worldRayEnd,
                                                              [In] ref Matrix4x4	treeNodeLocalToWorldMatrix,

                                                              // TODO: clean this up and make it use meshQueries
                                                              int					filterLayerParameter0,
                                                              bool					ignoreInvisiblePolygons,
                                                              [In] IntPtr			ignoreNodeIDs,
                                                              Int32					ignoreNodeIDCount);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int FindNodesInFrustum(Int32 treeNodeID, Int32 planeCount, [In] IntPtr planes);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool RetrieveUserIDsInFrustum(Int32 userIDCount, [Out] IntPtr userIDs);

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool RetrieveNodesInFrustum(Int32 treeNodeCount, [Out] IntPtr treeNodes);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GetGeneratedMesh(Int32			treeNodeID, 
                                                    Int32			meshIndex,
                                                    Int32			subMeshIndex,

                                                    Int32			indexCount,
                                                    [Out] IntPtr	indices,

                                                    Int32			vertexCount,
                                                    [Out] IntPtr	positions,
                                                    [Out] IntPtr	tangents,
                                                    [Out] IntPtr	normals,
                                                    [Out] IntPtr	uvs,

                                                    out Vector3		boundsCenter,
                                                    out Vector3		boundsSize);



        private static GeneratedMeshDescription[] GetMeshDescriptions(Int32				 treeNodeID,
                                                                      MeshQuery[]		 meshQueries,
                                                                      VertexChannelFlags vertexChannelMask)
        {
            if (meshQueries == null)
                throw new ArgumentNullException("meshTypes");

            if (meshQueries.Length == 0)
                return null;

            Int32 meshDescriptionCount;
        
            var nativeMeshTypeHandle	= GCHandle.Alloc(meshQueries, GCHandleType.Pinned);
            var nativeMeshTypePtr		= nativeMeshTypeHandle.AddrOfPinnedObject();
            
            var result = GenerateMeshDescriptions(treeNodeID, meshQueries.Length, nativeMeshTypePtr, vertexChannelMask, out meshDescriptionCount);
            
            nativeMeshTypeHandle.Free();
            if (!result || meshDescriptionCount == 0)
                return null;
            
            var meshDescriptions        = new GeneratedMeshDescription[meshDescriptionCount];
            var meshDescriptionsHandle	= GCHandle.Alloc(meshDescriptions, GCHandleType.Pinned);
            var meshDescriptionsPtr		= meshDescriptionsHandle.AddrOfPinnedObject();
            
            result = GetMeshDescriptions(treeNodeID, meshDescriptionCount, meshDescriptionsPtr);
            
            meshDescriptionsHandle.Free();

            if (!result ||
                meshDescriptions[0].vertexCount <= 0 || 
                meshDescriptions[0].indexCount <= 0)
                return null;

            return meshDescriptions;
        }
        
        private static GeneratedMeshContents GetGeneratedMesh(int treeNodeID, GeneratedMeshDescription meshDescription, GeneratedMeshContents previousGeneratedMeshContents)
        {
            if (meshDescription.vertexCount <= 0 || 
                meshDescription.indexCount <= 0)
            {
                Debug.LogWarning(string.Format("{0} called with a {1} that isn't valid", typeof(CSGTree).Name, typeof(GeneratedMeshDescription).Name));
                return null;
            }
            
            var generatedMesh		= (previousGeneratedMeshContents != null) ? previousGeneratedMeshContents : new GeneratedMeshContents();
            var usedVertexChannels	= meshDescription.meshQuery.UsedVertexChannels;
            var vertexCount			= meshDescription.vertexCount;
            var indexCount			= meshDescription.indexCount;
            var meshIndex			= meshDescription.meshQueryIndex;
            var subMeshIndex		= meshDescription.subMeshQueryIndex;
            generatedMesh.description	= meshDescription;
            
            // create our arrays on the managed side with the correct size
            generatedMesh.tangents		= ((usedVertexChannels & VertexChannelFlags.Tangent) == 0) ? null : (generatedMesh.tangents != null && generatedMesh.tangents.Length == vertexCount) ? generatedMesh.tangents : new Vector4[vertexCount];
            generatedMesh.normals		= ((usedVertexChannels & VertexChannelFlags.Normal ) == 0) ? null : (generatedMesh.normals  != null && generatedMesh.normals .Length == vertexCount) ? generatedMesh.normals  : new Vector3[vertexCount];
            generatedMesh.uv0			= ((usedVertexChannels & VertexChannelFlags.UV0    ) == 0) ? null : (generatedMesh.uv0      != null && generatedMesh.uv0     .Length == vertexCount) ? generatedMesh.uv0      : new Vector2[vertexCount];
            generatedMesh.positions		= (generatedMesh.positions != null && generatedMesh.positions .Length == vertexCount) ? generatedMesh.positions : new Vector3[vertexCount];
            generatedMesh.indices		= (generatedMesh.indices   != null && generatedMesh.indices   .Length == indexCount ) ? generatedMesh.indices   : new int    [indexCount ];
            
            var indicesHandle	= GCHandle.Alloc(generatedMesh.indices,  GCHandleType.Pinned);
            var positionHandle	= GCHandle.Alloc(generatedMesh.positions, GCHandleType.Pinned);
            var tangentHandle	= new GCHandle();
            var normalHandle	= new GCHandle();
            var uv0Handle		= new GCHandle();
            
            var indicesPtr		= indicesHandle.AddrOfPinnedObject();
            var positionPtr		= positionHandle.AddrOfPinnedObject();
            var tangentPtr		= IntPtr.Zero;
            var normalPtr		= IntPtr.Zero;
            var uv0Ptr			= IntPtr.Zero;

            if (generatedMesh.tangents	!= null) { tangentHandle = GCHandle.Alloc(generatedMesh.tangents,	GCHandleType.Pinned); tangentPtr  = tangentHandle.AddrOfPinnedObject(); }
            if (generatedMesh.normals	!= null) { normalHandle	 = GCHandle.Alloc(generatedMesh.normals,	GCHandleType.Pinned); normalPtr   = normalHandle.AddrOfPinnedObject(); }
            if (generatedMesh.uv0		!= null) { uv0Handle	 = GCHandle.Alloc(generatedMesh.uv0,		GCHandleType.Pinned); uv0Ptr	  = uv0Handle.AddrOfPinnedObject(); }
            
            var boundsCenter	= Vector3.zero;
            var boundsSize		= Vector3.zero;
            var result = GetGeneratedMesh((Int32)treeNodeID,
                                          (Int32)meshIndex,
                                          (Int32)subMeshIndex,

                                          (Int32)indexCount,
                                          indicesPtr,

                                          (Int32)vertexCount,
                                          positionPtr,
                                          tangentPtr,
                                          normalPtr,
                                          uv0Ptr,
                                          out boundsCenter,
                                          out boundsSize);
            
            if (generatedMesh.uv0		!= null) { uv0Handle	 .Free(); }
            if (generatedMesh.normals	!= null) { normalHandle	 .Free(); }
            if (generatedMesh.tangents	!= null) { tangentHandle .Free(); }
            positionHandle.Free(); 
            indicesHandle.Free();
            
            if (!result ||
                float.IsInfinity(boundsSize.x) || float.IsInfinity(boundsSize.y) || float.IsInfinity(boundsSize.z) ||
                float.IsNaN(boundsSize.x) || float.IsNaN(boundsSize.y) || float.IsNaN(boundsSize.z))
                return null;

            generatedMesh.bounds = new Bounds(boundsCenter, boundsSize);
            return generatedMesh;
        }
        
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int		CountOfBrushesInTree			{ get { return GetNumberOfBrushesInTree(treeNodeID); } }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool		IsInTree(CSGTreeBrush brush)	{ return DoesTreeContainBrush(treeNodeID, brush.NodeID); }

        
        private bool RayCastMulti(MeshQuery[]						meshQuery, // TODO: add meshquery support here
                                  Vector3							worldRayStart,
                                  Vector3							worldRayEnd,
                                  int								filterLayerParameter0,
                                  out CSGTreeBrushIntersection[]	intersections,
                                  CSGTreeNode[]						ignoreNodes = null)
        {
            intersections = null;
            Int32 intersectionCount = 0;

            var ignoreNodeIDsHandle		= (ignoreNodes != null) ? GCHandle.Alloc(ignoreNodes, GCHandleType.Pinned) : new GCHandle();
            { 
                var ignoreNodeIDsPtr	= (ignoreNodes != null) ? ignoreNodeIDsHandle.AddrOfPinnedObject() : IntPtr.Zero;

                // TODO: remove this ...
                Matrix4x4 treeLocalToWorldMatrix = Matrix4x4.identity;

                intersectionCount		= RayCastIntoTreeMultiCount(treeNodeID, 
                                                                    ref worldRayStart,
                                                                    ref worldRayEnd,
                                                                    ref treeLocalToWorldMatrix,
                                                                    filterLayerParameter0,// TODO: implement this properly with MeshQuery
                                                                    true, // TODO: implement this properly with MeshQuery
                                                                    ignoreNodeIDsPtr,
                                                                    (ignoreNodes == null) ? 0 : ignoreNodes.Length);
            }
            if (ignoreNodeIDsHandle.IsAllocated)
                ignoreNodeIDsHandle.Free();
            
            if (intersectionCount > 0)
            {			
                var outputIntersections			= new CSGTreeBrushIntersection[intersectionCount];				
                var outputIntersectionsHandle	= GCHandle.Alloc(outputIntersections, GCHandleType.Pinned);
                {
                    var outputIntersectionsPtr	= outputIntersectionsHandle.AddrOfPinnedObject();
                    var result = RayCastMultiGet(intersectionCount, outputIntersectionsPtr);
                    if (result) intersections = outputIntersections;
                    else        intersections = null;
                }
                outputIntersectionsHandle.Free();
            }			
            return intersections != null;
        }
        
        private bool GetNodesInFrustum(Plane[]			 planes, 
                                       out CSGTreeNode[] nodes)
        {
            nodes = null;

            if (planes == null ||
                planes.Length != 6)
            {
                return false;
            }

            var planesHandle	= GCHandle.Alloc(planes, GCHandleType.Pinned);
            var planesPtr		= planesHandle.AddrOfPinnedObject();
            var itemCount		= FindNodesInFrustum(treeNodeID, planes.Length, planesPtr);
            planesHandle.Free();
            if (itemCount > 0)
            { 
                nodes			= new CSGTreeNode[itemCount];
                var nodesHandle	= GCHandle.Alloc(nodes, GCHandleType.Pinned);
                var nodesPtr	= nodesHandle.AddrOfPinnedObject();
                var result		= RetrieveNodesInFrustum(nodes.Length, nodesPtr);
                nodesHandle.Free();
                if (!result) nodes = null;
            }
            return nodes != null;
        }
        
        private bool GetUserIDsInFrustum(Plane[]	 planes, 
                                         out Int32[] userIDs)
        {
            userIDs = null;

            if (planes == null ||
                planes.Length != 6)
            {
                return false;
            }

            var planesHandle	= GCHandle.Alloc(planes, GCHandleType.Pinned);
            var planesPtr		= planesHandle.AddrOfPinnedObject();
            var itemCount		= FindNodesInFrustum(treeNodeID, planes.Length, planesPtr);
            planesHandle.Free();
            if (itemCount > 0)
            { 
                userIDs			= new Int32[itemCount];
                var idsHandle	= GCHandle.Alloc(userIDs, GCHandleType.Pinned);
                var idsPtr		= idsHandle.AddrOfPinnedObject();
                var result		= RetrieveUserIDsInFrustum(userIDs.Length, idsPtr);
                idsHandle.Free();
                if (!result) userIDs = null;
            }
            return userIDs != null;
        }
#endif
    }
}