using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;
using System.Linq;

namespace Chisel.Components
{
	public abstract class CSGGeneratorComponent : CSGNode
	{
		public CSGGeneratorComponent() : base() {  }

		public CSGTreeNode	TopNode { get { if (!ValidNodes) return CSGTreeNode.InvalidNode; return Nodes[0]; } }

		public override CSGTreeNode	GetTreeNodeByIndex(int index)
		{
			if (index < 0 || index > Nodes.Length)
				return CSGTreeNode.InvalidNode;
			return Nodes[index];
		}

		[HideInInspector] CSGTreeNode[] Nodes = new CSGTreeNode[] { new CSGTreeBrush() };

		[SerializeField,HideInInspector] protected CSGOperationType		operation;		// NOTE: do not rename, name is directly used in editors
		[SerializeField,HideInInspector] protected CSGBrushMeshAsset	brushMeshAsset;	// NOTE: do not rename, name is directly used in editors
		[SerializeField,HideInInspector] protected Matrix4x4			localTransformation = Matrix4x4.identity;
		[SerializeField,HideInInspector] protected Vector3				pivotOffset			= Vector3.zero;

		bool ValidNodes { get { return (Nodes != null && Nodes.Length > 0) && Nodes[0].Valid; } }
		
		protected override void OnResetInternal()
		{
			UpdateGenerator();
			UpdateBrushMeshInstances();
			base.OnResetInternal();
		}

		protected override void OnValidateInternal()
		{
			if (!ValidNodes)
				return;

            UpdateGenerator();
			UpdateBrushMeshInstances();

            CSGNodeHierarchyManager.NotifyContentsModified(this);
			base.OnValidateInternal();
        }

		public CSGOperationType     Operation
		{
			get
			{
				return operation;
			}
			set
			{
				if (value == operation)
					return;
				operation = value;

                if (ValidNodes)
					Nodes[0].Operation = operation;

				// Let the hierarchy manager know that the contents of this node has been modified
				//	so we can rebuild/update sub-trees and regenerate meshes
				CSGNodeHierarchyManager.NotifyContentsModified(this);
			}
		}
		
		public Vector3     PivotOffset
		{
			get
			{
				return pivotOffset;
			}
			set
			{
				if (value == pivotOffset)
					return;
				pivotOffset = value;
				
				UpdateInternalTransformation();

				// Let the hierarchy manager know that this node has moved, so we can regenerate meshes
				CSGNodeHierarchyManager.UpdateTreeNodeTranformation(this);
			}
		}

		public Matrix4x4			LocalTransformation
		{
			get
			{
				return localTransformation;
			}
			set
			{
				if (value == localTransformation)
					return;

				localTransformation = value;

				UpdateInternalTransformation();

				// Let the hierarchy manager know that this node has moved, so we can regenerate meshes
				CSGNodeHierarchyManager.UpdateTreeNodeTranformation(this);
			}
		}

        Matrix4x4 TopTransformation
        {
            get
            {
                var finalTransformation = localTransformation;
                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    finalTransformation *= Matrix4x4.TRS(-pivotOffset, Quaternion.identity, Vector3.one);                
                return finalTransformation;
            }
        }

		void UpdateInternalTransformation()
		{
			if (!ValidNodes)
				return;

			Nodes[0].LocalTransformation = TopTransformation;
		}

		public CSGBrushMeshAsset BrushMeshAsset
		{
			get { return brushMeshAsset; }
			set
			{
				if (value == brushMeshAsset)
					return;

				// Set the new BrushMeshAsset as current
				brushMeshAsset = value;

				UpdateBrushMeshInstances();

				// Let the hierarchy manager know that the contents of this node has been modified
				//	so we can rebuild/update sub-trees and regenerate meshes
				CSGNodeHierarchyManager.NotifyContentsModified(this);
			}
		}

		int RequiredNodeLength(BrushMeshInstance[] instances)
		{
			return (instances == null || instances.Length == 0) ? 0 : ((instances.Length == 1) ? 1 : instances.Length + 1);
		}

