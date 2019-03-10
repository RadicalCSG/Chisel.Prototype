//#define BE_CHATTY
using Chisel.Assets;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
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
			- operation passthrough mode
			- undo/redo
			- mind prefabs
			- multiple scenes
				- also keep in mind never dirtying non modified scenes
			- default TreeNode per scene
*/
namespace Chisel.Components
{ 
	public static class CSGNodeHierarchyManager
	{
		public static readonly Dictionary<Scene, CSGSceneHierarchy> sceneHierarchies        = new Dictionary<Scene, CSGSceneHierarchy>();

		static readonly HashSet<CSGNode>                        registeredNodes             = new HashSet<CSGNode>();
		static readonly Dictionary<int, CSGNode>                instanceIDToNodeLookup      = new Dictionary<int, CSGNode>();
		static readonly Dictionary<CSGNode, int>                nodeToinstanceIDLookup      = new Dictionary<CSGNode, int>();
		
		// Note: keep in mind that these work even when components have already been destroyed
		static readonly Dictionary<Transform, CSGNode>          componentLookup             = new Dictionary<Transform, CSGNode>();
		static readonly Dictionary<CSGNode, CSGHierarchyItem>   hierarchyItemLookup         = new Dictionary<CSGNode, CSGHierarchyItem>();
		static readonly Dictionary<CSGNode, CSGTreeNode[]>      treeNodeLookup              = new Dictionary<CSGNode, CSGTreeNode[]>();

		static readonly HashSet<CSGNode>                        registerQueueLookup         = new HashSet<CSGNode>();
		static readonly List<CSGNode>                           registerQueue               = new List<CSGNode>();
		static readonly HashSet<CSGNode>                        unregisterQueueLookup       = new HashSet<CSGNode>();
		static readonly List<CSGNode>                           unregisterQueue             = new List<CSGNode>();
		
		static readonly HashSet<CSGSceneHierarchy>				createDefaultModels			= new HashSet<CSGSceneHierarchy>();
		
		// Unfortunately we might need to create default models during the update loop, which kind of screws up the order of things.
		// so we remember them, and re-register at the end.
		static readonly List<CSGModel>							reregisterModelQueue		= new List<CSGModel>();

		static readonly List<CSGHierarchyItem>                  findChildrenQueue           = new List<CSGHierarchyItem>();

		static readonly HashSet<CSGTreeNode>                    destroyNodesList            = new HashSet<CSGTreeNode>();

		static readonly Dictionary<CSGNode, CSGHierarchyItem>   addToHierarchyLookup        = new Dictionary<CSGNode, CSGHierarchyItem>();
		static readonly List<CSGHierarchyItem>                  addToHierarchyQueue         = new List<CSGHierarchyItem>();
		static readonly List<CSGHierarchyItem>                  deferAddToHierarchyQueue    = new List<CSGHierarchyItem>();

		static readonly HashSet<CSGNode>                        rebuildTreeNodes            = new HashSet<CSGNode>();

		static readonly HashSet<CSGNode>						updateTransformationNodes   = new HashSet<CSGNode>();

		static readonly HashSet<CSGHierarchyItem>               updateChildrenQueue         = new HashSet<CSGHierarchyItem>();
		static readonly HashSet<List<CSGHierarchyItem>>         sortChildrenQueue           = new HashSet<List<CSGHierarchyItem>>();

		static readonly HashSet<CSGNode>                        hierarchyUpdateQueue        = new HashSet<CSGNode>();

		// Dictionaries used to keep track which brushMeshAssets are used by which nodes, which is necessary to update the right nodes when a brushMeshAsset has been changed
		static readonly Dictionary<CSGBrushMeshAsset, HashSet<CSGNode>> brushMeshAssetNodes	= new Dictionary<CSGBrushMeshAsset, HashSet<CSGNode>>();
		static readonly Dictionary<CSGNode, HashSet<CSGBrushMeshAsset>> nodeBrushMeshAssets	= new Dictionary<CSGNode, HashSet<CSGBrushMeshAsset>>();


		public static bool  ignoreNextChildrenChanged	= false;
		public static bool  firstStart                  = false;
		public static bool  prefabInstanceUpdatedEvent  = false;

