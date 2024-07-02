using Chisel.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Pool;

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
        internal static ChiselCompositeComponent GetGroupCompositeForNode(ChiselNode node)
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


        static ChiselIntersection Convert(CSGTreeBrushIntersection intersection)
        {
            var node                    = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(intersection.brush.UserID);
            var model                   = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(intersection.tree.UserID) as ChiselModelComponent;

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

        public static bool FindFirstWorldIntersection(List<ChiselIntersection> foundIntersections, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers = ~0, LayerUsageFlags visibleLayerFlags = LayerUsageFlags.Renderable, bool ignoreBackfaced = false, bool ignoreCulled = false, GameObject[] ignore = null, GameObject[] filter = null)
        {
            bool found = false;

            var ignoreInstanceIDs = HashSetPool<int>.Get();
            var filterInstanceIDs = HashSetPool<int>.Get();
            var ignoreNodes = ListPool<CSGTreeNode>.Get();
            var filterNodes = ListPool<CSGTreeNode>.Get();
            try
            {
                if (ignore != null)
                {
                    foreach (var go in ignore)
                    {
                        var node = go.GetComponent<ChiselNode>();
                        if (node)
                        {
                            ChiselNodeHierarchyManager.GetChildrenOfHierarchyItem(ignoreNodes, node.hierarchyItem);
                            ignoreInstanceIDs.Add(node.GetInstanceID());
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
                            ChiselNodeHierarchyManager.GetChildrenOfHierarchyItem(filterNodes, node.hierarchyItem);
                            filterInstanceIDs.Add(node.GetInstanceID());
                            if (node.hierarchyItem != null &&
                                node.hierarchyItem.Model)
                                filterInstanceIDs.Add(node.hierarchyItem.Model.GetInstanceID());
                        }
                    }
                }

                using (var allTrees = new NativeList<CSGTree>(Allocator.Temp))
                { 
                    CompactHierarchyManager.GetAllTrees(allTrees);
                    for (var t = 0; t < allTrees.Length; t++)
                    {
                        var tree	= allTrees[t];
                        var model	= ChiselNodeHierarchyManager.FindChiselNodeByTreeNode(tree) as ChiselModelComponent;
                        if (!ChiselModelManager.IsSelectable(model))
                            continue;

                        if (((1 << model.gameObject.layer) & visibleLayers) == 0)
                            continue;

                        var modelInstanceID = model.GetInstanceID();
                        if (ignoreInstanceIDs.Contains(modelInstanceID) ||
                            (filterInstanceIDs.Count > 0 && !filterInstanceIDs.Contains(modelInstanceID)))
                            continue;

                        var query           = ChiselMeshQueryManager.GetMeshQuery(model);
                        var visibleQueries  = ChiselMeshQueryManager.GetVisibleQueries(query, visibleLayerFlags);

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

                        var treeIntersections = CSGQueryManager.RayCastMulti(visibleQueries, tree, treeRayStart, treeRayEnd, ignoreNodes, filterNodes, ignoreBackfaced, ignoreCulled);
                        if (treeIntersections == null)
                            continue;

                        for (var i = 0; i < treeIntersections.Length; i++)
                        {
                            var intersection	= treeIntersections[i];
                            var brush			= intersection.brush;
                            var instanceID		= brush.UserID;

                            if ((filterInstanceIDs.Count > 0 && !filterInstanceIDs.Contains(instanceID)) ||
                                ignoreInstanceIDs.Contains(instanceID))
                                continue;

                            foundIntersections.Add(Convert(intersection));
                            found = true;
                        }
                    }
                    return found;
                }
            }
            finally
            {
                HashSetPool<int>.Release(ignoreInstanceIDs);
                HashSetPool<int>.Release(filterInstanceIDs);
                ListPool<CSGTreeNode>.Release(ignoreNodes);
                ListPool<CSGTreeNode>.Release(filterNodes);
            }
        }

        /*
        public static bool FindFirstWorldIntersection(ChiselModel model, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers, out ChiselIntersection foundIntersection)
        {
            return FindFirstWorldIntersection(model, worldRayStart, worldRayEnd, visibleLayers, null, null, out foundIntersection);
        }
        */
        public static bool FindFirstWorldIntersection(ChiselModelComponent model, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers, LayerUsageFlags visibleLayerFlags, GameObject[] ignore, GameObject[] filter, out ChiselIntersection foundIntersection)
        {
            foundIntersection = ChiselIntersection.None;

            if (!ChiselGeneratedComponentManager.IsValidModelToBeSelected(model))
                return false;

            var ignoreInstanceIDs = HashSetPool<int>.Get();
            var filterInstanceIDs = HashSetPool<int>.Get();
            var ignoreNodes = ListPool<CSGTreeNode>.Get();
            var filterNodes = ListPool<CSGTreeNode>.Get();
            try
            {
                if (ignore != null)
                {
                    foreach (var go in ignore)
                    {
                        var node = go.GetComponent<ChiselNode>();
                        if (node)
                        {
                            ChiselNodeHierarchyManager.GetChildrenOfHierarchyItem(ignoreNodes, node.hierarchyItem);
                            ignoreInstanceIDs.Add(node.GetInstanceID());
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
                            ChiselNodeHierarchyManager.GetChildrenOfHierarchyItem(filterNodes, node.hierarchyItem);
                            filterInstanceIDs.Add(node.GetInstanceID());
                            if (node.hierarchyItem != null &&
                                node.hierarchyItem.Model)
                                filterInstanceIDs.Add(node.hierarchyItem.Model.GetInstanceID());
                        }
                    }
                }

                var tree	= model.Node;
                if (ignoreInstanceIDs.Contains(model.GetInstanceID()) ||
                    (filterInstanceIDs.Count > 0 && !filterInstanceIDs.Contains(model.GetInstanceID())))
                    return false;

                if (((1 << model.gameObject.layer) & visibleLayers) == 0)
                    return false;

                var query           = ChiselMeshQueryManager.GetMeshQuery(model);
                var visibleQueries  = ChiselMeshQueryManager.GetVisibleQueries(query, visibleLayerFlags);

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

                var treeIntersections = CSGQueryManager.RayCastMulti(visibleQueries, tree, treeRayStart, treeRayEnd, ignoreNodes, filterNodes, ignoreBackfaced: true, ignoreCulled: true);
                if (treeIntersections == null)
                    return false;

                bool found = false;
                for (var i = 0; i < treeIntersections.Length; i++)
                {
                    var intersection	= treeIntersections[i];
                    var brush			= intersection.brush;
                    var instanceID		= brush.UserID;
                
                    if ((filterInstanceIDs.Count > 0 && !filterInstanceIDs.Contains(instanceID)) ||
                        ignoreInstanceIDs.Contains(instanceID))
                        continue;

                    if (intersection.surfaceIntersection.distance < foundIntersection.brushIntersection.surfaceIntersection.distance)
                    {
                        foundIntersection = Convert(intersection);
                        found = true;
                    }
                }
                return found;
            }
            finally
            {
                HashSetPool<int>.Release(ignoreInstanceIDs);
                HashSetPool<int>.Release(filterInstanceIDs);
                ListPool<CSGTreeNode>.Release(ignoreNodes);
                ListPool<CSGTreeNode>.Release(filterNodes);
            }
        }
        
        public static bool GetNodesInFrustum(Frustum frustum, int visibleLayers, LayerUsageFlags visibleLayerFlags, ref HashSet<CSGTreeNode> rectFoundNodes)
        {
            rectFoundNodes.Clear();
            var planes = new Plane[6];
            Vector4 srcVector;

            using (var allTrees = new NativeList<CSGTree>(Allocator.Temp))
            {
                CompactHierarchyManager.GetAllTrees(allTrees);
                for (var t = 0; t < allTrees.Length; t++)
                {
                    var tree	= allTrees[t];
                    var model	= ChiselNodeHierarchyManager.FindChiselNodeByTreeNode(tree) as ChiselModelComponent;
                    if (!ChiselModelManager.IsSelectable(model))
                        continue;

                    if (((1 << model.gameObject.layer) & visibleLayers) == 0)
                        continue;

                    var query           = ChiselMeshQueryManager.GetMeshQuery(model);
                    var visibleQueries  = ChiselMeshQueryManager.GetVisibleQueries(query, visibleLayerFlags);

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

                    var treeNodesInFrustum = CSGQueryManager.GetNodesInFrustum(tree, visibleQueries, planes);
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
}
