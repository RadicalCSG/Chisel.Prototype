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
/*
namespace HierarchyTests
{
    public partial class Model_Lifetime
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator CreateModel_DestroyModelComponent_ModelDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            Undo.DestroyObjectImmediate(model);
            yield return null;

            Assert.True(modelGameObject);
            Assert.False(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_DestroyModelGameObject_ModelDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            Undo.DestroyObjectImmediate(modelGameObject);
            yield return null;

            Assert.False(modelGameObject);
            Assert.False(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_DisableModelComponent_ModelDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            model.enabled = false;
            yield return null;

            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_DeactivateModelGameObject_ModelDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            modelGameObject.SetActive(false);
            yield return null;

            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_EnableModelComponent_ModelExists()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;
            model.enabled = false;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            model.enabled = true;
            yield return null;

            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_ActivateModelGameObject_ModelExists()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;
            modelGameObject.SetActive(false);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            modelGameObject.SetActive(true);
            yield return null;

            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateModel_DestroyModelComponent_OnlyDirtiesSceneOfModel()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            SceneManager.SetActiveScene(newScene);
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(modelGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.DestroyObjectImmediate(model);
            yield return null;

            Assert.True(modelGameObject);
            Assert.False(model);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateModel_DestroyModelGameObject_OnlyDirtiesSceneOfModel()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(modelGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.DestroyObjectImmediate(modelGameObject);
            yield return null;

            Assert.False(modelGameObject);
            Assert.False(model);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [Test]
        public void CreateModel_OnlyDirtiesSceneOfModel()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(modelGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator ModelWithChildBrush_DestroyModel_ChildIsAlsoDestroyed()
        {
            var scene			= TestUtility.defaultScene;
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject	= model.gameObject;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model.transform;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Assert.AreEqual(1, model.Node.Count, 1);
            Assert.AreEqual(model.Node, brush.TopNode.Tree); 
            
            Undo.DestroyObjectImmediate(modelGameObject);
            yield return null;
            
            Assert.False(modelGameObject);
            Assert.False(model);
            Assert.False(brushGameObject); 
            Assert.False(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator Model1WithModel2WithChildBrush_DeactivateModel2GameObject_ChildIsAlsoDeactivated()
        {
            var scene				= TestUtility.defaultScene;
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;
            
            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;
            model2GameObject.SetActive(false);
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(CSGTreeNode.InvalidNode, brush.TopNode); 
            Assert.AreEqual(CSGTreeNode.InvalidNode, model2.Node);	
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

    }
}*/