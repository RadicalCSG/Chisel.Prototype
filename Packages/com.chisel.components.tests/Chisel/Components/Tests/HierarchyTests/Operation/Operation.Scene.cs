using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace HierarchyTests
{
	public partial class Operation_Scene
	{
		[SetUp] public void Setup() { TestUtility.ClearScene(); }


		[UnityTest]
		public IEnumerator OperationInScene1_MoveToScene2_OperationOnlyExistsInScene2()
		{
			var scene2			= TestUtility.defaultScene;
			EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
			var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			EditorSceneManager.SetActiveScene(scene1);

			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject = operation.gameObject;
			
			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(scene1, operation.hierarchyItem.Scene);

			Undo.MoveGameObjectToScene(operationGameObject, scene2, "Move gameObject to different scene");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			
			Assert.AreEqual(scene2, operation.gameObject.scene, "Operation is not part of expected scene");
			Assert.AreEqual(scene2, operation.hierarchyItem.Scene, "Operation is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}

		[UnityTest]
		public IEnumerator OperationWithChildInScene1_MoveToScene2_OperationWithChildOnlyExistsInScene2()
		{
			var scene2			= TestUtility.defaultScene;
			EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
			var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			EditorSceneManager.SetActiveScene(scene1);

			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject = operation.gameObject;

			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;
			brush.transform.parent = operation.transform;
			
			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			Assert.AreEqual(scene1, operation.gameObject.scene);
			Assert.AreEqual(scene1, operation.hierarchyItem.Scene);
			
			Undo.MoveGameObjectToScene(operationGameObject, scene2, "Move gameObject to different scene");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
			
			Assert.AreEqual(operation.hierarchyItem, brush.hierarchyItem.Parent);
			Assert.AreEqual(operation.NodeID, brush.hierarchyItem.Parent.Component.NodeID);
			Assert.AreEqual(operation.NodeID, brush.TopNode.Parent.NodeID);
			
			Assert.AreEqual(scene2, operation.gameObject.scene, "Operation is not part of expected scene");
			Assert.AreEqual(scene2, operation.hierarchyItem.Scene, "Operation is not registered to expected scene");
			
			Assert.AreEqual(scene2, brush.gameObject.scene, "Brush is not part of expected scene");
			Assert.AreEqual(scene2, brush.hierarchyItem.Scene, "Brush is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}

		[UnityTest]
		public IEnumerator GameObjectInScene1WithOperation_MoveGameObjectToScene2_OperationOnlyExistsInScene2()
		{
			var scene2			= TestUtility.defaultScene;
			EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
			var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			EditorSceneManager.SetActiveScene(scene1);
			var scene1GameObject = new GameObject();

			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject = operation.gameObject;
			operation.transform.parent = scene1GameObject.transform;
			
			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(scene1, operation.hierarchyItem.Scene);

			
			Undo.MoveGameObjectToScene(scene1GameObject, scene2, "Move gameObject to different scene");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			
			Assert.AreEqual(scene2, operation.gameObject.scene, "Operation is not part of expected scene");
			Assert.AreEqual(scene2, operation.hierarchyItem.Scene, "Operation is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}

		[UnityTest]
		public IEnumerator GameObjectInScene1WithOperation_MoveOperationToGameObjectInScene2_OperationOnlyExistsInScene2()
		{
			var scene2			= TestUtility.defaultScene;
			var scene2GameObject = new GameObject();
			EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);

			var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			EditorSceneManager.SetActiveScene(scene1);
			var scene1GameObject = new GameObject();

			var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
			var operationGameObject = operation.gameObject;
			operation.transform.parent = scene1GameObject.transform;
			
			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(scene1, operation.hierarchyItem.Scene);

			
			operation.transform.parent = scene2GameObject.transform;
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			
			Assert.AreEqual(scene2, operation.gameObject.scene, "Operation is not part of expected scene");
			Assert.AreEqual(scene2, operation.hierarchyItem.Scene, "Operation is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}

		[UnityTest]
		public IEnumerator SaveOperationInScene_LoadScene_OperationTreeNodeIsGenerated()
		{
			var scene1	= TestUtility.defaultScene;
			{ 
				TestUtility.CreateUndoableGameObjectWithOperation();
				EditorSceneManager.SaveScene(scene1, TestUtility.tempFilename);
			}

			var scene2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
			yield return null;
			
			Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene2));
			
			var scene3		= EditorSceneManager.OpenScene(TestUtility.tempFilename);
			var operations	= Object.FindObjectsOfType<CSGOperation>();
			yield return null;

			Assert.NotNull(operations);
			Assert.AreEqual(1, operations.Length);

			Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			Assert.AreEqual(scene3, operations[0].gameObject.scene, "Operation is not part of expected scene");
			Assert.AreEqual(scene3, operations[0].hierarchyItem.Scene, "Operation is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1)); // unloaded, so should be unknown to us
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene2)); // unloaded, so should be unknown to us
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene3));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}
	}
}