		public static event Action NodeHierarchyModified;
		public static event Action NodeHierarchyReset;
		public static event Action TransformationChanged;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#endif
		[RuntimeInitializeOnLoadMethod]
		public static void Initialize()
		{
			CSGNodeHierarchyManager.firstStart = false;

			CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= OnBrushMeshInstanceChanged;
			CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged += OnBrushMeshInstanceChanged;

			CSGBrushMeshAssetManager.OnBrushMeshInstanceDestroyed -= OnBrushMeshInstanceDestroyed;
			CSGBrushMeshAssetManager.OnBrushMeshInstanceDestroyed += OnBrushMeshInstanceDestroyed;
		}
		
		// TODO: Clean up API
		public static void Rebuild()
		{
            CSGManager.Clear();
			CSGBrushMeshAssetManager.Reset();
			CSGSurfaceAssetManager.Reset();
            CSGGeneratedModelMeshManager.Reset();
            Reset();

            CSGNodeHierarchyManager.FindAndReregisterAllNodes();
            CSGNodeHierarchyManager.UpdateAllTransformations();
      	    CSGNodeHierarchyManager.Update();
			CSGGeneratedModelMeshManager.UpdateModels();
		}

		// TODO: Probably needs to be internal?
		public static void Reset()
		{
#if BE_CHATTY
			Debug.Log("Reset");
#endif
			sceneHierarchies	.Clear();
		
			registeredNodes		.Clear();
			instanceIDToNodeLookup.Clear();
			nodeToinstanceIDLookup.Clear();

			componentLookup		.Clear();
			hierarchyItemLookup	.Clear();
			treeNodeLookup		.Clear();
	
			registerQueueLookup	.Clear();
			registerQueue		.Clear();		
			unregisterQueueLookup.Clear();
			unregisterQueue		.Clear();

			reregisterModelQueue.Clear();

			findChildrenQueue	.Clear();
		
			CSGManager.Destroy(destroyNodesList.ToArray());
			destroyNodesList	.Clear();

			addToHierarchyLookup.Clear();
			addToHierarchyQueue	.Clear();
			deferAddToHierarchyQueue.Clear();

			rebuildTreeNodes	.Clear();
			updateTransformationNodes.Clear();

			updateChildrenQueue	.Clear();
			sortChildrenQueue	.Clear();
	
			hierarchyUpdateQueue.Clear();

			brushMeshAssetNodes	.Clear();
			nodeBrushMeshAssets	.Clear();

			createDefaultModels .Clear();
			
			CSGGeneratedModelMeshManager.Reset();
			
			if (NodeHierarchyReset != null)
				NodeHierarchyReset();
		}
		
		// *Workaround*
		// Unfortunately prefabs do not always send out all the necessary events
		// so we need to go through all the nodes and assume they've changed
		public static void OnPrefabInstanceUpdated(GameObject instance)
		{
			var nodes = instance.GetComponentsInChildren<CSGNode>();
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
			CSGSceneHierarchy hierarchy;
			if (!sceneHierarchies.TryGetValue(scene, out hierarchy))
				return 0;
			return hierarchy.RootItems.Count;
		}
		
		public static bool IsNodeDirty(CSGNode component)
		{
			if (!component)
				return false;

			if (!registerQueueLookup.Add(component))
				return false;

			return	registerQueue.Contains(component) ||
					hierarchyUpdateQueue.Contains(component);
		}

		public static void Register(CSGNode component)
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

		public static void Unregister(CSGNode component)
		{
			// NOTE: this method is called from destructor and cannot use Debug.Log, get Transforms etc.

			if (registerQueueLookup.Remove(component))
			{
				registerQueue.Remove(component);
			}

			if (unregisterQueueLookup.Add(component))
			{
				CSGHierarchyItem hierarchyItem; 
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
				if (!children[n].Component || children[n].Component.SkipThisNode)
					continue;
				hierarchyUpdateQueue.Add(children[n].Component);
				updateTransformationNodes.Add(children[n].Component);
			}

			component.hierarchyItem.Registered = false;
		}

		public static void UpdateAvailability(CSGNode node)
		{
			if (node.SkipThisNode)
			{
				CSGNodeHierarchyManager.Unregister(node);
			} else
			{
				CSGNodeHierarchyManager.Register(node);
			}
		}

		public static void OnTransformParentChanged(CSGNode node)
		{
			if (!node ||
				!node.hierarchyItem.Registered || 
				node.SkipThisNode)
				return;
			hierarchyUpdateQueue.Add(node);
		}


