using System;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTree
    {
        private static bool	    GenerateTree(Int32 userID, out Int32 generatedTreeNodeID)
        {
            return CSGManager.GenerateTree(userID, out generatedTreeNodeID);
        }

        private static Int32	GetNumberOfBrushesInTree(Int32 nodeID)
        {
            return CSGManager.GetNumberOfBrushesInTree(nodeID);
        }

        private static bool	    DoesTreeContainBrush(Int32 nodeID, Int32 brushID)
        {
            return CSGManager.DoesTreeContainBrush(nodeID, brushID);
        }

        private static Int32	FindTreeByUserID(Int32 userID)
        {
            return CSGManager.FindTreeByUserID(userID);
        }

        private static GeneratedMeshDescription[] GetMeshDescriptions(Int32				 treeNodeID,
                                                                      MeshQuery[]		 meshQueries,
                                                                      VertexChannelFlags vertexChannelMask)
        {
            return CSGManager.GetMeshDescriptions(treeNodeID, meshQueries, vertexChannelMask);
        }

        private static GeneratedMeshContents GetGeneratedMesh(int treeNodeID, GeneratedMeshDescription meshDescription)
        {
            return CSGManager.GetGeneratedMesh(treeNodeID, meshDescription);
        }
        
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int		CountOfBrushesInTree			{ get { return GetNumberOfBrushesInTree(treeNodeID); } }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool		IsInTree(CSGTreeBrush brush)	{ return DoesTreeContainBrush(treeNodeID, brush.NodeID); }


        private bool RayCastMulti(MeshQuery[]						meshQuery,
                                  Vector3							worldRayStart,
                                  Vector3							worldRayEnd,
                                  Matrix4x4                         treeLocalToWorldMatrix,
                                  int								filterLayerParameter0,
                                  out CSGTreeBrushIntersection[]	intersections,
                                  CSGTreeNode[]						ignoreNodes = null)
        {
            return CSGManager.RayCastMulti(this, meshQuery, worldRayStart, worldRayEnd, treeLocalToWorldMatrix, filterLayerParameter0, out intersections, ignoreNodes);
        }

        private bool GetNodesInFrustum(MeshQuery[]       meshQuery,
                                       Plane[]			 planes, 
                                       out CSGTreeNode[] nodes)
        {
            return CSGManager.GetNodesInFrustum(meshQuery, planes, out nodes);

        }
    }
}