		bool InitializeBrushMeshInstances()
		{
			var instances			= brushMeshAsset ? brushMeshAsset.Instances : null;

			// TODO: figure out why this can happen (mess around with spiral stairs)
			// TODO: does this have anything to do with spiral stairs not updating all submeshes when being modified?
			if (instances != null &&
				instances.Length !=
				brushMeshAsset.SubMeshCount)
			{
				brushMeshAsset.UpdateInstances();
				instances = brushMeshAsset ? brushMeshAsset.Instances : null;
			}

			var requiredNodeLength	= RequiredNodeLength(instances);
			
			if (Nodes != null && Nodes.Length == requiredNodeLength)
			{
				if (Nodes.Length == 0)
				{
					var brush = (CSGTreeBrush)Nodes[0];
					brush.BrushMesh = BrushMeshInstance.InvalidInstance;
					brush.Operation = CSGOperationType.Additive;
				} else
				if (Nodes.Length == 1)
				{
					var brush = (CSGTreeBrush)TopNode;
					brush.BrushMesh = brushMeshAsset.Instances[0];
					brush.Operation = brushMeshAsset.SubMeshes[0].Operation;
				} else
				{
					for (int i = 0; i < instances.Length; i++)
					{
						var brush = (CSGTreeBrush)Nodes[i + 1];
						brush.BrushMesh = brushMeshAsset.Instances[i];
						brush.Operation = brushMeshAsset.SubMeshes[i].Operation;
					}
				}
				return true;
			} else
			{
				bool needRebuild = Nodes != null && Nodes.Length != requiredNodeLength;
				if (Nodes.Length <= 1)
				{
					var brush = (CSGTreeBrush)TopNode;
					if (brush.BrushMesh != BrushMeshInstance.InvalidInstance)
					{
						brush.BrushMesh = BrushMeshInstance.InvalidInstance;
						brush.Operation = CSGOperationType.Additive;
					}
				} else
				{
					for (int i = 1; i < Nodes.Length; i++)
					{
						var brush = (CSGTreeBrush)Nodes[i];
						if (brush.BrushMesh != BrushMeshInstance.InvalidInstance)
						{
							brush.BrushMesh = BrushMeshInstance.InvalidInstance;
							brush.Operation = CSGOperationType.Additive;
						}
					}
				}
				if (needRebuild) // if we don't do this, we'll end up creating nodes infinitely, when the node can't make a valid brushMesh
					CSGNodeHierarchyManager.RebuildTreeNodes(this);
				return false;
			}
		}

		public void GenerateAllTreeNodes()
		{
			var instanceID			= GetInstanceID();
			var instances			= brushMeshAsset ? brushMeshAsset.Instances : null;
			var requiredNodeLength	= RequiredNodeLength(instances);

			if (requiredNodeLength == 0)
			{
				Nodes = new CSGTreeNode[1];
				Nodes[0] = CSGTreeBrush.Create(userID: instanceID, localTransformation: TopTransformation, operation: operation);
			} else
			if (requiredNodeLength == 1)
			{
				Nodes = new CSGTreeNode[1];
				Nodes[0] = CSGTreeBrush.Create(userID: instanceID, localTransformation: TopTransformation, operation: operation);
			} else
			{
				Nodes = new CSGTreeNode[requiredNodeLength];
				var children = new CSGTreeNode[requiredNodeLength - 1];
				for (int i = 0; i < requiredNodeLength - 1; i++)
					children[i] = CSGTreeBrush.Create(userID: instanceID);

				Nodes[0] = CSGTreeBranch.Create(instanceID, operation, children);
				for (int i = 1; i < Nodes.Length; i++)
					Nodes[i] = children[i - 1];
            }
            Nodes[0].Operation = operation;
            Nodes[0].LocalTransformation = TopTransformation;
        }

		public override void UpdateBrushMeshInstances()
		{
			// Update the Node (if it exists)
			if (!ValidNodes)
				return;

			InitializeBrushMeshInstances();
			SetDirty();
            
            if (Nodes[0].Operation != operation)
                Nodes[0].Operation = operation;
        }

