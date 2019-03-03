using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HierarchyTests
{
	public partial class Operation_Parenting
	{
		[SetUp] public void Setup() { TestUtility.ClearScene(); }


		[UnityTest]
		public IEnumerator OperationWithChildBrush_ChildHasOperationAsParent()
		{
			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject = operation.gameObject;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation.transform;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.True(operationGameObject);
			Assert.True(operation);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation.Node.Count);
			Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator OperationWithChildOperation_ChildHasOperationAsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent = operation1.transform;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);

			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator OperationWithWithGameObjectWithBrush_BrushHasOperationAsParent()
		{
			var operation			= TestUtility.CreateUndoableGameObjectWithOperation("operation");
			var operationGameObject = operation.gameObject;

			var plainGameObject		= TestUtility.CreateGameObject("gameObject");
			plainGameObject.transform.parent = operationGameObject.transform;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
			var brushGameObject		= brush.gameObject;
			brush.transform.parent = plainGameObject.transform;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.True(operationGameObject);
			Assert.True(operation);
			Assert.True(plainGameObject);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation.Node.Count);
			Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator Operation1WithWithGameObjectWithBrush_AddOperation2ToGameObject_BrushHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation("operation1");
			var operation1GameObject	= operation1.gameObject;

			var operation2GameObject	= TestUtility.CreateGameObject("operation2");
			operation2GameObject.transform.parent = operation1GameObject.transform;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2GameObject.transform;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID);

			var operation2			= TestUtility.CreateUndoableOperationComponent(operation2GameObject);
			yield return null;
			
			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
			Assert.AreEqual(1, operation2.Node.Count);
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);
			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator GameObjectWithBrush_AddOperationToGameObject_BrushHasOperationAsParent()
		{
			var operationGameObject	= TestUtility.CreateGameObject("operation");

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operationGameObject.transform;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.True(operationGameObject);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);

			var operation			= TestUtility.CreateUndoableOperationComponent(operationGameObject);
			yield return null;
			Assert.True(operationGameObject);
			Assert.True(operation);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation.Node.Count);
			Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator CreateOperation_AddChildBrush_ChildHasOperationAsParent()
		{
			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject = operation.gameObject;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);

			brush.transform.parent = operation.transform;
			yield return null;
			
			Assert.True(operationGameObject);
			Assert.True(operation);
			Assert.True(brushGameObject); 
			Assert.True(brush);
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation.Node.Count);
			Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID); 
		}

		[UnityTest]
		public IEnumerator CreateOperation_AddChildOperation_ChildHasOperationAsParent()
		{
			var operation1			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject = operation1.gameObject;

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.Parent.NodeID);

			operation2.transform.parent = operation1.transform;
			yield return null;
			
			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject); 
			Assert.True(operation2);
			
			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID); 
		}

		[UnityTest]
		public IEnumerator Operation1WithDisabledOperation2_AddChildBrush_ChildHasParentOfOperationAsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;
			operation2.enabled			= false;
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject); 
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist"); 
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);	
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID); 		
		}

		[UnityTest]
		public IEnumerator Operation1WithDisabledOperation2_AddChildOperation_ChildHasParentOfOperationAsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;
			operation2.enabled			= false;
			
			var operation3				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation3GameObject	= operation3.gameObject;
			operation3.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(operation3GameObject); 
			Assert.True(operation3);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist"); 
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);	
			Assert.AreEqual(operation1.Node.NodeID, operation3.Node.Parent.NodeID); 		
		}

		[UnityTest]
		public IEnumerator Operation1WithChildBrush_MoveChildToOperation2_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation("operation1");
			var operation1GameObject	= operation1.gameObject;

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation("operation2");
			var operation2GameObject	= operation2.gameObject;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation1.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
				
			yield return null;

			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation1.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
				
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(0, operation2.Node.Count);
				
			brush.transform.parent	= operation2.transform;
			yield return null;
				
			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(0, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);
		}

		[UnityTest]
		public IEnumerator Operation1WithChildBrush_MoveChildToGameObject_ChildHasNoParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation("operation1");
			var operation1GameObject	= operation1.gameObject;

			var plainGameObject			= TestUtility.CreateGameObject("gameObject");

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation1.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(plainGameObject);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
				
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
				
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation1.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
				
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(1, operation1.Node.Count);
				
			brush.transform.parent	= plainGameObject.transform;
			yield return null;

			Assert.AreEqual(0, operation1.Node.Count);
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator Operation1WithChildBrush_MoveBrushToNoneCSGNodeChildOfOperation2_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation("operation1");
			var operation1GameObject	= operation1.gameObject;

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation("operation2");
			var operation2GameObject	= operation2.gameObject;
				
			var plainGameObject			= TestUtility.CreateGameObject("gameObject");
			plainGameObject.transform.parent = operation2.transform;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation1.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
				
			yield return null;
			
			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation1.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
				
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(0, operation2.Node.Count);
				
			brush.transform.parent	= plainGameObject.transform;
			yield return null;
				
			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(0, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);
		}

		[UnityTest]
		public IEnumerator Operation1WithChildGameObjectWithChildBrush_MoveChildToOperation2_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation("operation1");
			var operation1GameObject	= operation1.gameObject;

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation("operation2");
			var operation2GameObject	= operation2.gameObject;
				
			var plainGameObject			= TestUtility.CreateGameObject("gameObject");
			plainGameObject.transform.parent = operation1.transform;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= plainGameObject.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
				
			yield return null;

			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation1.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
				
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(0, operation2.Node.Count);
				
			brush.transform.parent	= operation2.transform;
			yield return null;
				
			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID);

			Assert.AreEqual(0, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);
		}


		[UnityTest]
		public IEnumerator Operation1WithDisabledOperation2WithChildBrush_EnableOperation2_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation("operation1");
			var operation1GameObject	= operation1.gameObject;

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation("operation2");
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent = operation1.transform;
			operation2.enabled = false;

			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
				
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

			Assert.AreEqual(1, operation1.Node.Count);

			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID);

			operation2.enabled = true;
			yield return null;

			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID);
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);
		}

		[UnityTest]
		public IEnumerator Operation1WithOperation2WithChildBrush_DisableOperation2Component_ChildHasOperation1AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;
			operation2.enabled			= false;
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject); 
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID); 
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
		}

		[UnityTest]
		public IEnumerator OperationWithChildBrush_DisableAndEnableOperationComponent_ChildHasOperationAsParent()
		{
			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject	= operation.gameObject;
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation.transform;
			
			
			yield return null;
			
			operation.enabled			= false;
			
			yield return null;

			operation.enabled			= true;

			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

			Assert.AreEqual(1, operation.Node.Count);
			Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID); 
		}

		[UnityTest]
		public IEnumerator Operation1WithOperation2WithChildBrush_DestroyOperationComponent2_ChildHasOperation1AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject); 
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");	

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);
			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID); 
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID); 
			
			Undo.DestroyObjectImmediate(operation2);
			yield return null;
			
			Assert.False(operation2);

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	
			
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID); 
		}

		[UnityTest]
		public IEnumerator Operation1WithDisabledOperation2WithChildBrush_EnableOperation2Component_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;
			operation2.enabled			= false;
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject); 
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

			Assert.AreEqual(1, operation1.Node.Count);

			Assert.AreEqual(operation1.Node.NodeID, brush.TopNode.Parent.NodeID); 
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			
			operation2.enabled				= true;
			yield return null;	
			
			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");	

			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);

			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID); 
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID); 
		}

		[UnityTest]
		public IEnumerator Operation1WithDeactivatedOperation2WithChildBrush_ActivateOperation2GameObject_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;
			operation2GameObject.SetActive(false);
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject); 
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");	

			Assert.AreEqual(0, operation1.Node.Count);

			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID); 
			Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);
			
			operation2GameObject.SetActive(true); 
			yield return null;	
			
			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
			Assert.AreEqual(1, operation1.Node.Count);
			Assert.AreEqual(1, operation2.Node.Count);

			Assert.AreEqual(operation2.Node.NodeID, brush.TopNode.Parent.NodeID);
			Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);
		}

		[UnityTest]
		public IEnumerator Operation1WithPassthroughOperation2_AddChild_ChildHasOperation1AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;
			
			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent	= operation1.transform;

			operation2.PassThrough		= true;
			
			var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject		= brush.gameObject;
			brush.transform.parent	= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject); 
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(brush.TopNode.Parent, operation1.Node); 
			Assert.AreEqual(operation1.Node.Count, 1);
			Assert.AreEqual(operation2.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);			
		}

		[UnityTest]
		public IEnumerator Operation1WithPassthroughOperation2WithChildBrush_DisablePassthrough_ChildHasOperation2AsParent()
		{
			var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation1GameObject	= operation1.gameObject;

			var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
			var operation2GameObject	= operation2.gameObject;
			operation2.transform.parent = operation1.transform;

			operation2.PassThrough = true;

			var brush					= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject			= brush.gameObject;
			brush.transform.parent		= operation2.transform;

			Assert.True(operation1GameObject);
			Assert.True(operation1);
			Assert.True(operation2GameObject);
			Assert.True(operation2);
			Assert.True(brushGameObject);
			Assert.True(brush);

			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(brush.TopNode.Parent, operation1.Node);
			Assert.AreEqual(operation1.Node.Count, 1);
			Assert.AreEqual(operation2.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);

			operation2.PassThrough = false;
			yield return null;

			Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
			Assert.AreEqual(brush.TopNode.Parent, operation2.Node);
			Assert.AreEqual(operation2.Node.Parent, operation1.Node);
			Assert.AreNotEqual(operation2.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
			Assert.AreEqual(operation1.Node.Count, 1);
			Assert.AreEqual(operation2.Node.Count, 1);
		}
	}
}