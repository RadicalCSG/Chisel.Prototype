using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;


/*
    goals:
    - fast detection of changes in transform hierarchy
        - parent chain
        - sibling index
            - also at root
        - keep in mind 
            - make sure it works when unity starts up / loading scene
            - adding/removing/enabling/disabling components, incl. in between nodes
            - adding/removing/activating/deactivating gameObjects
            - composite passthrough mode
            - undo/redo
            - mind prefabs
            - multiple scenes
                - also keep in mind never dirtying non modified scenes
            - default TreeNode per scene
*/
namespace Chisel.Components
{
    public static class ChiselNodeHierarchyManager
    {
        public static readonly Dictionary<Scene, ChiselSceneHierarchy> sceneHierarchies = new Dictionary<Scene, ChiselSceneHierarchy>();

        static readonly HashSet<ChiselNode> registeredNodes = new HashSet<ChiselNode>();
        static readonly Dictionary<int, ChiselNode> instanceIDToNodeLookup = new Dictionary<int, ChiselNode>();
        static readonly Dictionary<ChiselNode, int> nodeToinstanceIDLookup = new Dictionary<ChiselNode, int>();

        // Note: keep in mind that these work even when components have already been destroyed
        static readonly Dictionary<Transform, ChiselNode> componentLookup = new Dictionary<Transform, ChiselNode>();
        static readonly Dictionary<ChiselNode, ChiselHierarchyItem> hierarchyItemLookup = new Dictionary<ChiselNode, ChiselHierarchyItem>();
        static readonly Dictionary<ChiselNode, CSGTreeNode> treeNodeLookup = new Dictionary<ChiselNode, CSGTreeNode>();

        static readonly HashSet<ChiselNode> registerQueueLookup = new HashSet<ChiselNode>();
        static readonly List<ChiselNode> registerQueue = new List<ChiselNode>();
        static readonly HashSet<ChiselNode> unregisterQueueLookup = new HashSet<ChiselNode>();
        static readonly List<ChiselNode> unregisterQueue = new List<ChiselNode>();

        static readonly HashSet<ChiselSceneHierarchy> createDefaultModels = new HashSet<ChiselSceneHierarchy>();

        // Unfortunately we might need to create default models during the update loop, which kind of screws up the order of things.
        // so we remember them, and re-register at the end.
        static readonly List<ChiselModel> reregisterModelQueue = new List<ChiselModel>();

        static readonly List<ChiselHierarchyItem> findChildrenQueue = new List<ChiselHierarchyItem>();

        static readonly HashSet<CSGTreeNode> destroyNodesList = new HashSet<CSGTreeNode>();

        static readonly Dictionary<ChiselNode, ChiselHierarchyItem> addToHierarchyLookup = new Dictionary<ChiselNode, ChiselHierarchyItem>();
        static readonly List<ChiselHierarchyItem> addToHierarchyQueue = new List<ChiselHierarchyItem>(5000);
        static readonly List<ChiselHierarchyItem> deferAddToHierarchyQueue = new List<ChiselHierarchyItem>(5000);

        static readonly HashSet<ChiselNode> rebuildTreeNodes = new HashSet<ChiselNode>();

        static readonly HashSet<ChiselNode> updateTransformationNodes = new HashSet<ChiselNode>();

        static readonly HashSet<ChiselHierarchyItem> updateChildrenQueue = new HashSet<ChiselHierarchyItem>();
        static readonly HashSet<List<ChiselHierarchyItem>> sortChildrenQueue = new HashSet<List<ChiselHierarchyItem>>();

        static readonly HashSet<ChiselNode> hierarchyUpdateQueue = new HashSet<ChiselNode>();
        static readonly HashSet<ChiselNode> onHierarchyChangeCalled = new HashSet<ChiselNode>();

        // Dictionaries used to keep track which brushContainerAssets are used by which nodes, which is necessary to update the right nodes when an brushContainerAsset has been changed
//      static readonly Dictionary<ChiselBrushContainerAsset, HashSet<ChiselNode>> generatedBrushNodes = new Dictionary<ChiselBrushContainerAsset, HashSet<ChiselNode>>();
//      static readonly Dictionary<ChiselNode, HashSet<ChiselBrushContainerAsset>> nodegeneratedBrush = new Dictionary<ChiselNode, HashSet<ChiselBrushContainerAsset>>();


        public static bool ignoreNextChildrenChanged = false;
        public static bool firstStart = false;
        public static bool prefabInstanceUpdatedEvent = false;

        public static event Action NodeHierarchyModified;
        public static event Action NodeHierarchyReset;
        public static event Action TransformationChanged;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            ChiselNodeHierarchyManager.firstStart = false;
            /*
            ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= OnBrushMeshInstanceChanged;
            ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged += OnBrushMeshInstanceChanged;

            ChiselBrushContainerAssetManager.OnBrushMeshInstanceDestroyed -= OnBrushMeshInstanceDestroyed;
            ChiselBrushContainerAssetManager.OnBrushMeshInstanceDestroyed += OnBrushMeshInstanceDestroyed;
            */
        }

        // TODO: Clean up API
        public static void Rebuild()
        {
            double endTime, startTime;

            startTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("CSGManager.Clear");
            Chisel.Core.CompactHierarchyManager.Clear();
            Profiler.EndSample();

            //Profiler.BeginSample("ChiselBrushContainerAssetManager.Reset");
            //ChiselBrushContainerAssetManager.Reset();
            //Profiler.EndSample();

            endTime = Time.realtimeSinceStartup;
            Debug.Log($"  Reset done in {((endTime - startTime) * 1000)} ms. ");


            startTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("FindAndReregisterAllNodes");
            ChiselNodeHierarchyManager.FindAndReregisterAllNodes();
            Profiler.EndSample();
            endTime = Time.realtimeSinceStartup;
            Debug.Log($"  FindAndReregisterAllNodes done in {((endTime - startTime) * 1000)} ms. ");


            startTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("UpdateAllTransformations");
            ChiselNodeHierarchyManager.UpdateAllTransformations();
            Profiler.EndSample();
            endTime = Time.realtimeSinceStartup;
            Debug.Log($"  UpdateAllTransformations done in {((endTime - startTime) * 1000)} ms. ");


            startTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("UpdateHierarchy");
            ChiselNodeHierarchyManager.Update();
            Profiler.EndSample();
            endTime = Time.realtimeSinceStartup;
            Debug.Log($"  UpdateHierarchy done in {((endTime - startTime) * 1000)} ms. ");

            startTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("UpdateModels");
            ChiselGeneratedModelMeshManager.UpdateModels();
            Profiler.EndSample();
            endTime = Time.realtimeSinceStartup;
            Debug.Log($"  UpdateModels done in {((endTime - startTime) * 1000)} ms. ");

            ChiselGeneratedComponentManager.UpdateVisibility(force: true);
        }