		internal override void UpdateTransformation()
		{
			// TODO: recalculate transformation based on hierarchy up to (but not including) model
			var transform = hierarchyItem.Transform;
			if (!transform)
				return;

			var localToWorldMatrix = transform.localToWorldMatrix;
			var modelTransform = CSGNodeHierarchyManager.FindModelTransformOfTransform(transform);
			if (modelTransform)
				localTransformation = modelTransform.worldToLocalMatrix * localToWorldMatrix;
			else
				localTransformation = localToWorldMatrix;

			if (ValidNodes)
				UpdateInternalTransformation();
		}

		internal override void ClearTreeNodes(bool clearCaches = false)
		{
			for (int i = 0; i < Nodes.Length; i++)
				Nodes[i].SetInvalid();
		}

		internal override CSGTreeNode[] CreateTreeNodes()
		{
			if (ValidNodes)
				Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);
			
			
			UpdateGenerator();
			UpdateBrushMeshInstances();

			GenerateAllTreeNodes();

			InitializeBrushMeshInstances();
			
			UpdateInternalTransformation();


            if (Nodes[0].Operation != operation)
                Nodes[0].Operation = operation;
            return Nodes;
		}

		public override int NodeID								{ get { return TopNode.NodeID; } }
		
		public override void SetDirty()
		{
			if (!ValidNodes)
				return;

			if (Nodes.Length == 1)
			{
				TopNode.SetDirty();
			} else
			{
				for (int i = 1; i < Nodes.Length; i++)
					Nodes[i].SetDirty();
			}
		}

		internal override void CollectChildNodesForParent(List<CSGTreeNode> childNodes)
		{
			childNodes.Add(TopNode);
		}

		public override CSGBrushMeshAsset[] GetUsedBrushMeshAssets()
		{
			return new CSGBrushMeshAsset[] { brushMeshAsset };
		}

		// TODO: clean this up
		public delegate IEnumerable<CSGTreeBrush> GetSelectedVariantsOfBrushOrSelfDelegate(CSGTreeBrush brush);
		public static GetSelectedVariantsOfBrushOrSelfDelegate GetSelectedVariantsOfBrushOrSelf;

		public override Bounds CalculateBounds()
		{
			if (!brushMeshAsset)
				return CSGHierarchyItem.EmptyBounds;

			var modelMatrix		= CSGNodeHierarchyManager.FindModelTransformMatrixOfTransform(hierarchyItem.Transform);
			var bounds			= CSGHierarchyItem.EmptyBounds;

			var foundBrushes = new HashSet<CSGTreeBrush>();
			GetAllTreeBrushes(foundBrushes, false);
			foreach (var brush in foundBrushes)
			{
				var transformation = modelMatrix * brush.NodeToTreeSpaceMatrix;
				var assetBounds = brushMeshAsset.CalculateBounds(transformation);
				var magnitude = assetBounds.size.sqrMagnitude;
				if (float.IsInfinity(magnitude) ||
					float.IsNaN(magnitude))
				{
					var center = transformation.GetColumn(3);
					assetBounds = new Bounds(center, Vector3.zero);
				}
				if (assetBounds.size.sqrMagnitude != 0)
				{
					if (bounds.size.sqrMagnitude == 0)
						bounds = assetBounds;
					else
						bounds.Encapsulate(assetBounds);
				}
			}

			return bounds;
		}
		
		public override int GetAllTreeBrushCount()
		{
			if (Nodes.Length > 1)
				return Nodes.Length - 1;
			return Nodes.Length;
		}

		// Get all brushes directly contained by this CSGNode (not its children)
		public override void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes)
		{
			if (Nodes.Length > 1)
			{
#if UNITY_EDITOR
				if (!ignoreSynchronizedBrushes)
				{
					for (int i = 1; i < Nodes.Length; i++)
						foundBrushes.AddRange(GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)Nodes[i]));
				} else
#endif
				{
					for (int i = 1; i < Nodes.Length; i++)
						foundBrushes.Add((CSGTreeBrush)Nodes[i]);
				}
			} else
			{
#if UNITY_EDITOR
				if (ignoreSynchronizedBrushes)
					foundBrushes.AddRange(GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)TopNode));
				else
