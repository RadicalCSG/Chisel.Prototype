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
    public partial class Model_Undo
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator CreateModel_UndoCreateGameObject_ModelDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.False(modelGameObject);
            Assert.False(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_UndoCreateComponent_ModelDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateGameObjectWithUndoableModelComponent();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.True(modelGameObject);
            Assert.False(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateAndDestroyModelComponent_Undo_ModelExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateGameObjectWithUndoableModelComponent();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            
            Undo.DestroyObjectImmediate(model);
            yield return null;

            Assert.True(modelGameObject);
            Assert.False(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            
            Undo.PerformUndo();
            yield return null;

            model = Object.FindObjectsOfType<ChiselModel>()[0];
            modelGameObject = model.gameObject;
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateAndDestroyModelGameObject_Undo_ModelExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            
            Undo.DestroyObjectImmediate(modelGameObject);
            yield return null;

            Assert.False(modelGameObject);
            Assert.False(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            
            Undo.PerformUndo();
            yield return null;

            model = modelGameObject.GetComponent<ChiselModel>();
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_UndoCreateGameObject_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(modelGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.False(modelGameObject);
            Assert.False(model);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateModel_UndoCreateComponent_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var model			= TestUtility.CreateGameObjectWithUndoableModelComponent();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(modelGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.True(modelGameObject);
            Assert.False(model);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }
    }
}