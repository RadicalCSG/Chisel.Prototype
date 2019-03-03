using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HierarchyTests
{
	public partial class Brush_Scene
	{
		[SetUp] public void Setup() { TestUtility.ClearScene(); }


		[UnityTest]
		public IEnumerator CreateBrushInScene1_MoveToScene2_BrushOnlyExistsInScene2()
		{
			var scene2			= TestUtility.defaultScene;
			EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
			var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			EditorSceneManager.SetActiveScene(scene1);

			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;
			
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(scene1, brush.hierarchyItem.Scene);
			Undo.MoveGameObjectToScene(brushGameObject, scene2, "Move gameObject to different scene");
			yield return null;
			
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			
			Assert.AreEqual(scene2, brush.gameObject.scene, "Brush is not part of expected scene");
			Assert.AreEqual(scene2, brush.hierarchyItem.Scene, "Brush is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}

		[UnityTest]
		public IEnumerator SaveBrushInScene_LoadScene_BrushTreeNodeIsGenerated()
		{
			var scene1	= TestUtility.defaultScene;
			{ 
				TestUtility.CreateUndoableGameObjectWithBrush();
				EditorSceneManager.SaveScene(scene1, TestUtility.tempFilename);
			}

			var scene2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
			yield return null;
			
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene2));
			
			var scene3	= EditorSceneManager.OpenScene(TestUtility.tempFilename);
			var brushes = Object.FindObjectsOfType<CSGBrush>();
			yield return null;

			Assert.NotNull(brushes);
			Assert.AreEqual(1, brushes.Length);

			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene3));

			Assert.AreEqual(scene3, brushes[0].gameObject.scene, "Brush is not part of expected scene");
			Assert.AreEqual(scene3, brushes[0].hierarchyItem.Scene, "Brush is not registered to expected scene");

			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1)); // unloaded, so should be unknown to us
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene2)); // unloaded, so should be unknown to us
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene3));

			// make sure test runner doesn't puke on its own bugs
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
		}
	}
}