#endif
					foundBrushes.Add((CSGTreeBrush)TopNode);
			}
		}

		public override CSGSurfaceAsset FindSurfaceAsset(CSGTreeBrush brush, int surfaceID)
		{
			if (!brushMeshAsset)
				return null;
			if (Nodes.Length > 1)
			{
				for (int n = 1; n < Nodes.Length; n++)
				{
					if (brush.NodeID != Nodes[n].NodeID)
						continue;
					
					var subMesh		= brushMeshAsset.SubMeshes[n - 1];

					var surfaceIndex = -1;
					for (int i=0;i<subMesh.Polygons.Length;i++)
					{
						if (subMesh.Polygons[i].surfaceID == surfaceID)
						{
							surfaceIndex = i;
							break;
						}
					}
					
					if (surfaceIndex < 0 || surfaceIndex >= subMesh.Polygons.Length)
						return null;

					return subMesh.Polygons[surfaceIndex].surfaceAsset;
				}
				return null;
			} else
			{
				if (brush.NodeID != TopNode.NodeID)
					return null;
				
				var subMesh		= brushMeshAsset.SubMeshes[0];

				var surfaceIndex = -1;
				for (int i=0;i<subMesh.Polygons.Length;i++)
				{
					if (subMesh.Polygons[i].surfaceID == surfaceID)
					{
						surfaceIndex = i;
						break;
					}
				}
					
				if (surfaceIndex < 0 || surfaceIndex >= subMesh.Polygons.Length)
					return null;

				return subMesh.Polygons[surfaceIndex].surfaceAsset;
			}
		}

		public override CSGSurfaceAsset[] GetAllSurfaceAssets(CSGTreeBrush brush)
		{
			if (!brushMeshAsset)
				return null;
			if (Nodes.Length > 1)
			{
				for (int n = 1; n < Nodes.Length; n++)
				{
					if (brush.NodeID != Nodes[n].NodeID)
						continue;

					var subMesh		= brushMeshAsset.SubMeshes[n - 1];
					var surfaces	= new HashSet<CSGSurfaceAsset>();
					for (int i = 0; i < subMesh.Polygons.Length; i++)
						surfaces.Add(subMesh.Polygons[i].surfaceAsset);

					return surfaces.ToArray();
				}
				return null;
			} else
			{
				if (brush.NodeID != TopNode.NodeID)
					return null;

				var surfaces = new HashSet<CSGSurfaceAsset>();
				for (int i = 0; i < brushMeshAsset.Polygons.Length; i++)
					surfaces.Add(brushMeshAsset.Polygons[i].surfaceAsset);

				return surfaces.ToArray();
			}
		}

		public override SurfaceReference FindSurfaceReference(CSGTreeBrush brush, int surfaceID)
		{
			if (!brushMeshAsset)
				return null;
			if (Nodes.Length > 1)
			{
				for (int n = 1; n < Nodes.Length; n++)
				{
					if (brush.NodeID != Nodes[n].NodeID)
						continue;
					
					var subMesh = brushMeshAsset.SubMeshes[n - 1];

					var surfaceIndex = -1;
					for (int i=0;i<subMesh.Polygons.Length;i++)
					{
						if (subMesh.Polygons[i].surfaceID == surfaceID)
						{
							surfaceIndex = i;
							break;
						}
					}
					
					if (surfaceIndex < 0 || surfaceIndex >= subMesh.Polygons.Length)
						return null;

					return new SurfaceReference(this, brushMeshAsset, n, n - 1, surfaceIndex, surfaceID);
				}
				return null;
			} else
			{
				if (brush.NodeID != TopNode.NodeID)
					return null;

				if (brushMeshAsset.SubMeshCount == 0)
					return null;

				var subMesh = brushMeshAsset.SubMeshes[0];

				var surfaceIndex = -1;
				for (int i=0;i<subMesh.Polygons.Length;i++)
				{
					if (subMesh.Polygons[i].surfaceID == surfaceID)
					{
						surfaceIndex = i;
						break;
					}
				}
					
				if (surfaceIndex < 0 || surfaceIndex >= subMesh.Polygons.Length)
					return null;
				
				return new SurfaceReference(this, brushMeshAsset, 0, 0, surfaceIndex, surfaceID);
			}
		}

		public override SurfaceReference[] GetAllSurfaceReferences()
		{
			if (!brushMeshAsset)
				return null;
			if (Nodes.Length > 1)
			{
				var surfaces	= new HashSet<SurfaceReference>();
				for (int n = 1; n < Nodes.Length; n++)
				{
					var subMesh		= brushMeshAsset.SubMeshes[n - 1];
					for (int i = 0; i < subMesh.Polygons.Length; i++)
					{
						var surfaceID	= subMesh.Polygons[i].surfaceID;
						surfaces.Add(new SurfaceReference(this, brushMeshAsset, n, n - 1, i, surfaceID));
					}

				}
				return surfaces.ToArray();
			} else
			{
				if (brushMeshAsset.SubMeshes == null ||
					brushMeshAsset.SubMeshes.Length == 0)
					return null;

				var subMesh		= brushMeshAsset.SubMeshes[0];
				var surfaces	= new HashSet<SurfaceReference>();
				for (int i = 0; i < brushMeshAsset.Polygons.Length; i++)
				{
					var surfaceID = subMesh.Polygons[i].surfaceID;
					surfaces.Add(new SurfaceReference(this, brushMeshAsset, 0, 0, i, surfaceID));
				}
				return surfaces.ToArray();
			}
		}

		public override SurfaceReference[] GetAllSurfaceReferences(CSGTreeBrush brush)
		{
			if (!brushMeshAsset)
				return null;
			if (Nodes.Length > 1)
			{
				for (int n = 1; n < Nodes.Length; n++)
				{
					if (brush.NodeID != Nodes[n].NodeID)
						continue;

					var subMesh		= brushMeshAsset.SubMeshes[n - 1];
					var surfaces	= new HashSet<SurfaceReference>();
					for (int i = 0; i < subMesh.Polygons.Length; i++)
					{
						var surfaceID	= subMesh.Polygons[i].surfaceID;
						surfaces.Add(new SurfaceReference(this, //(CSGTreeBrush)Nodes[n], 
															brushMeshAsset, n, n - 1, i, surfaceID));
					}

					return surfaces.ToArray();
				}
				return null;
			} else
			{
				if (brush.NodeID != TopNode.NodeID)
					return null;
				
				var subMesh		= brushMeshAsset.SubMeshes[0];
				var surfaces	= new HashSet<SurfaceReference>();
				for (int i = 0; i < brushMeshAsset.Polygons.Length; i++)
				{
					var surfaceID = subMesh.Polygons[i].surfaceID;
					surfaces.Add(new SurfaceReference(this, //(CSGTreeBrush)TopNode, 
														brushMeshAsset, 0, 0, i, surfaceID));
				}

				return surfaces.ToArray();
			}
		}

		public override Vector3 SetPivot(Vector3 newWorldPosition)
		{
			var delta = base.SetPivot(newWorldPosition);
			if (delta.x == 0 && delta.y == 0 && delta.z == 0)
				return Vector3.zero;

			PivotOffset = PivotOffset + delta;
			return delta;
		}

		public virtual void UpdateGenerator()
		{
			// BrushMeshes of generators must always be unique
			if (!brushMeshAsset ||
				!CSGBrushMeshAssetManager.IsBrushMeshUnique(brushMeshAsset))
			{
				brushMeshAsset = UnityEngine.ScriptableObject.CreateInstance<CSGBrushMeshAsset>();
				brushMeshAsset.name = "Generated " + NodeTypeName;
			}

			UpdateGeneratorInternal();

			UpdateBrushMeshInstances();
        }

		protected abstract void UpdateGeneratorInternal();

