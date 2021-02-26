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
/*
namespace HierarchyTests
{
    public partial class Composite_Scene
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator CompositeInScene1_MoveToScene2_CompositeOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(scene1, composite.hierarchyItem.Scene);

            Undo.MoveGameObjectToScene(compositeGameObject, scene2, "Move gameObject to different scene");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Assert.AreEqual(scene2, composite.gameObject.scene, "Composite is not part of expected scene");
            Assert.AreEqual(scene2, composite.hierarchyItem.Scene, "Composite is not registered to expected scene");

            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildInScene1_MoveToScene2_CompositeWithChildOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject = brush.gameObject;
            brush.transform.parent = composite.transform;
            
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(scene1, composite.gameObject.scene);
            Assert.AreEqual(scene1, composite.hierarchyItem.Scene);
            
            Undo.MoveGameObjectToScene(compositeGameObject, scene2, "Move gameObject to different scene");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            
            Assert.AreEqual(composite.hierarchyItem, brush.hierarchyItem.Parent);
            Assert.AreEqual(composite.NodeID, brush.hierarchyItem.Parent.Component.NodeID);
            Assert.AreEqual(composite.NodeID, brush.TopNode.Parent.NodeID);
            
            Assert.AreEqual(scene2, composite.gameObject.scene, "Composite is not part of expected scene");
            Assert.AreEqual(scene2, composite.hierarchyItem.Scene, "Composite is not registered to expected scene");
            
            Assert.AreEqual(scene2, brush.gameObject.scene, "Brush is not part of expected scene");
            Assert.AreEqual(scene2, brush.hierarchyItem.Scene, "Brush is not registered to expected scene");

            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator GameObjectInScene1WithComposite_MoveGameObjectToScene2_CompositeOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);
            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);
            var scene1GameObject = new GameObject();

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.transform.parent = scene1GameObject.transform;
            
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(scene1, composite.hierarchyItem.Scene);

            
            Undo.MoveGameObjectToScene(scene1GameObject, scene2, "Move gameObject to different scene");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Assert.AreEqual(scene2, composite.gameObject.scene, "Composite is not part of expected scene");
            Assert.AreEqual(scene2, composite.hierarchyItem.Scene, "Composite is not registered to expected scene");

            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator GameObjectInScene1WithComposite_MoveCompositeToGameObjectInScene2_CompositeOnlyExistsInScene2()
        {
            var scene2			= TestUtility.defaultScene;
            var scene2GameObject = new GameObject();
            EditorSceneManager.SaveScene(scene2, TestUtility.tempFilename);

            var scene1			= EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene1);
            var scene1GameObject = new GameObject();

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.transform.parent = scene1GameObject.transform;
            
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(scene1, composite.hierarchyItem.Scene);

            
            composite.transform.parent = scene2GameObject.transform;
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Assert.AreEqual(scene2, composite.gameObject.scene, "Composite is not part of expected scene");
            Assert.AreEqual(scene2, composite.hierarchyItem.Scene, "Composite is not registered to expected scene");

            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene1));
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene2));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator SaveCompositeInScene_LoadScene_CompositeTreeNodeIsGenerated()
        {
            var scene1	= TestUtility.defaultScene;
            { 
                TestUtility.CreateUndoableGameObjectWithComposite();
                EditorSceneManager.SaveScene(scene1, TestUtility.tempFilename);
            }

            var scene2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return null;
            
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene2));
            
            var scene3		= EditorSceneManager.OpenScene(TestUtility.tempFilename);
            var composites	= Object.FindObjectsOfType<ChiselComposite>();
            yield return null;

            Assert.NotNull(composites);
            Assert.AreEqual(1, composites.Length);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Assert.AreEqual(scene3, composites[0].gameObject.scene, "Composite is not part of expected scene");
            Assert.AreEqual(scene3, composites[0].hierarchyItem.Scene, "Composite is not registered to expected scene");

            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene1)); // unloaded, so should be unknown to us
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene2)); // unloaded, so should be unknown to us
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene3));

            // make sure test runner doesn't puke on its own bugs
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
    }
}*/