        // TODO: Probably needs to be internal?
        public static void Reset()
        {
            sceneHierarchies.Clear();

            registeredNodes.Clear();
            instanceIDToNodeLookup.Clear();
            nodeToinstanceIDLookup.Clear();

            componentLookup.Clear();
            hierarchyItemLookup.Clear();
            treeNodeLookup.Clear();

            reregisterModelQueue.Clear();

            
            foreach(var item in destroyNodesList)
                item.Destroy();

            //generatedBrushNodes.Clear();
            //nodegeneratedBrush.Clear();

            reregisterModelQueue.Clear();
            ClearQueues();
            ClearTemporaries();

            ChiselGeneratedModelMeshManager.Reset();

            if (NodeHierarchyReset != null)
                NodeHierarchyReset();
        }

        static void ClearQueues()
        {
            registerQueueLookup.Clear();
            registerQueue.Clear();
            unregisterQueueLookup.Clear();
            unregisterQueue.Clear();

            createDefaultModels.Clear();

            findChildrenQueue.Clear();

            destroyNodesList.Clear();

            addToHierarchyLookup.Clear();
            addToHierarchyQueue.Clear();
            deferAddToHierarchyQueue.Clear();

            rebuildTreeNodes.Clear();

            updateTransformationNodes.Clear();

            updateChildrenQueue.Clear();
            sortChildrenQueue.Clear();
            hierarchyUpdateQueue.Clear();
        }

        static void ClearTemporaries()
        {
            __rootGameObjects.Clear();
            __transforms.Clear();
            __hierarchyQueueLists.Clear();
            __registerNodes.Clear();
            __unregisterNodes.Clear();
            __childNodes.Clear();
        }

        // *Workaround*
        // Unfortunately prefabs do not always send out all the necessary events
        // so we need to go through all the nodes and assume they've changed
        public static void OnPrefabInstanceUpdated(GameObject instance)
        {
            var nodes = instance.GetComponentsInChildren<ChiselNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (registeredNodes.Contains(node))
                    OnTransformParentChanged(node);
                else
                    Register(node);
            }

            // Figure out if a components have been removed or created
            prefabInstanceUpdatedEvent = true;
        }

        public static int RootCount(Scene scene)
        {
            ChiselSceneHierarchy hierarchy;
            if (!sceneHierarchies.TryGetValue(scene, out hierarchy))
                return 0;
            return hierarchy.RootItems.Count;
        }

        public static bool IsNodeDirty(ChiselNode component)
        {
            if (!component)
                return false;

            if (!registerQueueLookup.Add(component))
                return false;

            return registerQueue.Contains(component) ||
                    hierarchyUpdateQueue.Contains(component);
        }

        public static void Register(ChiselNode component)
        {
            if (!component)
                return;

            // NOTE: this method is called from constructor and cannot use Debug.Log, get Transforms etc.

            if (unregisterQueueLookup.Remove(component))
                unregisterQueue.Remove(component);

            if (registerQueueLookup.Add(component))
            {
                if (addToHierarchyLookup.Remove(component))
                    addToHierarchyQueue.Remove(component.hierarchyItem);
                registerQueue.Add(component);
            }

            component.hierarchyItem.Registered = true;
        }

        public static void Unregister(ChiselNode component)
        {
            // NOTE: this method is called from destructor and cannot use Debug.Log, get Transforms etc.

            if (registerQueueLookup.Remove(component))
            {
                registerQueue.Remove(component);
            }

            if (unregisterQueueLookup.Add(component))
            {
                ChiselHierarchyItem hierarchyItem;
                // we can't get the hierarchyItem from our component since it might've already been destroyed
                if (addToHierarchyLookup.TryGetValue(component, out hierarchyItem))
                {
                    addToHierarchyLookup.Remove(component);
                    addToHierarchyQueue.Remove(component.hierarchyItem);
                }

                unregisterQueue.Add(component);
            }

            if (ReferenceEquals(component, null))
                return;

            var children = component.hierarchyItem.Children;
            for (int n = 0; n < children.Count; n++)
            {
                if (!children[n].Component || !children[n].Component.IsActive)
                    continue;
                hierarchyUpdateQueue.Add(children[n].Component);
                updateTransformationNodes.Add(children[n].Component);
            }

            component.hierarchyItem.Registered = false;
        }

        public static void UpdateAvailability(ChiselNode node)
        {
            if (!node.IsActive)
            {
                ChiselNodeHierarchyManager.Unregister(node);
            } else
            {
                ChiselNodeHierarchyManager.Register(node);
            }
        }

        public static void OnTransformParentChanged(ChiselNode node)
        {
            if (!node ||
                !node.hierarchyItem.Registered ||
                !node.IsActive)
                return;
            hierarchyUpdateQueue.Add(node);
        }


        // Let the hierarchy manager know that this/these node(s) has/have moved, so we can regenerate meshes
        public static void RebuildTreeNodes(ChiselNode node) { rebuildTreeNodes.Add(node); }
        public static void UpdateTreeNodeTransformation(ChiselNode node) { updateTransformationNodes.Add(node); }
        public static void NotifyTransformationChanged(HashSet<ChiselNode> nodes) { foreach (var node in nodes) updateTransformationNodes.Add(node); }
        public static void UpdateAllTransformations() { foreach (var node in registeredNodes) updateTransformationNodes.Add(node); }


        // Let the hierarchy manager know that the contents of this node has been modified
        //	so we can rebuild/update sub-trees and regenerate meshes
        public static void NotifyContentsModified(ChiselNode node)
        {
            node.hierarchyItem.SetBoundsDirty();
            UpdateGeneratedBrushes(node);
        }