#if UNITY_EDITOR

		class DefaultOperationIcons
		{
			public DefaultOperationIcons(string name)
			{
                this.name = name;
                Update();
            }

            string name;
            GUIContent additiveIcon;
            GUIContent subtractiveIcon;
            GUIContent intersectingIcon;

            public GUIContent AdditiveIcon      { get { if (additiveIcon     == null || CSGDefaults.Style.AdditiveImage     == null) Update(); return additiveIcon; }  }
			public GUIContent SubtractiveIcon   { get { if (subtractiveIcon  == null || CSGDefaults.Style.SubtractiveImage  == null) Update(); return subtractiveIcon; }  }
			public GUIContent IntersectingIcon  { get { if (intersectingIcon == null || CSGDefaults.Style.IntersectingImage == null) Update(); return intersectingIcon; }  }


            void Update()
            {
                if (additiveIcon     == null) additiveIcon     = new GUIContent("Additive " + name);
                if (subtractiveIcon  == null) subtractiveIcon  = new GUIContent("Subtractive " + name);
                if (intersectingIcon == null) intersectingIcon = new GUIContent("Intersecting " + name);

                Texture2D image;
                image = CSGDefaults.Style.AdditiveImage;     if (image != null && additiveIcon.image != image) additiveIcon.image = image;
                image = CSGDefaults.Style.SubtractiveImage;  if (image != null && subtractiveIcon.image != image) subtractiveIcon.image = image;
                image = CSGDefaults.Style.IntersectingImage; if (image != null && intersectingIcon.image != image) intersectingIcon.image = image;
            }
		}
		static Dictionary<string, DefaultOperationIcons> DefaultIcons = new Dictionary<string, DefaultOperationIcons>();

		// The icon used in the hierarchy
		public override GUIContent Icon
		{
			get
			{
				DefaultOperationIcons icons;
				if (!DefaultIcons.TryGetValue(NodeTypeName, out icons))
				{
					icons = new DefaultOperationIcons(NodeTypeName);
					DefaultIcons[NodeTypeName] = icons;
				}
				switch (this.operation)
				{
					default:
					case CSGOperationType.Additive:		return icons.AdditiveIcon;
					case CSGOperationType.Subtractive:	return icons.SubtractiveIcon;
					case CSGOperationType.Intersecting:	return icons.IntersectingIcon;
				}
			}
		}

		public override bool ConvertToBrushes()
		{
			var topGameObject = this.gameObject;
			UnityEditor.Undo.DestroyObjectImmediate(this);
			topGameObject.SetActive(false);
			bool success = false;
			try
			{
				if (brushMeshAsset.SubMeshCount == 1)
				{
					var brush = UnityEditor.Undo.AddComponent<CSGBrush>(topGameObject);
					brush.Operation = this.operation;
					brush.BrushMeshAsset = brushMeshAsset;
					brush.LocalTransformation = localTransformation;
					brush.PivotOffset = pivotOffset;
					UnityEditor.Undo.SetCurrentGroupName("Converted Shape to Brush");
				} else
				{
					var operationComponent = UnityEditor.Undo.AddComponent<CSGOperation>(topGameObject);
					operationComponent.Operation = this.operation;
					var parentTransform = topGameObject.transform;
					for (int i = 0; i < brushMeshAsset.SubMeshCount; i++)
					{
						var newBrushMeshAsset = UnityEngine.ScriptableObject.CreateInstance<CSGBrushMeshAsset>();
						newBrushMeshAsset.SubMeshes = new[] { new CSGBrushSubMesh(brushMeshAsset.SubMeshes[i]) };
						var brushGameObject = new GameObject("Brush (" + (i + 1) + ")");
						UnityEditor.Undo.RegisterCreatedObjectUndo(brushGameObject, "Created GameObject");
						brushGameObject.SetActive(false);
						try
						{
							var brushTransform = brushGameObject.transform;
							UnityEditor.Undo.SetTransformParent(brushTransform, parentTransform, "Move child brush underneath parent operation");
							UnityEditor.Undo.RecordObject(brushTransform, "Reset child brush transform");
							brushTransform.localPosition = Vector3.zero;
							brushTransform.localRotation = Quaternion.identity;
							brushTransform.localScale = Vector3.one;

							var brush = UnityEditor.Undo.AddComponent<CSGBrush>(brushGameObject);
							brush.BrushMeshAsset = newBrushMeshAsset;
							brush.LocalTransformation = localTransformation;
							brush.PivotOffset = pivotOffset;
							brush.Operation = brushMeshAsset.SubMeshes[i].Operation;
						}
						finally
						{
							brushGameObject.SetActive(true);
						}
					}
					UnityEditor.Undo.SetCurrentGroupName("Converted " + NodeTypeName + " to Multiple Brushes");
				}
				success = true;
			}
			finally
			{
				topGameObject.SetActive(true);
			}
			return success;
		}
#endif

	}
}