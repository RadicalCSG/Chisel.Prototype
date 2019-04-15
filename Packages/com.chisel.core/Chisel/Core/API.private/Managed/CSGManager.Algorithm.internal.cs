using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
    static partial class CSGManager
    {
#if USE_INTERNAL_IMPLEMENTATION
        // TODO: review flags, might not make sense any more
        enum NodeStatusFlags : UInt16
        {
            None						= 0,
//			NeedChildUpdate				= 1,
            NeedPreviousSiblingsUpdate	= 2,

            TreeIsDisabled				= 1024,// TODO: remove, or make more useful
            OperationNeedsUpdate		= 4,
            TreeNeedsUpdate				= 8,
            TreeMeshNeedsUpdate			= 16,
            
            NeedBaseMeshUpdate			= 32,	// -> leads to NeedsMeshReset
            NeedMeshReset				= 64,	
            NeedOutlineUpdate			= 128,
            NeedAllTouchingUpdated		= 256,	// all brushes that touch this brush need to be updated,
            NeedFullUpdate				= NeedBaseMeshUpdate | NeedMeshReset | NeedOutlineUpdate | NeedAllTouchingUpdated,
            NeedCSGUpdate				= NeedBaseMeshUpdate | NeedMeshReset | NeedAllTouchingUpdated,
            NeedUpdate					= NeedMeshReset | NeedOutlineUpdate | NeedAllTouchingUpdated,
            NeedUpdateDirectOnly		= NeedMeshReset | NeedOutlineUpdate,
        };


        // TODO: not really CSG yet .. just a hack
        // TODO: move somewhere else
        internal static LoopList PerformCSG(int brushNodeID, bool mode)
        {
            var brushNodeIndex	= brushNodeID - 1;
            var output			= CSGManager.GetBrushOutput(brushNodeID);
            var outputLoops		= output.brushOutputLoops;
            var brushInstance	= CSGManager.GetBrushMeshID(brushNodeID);
            var surfaceLoops	= (outputLoops.intersectionLoops == null || outputLoops.intersectionLoops.Count == 0) ? null : outputLoops.intersectionLoops.Values.First(); // temp. Hack

            // TODO: get rid of needing mesh here
            var mesh			= BrushMeshManager.GetBrushMesh(brushInstance);
            var meshPolygons	= mesh.polygons;
            var meshSurfaces	= mesh.surfaces;


            var loopList = new LoopList(); // TODO: get rid of this somehow

            // TODO: separate base loops with holes 
            //          need a seperate set of holes per intersecting brush
            for (int p = 0; p < meshPolygons.Length; p++)
            {
                // TODO: get rid of needing mesh here
                var meshPolygon	 = meshPolygons[p];
                var surfaceIndex = meshPolygon.surfaceID;


                // Add all holes that share the same plane to the polygon
                var holeLoops = (surfaceLoops == null) ? (List<Loop>)null : surfaceLoops.surfaces[surfaceIndex];
                // Add all holes that share the same plane to the polygon
                if (mode)
                {
                    var loop = outputLoops.basePolygons[p]; // TODO: need to copy this
                    loopList.loops.Add(loop);
                    if (holeLoops != null)
                    {
                        for (int l = holeLoops.Count - 1; l >= 0; l--)
                        {
                            //if (holeLoops[l].basePlaneIndex != loop.basePlaneIndex)
                            //	continue;

                            // Cut polygons with its holes if they overlap
                            if (loopList.RemoveFrom(loop, holeLoops[l]))
                            {
                                holeLoops.RemoveAt(l);
                                //	lookHierarchies.Add(holeLoops[l]);
                                continue;
                            }
                            //loop.holes.Add(holeLoops[l]);
                        }
                        loop.holes.AddRange(holeLoops);
                    }
                } else
                {
                    if (holeLoops != null)
                    {
                        foreach (var hole in holeLoops)
                        {
                            hole.interiorCategory = Category.ReverseAligned;
                            loopList.loops.Add(hole);
                        }
                    }
                }
            }
            return loopList;
        }

        internal static void GenerateSurfaceRenderBuffers(int brushNodeID, LoopList loopList)
        {
            var output			= CSGManager.GetBrushOutput(brushNodeID);
            var brushInstance	= CSGManager.GetBrushMeshID(brushNodeID);
            var mesh			= BrushMeshManager.GetBrushMesh(brushInstance);
            var meshPolygons	= mesh.polygons;
            var meshSurfaces	= mesh.surfaces;

            Matrix4x4 worldToLocal = Matrix4x4.identity;
            CSGManager.GetTreeToNodeSpaceMatrix(brushNodeID, out worldToLocal);

            var outputSurfaces = new CSGSurfaceRenderBuffer[loopList.loops.Count]; // TODO: should be same size as brush.surfaces.Length
            int outputSurfaceCount = 0;
            foreach (var loop in loopList.loops)
            {
                var polygonIndex	= loop.basePlaneIndex;// TODO: fix this
                var meshPolygon		= meshPolygons[polygonIndex];

                var surfaceIndex	= meshPolygon.surfaceID;// TODO: fix this

                var localSpaceToPlaneSpace	= MathExtensions.GenerateLocalToPlaneSpaceMatrix(meshSurfaces[surfaceIndex].plane);
                var uv0Matrix				= meshPolygon.description.UV0.ToMatrix() * (localSpaceToPlaneSpace * worldToLocal);

                // TODO: all separate loops on same surface should be put in same OutputSurfaceMesh
                if (!loop.Triangulate(uv0Matrix, ref outputSurfaces[outputSurfaceCount]))
                    continue;

                // TODO: make this work
//				outputSurfaces[outputSurfaceCount].meshQuery		= loop.layers; //???
//				outputSurfaces[outputSurfaceCount].surfaceParameter = loop.surfaceParameter; //???
                outputSurfaces[outputSurfaceCount].surfaceIndex		= surfaceIndex;
                outputSurfaceCount++;
            }
            if (outputSurfaceCount != loopList.loops.Count)
                Array.Resize(ref outputSurfaces, outputSurfaceCount);
            output.renderBuffers.surfaceRenderBuffers.AddRange(outputSurfaces);
        }


        internal static bool UpdateTreeMesh(int treeNodeID)
        {
            if (!IsValidNodeID(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree))
                return false;

            // TODO: optimize, only do this when necessary
            for (int i=0;i<brushes.Count;i++)
            {
                var brushNodeID		= brushes[i];
                var brushNodeIndex	= brushNodeID - 1;
                var parentNodeID	= nodeHierarchies[brushNodeIndex].parentNodeID;
                var parentNodeIndex = parentNodeID - 1;
                var parentLocalTransformation		= (parentNodeIndex < 0) ? Matrix4x4.identity : nodeLocalTransforms[parentNodeIndex].localTransformation;
                var parentLocalInvTransformation	= (parentNodeIndex < 0) ? Matrix4x4.identity : nodeLocalTransforms[parentNodeIndex].invLocalTransformation;

                // TODO: should be transformations the way up to the tree, not just tree vs brush
                var brushLocalTransformation		= nodeLocalTransforms[brushNodeIndex].localTransformation;
                var brushLocalInvTransformation		= nodeLocalTransforms[brushNodeIndex].invLocalTransformation;

                var nodeTransform = nodeTransforms[brushNodeIndex];
                nodeTransform.nodeToTree = brushLocalTransformation * parentLocalInvTransformation;
                nodeTransform.treeToNode = parentLocalTransformation * brushLocalInvTransformation;
                nodeTransforms[brushNodeIndex] = nodeTransform;
            }

            // TODO: find intersecting brushes

            // TODO: build categorization tree



            // check if we even have a valid brushMesh

            // TODO: update all renderbuffers for all brushes
            /*
            var brushMesh = BrushMeshManager.GetBrushMesh(brushOutput.brushMeshInstanceID);
            if (brushMesh == null)
                continue;
            {
                renderBuffers = new CSGBrushRenderBuffer();
                if (!brushOutput.triangleMesh.UpdateRenderBuffer(nodeIndex,
                                                             brushMesh,
                                                             renderBuffers,
                                                             meshQueries.Length, meshQueries, vertexChannelMask))
                    continue;
                brushOutput.renderBuffers = renderBuffers;
            }
            */

            // FIXME: surfaceIndex == planeIndex == polygonIndex

            for (int b = 0; b < CSGManager.brushes.Count; b++)
            {
                var brushNodeID = CSGManager.brushes[b];
                var output		= CSGManager.GetBrushOutput(brushNodeID);
                var outputLoops	= output.brushOutputLoops;
                outputLoops.brush = new CSGTreeBrush() { brushNodeID = brushNodeID };
                outputLoops.GenerateBasePolygons();
            }
            
            for (int b0 = 0; b0 < CSGManager.brushes.Count; b0++)
            {
                var brush0NodeID	= CSGManager.brushes[b0];
                var output			= CSGManager.GetBrushOutput(brush0NodeID);
                var outputLoops		= output.brushOutputLoops;

                // FIXME: for now assume that all brushes touch all brushes, fix this
                for (int b1 = 0; b1 < CSGManager.brushes.Count; b1++)
                {
                    if (b0 == b1)
                        continue;
                    
                    var brush1NodeID	= CSGManager.brushes[b1];
                    // TODO: only when they actually touch & need updating
                    // TODO: ensure intersections between brushes are the same on all intersecting brushes
                    outputLoops.GenerateIntersectionLoops(new CSGTreeBrush() { brushNodeID = brush1NodeID });
                }
            }

            // TODO: Cache the output surface meshes, only update when necessary
            for (int b = 0; b < CSGManager.brushes.Count; b++)
            {
                // TODO: Be able to perform actual csg on brushes
                var brushNodeID	= CSGManager.brushes[b];
                var loopList = PerformCSG(brushNodeID, (b & 1) == 0);
                GenerateSurfaceRenderBuffers(brushNodeID, loopList);
            }
            return true;
        }


        internal static void RebuildAll()
        {
            Reset();
            UpdateAllTreeMeshes();
        }

        internal static void Reset()
        {
            for (int t = 0; t < brushes.Count; t++)
            {
                var brushNodeID = brushes[t];
                var brushNodeIndex = brushNodeID - 1;
                var brushOutput = CSGManager.nodeHierarchies[brushNodeIndex].brushOutput;
                if (brushOutput == null)
                    continue;

                brushOutput.Reset();
            }

            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                var treeNodeIndex = treeNodeID - 1;
                var treeInfo = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;
                if (treeInfo == null)
                    continue;
                treeInfo.Reset();
            }
        }

        internal static GeneratedMeshDescription[] GetMeshDescriptions(Int32 treeNodeID,
                                                                       MeshQuery[] meshQueries,
                                                                       VertexChannelFlags vertexChannelMask)
        {
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return null;
            if (meshQueries == null)
                throw new ArgumentNullException("meshTypes");

            if (meshQueries.Length == 0)
                return null;

            if (!IsValidNodeID(treeNodeID))
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return null;
            }

            var treeNodeIndex = treeNodeID - 1;
            var treeInfo = nodeHierarchies[treeNodeIndex].treeInfo;
            if (treeInfo == null)
            {
                Debug.LogError("GenerateMeshDescriptions: Invalid node index used");
                return null;
            }

            treeInfo.subMeshCounts.Clear();
            treeInfo.meshDescriptions.Clear();

            if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                UpdateTreeMesh(treeNodeID);

            CombineSubMeshes(treeInfo, meshQueries, vertexChannelMask);

            if (treeInfo.subMeshCounts.Count <= 0)
            {
                nodeFlags[treeNodeIndex].UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                //				Debug.LogWarning("GetMeshDescriptions: No meshes found");
                return null;
            }

            if (treeInfo.meshDescriptions == null ||
                treeInfo.meshDescriptions.Count == 0 ||
                treeInfo.meshDescriptions[0].vertexCount <= 0 ||
                treeInfo.meshDescriptions[0].indexCount <= 0)
                return null;

            nodeFlags[treeNodeIndex].UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
            return treeInfo.meshDescriptions.ToArray();
        }

        private static void UpdateDelayedHierarchyModifications()
        {
            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;

                nodeFlags[branchNodeIndex].UnSetNodeFlag(NodeStatusFlags.OperationNeedsUpdate);
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedPreviousSiblingsUpdate))
                    continue;

                // TODO: implement
                //operation->RebuildPreviousSiblings();
                nodeFlags[branchNodeIndex].UnSetNodeFlag(NodeStatusFlags.NeedPreviousSiblingsUpdate);
            }

            var foundOperations = new List<int>();
            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                foundOperations.Add(branchNodeIndex);
            }

            for (int i = 0; i < foundOperations.Count; i++)
            {
                // TODO: implement
                //UpdateChildOperationTouching(foundOperations[i]);
            }

            for (var i = 0; i < branches.Count; i++)
            {
                var branchNodeID = branches[i];
                var branchNodeIndex = branchNodeID - 1;
                if (!nodeFlags[branchNodeIndex].IsNodeFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                // TODO: implement
                //UpdateChildBrushTouching(branchNodeID);
                nodeFlags[branchNodeIndex].UnSetNodeFlag(NodeStatusFlags.NeedAllTouchingUpdated);
            }
        }

        internal static bool UpdateAllTreeMeshes()
        {
            bool needUpdate = false;
            // Check if we have a tree that needs updates
            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                var treeNodeIndex = treeNodeID - 1;
                if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    needUpdate = true;
                    break;
                }
            }

            if (!needUpdate)
                return false;

            UpdateDelayedHierarchyModifications();

            for (int t = 0; t < trees.Count; t++)
                UpdateTreeMesh(trees[t]);

            // Check if we have a tree that actually has an updated mesh
            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                var treeNodeIndex = treeNodeID - 1;

                if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeMeshNeedsUpdate))
                    return true;
            }

            return false;
        }
#endif
    }
}
