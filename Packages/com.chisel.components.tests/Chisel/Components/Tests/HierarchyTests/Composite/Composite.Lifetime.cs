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
    public partial class Composite_Lifetime
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }

        // TODO: test passthrough

        [UnityTest]
        public IEnumerator CreateComposite_DestroyCompositeGameObject_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.DestroyObjectImmediate(compositeGameObject);
            yield return null;

            Assert.False(compositeGameObject);
            Assert.False(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_DestroyCompositeComponent_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.DestroyObjectImmediate(composite);
            yield return null;

            Assert.True(compositeGameObject);
            Assert.False(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_DeactivateCompositeGameObject_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            compositeGameObject.SetActive(false);
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_DisableCompositeComponent_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            composite.enabled = false;
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_ActivateCompositeGameObject_CompositeExists()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            compositeGameObject.SetActive(false);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            compositeGameObject.SetActive(true);
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_EnableCompositeComponent_CompositeExists()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.enabled = false;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            composite.enabled = true;
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_DestroyCompositeGameObject_OnlyDirtiesSceneOfComposite()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(newScene, compositeGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.DestroyObjectImmediate(compositeGameObject);
            yield return null;

            Assert.False(compositeGameObject);
            Assert.False(composite);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateComposite_DestroyCompositeComponent_OnlyDirtiesSceneOfComposite()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(newScene, compositeGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.DestroyObjectImmediate(composite);
            yield return null;

            Assert.True(compositeGameObject);
            Assert.False(composite);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [Test]
        public void CreateComposite_OnlyDirtiesSceneOfComposite()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(newScene, compositeGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildBrush_DestroyComposite_ChildIsDestroyed()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject	= composite.gameObject;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreEqual(1, composite.Node.Count, 1);
            Assert.AreEqual((CSGTreeNode)composite.Node, (CSGTreeNode)brush.TopNode.Parent); 
            
            Undo.DestroyObjectImmediate(compositeGameObject);
            yield return null;
            
            Assert.False(compositeGameObject);
            Assert.False(composite);
            Assert.False(brushGameObject); 
            Assert.False(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator Composite1WithComposite2WithChildBrush_DeactivateComposite2GameObject_ChildIsAlsoDeactivated()
        {
            var scene					= TestUtility.defaultScene;
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;
            composite2GameObject.SetActive(false);
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite2.transform;

            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Assert.AreEqual(0, composite1.Node.Count);
            Assert.AreEqual((CSGTreeNode)CSGTreeNode.InvalidNode, (CSGTreeNode)brush.TopNode); 
            Assert.AreEqual((CSGTreeNode)CSGTreeNode.InvalidNode, (CSGTreeNode)composite2.Node);	
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_EnablePassThrough_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            composite.PassThrough = true;
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(composite.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CompositeWithPassThroughEnabled_DisablePassThrough_CompositeExists()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            composite.PassThrough = false;
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreNotEqual(composite.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CompositeWithPassThroughEnabled_DestroyCompositeGameObject_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.True(compositeGameObject);
            Assert.True(composite);

            Undo.DestroyObjectImmediate(compositeGameObject);
            yield return null;

            Assert.False(compositeGameObject);
            Assert.False(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CompositeWithPassThroughEnabled_DestroyCompositeComponent_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.True(compositeGameObject);
            Assert.True(composite);

            Undo.DestroyObjectImmediate(composite);
            yield return null;

            Assert.True(compositeGameObject);
            Assert.False(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator DeactivatedCompositeGameObjectWithPassThroughEnabled_ActivateCompositeGameObject_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            compositeGameObject.SetActive(false);
            composite.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            compositeGameObject.SetActive(true);
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(composite.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator DisabledCompositeWithPassThroughEnabled_EnableCompositeComponent_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;
            composite.enabled = false;
            composite.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            composite.enabled = true;
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(composite.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }
    }
}*/