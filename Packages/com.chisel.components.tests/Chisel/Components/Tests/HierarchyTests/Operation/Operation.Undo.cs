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
    public partial class Operation_Undo
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }
        

        [UnityTest]
        public IEnumerator CreateOperation_UndoCreateGameObject_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.False(operationGameObject);
            Assert.False(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateOperation_UndoCreateComponent_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateGameObjectWithUndoableOperationComponent();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.True(operationGameObject);
            Assert.False(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }
        

        [UnityTest]
        public IEnumerator CreateAndDestroyOperationGameObject_Undo_OperationExists()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Undo.DestroyObjectImmediate(operationGameObject);
            yield return null;

            Assert.False(operationGameObject);
            Assert.False(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));

            Undo.PerformUndo();
            operation = Object.FindObjectsOfType<ChiselOperation>()[0];
            operationGameObject = operation.gameObject;
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateAndDestroyOperationComponent_Undo_OperationExists()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateGameObjectWithUndoableOperationComponent();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Undo.DestroyObjectImmediate(operation);
            yield return null;

            Assert.True(operationGameObject);
            Assert.False(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));

            Undo.PerformUndo();
            operation = operationGameObject.GetComponent<ChiselOperation>();
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }


        [UnityTest]
        public IEnumerator CreateOperation_UndoCreateGameObject_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(operationGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.False(operationGameObject);
            Assert.False(operation);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateOperation_UndoCreateComponent_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var operation			= TestUtility.CreateGameObjectWithUndoableBrushComponent();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(newScene, operationGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.True(operationGameObject);
            Assert.False(operation);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }
    }
}