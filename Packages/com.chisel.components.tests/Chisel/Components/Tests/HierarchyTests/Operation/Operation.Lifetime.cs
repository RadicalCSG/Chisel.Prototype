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
    public partial class Operation_Lifetime
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }

        // TODO: test passthrough

        [UnityTest]
        public IEnumerator CreateOperation_DestroyOperationGameObject_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
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
        }

        [UnityTest]
        public IEnumerator CreateOperation_DestroyOperationComponent_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
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
        }

        [UnityTest]
        public IEnumerator CreateOperation_DeactivateOperationGameObject_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            operationGameObject.SetActive(false);
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateOperation_DisableOperationComponent_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            operation.enabled = false;
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateOperation_ActivateOperationGameObject_OperationExists()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operationGameObject.SetActive(false);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            operationGameObject.SetActive(true);
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateOperation_EnableOperationComponent_OperationExists()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operation.enabled = false;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            operation.enabled = true;
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateOperation_DestroyOperationGameObject_OnlyDirtiesSceneOfOperation()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(newScene, operationGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.DestroyObjectImmediate(operationGameObject);
            yield return null;

            Assert.False(operationGameObject);
            Assert.False(operation);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateOperation_DestroyOperationComponent_OnlyDirtiesSceneOfOperation()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(newScene, operationGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.DestroyObjectImmediate(operation);
            yield return null;

            Assert.True(operationGameObject);
            Assert.False(operation);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [Test]
        public void CreateOperation_OnlyDirtiesSceneOfOperation()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(newScene, operationGameObject.scene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator OperationWithChildBrush_DestroyOperation_ChildIsDestroyed()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject	= operation.gameObject;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation.transform;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreEqual(1, operation.Node.Count, 1);
            Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID); 
            
            Undo.DestroyObjectImmediate(operationGameObject);
            yield return null;
            
            Assert.False(operationGameObject);
            Assert.False(operation);
            Assert.False(brushGameObject); 
            Assert.False(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator Operation1WithOperation2WithChildBrush_DeactivateOperation2GameObject_ChildIsAlsoDeactivated()
        {
            var scene					= TestUtility.defaultScene;
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;
            
            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent	= operation1.transform;
            operation2GameObject.SetActive(false);
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation2.transform;

            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Assert.AreEqual(0, operation1.Node.Count);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, operation2.Node.NodeID);	
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateOperation_EnablePassThrough_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            operation.PassThrough = true;
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(operation.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator OperationWithPassThroughEnabled_DisablePassThrough_OperationExists()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operation.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.True(operationGameObject);
            Assert.True(operation);
            
            operation.PassThrough = false;
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreNotEqual(operation.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator OperationWithPassThroughEnabled_DestroyOperationGameObject_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operation.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.True(operationGameObject);
            Assert.True(operation);

            Undo.DestroyObjectImmediate(operationGameObject);
            yield return null;

            Assert.False(operationGameObject);
            Assert.False(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator OperationWithPassThroughEnabled_DestroyOperationComponent_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operation.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.True(operationGameObject);
            Assert.True(operation);

            Undo.DestroyObjectImmediate(operation);
            yield return null;

            Assert.True(operationGameObject);
            Assert.False(operation);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator DeactivatedOperationGameObjectWithPassThroughEnabled_ActivateOperationGameObject_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operationGameObject.SetActive(false);
            operation.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            operationGameObject.SetActive(true);
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(operation.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator DisabledOperationWithPassThroughEnabled_EnableOperationComponent_OperationDoesNotExist()
        {
            var scene				= TestUtility.defaultScene;
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;
            operation.enabled = false;
            operation.PassThrough = true;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            operation.enabled = true;
            yield return null;

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.AreEqual(operation.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, ChiselNodeHierarchyManager.RootCount(scene));
        }
    }
}