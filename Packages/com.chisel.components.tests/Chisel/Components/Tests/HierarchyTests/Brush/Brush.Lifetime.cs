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
	public partial class Brush_Lifetime
	{
		[SetUp] public void Setup() { TestUtility.ClearScene(); }


		[UnityTest]
		public IEnumerator CreateBrush_DestroyBrushGameObject_BrushDoesNotExist()
		{
			var scene			= TestUtility.defaultScene;
			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			Undo.DestroyObjectImmediate(brushGameObject);
			yield return null;

			Assert.False(brushGameObject);
			Assert.False(brush);
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
		}

		[UnityTest]
		public IEnumerator CreateBrush_DestroyBrushComponent_BrushDoesNotExist()
		{
			var scene			= TestUtility.defaultScene;
			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			Undo.DestroyObjectImmediate(brush);
			yield return null;

			Assert.True(brushGameObject);
			Assert.False(brush);
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
		}

		[UnityTest]
		public IEnumerator CreateBrush_DeactivateBrushGameObject_BrushDoesNotExist()
		{
			var scene			= TestUtility.defaultScene;
			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");	// default model
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			brushGameObject.SetActive(false);
			yield return null;

			Assert.True(brushGameObject);
			Assert.True(brush);
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
		}

		[UnityTest]
		public IEnumerator CreateBrush_DisableBrushComponent_BrushDoesNotExist()
		{
			var scene			= TestUtility.defaultScene;
			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

			brush.enabled = false;
			yield return null;

			Assert.True(brushGameObject);
			Assert.True(brush);
			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
		}

		[UnityTest]
		public IEnumerator CreateBrush_ActivateBrushGameObject_BrushExist()
		{
			var scene			= TestUtility.defaultScene;
			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;
			brushGameObject.SetActive(false);

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

			brushGameObject.SetActive(true);
			yield return null;

			Assert.True(brushGameObject);
			Assert.True(brush);
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); // default model
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene));
		}

		[UnityTest]
		public IEnumerator CreateBrush_EnableBrushComponent_BrushExist()
		{
			var scene			= TestUtility.defaultScene;
			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;
			brush.enabled = false;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
			yield return null;

			Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
			Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

			brush.enabled = true;
			yield return null;

			Assert.True(brushGameObject);
			Assert.True(brush);
			Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
			Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); // default model
			Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
			Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene));
		}

		[UnityTest]
		public IEnumerator CreateBrush_DestroyBrushGameObject_OnlyDirtiesSceneOfBrush()
		{
			var currentScene	= SceneManager.GetActiveScene();
			var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
			Assert.False(currentScene.isDirty);
			Assert.False(newScene.isDirty);

			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(brushGameObject.scene, newScene);
			Assert.True(newScene.isDirty);
			Assert.False(currentScene.isDirty);

			Undo.DestroyObjectImmediate(brushGameObject);
			yield return null;

			Assert.False(brushGameObject);
			Assert.False(brush);
			Assert.True(newScene.isDirty);
			Assert.False(currentScene.isDirty);
		}

		[UnityTest]
		public IEnumerator CreateBrush_DestroyBrushComponent_OnlyDirtiesSceneOfBrush()
		{
			var currentScene	= SceneManager.GetActiveScene();
			var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
			Assert.False(currentScene.isDirty);
			Assert.False(newScene.isDirty);

			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(brushGameObject.scene, newScene);
			Assert.True(newScene.isDirty);
			Assert.False(currentScene.isDirty);

			Undo.DestroyObjectImmediate(brush);
			yield return null;

			Assert.True(brushGameObject);
			Assert.False(brush);
			Assert.True(newScene.isDirty);
			Assert.False(currentScene.isDirty);
		}

		[Test]
		public void CreateBrush_OnlyDirtiesSceneOfBrush()
		{
			var currentScene	= SceneManager.GetActiveScene();
			var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
			Assert.False(currentScene.isDirty);
			Assert.False(newScene.isDirty);

			var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
			var brushGameObject = brush.gameObject;

			Assert.AreEqual(brushGameObject.scene, newScene);
			Assert.True(newScene.isDirty);
			Assert.False(currentScene.isDirty);
		}
	}
}