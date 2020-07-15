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
    public partial class Composite_Undo
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }
        

        [UnityTest]
        public IEnumerator CreateComposite_UndoCreateGameObject_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.False(compositeGameObject);
            Assert.False(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateComposite_UndoCreateComponent_CompositeDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateGameObjectWithUndoableCompositeComponent();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.True(compositeGameObject);
            Assert.False(composite);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }
        

        [UnityTest]
        public IEnumerator CreateAndDestroyCompositeGameObject_Undo_CompositeExists()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
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

            Undo.PerformUndo();
            composite = Object.FindObjectsOfType<ChiselComposite>()[0];
            compositeGameObject = composite.gameObject;
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateAndDestroyCompositeComponent_Undo_CompositeExists()
        {
            var scene				= TestUtility.defaultScene;
            var composite			= TestUtility.CreateGameObjectWithUndoableCompositeComponent();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
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

            Undo.PerformUndo();
            composite = compositeGameObject.GetComponent<ChiselComposite>();
            yield return null;

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }


        [UnityTest]
        public IEnumerator CreateComposite_UndoCreateGameObject_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(compositeGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.False(compositeGameObject);
            Assert.False(composite);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateComposite_UndoCreateComponent_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var composite			= TestUtility.CreateGameObjectWithUndoableBrushComponent();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(newScene, compositeGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.True(compositeGameObject);
            Assert.False(composite);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }
    }
}