		// Let the hierarchy manager know that this/these node(s) has/have moved, so we can regenerate meshes
		public static void RebuildTreeNodes(CSGNode node) { rebuildTreeNodes.Add(node); }
		public static void UpdateTreeNodeTranformation(CSGNode node) { updateTransformationNodes.Add(node); }
		public static void NotifyTransformationChanged(HashSet<CSGNode> nodes) { foreach (var node in nodes) updateTransformationNodes.Add(node); }
		public static void UpdateAllTransformations() { foreach (var node in registeredNodes) updateTransformationNodes.Add(node); }


		// Let the hierarchy manager know that the contents of this node has been modified
		//	so we can rebuild/update sub-trees and regenerate meshes
		public static void NotifyContentsModified(CSGNode node)
		{
			node.hierarchyItem.SetBoundsDirty();
			UpdateBrushMeshAssets(node);
		}
		

		private static void OnBrushMeshInstanceChanged(CSGBrushMeshAsset brushMeshAsset)
		{
			HashSet<CSGNode> nodes;
			if (brushMeshAssetNodes.TryGetValue(brushMeshAsset, out nodes))
			{
				foreach(var node in nodes)
				{
					if (node)
					{
						node.UpdateBrushMeshInstances();
						hierarchyUpdateQueue.Add(node);
						updateTransformationNodes.Add(node);
					}
				}
			}
		}

		private static void OnBrushMeshInstanceDestroyed(CSGBrushMeshAsset brushMeshAsset)
		{
			HashSet<CSGNode> nodes;
			if (brushMeshAssetNodes.TryGetValue(brushMeshAsset, out nodes))
			{
				foreach (var node in nodes)
				{
					if (!node)
						continue;

					HashSet<CSGBrushMeshAsset> uniqueBrushMeshAssets;
					if (nodeBrushMeshAssets.TryGetValue(node, out uniqueBrushMeshAssets))
						uniqueBrushMeshAssets.Remove(brushMeshAsset);
				}
			}
			brushMeshAssetNodes.Remove(brushMeshAsset);
		}

		static void UpdateBrushMeshAssets(CSGNode node)
		{
			HashSet<CSGBrushMeshAsset> uniqueBrushMeshAssets;
			if (nodeBrushMeshAssets.TryGetValue(node, out uniqueBrushMeshAssets))
			{
				// Remove previously set brushMeshes for this node
				foreach (var brushMeshAsset in uniqueBrushMeshAssets)
				{
					HashSet<CSGNode> nodes;
					if (brushMeshAssetNodes.TryGetValue(brushMeshAsset, out nodes))
						nodes.Remove(node);
				}
				uniqueBrushMeshAssets.Clear();
			} else
				uniqueBrushMeshAssets = new HashSet<CSGBrushMeshAsset>();

			if (!node)
				return;
			
			var brushMeshAssets = node.GetUsedBrushMeshAssets();
			if (brushMeshAssets == null)
				return;
			
			for (int i = 0; i < brushMeshAssets.Length; i++)
			{
				var brushMeshAsset = brushMeshAssets[i];
				if (object.Equals(brushMeshAsset, null)) 
					continue;
					
				// Add current brushMesh of this node
				if (uniqueBrushMeshAssets.Add(brushMeshAsset))
				{
					HashSet<CSGNode> nodes;
					if (!brushMeshAssetNodes.TryGetValue(brushMeshAsset, out nodes))
					{
						nodes = new HashSet<CSGNode>();
						brushMeshAssetNodes[brushMeshAsset] = nodes;
					}
					nodes.Add(node);
				}
			}
			nodeBrushMeshAssets[node] = uniqueBrushMeshAssets;
			node.UpdateBrushMeshInstances();
		}

		static void RemoveBrushMeshAssets(CSGNode node)
		{
			// NOTE: node is likely destroyed at this point, it can still be used as a lookup key however.

			HashSet<CSGBrushMeshAsset> brushMeshAssets;
			if (nodeBrushMeshAssets.TryGetValue(node, out brushMeshAssets))
			{
				// Remove previously set brushMeshes for this node
				foreach (var brushMeshAsset in brushMeshAssets)
				{
					HashSet<CSGNode> nodes;
					if (brushMeshAssetNodes.TryGetValue(brushMeshAsset, out nodes))
						nodes.Remove(node);
				}
				brushMeshAssets.Clear();
			}
			nodeBrushMeshAssets.Remove(node);
		}

