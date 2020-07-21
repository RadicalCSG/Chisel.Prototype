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
    public partial class Composite_Children
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator Composite_AddTwoChildBrushes_CompositeHasChildrenInOrder()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush1				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush1GameObject	= brush1.gameObject;
            brush1.transform.parent	= composite.transform;

            var brush2				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush2GameObject	= brush2.gameObject;
            brush2.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brush1GameObject);
            Assert.True(brush1);
            Assert.True(brush2GameObject);
            Assert.True(brush2);

            Assert.AreEqual(composite.transform.GetChild(0), brush1.transform);
            Assert.AreEqual(composite.transform.GetChild(1), brush2.transform);

            yield return null;			

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(2, composite.Node.Count);
            Assert.AreEqual(brush1.TopNode.NodeID, composite.Node[0].NodeID);
            Assert.AreEqual(brush2.TopNode.NodeID, composite.Node[1].NodeID);
        }

        [UnityTest]
        public IEnumerator CompositeWithTwoChildBrushes_SwapOrder_CompositeHasChildrenInOrder()
        {
            var composite = TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush1				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush1GameObject	= brush1.gameObject;
            brush1.transform.parent	= composite.transform;

            var brush2				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush2GameObject	= brush2.gameObject;
            brush2.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brush1GameObject);
            Assert.True(brush1);
            Assert.True(brush2GameObject);
            Assert.True(brush2);

            Assert.AreEqual(composite.transform.GetChild(0), brush1.transform);
            Assert.AreEqual(composite.transform.GetChild(1), brush2.transform);

            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(2, composite.Node.Count);
            brush2.transform.SetSiblingIndex(0);

            Assert.AreEqual(composite.transform.GetChild(0), brush2.transform);
            Assert.AreEqual(composite.transform.GetChild(1), brush1.transform);
            yield return null;		

            Assert.AreEqual(brush2.TopNode.NodeID, composite.Node[0].NodeID);
            Assert.AreEqual(brush1.TopNode.NodeID, composite.Node[1].NodeID);
        }

        [UnityTest]
        public IEnumerator Composite_AddChildBrush_CompositeHasChild()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject	= composite.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(brush.TopNode.NodeID, composite.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator Composite_AddChildComposite_CompositeHasChild()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite2.Node.NodeID, composite1.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator Composite_AddChildModel_CompositeHasNoChildren()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject	= composite.gameObject;

            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;
            model.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(modelGameObject);
            Assert.True(model);
            
            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(0, composite.Node.Count);
        }

        [UnityTest]
        public IEnumerator CompositeWithGameObject_AddChildBrush_CompositeHasChild()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject	= composite.gameObject;

            var plainGameObject		= TestUtility.CreateGameObject();
            plainGameObject.transform.parent = composite.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= plainGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(brush.TopNode.NodeID, composite.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator CompositeWithGameObject_AddChildComposite_CompositeHasChild()
        { 
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var plainGameObject			= TestUtility.CreateGameObject();
            plainGameObject.transform.parent = composite1.transform;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= plainGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite2.Node.NodeID, composite1.Node[0].NodeID);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildBrush_DestroyChildGameObject_CompositeHasNoChildren()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(composite.Node.NodeID, brush.TopNode.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(brushGameObject);
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.False(brushGameObject);
            Assert.False(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite.Node.Count);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildComposite_DestroyChildGameObject_CompositeHasNoChildren()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent = composite1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(composite2GameObject);
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.False(composite2GameObject);
            Assert.False(composite2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite1.Node.Count);
        }
        
        [UnityTest]
        public IEnumerator CompositeWithChildBrush_DestroyChildComponent_CompositeHasNoChildren()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(composite.Node.NodeID, brush.TopNode.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(brush);
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.False(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite.Node.Count);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildComposite_DestroyChildComponent_CompositeHasNoChildren()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent = composite1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);
            
            Undo.DestroyObjectImmediate(composite2);
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.False(composite2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite1.Node.Count);
        }
        
        [UnityTest]
        public IEnumerator CompositeWithChildBrush_DisableChildComponent_CompositeHasNoChildren()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(composite.Node.NodeID, brush.TopNode.Parent.NodeID);

            brush.enabled = false;
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite.Node.Count);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildComposite_DisableChildComponent_CompositeHasNoChildren()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent = composite1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);
            
            composite2.enabled = false;
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite1.Node.Count);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildBrush_DeactivateChildGameObject_CompositeHasNoChildren()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(composite.Node.NodeID, brush.TopNode.Parent.NodeID);

            brushGameObject.SetActive(false);
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite.Node.Count);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildComposite_DeactivateChildGameObject_CompositeHasNoChildren()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent = composite1.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);

            composite2GameObject.SetActive(false);
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist"); 
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, composite1.Node.Count);
        }
    }
}