/*
        private static void OnBrushMeshInstanceChanged(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!generatedBrushNodes.TryGetValue(brushContainerAsset, out var nodes))
                return;
            
            foreach (var node in nodes)
            {
                if (node)
                {
                    node.UpdateBrushMeshInstances();
                    hierarchyUpdateQueue.Add(node);
                    updateTransformationNodes.Add(node);
                }
            }
        }

        private static void OnBrushMeshInstanceDestroyed(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (generatedBrushNodes.TryGetValue(brushContainerAsset, out var nodes))
            {
                foreach (var node in nodes)
                {
                    if (!node)
                        continue;

                    HashSet<ChiselBrushContainerAsset> uniqueGeneratedBrushes;
                    if (nodegeneratedBrush.TryGetValue(node, out uniqueGeneratedBrushes))
                        uniqueGeneratedBrushes.Remove(brushContainerAsset);
                }
            }
            generatedBrushNodes.Remove(brushContainerAsset);
        }

        static List<ChiselBrushContainerAsset> nodeGeneratedBrushes = new List<ChiselBrushContainerAsset>();


        static HashSet<ChiselBrushContainerAsset> RemoveBrushReference(ChiselNode node)
        {
            if (nodegeneratedBrush.TryGetValue(node, out var uniqueGeneratedBrushes))
            {
                // Remove previously set brushMeshes for this node
                foreach (var brushContainerAsset in uniqueGeneratedBrushes)
                {
                    if (generatedBrushNodes.TryGetValue(brushContainerAsset, out var nodes))
                        nodes.Remove(node);
                }
                uniqueGeneratedBrushes.Clear();
            } else
                uniqueGeneratedBrushes = new HashSet<ChiselBrushContainerAsset>();
            return uniqueGeneratedBrushes;
        }*/

        static void UpdateGeneratedBrushes(ChiselNode node)
        {/*
            if (!node)
            {
                RemoveBrushReference(node);
                return;
            }

            nodeGeneratedBrushes.Clear();
            if (!node.GetUsedGeneratedBrushes(nodeGeneratedBrushes))
                return;

            var uniqueGeneratedBrushes = RemoveBrushReference(node);
            for (int i = 0; i < nodeGeneratedBrushes.Count; i++)
            {
                var brushContainerAsset = nodeGeneratedBrushes[i];
                if (object.Equals(brushContainerAsset, null))
                    continue;

                // Add current brushMesh of this node
                if (uniqueGeneratedBrushes.Add(brushContainerAsset))
                {
                    if (!generatedBrushNodes.TryGetValue(brushContainerAsset, out var nodes))
                        generatedBrushNodes[brushContainerAsset] = nodes = new HashSet<ChiselNode>();
                    nodes.Add(node);
                }
            }
            nodegeneratedBrush[node] = uniqueGeneratedBrushes;*/
            node.UpdateBrushMeshInstances();
        }

        static void RemoveGeneratedBrushes(ChiselNode node)
        {/*
            // NOTE: node is likely destroyed at this point, it can still be used as a lookup key however.

            HashSet<ChiselBrushContainerAsset> nodeGeneratedBrushes;
            if (nodegeneratedBrush.TryGetValue(node, out nodeGeneratedBrushes))
            {
                // Remove previously set brushMeshes for this node
                foreach (var brushContainerAsset in nodeGeneratedBrushes)
                {
                    if (generatedBrushNodes.TryGetValue(brushContainerAsset, out var nodes))
                        nodes.Remove(node);
                }
                nodeGeneratedBrushes.Clear();
            }
            nodegeneratedBrush.Remove(node);*/
        }

        public static void OnTransformChildrenChanged(ChiselNode component)
        {
            if (ignoreNextChildrenChanged)
            {
                ignoreNextChildrenChanged = false;
                return;
            }

            if (onHierarchyChangeCalled.Contains(component))
                return;

            onHierarchyChangeCalled.Add(component);

            if (!component ||
                !component.hierarchyItem.Registered ||
                !component.IsActive)
                return;

            var children = component.hierarchyItem.Children;
            for (int i = 0; i < children.Count; i++)
            {
                var childComponent = children[i].Component;
                if (!childComponent || !childComponent.IsActive)
                {
                    continue;
                }
                hierarchyUpdateQueue.Add(childComponent);
                onHierarchyChangeCalled.Add(component);
            }
        }

        // Find parent node & update siblingIndices for each level
        static ChiselNode UpdateSiblingIndices(ChiselHierarchyItem hierarchyItem)
        {
            var transform = hierarchyItem.Transform;
            if (!transform)
                return null;

            var parent = transform.parent;

            hierarchyItem.SiblingIndices.Clear();
            hierarchyItem.SiblingIndices.Add(transform.GetSiblingIndex());

            if (ReferenceEquals(parent, null))
                return null;

            // Find siblingIndexs up the parents, until we find a ChiselNode
            if (componentLookup.TryGetValue(parent, out ChiselNode parentComponent)
                && !parentComponent.IsContainer)
                parentComponent = null;
            while (ReferenceEquals(parentComponent, null))
            {
                hierarchyItem.SiblingIndices.Insert(0, parent.GetSiblingIndex());

                parent = parent.parent;
                if (ReferenceEquals(parent, null))
                    break;

                if (componentLookup.TryGetValue(parent, out parentComponent) && !parentComponent.IsContainer)
                    parentComponent = null;
            }

            return parentComponent;
        }

        static List<GameObject> __rootGameObjects = new List<GameObject>(); // static to avoid allocations
        static Queue<Transform> __transforms = new Queue<Transform>(); // static to avoid allocations
        static void UpdateChildren(Transform rootTransform)
        {
            __transforms.Clear();
            if (rootTransform.parent == null &&
                ChiselGeneratedComponentManager.IsDefaultModel(rootTransform))
            {
                // The default model is special in the sense that, unlike all other models, it doesn't
                // simply contain all the nodes that are its childrens. Instead, it contains all the nodes 
                // that do not have a model as a parent. So we go through the hierarchy and find the top
                // level nodes that are not a model
                var scene = rootTransform.gameObject.scene;
                scene.GetRootGameObjects(__rootGameObjects);
                for (int i = 0; i < __rootGameObjects.Count; i++)
                {
                    var childTransform = __rootGameObjects[i].transform;
                    var childNode = childTransform.GetComponentInChildren<ChiselNode>();
                    if (!childNode)
                        continue;
                    if (childNode is ChiselModel)
                        continue;
                    __transforms.Enqueue(childTransform);
                    hierarchyUpdateQueue.Add(childNode);
                }
            } else
                __transforms.Enqueue(rootTransform);

            while (__transforms.Count > 0)
            {
                var transform = __transforms.Dequeue();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var childTransform = transform.GetChild(i);
                    var childNode = childTransform.GetComponent<ChiselNode>();
                    if (!childNode || !childNode.IsActive)
                    {
                        __transforms.Enqueue(childTransform);
                        continue;
                    }
                    hierarchyUpdateQueue.Add(childNode);
                }
            }
            __transforms.Clear();
        }

        static Queue<List<ChiselHierarchyItem>> __hierarchyQueueLists = new Queue<List<ChiselHierarchyItem>>(); // static to avoid allocations
        static void SetChildScenes(ChiselHierarchyItem hierarchyItem, Scene scene)
        {
            // Note: we're only setting the scene here.
            //		 we're not updating the hierarchy, that's done in hierarchyUpdateQueue/addToHierarchyQueue
            __hierarchyQueueLists.Clear();
            __hierarchyQueueLists.Enqueue(hierarchyItem.Children);
            while (__hierarchyQueueLists.Count > 0)
            {
                var children = __hierarchyQueueLists.Dequeue();
                for (int i = 0; i < children.Count; i++)
                {
                    var childItem = children[i];
                    var childNode = childItem.Component;
                    if (!childNode || !childNode.IsActive)
                    {
                        __hierarchyQueueLists.Enqueue(childItem.Children);
                        continue;
                    }
                    if (childItem.Scene != scene)
                    {
                        childItem.Scene = scene;
                        hierarchyUpdateQueue.Add(childNode);
                    }
                }
            }
            __hierarchyQueueLists.Clear();
        }

        static void AddChildNodesToHashSet(HashSet<ChiselNode> allFoundChildren)
        {
            __hierarchyQueueLists.Clear();
            foreach (var node in allFoundChildren)
                __hierarchyQueueLists.Enqueue(node.hierarchyItem.Children);
            while (__hierarchyQueueLists.Count > 0)
            {
                var children = __hierarchyQueueLists.Dequeue();
                for (int i = 0; i < children.Count; i++)
                {
                    var childItem = children[i];
                    var childNode = childItem.Component;
                    if (!allFoundChildren.Add(childNode))
                        continue;
                    __hierarchyQueueLists.Enqueue(childItem.Children);
                }
            }
            __hierarchyQueueLists.Clear();
        }

        static void RegisterInternal(ChiselNode component)
        {
            if (!component)
                return;

            int index = addToHierarchyQueue.Count;
            var parent = component.hierarchyItem.Transform ? component.hierarchyItem.Transform : component.transform;
            var parentComponent = component;
            do
            {
                if (parentComponent &&
                    parentComponent.IsActive)
                {
                    if (registeredNodes.Add(parentComponent))
                    {
                        var instanceID = parentComponent.GetInstanceID();
                        nodeToinstanceIDLookup[parentComponent] = instanceID;
                        instanceIDToNodeLookup[instanceID] = parentComponent;
                        var parentHierarchyItem = parentComponent.hierarchyItem;

                        addToHierarchyLookup[parentComponent] = parentHierarchyItem;
                        addToHierarchyQueue.Insert(index, parentHierarchyItem);

                        UpdateGeneratedBrushes(parentComponent);
                    }
                }
                parent = parent.parent;
                if (parent == null)
                    break;
                parentComponent = parent.GetComponent<ChiselNode>();
            } while (true);
        }

        static bool RemoveFromHierarchy(List<ChiselHierarchyItem> rootItems, ChiselNode component)
        {
            __hierarchyQueueLists.Clear();
            __hierarchyQueueLists.Enqueue(rootItems);
            try
            {
                while (__hierarchyQueueLists.Count > 0)
                {
                    var children = __hierarchyQueueLists.Dequeue();
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i].Component == component)
                        {
                            children.RemoveAt(i);
                            return true;
                        }
                    }
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i].Children == null)
                            continue;

                        children[i].SetChildBoundsDirty();
                        __hierarchyQueueLists.Enqueue(children[i].Children);
                    }
                }
            }
            finally
            {
                __hierarchyQueueLists.Clear();
            }
            return false;
        }

        static void UnregisterInternal(ChiselNode component)
        {
            if (!registeredNodes.Remove(component))
                return;

            var instanceID = nodeToinstanceIDLookup[component];
            nodeToinstanceIDLookup.Remove(component);
            instanceIDToNodeLookup.Remove(instanceID);

            var sceneHierarchy = component.hierarchyItem.sceneHierarchy;
            if (sceneHierarchy == null)
                return;

            var rootItems = sceneHierarchy.RootItems;
            RemoveFromHierarchy(rootItems, component);

            if (rootItems.Count == 0)
            {
                sceneHierarchies.Remove(sceneHierarchy.Scene);
            }

            RemoveGeneratedBrushes(component);
        }

        static readonly List<GameObject> rootObjects = new List<GameObject>();
        static readonly List<ChiselNode> children = new List<ChiselNode>();

        static void FindAndReregisterAllNodes()
        {
            Reset();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                rootObjects.Clear();
                scene.GetRootGameObjects(rootObjects);
                for (int r = 0; r < rootObjects.Count; r++)
                {
                    var rootObject = rootObjects[r];
                    children.Clear();
                    rootObject.GetComponentsInChildren<ChiselNode>(includeInactive: false, children);
                    for (int n = 0; n < children.Count; n++)
                    {
                        var node = children[n];
                        if (node.isActiveAndEnabled)
                            Register(node);
                    }
                }
            }
            children.Clear();
            rootObjects.Clear();
        }

        public static void Update()
        {
            int loops = 0;
UpdateAgain:
            reregisterModelQueue.Clear();

            // Unfortunately we might need to create default models during the update loop, which kind of screws up the order of things.
            // so we remember them, and re-register at the end.
            bool tryAgain = false;

            try
            {
                bool haveCreatedTreeNodes = false;
                Profiler.BeginSample("UpdateTrampoline");
                haveCreatedTreeNodes = UpdateTrampoline();
                Profiler.EndSample();

                // TODO: fix that generators create brushes inside the trampoline, requiring us to call things twice
                if (haveCreatedTreeNodes)
                {
                    Profiler.BeginSample("UpdateTrampoline");
                    try { UpdateTrampoline(); }
                    finally { Profiler.EndSample(); }
                }
            }
            // If we get an exception we don't want to end up infinitely spawning this exception ..
            finally
            {
                ClearTemporaries();
                ClearQueues();
            }

            if (reregisterModelQueue.Count > 0)
            {
                for (int i = 0; i < reregisterModelQueue.Count; i++)
                {
                    reregisterModelQueue[i].hierarchyItem.Registered = false;
                    Register(reregisterModelQueue[i]);
                }
                tryAgain = true;
            }

            if (createDefaultModels.Count > 0)
                tryAgain = true;

            if (deferAddToHierarchyQueue.Count > 0)
                tryAgain = true;

            if (tryAgain)
            {
                loops++;
                if (loops > 2) // defense against infinite loop bugs
                    return;
                goto UpdateAgain;
            }
        }

        // Returns true when the number of brushes has been changed
        static bool CreateOrDestroyTreeNodes(ChiselNode node)
        {
            Profiler.BeginSample("CreateOrDestroyTreeNodes");

            // Create the treeNodes for this node
            Profiler.BeginSample("ClearTreeNodes");
            node.ResetTreeNodes();
            Profiler.EndSample();
            Profiler.BeginSample("CreateTreeNodes");
            var createdTreeNode = node.CreateTreeNode();
            Profiler.EndSample();
            if (createdTreeNode.Valid)
                treeNodeLookup[node] = createdTreeNode;
            else
                treeNodeLookup.Remove(node);
            Profiler.EndSample();
            return true;
        }

        public static ChiselSceneHierarchy GetSceneHierarchyForScene(Scene scene)
        {
            if (sceneHierarchies.TryGetValue(scene, out ChiselSceneHierarchy sceneHierarchy))
                return sceneHierarchy;

            return sceneHierarchies[scene] = new ChiselSceneHierarchy { Scene = scene };
        }

        public static ChiselModel GetDefaultModelForScene(Scene scene)
        {
            var sceneHierarchy = GetSceneHierarchyForScene(scene);
            if (!sceneHierarchy.Scene.IsValid() ||
                !sceneHierarchy.Scene.isLoaded)
                return null;

            if (!sceneHierarchy.DefaultModel)
                sceneHierarchy.DefaultModel = ChiselGeneratedComponentManager.CreateDefaultModel(sceneHierarchy);

            return sceneHierarchy.DefaultModel;
        }


        static readonly HashSet<ChiselNode> __registerNodes = new HashSet<ChiselNode>();	// static to avoid allocations
        static readonly HashSet<ChiselNode> __unregisterNodes = new HashSet<ChiselNode>();	// static to avoid allocations
        static readonly List<CSGTreeNode> __childNodes = new List<CSGTreeNode>(5000);  // static to avoid allocations
        static readonly List<ChiselNode> __prevUpdateQueue = new List<ChiselNode>();
        static readonly List<KeyValuePair<Scene, ChiselSceneHierarchy>> __prevSceneHierarchy = new List<KeyValuePair<Scene, ChiselSceneHierarchy>>();

        internal static bool prevPlaying = false;
        internal static bool UpdateTrampoline()
        {
            Profiler.BeginSample("UpdateTrampoline.Setup");
            bool haveModifiedTreeNodeCount = false;
            if (createDefaultModels.Count > 0)
            {
                foreach (var sceneHierarchy in createDefaultModels)
                {
                    if (sceneHierarchy.DefaultModel ||
                        !sceneHierarchy.Scene.IsValid() ||
                        !sceneHierarchy.Scene.isLoaded)
                        continue;
                    sceneHierarchy.DefaultModel = ChiselGeneratedComponentManager.CreateDefaultModel(sceneHierarchy);
                }
                createDefaultModels.Clear();
            }

            // Used to defers the contents of addToHierarchyQueue to the next tick
            if (deferAddToHierarchyQueue.Count > 0)
            {
                addToHierarchyQueue.AddRange(deferAddToHierarchyQueue);
                deferAddToHierarchyQueue.Clear();
            }


#if UNITY_EDITOR
            // *Workaround*
            // Events are not properly called, and can be even duplicated, on entering and exiting playmode
            var currentPlaying = UnityEditor.EditorApplication.isPlaying;
            if (currentPlaying != prevPlaying)
            {
                prevPlaying = currentPlaying;
                return false;
            }
#endif
            // *Workaround*
            // It's possible that some events aren't properly called at UnityEditor startup (possibly runtime as well)
            if (!firstStart)
            {
                firstStart = true;
                Chisel.Core.CompactHierarchyManager.Clear();
                //ChiselBrushContainerAssetManager.Reset();

                // Prefabs can fire events that look like objects have been loaded/created ..
                // Also, starting up in the editor can swallow up events and cause some nodes to not be registered properly
                // So to ensure that the first state is correct, we get it explicitly
                FindAndReregisterAllNodes();
                //ChiselBrushContainerAssetManager.Update();
#if UNITY_EDITOR
                ChiselGeneratedComponentManager.OnVisibilityChanged();
#endif
            }

            // *Workaround*
            // Unity has no event to tell us if an object has moved between scenes
            // fortunately, when you change the parent of a gameobject, we get that event.
            // We still need to check the highest level objects though.
            foreach (var pair in sceneHierarchies)
            {
                var defaultModel = pair.Value.DefaultModel;
                if (defaultModel)
                {
                    var defaultChildren = defaultModel.hierarchyItem.Children;
                    var expectedScene = pair.Key;
                    for (int n = defaultChildren.Count - 1; n >= 0; n--)
                    {
                        if (!defaultChildren[n].GameObject)
                        {
                            var rootComponent = defaultChildren[n].Component;
                            defaultChildren.RemoveAt(n);      // prevent potential infinite loops
                            Unregister(rootComponent);  // .. try to clean this up
                            continue;
                        }

                        if (defaultChildren[n].GameObject.scene == expectedScene &&
                            defaultChildren[n].Scene == expectedScene)
                            continue;

                        var component = defaultChildren[n].Component;
                        updateChildrenQueue.Add(defaultModel.hierarchyItem);
                        OnTransformParentChanged(component);
                    }
                }
                var rootItems = pair.Value.RootItems;
                if (rootItems.Count > 0)
                {
                    var expectedScene = pair.Key;
                    for (int n = rootItems.Count - 1; n >= 0; n--)
                    {
                        if (!rootItems[n].GameObject)
                        {
                            var rootComponent = rootItems[n].Component;
                            rootItems.RemoveAt(n);      // prevent potential infinite loops
                            Unregister(rootComponent);  // .. try to clean this up
                            continue;
                        }

                        if (rootItems[n].GameObject.scene == expectedScene &&
                            rootItems[n].Scene == expectedScene)
                            continue;

                        var component = rootItems[n].Component;
                        OnTransformParentChanged(component);
                    }
                }
            }

            // *Workaround*
            // Prefabs can fire events that look like objects have been loaded/created ..
            // So we're forced to filter them out
            if (prefabInstanceUpdatedEvent)
            {
                prefabInstanceUpdatedEvent = false;
                for (int i = registerQueue.Count - 1; i >= 0; i--)
                {
                    var component = registerQueue[i];
                    if (component)
                    {
#if UNITY_EDITOR

                        if (UnityEditor.PrefabUtility.GetPrefabAssetType(component) != UnityEditor.PrefabAssetType.Regular)

#endif
                            continue;
                    }
                    registerQueue.RemoveAt(i);
                }

                var knownNodes = registeredNodes.ToArray();
                for (int i = 0; i < knownNodes.Length; i++)
                {
                    var knownNode = knownNodes[i];
                    if (knownNode)
                        continue;

                    Unregister(knownNode);
                }
            }

            if (registerQueue.Count != 0)
            {
                for (int i = registerQueue.Count - 1; i >= 0; i--)
                {
                    if (!registerQueue[i] ||
                        !registerQueue[i].isActiveAndEnabled)
                        registerQueue.RemoveAt(i);
                }
            }
            Profiler.EndSample();


            if (registerQueue.Count == 0 &&
                unregisterQueue.Count == 0 &&
                sortChildrenQueue.Count == 0 &&
                findChildrenQueue.Count == 0 &&
                hierarchyUpdateQueue.Count == 0 &&
                updateChildrenQueue.Count == 0 &&
                addToHierarchyQueue.Count == 0 &&
                updateTransformationNodes.Count == 0 &&
                destroyNodesList.Count == 0 &&
                rebuildTreeNodes.Count == 0)
                return false;

            __registerNodes   .Clear();
            __unregisterNodes .Clear();

            if (rebuildTreeNodes.Count > 0)
            {
                foreach (var component in rebuildTreeNodes)
                {
                    if (!unregisterQueueLookup.Contains(component))
                    {
                        unregisterQueue.Add(component);
                    }
                    if (!registerQueueLookup.Contains(component))
                    {
                        registerQueue.Add(component);
                    }
                }
            }

            Profiler.BeginSample("UpdateTrampoline.unregisterQueue");
            if (unregisterQueue.Count > 0)
            {
                for (int i = 0; i < unregisterQueue.Count; i++)
                {
                    var node = unregisterQueue[i];

                    // Remove any treeNodes that are part of the components we're trying to unregister 
                    // (including components that may have been already destroyed)
                    CSGTreeNode createdTreeNode;
                    if (treeNodeLookup.TryGetValue(node, out createdTreeNode))
                    {
                        if (createdTreeNode.Valid)
                            destroyNodesList.Add(createdTreeNode);
                        treeNodeLookup.Remove(node);
                    }
                }

                for (int i = 0; i < unregisterQueue.Count; i++)
                {
                    var node = unregisterQueue[i];
                    ChiselHierarchyItem hierarchyItem;
                    if (hierarchyItemLookup.TryGetValue(node, out hierarchyItem))
                    {
                        __unregisterNodes.Add(node);

                        hierarchyItemLookup.Remove(node);
                        if (hierarchyItem.Transform != null)
                            componentLookup.Remove(hierarchyItem.Transform);

                        var parentHierarchyItem	= hierarchyItem.Parent;
                        if (parentHierarchyItem != null)
                        {
                            if (parentHierarchyItem.Parent == null &&
                                ChiselGeneratedComponentManager.IsDefaultModel(parentHierarchyItem.Component))
                            {
                                var hierarchy = hierarchyItem.sceneHierarchy;
                                if (hierarchy != null)
                                    hierarchy.RootItems.Remove(hierarchyItem);
                            }

                            parentHierarchyItem.SetChildBoundsDirty();
                            parentHierarchyItem.Children.Remove(hierarchyItem);
                            if (parentHierarchyItem.Component &&
                                parentHierarchyItem.Component.IsActive)
                            {
                                updateChildrenQueue.Add(parentHierarchyItem);
                                if (parentHierarchyItem.Children.Count > 0)
                                {
                                    sortChildrenQueue.Add(parentHierarchyItem.Children);
                                }
                            }
                        } else
                        {
                            var hierarchy = hierarchyItem.sceneHierarchy;
                            if (hierarchy != null)
                            {
                                hierarchy.RootItems.Remove(hierarchyItem);
                                sortChildrenQueue.Add(hierarchy.RootItems);
                            }
                        }
                    }
                }

                for (int i = 0; i < unregisterQueue.Count; i++)
                {
                    var node = unregisterQueue[i];
                    if (!node)
                        continue;
                
                    node.hierarchyItem.Transform = null;
                    node.ResetTreeNodes();
                    UnregisterInternal(node);
                }
                
                unregisterQueue.Clear();
                unregisterQueueLookup.Clear();
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("UpdateTrampoline.registerQueue");
            if (registerQueue.Count > 0)
            {
                Profiler.BeginSample("UpdateTrampoline.registerQueue.A");
                for (int i = 0; i < registerQueue.Count; i++)
                {
                    var node = registerQueue[i];

                    // Remove any treeNodes that are part of the components we're trying to register 
                    // (including components that may have been already destroyed)
                    CSGTreeNode createdTreeNode;
                    if (treeNodeLookup.TryGetValue(node, out createdTreeNode))
                    {
                        if (createdTreeNode.Valid)
                            destroyNodesList.Add(createdTreeNode);
                        treeNodeLookup.Remove(node);
                    }
                }
                Profiler.EndSample();
            
                // Initialize the components
                Profiler.BeginSample("UpdateTrampoline.registerQueue.B");
                for (int i = registerQueue.Count - 1; i >= 0; i--) // reversed direction because we're also potentially removing items
                {
                    var node = registerQueue[i];
                    if (!node ||			// component might've been destroyed between adding it to the registerQueue and here
                        !node.IsActive)	// component might be active/enabled etc.
                    {
                        registerQueue.RemoveAt(i);
                        continue;
                    }

                    { 
                        var hierarchyItem	= node.hierarchyItem;
                        var transform		= hierarchyItem.Transform;
                        if (ReferenceEquals(transform, null))
                        {
                            hierarchyItem.Transform				= transform = node.transform;
                            hierarchyItem.GameObject			= node.gameObject;
                            hierarchyItem.Scene					= hierarchyItem.GameObject.scene;
                            hierarchyItem.LocalToWorldMatrix	= hierarchyItem.Transform.localToWorldMatrix;
                            hierarchyItem.WorldToLocalMatrix	= hierarchyItem.Transform.worldToLocalMatrix;
                            findChildrenQueue.Add(hierarchyItem);
                        }
                        updateTransformationNodes.Add(node);
                        componentLookup[transform]	= node;
                        hierarchyItemLookup[node]	= hierarchyItem;
                        if (__unregisterNodes.Contains(node))
                        {
                            // we removed it before we added it, so nothing has actually changed
                            __unregisterNodes.Remove(node);
                        } else
                            __registerNodes.Add(node); 
                    }

                    haveModifiedTreeNodeCount = CreateOrDestroyTreeNodes(node) || haveModifiedTreeNodeCount;
                }
                Profiler.EndSample();
                 
                // Separate loop to ensure all parent components are already initialized
                // this is because the order of the registerQueue is essentially random
                Profiler.BeginSample("UpdateTrampoline.registerQueue.C");
                for (int i = 0; i < registerQueue.Count; i++)
                {
                    var node = registerQueue[i];
                    RegisterInternal(node);
                }
                Profiler.EndSample();

                Profiler.BeginSample("UpdateTrampoline.registerQueue.D");
                for (int i = 0; i < registerQueue.Count; i++)
                {
                    var node = registerQueue[i];
                    if (!addToHierarchyLookup.ContainsKey(node))
                    {
                        addToHierarchyLookup.Add(node, node.hierarchyItem);
                        addToHierarchyQueue.Add(node.hierarchyItem);
                    }
                }
                Profiler.EndSample();

                registerQueue.Clear();
                registerQueueLookup.Clear();
            }
            Profiler.EndSample();

            Profiler.BeginSample("UpdateTrampoline.findChildrenQueue");
            if (findChildrenQueue.Count > 0)
            {
                for (int i = 0; i < findChildrenQueue.Count; i++)
                {
                    var hierarchyItem = findChildrenQueue[i];
                    UpdateChildren(hierarchyItem.Transform);
                }
                findChildrenQueue.Clear();
            }
            Profiler.EndSample();
        
            Profiler.BeginSample("UpdateTrampoline.hierarchyUpdateQueue");
            if (hierarchyUpdateQueue.Count > 0)
            {
                __prevUpdateQueue.Clear();
                __prevUpdateQueue.AddRange(hierarchyUpdateQueue);
                for (int i = 0; i < __prevUpdateQueue.Count; i++)
                {
                    var component = __prevUpdateQueue[i];
                    if (!component ||
                        !component.IsActive)
                        continue;

                    var hierarchyItem	= component.hierarchyItem;
                    if (hierarchyItem.GameObject == null)
                    {
                        hierarchyItem.Transform			 = component.transform;
                        hierarchyItem.GameObject		 = component.gameObject;
                        hierarchyItem.Scene				 = hierarchyItem.GameObject.scene;
                        hierarchyItem.LocalToWorldMatrix = hierarchyItem.Transform.localToWorldMatrix;
                        hierarchyItem.WorldToLocalMatrix = hierarchyItem.Transform.worldToLocalMatrix;
                        UpdateChildren(hierarchyItem.Transform);
                    } else
                    {
                        // Determine if our node has been moved to another scene
                        var currentScene = hierarchyItem.GameObject.scene;
                        if (currentScene != hierarchyItem.Scene)
                        {
                            hierarchyItem.Scene = currentScene;
                            SetChildScenes(hierarchyItem, currentScene);
                        }
                    }
                }
                
                foreach (var component in hierarchyUpdateQueue)
                {
                    if (!component)
                        continue;

                    // make sure we update our old parent
                    var hierarchyItem		= component.hierarchyItem;
                    var parentHierarchyItem	= hierarchyItem.Parent;
                    if (parentHierarchyItem != null)
                    {
                        if (parentHierarchyItem.Parent == null &&
                            ChiselGeneratedComponentManager.IsDefaultModel(parentHierarchyItem.Component))
                        {
                            var hierarchy = hierarchyItem.sceneHierarchy;
                            if (hierarchy != null)
                                hierarchy.RootItems.Remove(hierarchyItem);
                        }

                        parentHierarchyItem.SetChildBoundsDirty();
                        parentHierarchyItem.Children.Remove(hierarchyItem);
                        if (parentHierarchyItem.Component &&
                            parentHierarchyItem.Component.IsActive)
                        {
                            updateChildrenQueue.Add(parentHierarchyItem);
                            if (parentHierarchyItem.Children.Count > 0)
                            {
                                sortChildrenQueue.Add(parentHierarchyItem.Children);
                            }
                        }
                    } else
                    {
                        var hierarchy = hierarchyItem.sceneHierarchy;
                        if (hierarchy != null)
                        {
                            hierarchy.RootItems.Remove(hierarchyItem);
                            sortChildrenQueue.Add(hierarchy.RootItems);
                        }
                    }

                    if (!addToHierarchyLookup.ContainsKey(hierarchyItem.Component))
                    {
                        addToHierarchyLookup.Add(hierarchyItem.Component, hierarchyItem);
                        addToHierarchyQueue.Add(hierarchyItem);
                    }
                }
                hierarchyUpdateQueue.Clear();
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("UpdateTrampoline.addToHierarchyQueue");
            if (addToHierarchyQueue.Count > 0)
            {
                for (int i = 0; i < addToHierarchyQueue.Count; i++)
                { 
                    var hierarchyItem = addToHierarchyQueue[i];
                    if (!hierarchyItem.Component ||
                        !hierarchyItem.Component.IsActive)
                        continue;
        
                    var sceneHierarchy = GetSceneHierarchyForScene(hierarchyItem.Scene);
                    hierarchyItem.sceneHierarchy = sceneHierarchy;

                    var defaultModel = false;
                    var parentComponent = UpdateSiblingIndices(hierarchyItem);
                    if (ReferenceEquals(parentComponent, null))
                    {
                        if (!(hierarchyItem.Component is ChiselModel))
                        {
                            if (!sceneHierarchy.DefaultModel)
                            {
                                createDefaultModels.Add(sceneHierarchy);
                                // defer adding the item to the next tick because the default model has not been created yet
                                deferAddToHierarchyQueue.Add(hierarchyItem);
                                continue;
                            }

                            parentComponent = sceneHierarchy.DefaultModel;
                            defaultModel = true;
                        }
                    }

                    if (!ReferenceEquals(parentComponent, null))
                    {
                        var parentHierarchyItem = parentComponent.hierarchyItem;
                        hierarchyItem.Parent = parentHierarchyItem;

                        if (defaultModel)
                        {
                            if (sceneHierarchy.RootItems.Contains(hierarchyItem))
                                sceneHierarchy.RootItems.Remove(hierarchyItem);
                        }

                        if (!parentHierarchyItem.Children.Contains(hierarchyItem))
                        {
                            parentHierarchyItem.SetChildBoundsDirty();
                            parentHierarchyItem.Children.Add(hierarchyItem);
                        }
                        
                        var iterator = parentHierarchyItem;
                        do
                        {
                            if (iterator.Children.Count > 0)
                            {
                                sortChildrenQueue.Add(iterator.Children);
                            }
                            if (iterator.Component.IsContainer) 
                            {
                                updateChildrenQueue.Add(iterator);
                                break;
                            }
                            iterator = iterator.Parent;
                        } while (iterator != null);
                    } else
                    { 
                        hierarchyItem.Parent = null;
                        if (!sceneHierarchy.RootItems.Contains(hierarchyItem))
                        {
                            sceneHierarchy.RootItems.Add(hierarchyItem);
                        }
                        
                        sortChildrenQueue.Add(sceneHierarchy.RootItems);
                    }
                }
                addToHierarchyQueue.Clear();
                addToHierarchyLookup.Clear();
            }
            Profiler.EndSample();
             
            Profiler.BeginSample("UpdateTrampoline.sortChildrenQueue");
            if (sortChildrenQueue.Count > 0)
            {
                foreach (var items in sortChildrenQueue)
                {
                    if (items.Count == 0)
                        continue;

                    items.Sort(delegate (ChiselHierarchyItem x, ChiselHierarchyItem y)
                    {
                        var xIndices = x.SiblingIndices;
                        var yIndices = y.SiblingIndices;
                        var count = Mathf.Min(xIndices.Count, yIndices.Count);
                        for (int i = 0; i < count; i++)
                        {
                            var difference = xIndices[i].CompareTo(yIndices[i]);
                            if (difference != 0)
                                return difference;
                        }
                        return 0;
                    });
                }
                sortChildrenQueue.Clear();
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue");
            if (updateChildrenQueue.Count > 0)
            {
                foreach (var item in updateChildrenQueue)
                {
                    if (!item.Component)
                        continue;
                    
                    if (!item.Component.IsContainer)
                        continue;
                    
                    // TODO: create a virtual updateChildrenQueue list for the default model instead?
                    if (item.Children.Count == 0 &&
                        ChiselGeneratedComponentManager.IsDefaultModel(item.Component))
                    {
                        var itemModel = item.Component as ChiselModel;

                        // If the default model is empty, we'll destroy it to remove clutter
                        var scene = item.Scene;
                        ChiselSceneHierarchy sceneHierarchy;
                        if (sceneHierarchies.TryGetValue(scene, out sceneHierarchy))
                        {
                            if (sceneHierarchy.DefaultModel == itemModel)
                            {
                                sceneHierarchy.DefaultModel = null;
                            }
                            sceneHierarchy.RootItems.Remove(itemModel.hierarchyItem);
                        }
                        destroyNodesList.Add(itemModel.Node);
                        ChiselObjectUtility.SafeDestroy(item.GameObject);
                        continue;
                    }
                }
            }
            Profiler.EndSample();
        
            Profiler.BeginSample("UpdateTrampoline.destroyNodesList");
            if (destroyNodesList.Count > 0)
            {
                // Destroy all old nodes after we created new nodes, to make sure we don't get conflicting IDs
                // TODO: add 'generation' to indices to avoid needing to do this
                foreach (var item in destroyNodesList)
                {
                    if (item.Valid)
                        item.Destroy();
                }
                destroyNodesList.Clear();
            }
            Profiler.EndSample();
        
            Profiler.BeginSample("UpdateTrampoline.updateChildrenQueue2");
            if (updateChildrenQueue.Count > 0)
            {
                foreach (var item in updateChildrenQueue)
                {
                    if (!item.Component)
                        continue;

                    if (!item.Component.IsContainer)
                        continue;
                    
                    __childNodes.Clear();
                    GetChildrenOfHierachyItem(__childNodes, item);
                    if (__childNodes.Count == 0)
                        continue;
                    try 
                    {
                        var parentComponent = item.Component;
                        var parentNode = parentComponent.TopTreeNode;
                        if (!parentNode.Valid)
                        {
                            Debug.LogWarning($"SetChildren called on a {nameof(ChiselComposite)} that isn't properly initialized", parentComponent);
                        } else
                        {
                            if (!parentNode.SetChildren(__childNodes))
                                Debug.LogError($"Failed to assign list of children to {parentComponent.ChiselNodeTypeName}", parentComponent);
                        }
                    }
                    catch(Exception ex)
                    {
                        Debug.Log($"{item.Component}", item.Component);
                        Debug.LogException(ex, item.Component);
                    }
                }
                updateChildrenQueue.Clear();
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("UpdateTrampoline.updateTransformationNodes");
            if (updateTransformationNodes.Count > 0)
            {
                // Make sure we also update the child node matrices
                AddChildNodesToHashSet(updateTransformationNodes);
                foreach (var node in updateTransformationNodes)
                {
                    if (!node)
                        continue;
                    node.UpdateTransformation();
                    node.hierarchyItem.SetBoundsDirty();
                }
                if (TransformationChanged != null)
                    TransformationChanged();
                updateTransformationNodes.Clear();
            }
            Profiler.EndSample();

            Profiler.BeginSample("UpdateTrampoline.__unregisterNodes");
            if (__unregisterNodes.Count > 0)
            {
                foreach (var node in __unregisterNodes)
                    ChiselGeneratedModelMeshManager.Unregister(node);
                __unregisterNodes.Clear();
            }
            Profiler.EndSample();

            Profiler.BeginSample("UpdateTrampoline.__registerNodes");
            if (__registerNodes.Count > 0)
            {
                foreach (var node in __registerNodes)
                    ChiselGeneratedModelMeshManager.Register(node);
                __registerNodes.Clear();
            }
            Profiler.EndSample();

            Profiler.BeginSample("UpdateTrampoline.End");
            __registerNodes		.Clear();
            __unregisterNodes	.Clear();
            __childNodes		.Clear();

            __prevSceneHierarchy.Clear();
            __prevSceneHierarchy.AddRange(sceneHierarchies);
            for (int i = 0; i < __prevSceneHierarchy.Count; i++)
            {
                ChiselSceneHierarchy sceneHierarchy = __prevSceneHierarchy[i].Value;
                if (!sceneHierarchy.Scene.IsValid() ||
                    !sceneHierarchy.Scene.isLoaded ||
                    (sceneHierarchy.RootItems.Count == 0 && !sceneHierarchy.DefaultModel && !createDefaultModels.Contains(sceneHierarchy)))
                {
                    sceneHierarchies.Remove(__prevSceneHierarchy[i].Key);
                    createDefaultModels.Remove(sceneHierarchy);
                }
            }
            __prevSceneHierarchy.Clear();

            // Used to redraw windows etc.
            NodeHierarchyModified?.Invoke();
            Profiler.EndSample();
            return haveModifiedTreeNodeCount;
        }

        public static void GetChildrenOfHierachyItem(List<CSGTreeNode> childNodes, ChiselHierarchyItem item)
        {
            if (item == null)
                return;
            for (int i = 0; i < item.Children.Count; i++)
            {
                var childComponent = item.Children[i].Component;
                if (!childComponent && childComponent.IsActive)
                    continue;

                var topNode = childComponent.TopTreeNode;
                if (!topNode.Valid)
                    continue;

                childNodes.Add(topNode);
            }
        }

        public static Transform FindModelTransformOfTransform(Transform transform)
        {
            // TODO: optimize this
            do
            {
                if (!transform)
                    return null;
                var model = transform.GetComponentInParent<ChiselModel>();
                if (!model)
                    return null;
                transform = model.hierarchyItem.Transform;
                if (!transform)
                    return null;
                if (model.enabled)
                    return transform;
                transform = transform.parent;
            } while (true);
        }

        public static Matrix4x4 FindModelTransformMatrixOfTransform(Transform transform)
        {
            // TODO: optimize this
            do
            {
                if (!transform)
                    return Matrix4x4.identity;
                var model = transform.GetComponentInParent<ChiselModel>();
                if (!model)
                    return Matrix4x4.identity;
                transform = model.hierarchyItem.Transform;
                if (!transform)
                    return Matrix4x4.identity;
                if (model.enabled)
                    return transform.localToWorldMatrix;
                transform = transform.parent;
            } while (true);
        }

        public static ChiselNode FindChiselNodeByInstanceID(int instanceID)
        {
            instanceIDToNodeLookup.TryGetValue(instanceID, out var node);
            return node;
        }

        public static ChiselNode FindChiselNodeByTreeNode(CSGTreeNode node)
        {
            return FindChiselNodeByInstanceID(node.UserID);
        }
    }
}
