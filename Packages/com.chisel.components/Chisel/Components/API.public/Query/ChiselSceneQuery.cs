using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2	 = UnityEngine.Vector2;
using Vector3	 = UnityEngine.Vector3;
using Vector4	 = UnityEngine.Vector4;
using GameObject = UnityEngine.GameObject;
using Camera	 = UnityEngine.Camera;
using Plane		 = UnityEngine.Plane;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Components
{
    public static class ChiselSceneQuery
    {
#if UNITY_EDITOR
        // TODO: move somewhere else
        internal static bool GameObjectContainsAttribute<T>(GameObject go) where T : Attribute
        {
            var behaviours = go.GetComponents(typeof(Component));
            for (var index = 0; index < behaviours.Length; index++)
            {
                var behaviour = behaviours[index];
                if (behaviour == null)
                    continue;

                var behaviourType = behaviour.GetType();
                if (behaviourType.GetCustomAttributes(typeof(T), true).Length > 0)
                    return true;
            }
            return false;
        }

        // TODO: consider grouping functionality
        internal static ChiselComposite GetGroupCompositeForNode(ChiselNode node)
        {
            /*
            if (!node)
                return null;

            var parent = node.transform.parent;
            while (parent)
            {
                var model = parent.GetComponent<ChiselModel>();
                if (model)
                    return null;

                var parentOp = parent.GetComponent<ChiselComposite>();
                if (parentOp &&
                    //!parentOp.PassThrough && 
                    parentOp.HandleAsOne)
                    return parentOp;

                parent = parent.transform.parent;
            }
            */
            return null;
        }

        public static GameObject FindSelectionBase(GameObject go)
        {
            if (go == null)
                return null;

#if UNITY_2018_3_OR_NEWER
            Transform prefabBase = null;
            if (UnityEditor.PrefabUtility.IsPartOfNonAssetPrefabInstance(go))
            {
                prefabBase = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(go).transform;
            }
#endif

            GameObject group = null;
            Transform groupTransform = null;
            var node = go.GetComponentInChildren<ChiselNode>();
            if (node)
            {
                var composite = GetGroupCompositeForNode(node);
                group = (composite == null) ? null : composite.gameObject;
                groupTransform = (composite == null) ? null : composite.transform;
            }


            Transform tr = go.transform;
            while (tr != null)
            {
#if UNITY_2018_3_OR_NEWER
                if (tr == prefabBase)
                    return tr.gameObject;
#endif
                if (tr == groupTransform)
                    return group;

                if (GameObjectContainsAttribute<SelectionBaseAttribute>(tr.gameObject))
                    return tr.gameObject;

                tr = tr.parent;
            }

            return go;
        }

#endif


        static Dictionary<CSGTreeNode, ChiselIntersection> __foundIntersections = new Dictionary<CSGTreeNode, ChiselIntersection>(); // to avoid allocations

        static ChiselIntersection Convert(CSGTreeBrushIntersection intersection)
        {
            var node                    = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(intersection.brush.UserID);
            var model                   = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(intersection.tree.UserID) as ChiselModel;

            var treeLocalToWorldMatrix  = model.transform.localToWorldMatrix;            
            
            var worldPlaneIntersection  = treeLocalToWorldMatrix.MultiplyPoint(intersection.surfaceIntersection.treePlaneIntersection);
            var worldPlane              = treeLocalToWorldMatrix.TransformPlane(intersection.surfaceIntersection.treePlane);
            
            return new ChiselIntersection()
            {
                treeNode                    = node,
                model                   = model,
                worldPlane              = worldPlane,
                worldPlaneIntersection  = worldPlaneIntersection,
                brushIntersection       = intersection
            };
        }

        static List<CSGTree> s_AllTrees = new List<CSGTree>();
        static HashSet<int> s_IgnoreInstanceIDs = new HashSet<int>();
        static HashSet<int> s_FilterInstanceIDs = new HashSet<int>();
        static List<CSGTreeNode> s_IgnoreNodes = new List<CSGTreeNode>();
        static List<CSGTreeNode> s_FilterNodes  = new List<CSGTreeNode>();
        public static bool FindFirstWorldIntersection(List<ChiselIntersection> foundIntersections, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers = ~0, bool ignoreBackfaced = false, bool ignoreCulled = false, GameObject[] ignore = null, GameObject[] filter = null)
        {
            bool found = false;

            s_IgnoreInstanceIDs.Clear();
            s_FilterInstanceIDs.Clear();
            s_IgnoreNodes.Clear();
            s_FilterNodes.Clear();
            if (ignore != null)
            {
                foreach (var go in ignore)
                {
                    var node = go.GetComponent<ChiselNode>();
                    if (node)
                    {
                        ChiselNodeHierarchyManager.GetChildrenOfHierachyItem(s_IgnoreNodes, node.hierarchyItem);
                        s_IgnoreInstanceIDs.Add(node.GetInstanceID());
                    }
                }
            }
            if (filter != null)
            {
                foreach (var go in filter)
                {
                    var node = go.GetComponent<ChiselNode>();
                    if (node)
                    {
                        ChiselNodeHierarchyManager.GetChildrenOfHierachyItem(s_FilterNodes, node.hierarchyItem);
                        s_FilterInstanceIDs.Add(node.GetInstanceID());
                        if (node.hierarchyItem != null &&
                            node.hierarchyItem.Model)
                            s_FilterInstanceIDs.Add(node.hierarchyItem.Model.GetInstanceID());
                    }
                }
            }

            //CSGManager.GetAllTrees(s_AllTrees);
            CompactHierarchyManager.GetAllTrees(s_AllTrees);
            for (var t = 0; t < s_AllTrees.Count; t++)
            {
                var tree	= s_AllTrees[t];
                var model	= ChiselNodeHierarchyManager.FindChiselNodeByTreeNode(tree) as ChiselModel;
                if (!ChiselModelManager.IsSelectable(model))
                    continue;

                if (((1 << model.gameObject.layer) & visibleLayers) == 0)
                    continue;

                var modelInstanceID = model.GetInstanceID();
                if (s_IgnoreInstanceIDs.Contains(modelInstanceID) ||
                    (s_FilterInstanceIDs.Count > 0 && !s_FilterInstanceIDs.Contains(modelInstanceID)))
                    continue;

                var query           = ChiselMeshQueryManager.GetMeshQuery(model);
                var visibleQueries  = ChiselMeshQueryManager.GetVisibleQueries(query);

                // We only accept RayCasts into this model if it's visible
                if (visibleQueries == null ||
                    visibleQueries.Length == 0)
                    return false;

                Vector3 treeRayStart;
                Vector3 treeRayEnd;

                var transform = model.transform;
                if (transform)
                { 
                    var worldToLocalMatrix = transform.worldToLocalMatrix;
                    treeRayStart	= worldToLocalMatrix.MultiplyPoint(worldRayStart);
                    treeRayEnd		= worldToLocalMatrix.MultiplyPoint(worldRayEnd);
                } else
                {
                    treeRayStart	= worldRayStart;
                    treeRayEnd		= worldRayEnd;
                }

                var treeIntersections = CSGQueryManager.RayCastMulti(ChiselMeshQueryManager.GetMeshQuery(model), tree, treeRayStart, treeRayEnd, s_IgnoreNodes, s_FilterNodes, ignoreBackfaced, ignoreCulled);
                if (treeIntersections == null)
                    continue;

                for (var i = 0; i < treeIntersections.Length; i++)
                {
                    var intersection	= treeIntersections[i];
                    var brush			= intersection.brush;
                    var instanceID		= brush.UserID;

                    if ((s_FilterInstanceIDs.Count > 0 && !s_FilterInstanceIDs.Contains(instanceID)) ||
                        s_IgnoreInstanceIDs.Contains(instanceID))
                        continue;

                    foundIntersections.Add(Convert(intersection));
                    found = true;
                }
            }
            return found;
        }

        /*
        public static bool FindFirstWorldIntersection(ChiselModel model, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers, out ChiselIntersection foundIntersection)
        {
            return FindFirstWorldIntersection(model, worldRayStart, worldRayEnd, visibleLayers, null, null, out foundIntersection);
        }
        */
        public static bool FindFirstWorldIntersection(ChiselModel model, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers, GameObject[] ignore, GameObject[] filter, out ChiselIntersection foundIntersection)
        {
            foundIntersection = ChiselIntersection.None;

            if (!ChiselGeneratedComponentManager.IsValidModelToBeSelected(model))
                return false;

            s_FilterNodes.Clear();
            s_IgnoreNodes.Clear();
            s_IgnoreInstanceIDs.Clear();
            s_FilterInstanceIDs.Clear();
            if (ignore != null)
            {
                foreach (var go in ignore)
                {
                    var node = go.GetComponent<ChiselNode>();
                    if (node)
                    {
                        ChiselNodeHierarchyManager.GetChildrenOfHierachyItem(s_IgnoreNodes, node.hierarchyItem);
                        s_IgnoreInstanceIDs.Add(node.GetInstanceID());
                    }
                }
            }
            if (filter != null)
            {
                foreach (var go in filter)
                {
                    var node = go.GetComponent<ChiselNode>();
                    if (node)
                    {
                        ChiselNodeHierarchyManager.GetChildrenOfHierachyItem(s_FilterNodes, node.hierarchyItem);
                        s_FilterInstanceIDs.Add(node.GetInstanceID());
                        if (node.hierarchyItem != null &&
                            node.hierarchyItem.Model)
                            s_FilterInstanceIDs.Add(node.hierarchyItem.Model.GetInstanceID());
                    }
                }
            }

            var tree	= model.Node;
            if (s_IgnoreInstanceIDs.Contains(model.GetInstanceID()) ||
                (s_FilterInstanceIDs.Count > 0 && !s_FilterInstanceIDs.Contains(model.GetInstanceID())))
                return false;

            if (((1 << model.gameObject.layer) & visibleLayers) == 0)
                return false;

            var query           = ChiselMeshQueryManager.GetMeshQuery(model);
            var visibleQueries  = ChiselMeshQueryManager.GetVisibleQueries(query);

            // We only accept RayCasts into this model if it's visible
            if (visibleQueries == null ||
                visibleQueries.Length == 0)
                return false;

            Vector3 treeRayStart;
            Vector3 treeRayEnd;

            var transform = model.transform;
            if (transform)
            {
                var worldToLocalMatrix = transform.worldToLocalMatrix;
                treeRayStart = worldToLocalMatrix.MultiplyPoint(worldRayStart);
                treeRayEnd = worldToLocalMatrix.MultiplyPoint(worldRayEnd);
            } else
            {
                treeRayStart = worldRayStart;
                treeRayEnd = worldRayEnd;
            }

            var treeIntersections = CSGQueryManager.RayCastMulti(ChiselMeshQueryManager.GetMeshQuery(model), tree, treeRayStart, treeRayEnd, s_IgnoreNodes, s_FilterNodes, ignoreBackfaced: true, ignoreCulled: true);
            if (treeIntersections == null)
                return false;

            bool found = false;
            for (var i = 0; i < treeIntersections.Length; i++)
            {
                var intersection	= treeIntersections[i];
                var brush			= intersection.brush;
                var instanceID		= brush.UserID;
                
                if ((s_FilterInstanceIDs.Count > 0 && !s_FilterInstanceIDs.Contains(instanceID)) ||
                    s_IgnoreInstanceIDs.Contains(instanceID))
                    continue;

                if (intersection.surfaceIntersection.distance < foundIntersection.brushIntersection.surfaceIntersection.distance)
                {
                    foundIntersection = Convert(intersection);
                    found = true;
                }
            }
            return found;
        }
        
        public static bool GetNodesInFrustum(Frustum frustum, int visibleLayers, ref HashSet<CSGTreeNode> rectFoundNodes)
        {
            rectFoundNodes.Clear();
            var planes = new Plane[6];
            Vector4 srcVector;
            //CSGManager.GetAllTrees(s_AllTrees);
            CompactHierarchyManager.GetAllTrees(s_AllTrees);
            for (var t = 0; t < s_AllTrees.Count; t++)
            {
                var tree	= s_AllTrees[t];
                var model	= ChiselNodeHierarchyManager.FindChiselNodeByTreeNode(tree) as ChiselModel;
                if (!ChiselModelManager.IsSelectable(model))
                    continue;

                if (((1 << model.gameObject.layer) & visibleLayers) == 0)
                    continue;

                var query           = ChiselMeshQueryManager.GetMeshQuery(model);
                var visibleQueries  = ChiselMeshQueryManager.GetVisibleQueries(query);

                // We only accept RayCasts into this model if it's visible
                if (visibleQueries == null ||
                    visibleQueries.Length == 0)
                    continue;
                
                // Transform the frustum into the space of the tree				
                var transform								= model.transform;
                var worldToLocalMatrixInversed				= transform.localToWorldMatrix;	 // localToWorldMatrix == worldToLocalMatrix.inverse
                var worldToLocalMatrixInversedTransposed	= worldToLocalMatrixInversed.transpose;
                for (int p = 0; p < 6; p++)
                {
                    var srcPlane  = frustum.Planes[p];
                    srcVector.x = srcPlane.normal.x;
                    srcVector.y = srcPlane.normal.y;
                    srcVector.z = srcPlane.normal.z;
                    srcVector.w = srcPlane.distance;

                    srcVector = worldToLocalMatrixInversedTransposed * srcVector;

                    planes[p].normal   = srcVector;
                    planes[p].distance = srcVector.w;
                }

                var treeNodesInFrustum = CSGQueryManager.GetNodesInFrustum(tree, query, planes);
                if (treeNodesInFrustum == null)
                    continue;

                for (int n = 0; n < treeNodesInFrustum.Length; n++)
                {
                    var treeNode		= treeNodesInFrustum[n];
                    rectFoundNodes.Add(treeNode);
                }
            }
            return rectFoundNodes.Count > 0;
        }
    }
}