		public static void OnTransformChildrenChanged(CSGNode component)
		{
			if (ignoreNextChildrenChanged)
			{
				ignoreNextChildrenChanged = false;
				return;
			}
			if (!component ||
				!component.hierarchyItem.Registered || 
				component.SkipThisNode)
				return;
		
			var children = component.hierarchyItem.Children;
			for (int i = 0; i < children.Count; i++)
			{
				var childComponent = children[i].Component;
				if (!childComponent || childComponent.SkipThisNode)
				{
					continue;
				}
				hierarchyUpdateQueue.Add(childComponent);
			}
		}

		// Find parent node & update siblingIndices for each level
		static CSGNode UpdateSiblingIndices(CSGSceneHierarchy sceneHierarchy, CSGHierarchyItem hierarchyItem)
		{
			var transform	= hierarchyItem.Transform;
			if (!transform)
				return null;

			var parent		= transform.parent;

			hierarchyItem.SiblingIndices.Clear();
			hierarchyItem.SiblingIndices.Add(transform.GetSiblingIndex());

			if (ReferenceEquals(parent, null))
				return null;
			
			// Find siblingIndexs up the parents, until we find a CSGNode
			CSGNode parentComponent;
			if (componentLookup.TryGetValue(parent, out parentComponent) &&
				!parentComponent.CanHaveChildNodes)
				parentComponent = null;
			while (ReferenceEquals(parentComponent, null))
			{
				hierarchyItem.SiblingIndices.Insert(0, parent.GetSiblingIndex());

				parent = parent.parent;
				if (ReferenceEquals(parent, null))
					break;

				parentComponent = null;
				if (componentLookup.TryGetValue(parent, out parentComponent) && !parentComponent.CanHaveChildNodes)
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
				CSGGeneratedComponentManager.IsDefaultModel(rootTransform))
			{
				// The default model is special in the sense that, unlike all other models, it doesn't
				// simply contain all the nodes that are its childrens. Instead, it contains all the nodes 
				// that do not have a model as a parent. So we go through the hierarchy and find the top
				// level nodes that are not a model
				var scene = rootTransform.gameObject.scene;
				scene.GetRootGameObjects(__rootGameObjects);
				for (int i = 0; i < __rootGameObjects.Count; i++)
				{
					var childTransform	= __rootGameObjects[i].transform;
					var childNode		= childTransform.GetComponentInChildren<CSGNode>();
					if (!childNode)
						continue;
					if (childNode is CSGModel)
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
					var childNode = childTransform.GetComponent<CSGNode>();
					if (!childNode || childNode.SkipThisNode)
					{
						__transforms.Enqueue(childTransform);
						continue;
					}
					hierarchyUpdateQueue.Add(childNode);
				}
			}
			__transforms.Clear();
		}

		static Queue<List<CSGHierarchyItem>> __hierarchyQueueLists = new Queue<List<CSGHierarchyItem>>(); // static to avoid allocations
		static void SetChildScenes(CSGHierarchyItem hierarchyItem, Scene scene)
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
					if (!childNode || childNode.SkipThisNode)
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

