using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
/*
namespace HierarchyTests
{
    public partial class Composite_Parenting
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator CompositeWithChildBrush_ChildHasCompositeAsParent()
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
        }

        [UnityTest]
        public IEnumerator CompositeWithChildComposite_ChildHasCompositeAsParent()
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
        }

        [UnityTest]
        public IEnumerator CompositeWithWithGameObjectWithBrush_BrushHasCompositeAsParent()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite("composite");
            var compositeGameObject = composite.gameObject;

            var plainGameObject		= TestUtility.CreateGameObject("gameObject");
            plainGameObject.transform.parent = compositeGameObject.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
            var brushGameObject		= brush.gameObject;
            brush.transform.parent = plainGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(composite);
            Assert.True(plainGameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(composite.Node.NodeID, brush.TopNode.Parent.NodeID);
        }

        [UnityTest]
        public IEnumerator Composite1WithWithGameObjectWithBrush_AddComposite2ToGameObject_BrushHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite("composite1");
            var composite1GameObject	= composite1.gameObject;

            var composite2GameObject	= TestUtility.CreateGameObject("composite2");
            composite2GameObject.transform.parent = composite1GameObject.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite2GameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID);

            var composite2			= TestUtility.CreateUndoableCompositeComponent(composite2GameObject);
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(1, composite2.Node.Count);
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);
            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID);
        }

        [UnityTest]
        public IEnumerator GameObjectWithBrush_AddCompositeToGameObject_BrushHasCompositeAsParent()
        {
            var compositeGameObject	= TestUtility.CreateGameObject("composite");

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= compositeGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(compositeGameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);

            var composite			= TestUtility.CreateUndoableCompositeComponent(compositeGameObject);
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
        }

        [UnityTest]
        public IEnumerator CreateComposite_AddChildBrush_ChildHasCompositeAsParent()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject = composite.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);

            brush.transform.parent = composite.transform;
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
        }

        [UnityTest]
        public IEnumerator CreateComposite_AddChildComposite_ChildHasCompositeAsParent()
        {
            var composite1			= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject = composite1.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.Parent.NodeID);

            composite2.transform.parent = composite1.transform;
            yield return null;
            
            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject); 
            Assert.True(composite2);
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID); 
        }

        [UnityTest]
        public IEnumerator Composite1WithDisabledComposite2_AddChildBrush_ChildHasParentOfCompositeAsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;
            composite2.enabled			= false;
            
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
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist"); 
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);	
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID); 		
        }

        [UnityTest]
        public IEnumerator Composite1WithDisabledComposite2_AddChildComposite_ChildHasParentOfCompositeAsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;
            composite2.enabled			= false;
            
            var composite3				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite3GameObject	= composite3.gameObject;
            composite3.transform.parent	= composite2.transform;

            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(composite2GameObject);
            Assert.True(composite2);
            Assert.True(composite3GameObject); 
            Assert.True(composite3);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist"); 
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);	
            Assert.AreEqual(composite1.Node.NodeID, composite3.Node.Parent.NodeID); 		
        }

        [UnityTest]
        public IEnumerator Composite1WithChildBrush_MoveChildToComposite2_ChildHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite("composite1");
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite("composite2");
            var composite2GameObject	= composite2.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite1.transform;

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

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(0, composite2.Node.Count);
                
            brush.transform.parent	= composite2.transform;
            yield return null;
                
            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(0, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Composite1WithChildBrush_MoveChildToGameObject_ChildHasNoParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite("composite1");
            var composite1GameObject	= composite1.gameObject;

            var plainGameObject			= TestUtility.CreateGameObject("gameObject");

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite1.transform;

            Assert.True(composite1GameObject);
            Assert.True(composite1);
            Assert.True(plainGameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
                
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
                
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(1, composite1.Node.Count);
                
            brush.transform.parent	= plainGameObject.transform;
            yield return null;

            Assert.AreEqual(0, composite1.Node.Count);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);
        }

        [UnityTest]
        public IEnumerator Composite1WithChildBrush_MoveBrushToNoneChiselNodeChildOfComposite2_ChildHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite("composite1");
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite("composite2");
            var composite2GameObject	= composite2.gameObject;
                
            var plainGameObject			= TestUtility.CreateGameObject("gameObject");
            plainGameObject.transform.parent = composite2.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite1.transform;

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
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(0, composite2.Node.Count);
                
            brush.transform.parent	= plainGameObject.transform;
            yield return null;
                
            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(0, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Composite1WithChildGameObjectWithChildBrush_MoveChildToComposite2_ChildHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite("composite1");
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite("composite2");
            var composite2GameObject	= composite2.gameObject;
                
            var plainGameObject			= TestUtility.CreateGameObject("gameObject");
            plainGameObject.transform.parent = composite1.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= plainGameObject.transform;

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

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(0, composite2.Node.Count);
                
            brush.transform.parent	= composite2.transform;
            yield return null;
                
            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID);

            Assert.AreEqual(0, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);
        }


        [UnityTest]
        public IEnumerator Composite1WithDisabledComposite2WithChildBrush_EnableComposite2_ChildHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite("composite1");
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite("composite2");
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent = composite1.transform;
            composite2.enabled = false;

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
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreEqual(1, composite1.Node.Count);

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID);

            composite2.enabled = true;
            yield return null;

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Composite1WithComposite2WithChildBrush_DisableComposite2Component_ChildHasComposite1AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;
            composite2.enabled			= false;
            
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
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
        }

        [UnityTest]
        public IEnumerator CompositeWithChildBrush_DisableAndEnableCompositeComponent_ChildHasCompositeAsParent()
        {
            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject	= composite.gameObject;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= composite.transform;
            
            
            yield return null;
            
            composite.enabled			= false;
            
            yield return null;

            composite.enabled			= true;

            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

            Assert.AreEqual(1, composite.Node.Count);
            Assert.AreEqual(composite.Node.NodeID, brush.TopNode.Parent.NodeID); 
        }

        [UnityTest]
        public IEnumerator Composite1WithComposite2WithChildBrush_DestroyCompositeComponent2_ChildHasComposite1AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;
            
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
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");	

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);
            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID); 
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID); 
            
            Undo.DestroyObjectImmediate(composite2);
            yield return null;
            
            Assert.False(composite2);

            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	
            
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID); 
        }

        [UnityTest]
        public IEnumerator Composite1WithDisabledComposite2WithChildBrush_EnableComposite2Component_ChildHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;
            composite2.enabled			= false;
            
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
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

            Assert.AreEqual(1, composite1.Node.Count);

            Assert.AreEqual(composite1.Node.NodeID, brush.TopNode.Parent.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            
            composite2.enabled				= true;
            yield return null;	
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");	

            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);

            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID); 
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID); 
        }

        [UnityTest]
        public IEnumerator Composite1WithDeactivatedComposite2WithChildBrush_ActivateComposite2GameObject_ChildHasComposite2AsParent()
        {
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

            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite2.Node.NodeID);
            
            composite2GameObject.SetActive(true); 
            yield return null;	
            
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(1, composite1.Node.Count);
            Assert.AreEqual(1, composite2.Node.Count);

            Assert.AreEqual(composite2.Node.NodeID, brush.TopNode.Parent.NodeID);
            Assert.AreEqual(composite1.Node.NodeID, composite2.Node.Parent.NodeID);
        }

        [UnityTest]
        public IEnumerator Composite1WithPassthroughComposite2_AddChild_ChildHasComposite1AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;
            
            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent	= composite1.transform;

            composite2.PassThrough		= true;
            
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
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(brush.TopNode.Parent, composite1.Node); 
            Assert.AreEqual(composite1.Node.Count, 1);
            Assert.AreEqual(composite2.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);			
        }

        [UnityTest]
        public IEnumerator Composite1WithPassthroughComposite2WithChildBrush_DisablePassthrough_ChildHasComposite2AsParent()
        {
            var composite1				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite1GameObject	= composite1.gameObject;

            var composite2				= TestUtility.CreateUndoableGameObjectWithComposite();
            var composite2GameObject	= composite2.gameObject;
            composite2.transform.parent = composite1.transform;

            composite2.PassThrough = true;

            var brush					= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject			= brush.gameObject;
            brush.transform.parent		= composite2.transform;

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
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(brush.TopNode.Parent, composite1.Node);
            Assert.AreEqual(composite1.Node.Count, 1);
            Assert.AreEqual(composite2.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);

            composite2.PassThrough = false;
            yield return null;

            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
            Assert.AreEqual(brush.TopNode.Parent, composite2.Node);
            Assert.AreEqual(composite2.Node.Parent, composite1.Node);
            Assert.AreNotEqual(composite2.Node, (CSGTreeBranch)CSGTreeNode.InvalidNode);
            Assert.AreEqual(composite1.Node.Count, 1);
            Assert.AreEqual(composite2.Node.Count, 1);
        }
    }
}*/