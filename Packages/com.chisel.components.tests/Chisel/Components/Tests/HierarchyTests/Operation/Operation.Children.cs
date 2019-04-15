using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HierarchyTests
{
    public partial class Operation_Children
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator Operation_AddTwoChildBrushes_OperationHasChildrenInOrder()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject	= operation.gameObject;

            var brush1				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush1GameObject	= brush1.gameObject;
            brush1.transform.parent	= operation.transform;

            var brush2				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush2GameObject	= brush2.gameObject;
            brush2.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brush1GameObject);
            Assert.True(brush1);
            Assert.True(brush2GameObject);
            Assert.True(brush2);

            Assert.AreEqual(operation.transform.GetChild(0), brush1.transform);
            Assert.AreEqual(operation.transform.GetChild(1), brush2.transform);

            yield return null;			

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(2, operation.Node.Count);
            Assert.AreEqual(brush1.TopNode.NodeID, operation.Node[0].NodeID);
            Assert.AreEqual(brush2.TopNode.NodeID, operation.Node[1].NodeID);
        }

        [UnityTest]
        public IEnumerator OperationWithTwoChildBrushes_SwapOrder_OperationHasChildrenInOrder()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject	= operation.gameObject;

            var brush1				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush1GameObject	= brush1.gameObject;
            brush1.transform.parent	= operation.transform;

            var brush2				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush2GameObject	= brush2.gameObject;
            brush2.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brush1GameObject);
            Assert.True(brush1);
            Assert.True(brush2GameObject);
            Assert.True(brush2);

            Assert.AreEqual(operation.transform.GetChild(0), brush1.transform);
            Assert.AreEqual(operation.transform.GetChild(1), brush2.transform);

            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(2, operation.Node.Count);
            brush2.transform.SetSiblingIndex(0);

            Assert.AreEqual(operation.transform.GetChild(0), brush2.transform);
            Assert.AreEqual(operation.transform.GetChild(1), brush1.transform);
            yield return null;		

            Assert.AreEqual(brush2.TopNode.NodeID, operation.Node[0].NodeID);
            Assert.AreEqual(brush1.TopNode.NodeID, operation.Node[1].NodeID);
        }

        [UnityTest]
        public IEnumerator Operation_AddChildBrush_OperationHasChild()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject	= operation.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation.Node.Count);
            Assert.AreEqual(brush.TopNode.NodeID, operation.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator Operation_AddChildOperation_OperationHasChild()
        {
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;

            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent	= operation1.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation1.Node.Count);
            Assert.AreEqual(operation2.Node.NodeID, operation1.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator Operation_AddChildModel_OperationHasNoChildren()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject	= operation.gameObject;

            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;
            model.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(modelGameObject);
            Assert.True(model);
            
            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(0, operation.Node.Count);
        }

        [UnityTest]
        public IEnumerator OperationWithGameObject_AddChildBrush_OperationHasChild()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject	= operation.gameObject;

            var plainGameObject		= TestUtility.CreateGameObject();
            plainGameObject.transform.parent = operation.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= plainGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation.Node.Count);
            Assert.AreEqual(brush.TopNode.NodeID, operation.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator OperationWithGameObject_AddChildOperation_OperationHasChild()
        { 
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;

            var plainGameObject			= TestUtility.CreateGameObject();
            plainGameObject.transform.parent = operation1.transform;

            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent	= plainGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation1.Node.Count);
            Assert.AreEqual(operation2.Node.NodeID, operation1.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator OperationWithChildBrush_DestroyChildGameObject_OperationHasNoChildren()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation.Node.Count);
            Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(brushGameObject);
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.False(brushGameObject);
            Assert.False(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation.Node.Count);
        }

        [UnityTest]
        public IEnumerator OperationWithChildOperation_DestroyChildGameObject_OperationHasNoChildren()
        {
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;

            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent = operation1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation1.Node.Count);
            Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(operation2GameObject);
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.False(operation2GameObject);
            Assert.False(operation2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation1.Node.Count);
        }
        
        [UnityTest]
        public IEnumerator OperationWithChildBrush_DestroyChildComponent_OperationHasNoChildren()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation.Node.Count);
            Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(brush);
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.False(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation.Node.Count);
        }

        [UnityTest]
        public IEnumerator OperationWithChildOperation_DestroyChildComponent_OperationHasNoChildren()
        {
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;

            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent = operation1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation1.Node.Count);
            Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(operation2);
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.False(operation2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation1.Node.Count);
        }
        
        [UnityTest]
        public IEnumerator OperationWithChildBrush_DisableChildComponent_OperationHasNoChildren()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation.Node.Count);
            Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);

            brush.enabled = false;
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation.Node.Count);
        }

        [UnityTest]
        public IEnumerator OperationWithChildOperation_DisableChildComponent_OperationHasNoChildren()
        {
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;

            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent = operation1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation1.Node.Count);
            Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);
            
            operation2.enabled = false;
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation1.Node.Count);
        }

        [UnityTest]
        public IEnumerator OperationWithChildBrush_DeactivateChildGameObject_OperationHasNoChildren()
        {
            var operation			= TestUtility.CreateUndoableGameObjectWithOperation();
            var operationGameObject = operation.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= operation.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation.Node.Count);
            Assert.AreEqual(operation.Node.NodeID, brush.TopNode.Parent.NodeID);

            brushGameObject.SetActive(false);
            yield return null;
            
            Assert.True(operationGameObject);
            Assert.True(operation);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation.Node.Count);
        }

        [UnityTest]
        public IEnumerator OperationWithChildOperation_DeactivateChildGameObject_OperationHasNoChildren()
        {
            var operation1				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation1GameObject	= operation1.gameObject;

            var operation2				= TestUtility.CreateUndoableGameObjectWithOperation();
            var operation2GameObject	= operation2.gameObject;
            operation2.transform.parent = operation1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, operation1.Node.Count);
            Assert.AreEqual(operation1.Node.NodeID, operation2.Node.Parent.NodeID);

            operation2GameObject.SetActive(false);
            yield return null;
            
            Assert.True(operation1GameObject);
            Assert.True(operation1);
            Assert.True(operation2GameObject);
            Assert.True(operation2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, operation1.Node.Count);
        }
    }
}