		static void AddChildNodesToHashSet(HashSet<CSGNode> allFoundChildren)
		{
			__hierarchyQueueLists.Clear();
			foreach(var node in allFoundChildren)
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

		static void RegisterInternal(CSGNode component)
		{
			if (!component)
				return;
		
			int index			= addToHierarchyQueue.Count;
			var parent			= component.hierarchyItem.Transform ? component.hierarchyItem.Transform : component.transform;
			var	parentComponent = component;
			do
			{
				if (parentComponent &&
					!parentComponent.SkipThisNode)
				{
					if (registeredNodes.Add(parentComponent))
					{
						var instanceID = parentComponent.GetInstanceID();
						nodeToinstanceIDLookup[parentComponent] = instanceID;
						instanceIDToNodeLookup[instanceID] = parentComponent;
						var parentHierarchyItem = parentComponent.hierarchyItem;

						addToHierarchyLookup[parentComponent] = parentHierarchyItem;
						addToHierarchyQueue.Insert(index, parentHierarchyItem);

						UpdateBrushMeshAssets(parentComponent);
					}
				}
				parent = parent.parent;
				if (parent == null)
					break;
				parentComponent = parent.GetComponent<CSGNode>();
			} while (true);
		}
		
		static bool RemoveFromHierarchy(List<CSGHierarchyItem> rootItems, CSGNode component)
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

		static void UnregisterInternal(CSGNode component)
		{
			if (!registeredNodes.Remove(component))
				return;

			var instanceID = nodeToinstanceIDLookup[component];
			nodeToinstanceIDLookup.Remove(component);
			instanceIDToNodeLookup.Remove(instanceID);

			var sceneHierarchy = component.hierarchyItem.sceneHierarchy;
			if (sceneHierarchy == null)
				return;
		
			var rootItems	= sceneHierarchy.RootItems;
			RemoveFromHierarchy(rootItems, component);

			if (rootItems.Count == 0)
			{
				sceneHierarchies.Remove(sceneHierarchy.Scene);
			}

			RemoveBrushMeshAssets(component);
		}

		static void FindAndReregisterAllNodes()
		{
			for (int s = 0; s < SceneManager.sceneCount; s++)
			{
				var scene = SceneManager.GetSceneAt(s);
				var rootObjects = scene.GetRootGameObjects();
				for (int r = rootObjects.Length - 1; r >= 0; r--)
				{
					var rootObject	= rootObjects[r];
					var nodes		= rootObject.GetComponentsInChildren<CSGNode>(includeInactive: false);
					for (int n = nodes.Length - 1; n >= 0; n--)
					{
						var node = nodes[n];
						if (node.isActiveAndEnabled)
							Register(node);
					}
				}
			}
		}

		public static void Update()
		{
			int loops = 0;
			UpdateAgain:
			reregisterModelQueue.Clear();

			try
			{
				CSGBrushMeshAssetManager.Update();
				CSGSurfaceAssetManager.Update();
				UpdateTrampoline();
			}
			// If we get an exception we don't want to end up infinitely spawning this exception ..
			finally
			{
				registerQueue.Clear();
				rebuildTreeNodes.Clear();
				registerQueueLookup.Clear();
				unregisterQueue.Clear();
				sortChildrenQueue.Clear();
				findChildrenQueue.Clear();
				hierarchyUpdateQueue.Clear();
				updateChildrenQueue.Clear();
				addToHierarchyQueue.Clear();
				deferAddToHierarchyQueue.Clear();
				updateTransformationNodes.Clear();
			}

			// Unfortunately we might need to create default models during the update loop, which kind of screws up the order of things.
			// so we remember them, and re-register at the end.
			bool tryAgain = false;

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

		static void CreateTreeNodes(CSGNode node)
		{
			// Create the treeNodes for this node
			node.ClearTreeNodes(clearCaches: false);
			var createdTreeNodes = node.CreateTreeNodes();
			if (createdTreeNodes != null && createdTreeNodes.Length > 0)
				treeNodeLookup[node] = createdTreeNodes;
			else
				treeNodeLookup.Remove(node);
		}


		static readonly HashSet<CSGNode>	__registerNodes			= new HashSet<CSGNode>();	// static to avoid allocations
		static readonly HashSet<CSGNode>	__unregisterNodes		= new HashSet<CSGNode>();	// static to avoid allocations
		static readonly List<CSGTreeNode>	__childNodes			= new List<CSGTreeNode>();  // static to avoid allocations

		internal static bool prevPlaying = false;
		internal static void UpdateTrampoline()
		{
			if (createDefaultModels.Count > 0)
			{
				foreach (var sceneHierarchy in createDefaultModels)
				{
					if (sceneHierarchy.DefaultModel ||
						!sceneHierarchy.Scene.IsValid() ||
						!sceneHierarchy.Scene.isLoaded)
						continue;
					sceneHierarchy.DefaultModel = CSGGeneratedComponentManager.CreateDefaultModel(sceneHierarchy);
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
			var currentPlaying	= UnityEditor.EditorApplication.isPlaying;
			if (currentPlaying != prevPlaying)
			{
				prevPlaying = currentPlaying;
				return;
			}
#endif
			// *Workaround*
			// It's possible that some events aren't properly called at UnityEditor startup (possibly runtime as well)
			if (!firstStart)
			{
				firstStart = true;
#if BE_CHATTY
				Debug.Log("Start");
#endif
				CSGManager.Clear();
				CSGBrushMeshAssetManager.Reset();
				CSGSurfaceAssetManager.Reset();

				// Prefabs can fire events that look like objects have been loaded/created ..
				// Also, starting up in the editor can swallow up events and cause some nodes to not be registered properly
				// So to ensure that the first state is correct, we get it explicitly
				FindAndReregisterAllNodes();
				CSGBrushMeshAssetManager.Update();
				CSGSurfaceAssetManager.Update();
#if BE_CHATTY
				Debug.Log("found " + registerQueue.Count);
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
					var expectedScene	= pair.Key;
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

#if BE_CHATTY
						Debug.Log("scene changed");
#endif
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

#if BE_CHATTY
						Debug.Log("scene changed");
#endif
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
                        if( UnityEditor.PrefabUtility.GetPrefabAssetType( component ) != UnityEditor.PrefabAssetType.Regular )
                        //if (UnityEditor.PrefabUtility.GetPrefabType(component) != UnityEditor.PrefabType.Prefab)
#endif
                            continue;
#if BE_CHATTY
						Debug.Log("prefab");
#endif
					}
#if BE_CHATTY
					Debug.Log("remove");
#endif
					registerQueue.RemoveAt(i);
				}

				var knownNodes = registeredNodes.ToArray();
				for (int i = 0; i < knownNodes.Length; i++)
				{
					var knownNode = knownNodes[i];
					if (knownNode)
						continue;

#if BE_CHATTY
					Debug.Log("force find removed");
#endif
					Unregister(knownNode);
				}
			}

			if (registerQueue.Count != 0)
			{
				for (int i = registerQueue.Count - 1; i >= 0; i--)
				{
					if (!registerQueue[i].isActiveAndEnabled)
						registerQueue.RemoveAt(i);
				}
			}

			
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
				return;
			

#if BE_CHATTY
			Debug.Log("Update " + registerQueue.Count + " " + 
								  unregisterQueue.Count + " " + 
								  sortChildrenQueue.Count + " " + 
								  findChildrenQueue.Count + " " + 
								  
								  hierarchyUpdateQueue.Count + " " + 

								  updateChildrenQueue.Count + " " + 
								  addToHierarchyQueue.Count + " " + 

								  updateTransformationNodes.Count + " " + 
								  destroyNodesList.Count + " " +

								  rebuildTreeNodes.Count);
#endif

			__registerNodes   .Clear();
			__unregisterNodes .Clear();

			if (rebuildTreeNodes.Count > 0)
			{
				unregisterQueue.AddRange(rebuildTreeNodes);
				registerQueue.AddRange(rebuildTreeNodes);
			}

			if (unregisterQueue.Count > 0)
			{
#if BE_CHATTY
				Debug.Log("unregisterQueue " + unregisterQueue.Count);
#endif
				for (int i = 0; i < unregisterQueue.Count; i++)
				{
					var node = unregisterQueue[i];

					// Remove any treeNodes that are part of the components we're trying to unregister 
					// (including components that may have been already destroyed)
					CSGTreeNode[] createdTreeNodes;
					if (treeNodeLookup.TryGetValue(node, out createdTreeNodes))
					{
						for (int n = 0; n < createdTreeNodes.Length; n++)
						{
							if (!createdTreeNodes[n].Valid)
								continue;
						
							destroyNodesList.Add(createdTreeNodes[n]);
						}
						treeNodeLookup.Remove(node);
					}
				}

				for (int i = 0; i < unregisterQueue.Count; i++)
				{
					var node = unregisterQueue[i];
					CSGHierarchyItem hierarchyItem;
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
								CSGGeneratedComponentManager.IsDefaultModel(parentHierarchyItem.Component))
							{
								var hierarchy = hierarchyItem.sceneHierarchy;
								if (hierarchy != null)
									hierarchy.RootItems.Remove(hierarchyItem);
							}

							parentHierarchyItem.SetChildBoundsDirty();
							parentHierarchyItem.Children.Remove(hierarchyItem);
							if (parentHierarchyItem.Component &&
								!parentHierarchyItem.Component.SkipThisNode)
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
					node.ClearTreeNodes(clearCaches: true);
					UnregisterInternal(node);
				}
				
				unregisterQueue.Clear();
				unregisterQueueLookup.Clear();
			}
			
			if (registerQueue.Count > 0)
			{
#if BE_CHATTY
				Debug.Log("registerQueue " + registerQueue.Count);
#endif
				for (int i = 0; i < registerQueue.Count; i++)
				{
					var node = registerQueue[i];

					// Remove any treeNodes that are part of the components we're trying to register 
					// (including components that may have been already destroyed)
					CSGTreeNode[] createdTreeNodes;
					if (treeNodeLookup.TryGetValue(node, out createdTreeNodes))
					{
						for (int n = 0; n < createdTreeNodes.Length; n++)
						{
							if (!createdTreeNodes[n].Valid)
								continue;

							destroyNodesList.Add(createdTreeNodes[n]);
						}
						treeNodeLookup.Remove(node);
					}
				}
			
				// Initialize the components
				for (int i = registerQueue.Count - 1; i >= 0; i--) // reversed direction because we're also potentially removing items
				{
					var node = registerQueue[i];
					if (!node ||			// component might've been destroyed between adding it to the registerQueue and here
						node.SkipThisNode)	// component might be active/enabled etc.
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

					if (node.CreatesTreeNode)
						CreateTreeNodes(node);
				}
				 
				// Separate loop to ensure all parent components are already initialized
				// this is because the order of the registerQueue is essentially random
				for (int i = 0; i < registerQueue.Count; i++)
				{
					var node = registerQueue[i];
					RegisterInternal(node);
				}

				for (int i = 0; i < registerQueue.Count; i++)
				{
					var node = registerQueue[i];
					if (!addToHierarchyLookup.ContainsKey(node))
					{
						addToHierarchyLookup.Add(node, node.hierarchyItem);
						addToHierarchyQueue.Add(node.hierarchyItem);
					}
				}

				registerQueue.Clear();
				registerQueueLookup.Clear();
			}

			if (findChildrenQueue.Count > 0)
			{
				//Debug.Log("findChildrenQueue " +findChildrenQueue.Count);
				for (int i = 0; i < findChildrenQueue.Count; i++)
				{
					var hierarchyItem = findChildrenQueue[i];
					UpdateChildren(hierarchyItem.Transform);
				}
				findChildrenQueue.Clear();
			}
		
			if (hierarchyUpdateQueue.Count > 0)
			{
				//Debug.Log("hierarchyUpdateQueue " + hierarchyUpdateQueue.Count);
				var prevQueue = hierarchyUpdateQueue.ToArray();
				for (int i = 0; i < prevQueue.Length; i++)
				{
					var component = prevQueue[i];
					if (!component ||
						component.SkipThisNode)
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
							CSGGeneratedComponentManager.IsDefaultModel(parentHierarchyItem.Component))
						{
							var hierarchy = hierarchyItem.sceneHierarchy;
							if (hierarchy != null)
								hierarchy.RootItems.Remove(hierarchyItem);
						}

						parentHierarchyItem.SetChildBoundsDirty();
						parentHierarchyItem.Children.Remove(hierarchyItem);
						if (parentHierarchyItem.Component &&
							!parentHierarchyItem.Component.SkipThisNode)
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
			
			if (addToHierarchyQueue.Count > 0)
			{
				//Debug.Log("addToHierarchyQueue " + addToHierarchyQueue.Count);
				for (int i = 0; i < addToHierarchyQueue.Count; i++)
				{ 
					var hierarchyItem = addToHierarchyQueue[i];
					if (!hierarchyItem.Component ||
						hierarchyItem.Component.SkipThisNode)
						continue;
		
					CSGSceneHierarchy sceneHierarchy;
					var scene = hierarchyItem.Scene;
					if (!sceneHierarchies.TryGetValue(scene, out sceneHierarchy))
					{
						sceneHierarchy = new CSGSceneHierarchy() { Scene = scene };
						sceneHierarchies[scene] = sceneHierarchy;
					}
					hierarchyItem.sceneHierarchy = sceneHierarchy;

					var defaultModel = false;
					var parentComponent = UpdateSiblingIndices(sceneHierarchy, hierarchyItem);
					if (ReferenceEquals(parentComponent, null))
					{
						if (!(hierarchyItem.Component is CSGModel))
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
							if (iterator.Component.HasContainerTreeNode) 
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
			 
			if (sortChildrenQueue.Count > 0)
			{
				//Debug.Log("sortChildrenQueue " + sortChildrenQueue.Count);
				foreach (var items in sortChildrenQueue)
				{
					if (items.Count == 0)
						continue;

					items.Sort(delegate (CSGHierarchyItem x, CSGHierarchyItem y)
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
			
			if (updateChildrenQueue.Count > 0)
			{
				//Debug.Log("updateChildrenQueue " + updateChildrenQueue.Count);
				foreach (var item in updateChildrenQueue)
				{
					if (!item.Component)
						continue;
					
					if (!item.Component.HasContainerTreeNode)
						continue;
					
					// TODO: create a virtual updateChildrenQueue list for the default model instead?
					if (item.Children.Count == 0 &&
						CSGGeneratedComponentManager.IsDefaultModel(item.Component))
					{
						var itemModel = item.Component as CSGModel;

						// If the default model is empty, we'll destroy it to remove clutter
						var scene = item.Scene;
						CSGSceneHierarchy sceneHierarchy;
						if (sceneHierarchies.TryGetValue(scene, out sceneHierarchy))
						{
							if (sceneHierarchy.DefaultModel == itemModel)
							{
								sceneHierarchy.DefaultModel = null;
							}
							sceneHierarchy.RootItems.Remove(itemModel.hierarchyItem);
						}
						destroyNodesList.Add(itemModel.Node);
						CSGObjectUtility.SafeDestroy(item.GameObject);
						continue;
					}
				}
			}
		
			if (destroyNodesList.Count > 0)
			{
				//Debug.Log("destroyNodesList " + destroyNodesList.Count);
				
				// Destroy all old nodes after we created new nodes, to make sure we don't get conflicting IDs
				CSGManager.Destroy(destroyNodesList.ToArray());
				destroyNodesList.Clear();
			}
		
			if (updateChildrenQueue.Count > 0)
			{
				//Debug.Log("updateChildrenQueue " + updateChildrenQueue.Count);
				foreach (var item in updateChildrenQueue)
				{
					if (!item.Component)
						continue;

					if (!item.Component.HasContainerTreeNode)
						continue;
					
					__childNodes.Clear();
					GetChildrenOfHierachyItem(__childNodes, item);
					
					item.Component.SetChildren(__childNodes);
				}
				updateChildrenQueue.Clear();
			}
			
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


			if (__unregisterNodes.Count > 0)
			{
				foreach (var node in __unregisterNodes)
					CSGGeneratedModelMeshManager.Unregister(node);
				__unregisterNodes.Clear();
			}

			if (__registerNodes.Count > 0)
			{
				foreach (var node in __registerNodes)
					CSGGeneratedModelMeshManager.Register(node);
				__registerNodes.Clear();
			}

			__registerNodes		.Clear();
			__unregisterNodes	.Clear();
			__childNodes		.Clear();

			var allScenes = sceneHierarchies.ToArray();
			for (int i = 0; i < allScenes.Length; i++)
			{
				CSGSceneHierarchy sceneHierarchy = allScenes[i].Value;
				if (!sceneHierarchy.Scene.IsValid() ||
					!sceneHierarchy.Scene.isLoaded ||
					(sceneHierarchy.RootItems.Count == 0 && !sceneHierarchy.DefaultModel && !createDefaultModels.Contains(sceneHierarchy)))
				{
					sceneHierarchies.Remove(allScenes[i].Key);
					createDefaultModels.Remove(sceneHierarchy);
				}
			}
			

			// Used to redraw windows etc.
			if (NodeHierarchyModified != null)
				NodeHierarchyModified();
		}

		static void GetChildrenOfHierachyItem(List<CSGTreeNode> childNodes, CSGHierarchyItem item)
		{
			for (int i = 0; i < item.Children.Count; i++)
			{
				var childComponent = item.Children[i].Component;
				if (!childComponent)
					continue;

				childComponent.CollectChildNodesForParent(__childNodes);
			}
		}

		public static Transform FindModelTransformOfTransform(Transform transform)
		{
			// TODO: optimize this
			CSGModel model;
			do
			{
				if (!transform)
					return null;
				model = transform.GetComponentInParent<CSGModel>();
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
			CSGModel model;
			do
			{
				if (!transform)
					return Matrix4x4.identity;
				model = transform.GetComponentInParent<CSGModel>();
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

		public static CSGNode FindCSGNodeByInstanceID(int instanceID)
		{
			CSGNode node = null;
			instanceIDToNodeLookup.TryGetValue(instanceID, out node);
			return node;
		}

		public static CSGNode FindCSGNodeByTreeNode(CSGTreeNode node)
		{
			return FindCSGNodeByInstanceID(node.UserID);
		}
	}
}
