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
    public partial class Model_Scene
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator ModelInScene1_MoveToScene2_ModelOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(scene1, model.hierarchyItem.Scene);

            Undo.MoveGameObjectToScene(modelGameObject, scene2, "Move gameObject to different scene");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            
            Assert.AreEqual(scene2, model.gameObject.scene, "Model is not part of expected scene");
            Assert.AreEqual(scene2, model.hierarchyItem.Scene, "Model is not registered to expected scene");

            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator GameObjectInScene1WithModel_MoveModelToGameObjectInScene2_ModelOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            var scene2GameObject = new GameObject();
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);

            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);
            var scene1GameObject = new GameObject();

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;
            model.transform.parent = scene1GameObject.transform;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename2);
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(scene1, model.hierarchyItem.Scene);

            
            model.transform.parent = scene2GameObject.transform;
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            
            Assert.AreEqual(scene2, model.gameObject.scene, "Model is not part of expected scene");
            Assert.AreEqual(scene2, model.hierarchyItem.Scene, "Model is not registered to expected scene");

            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator GameObjectInScene1WithModel_MoveGameObjectToScene2_ModelOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);

            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);
            var scene1GameObject = new GameObject();

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;
            model.transform.parent = scene1GameObject.transform;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename2);
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(scene1, model.hierarchyItem.Scene);

            
            Undo.MoveGameObjectToScene(scene1GameObject, scene2, "Move gameObject to different scene");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            
            Assert.AreEqual(scene2, model.gameObject.scene, "Model is not part of expected scene");
            Assert.AreEqual(scene2, model.hierarchyItem.Scene, "Model is not registered to expected scene");

            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator ModelWithChildInScene1_MoveToScene2_ModelWithChildOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);

            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);

            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject = brush.gameObject;
            brush.transform.parent = model.transform;
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(scene1, model.gameObject.scene);
            Assert.AreEqual(scene1, model.hierarchyItem.Scene);

            Undo.MoveGameObjectToScene(modelGameObject, scene2, "Move gameObject to different scene");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Assert.AreEqual(model.hierarchyItem, brush.hierarchyItem.Parent);
            Assert.AreEqual(model.NodeID, brush.hierarchyItem.Parent.Component.NodeID);
            Assert.AreEqual(model.NodeID, brush.TopNode.Tree.NodeID);
            
            Assert.AreEqual(scene2, model.gameObject.scene, "Model is not part of expected scene");
            Assert.AreEqual(scene2, model.hierarchyItem.Scene, "Model is not registered to expected scene");
            
            Assert.AreEqual(scene2, brush.gameObject.scene, "Brush is not part of expected scene");
            Assert.AreEqual(scene2, brush.hierarchyItem.Scene, "Brush is not registered to expected scene");

            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator SaveModelInScene_LoadScene_ModelTreeNodeIsGenerated()
        {
            var scene1	= TestUtility.defaultScene;
            { 
                TestUtility.CreateUndoableGameObjectWithModel();
                EditorSceneManager.SaveScene(scene1, TestUtility.tempFilename);
            }

            var scene2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return null;
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene2));
            
            var scene3	= EditorSceneManager.OpenScene(TestUtility.tempFilename);
            var models	= Object.FindObjectsOfType<CSGModel>();
            yield return null;

            Assert.NotNull(models);
            Assert.AreEqual(1, models.Length);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene3));

            Assert.AreEqual(scene3, models[0].gameObject.scene, "Model is not part of expected scene");
            Assert.AreEqual(scene3, models[0].hierarchyItem.Scene, "Model is not registered to expected scene");

            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene1)); // unloaded, so should be unknown to us
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene2)); // unloaded, so should be unknown to us
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene3));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
    }
}