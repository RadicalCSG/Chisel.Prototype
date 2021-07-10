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
    public partial class Model_Children
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator Model_AddTwoChildBrushes_ModelHasChildrenInOrder()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;

            var brush1				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush1GameObject	= brush1.gameObject;
            brush1.transform.parent	= model.transform;

            var brush2				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brush2GameObject	= brush2.gameObject;
            brush2.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brush1GameObject);
            Assert.True(brush1);
            Assert.True(brush2GameObject);
            Assert.True(brush2);

            Assert.AreEqual(model.transform.GetChild(0), brush1.transform);
            Assert.AreEqual(model.transform.GetChild(1), brush2.transform);

            yield return null;			

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(2, model.Node.Count);
            Assert.AreEqual(brush1.TopNode, model.Node[0]);
            Assert.AreEqual(brush2.TopNode, model.Node[1]);
        }

        [UnityTest]
        public IEnumerator ModelWithTwoChildBrushes_SwapOrder_ModelHasChildrenInOrder()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel("model");
            var modelGameObject		= model.gameObject;

            var brush1				= TestUtility.CreateUndoableGameObjectWithBrush("brush1");
            var brush1GameObject	= brush1.gameObject;
            brush1.transform.parent	= model.transform;

            var brush2				= TestUtility.CreateUndoableGameObjectWithBrush("brush2");
            var brush2GameObject	= brush2.gameObject;
            brush2.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");

            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brush1GameObject);
            Assert.True(brush1);
            Assert.True(brush2GameObject);
            Assert.True(brush2);

            Assert.AreEqual(model.transform.GetChild(0), brush1.transform);
            Assert.AreEqual(model.transform.GetChild(1), brush2.transform);

            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(2, model.Node.Count);
            brush2.transform.SetSiblingIndex(0);

            Assert.AreEqual(model.transform.GetChild(0), brush2.transform);
            Assert.AreEqual(model.transform.GetChild(1), brush1.transform);
            yield return null;

            Assert.AreEqual(brush2.TopNode, model.Node[0]);
            Assert.AreEqual(brush1.TopNode, model.Node[1]);
        }

        [UnityTest]
        public IEnumerator Model_AddChildBrush_ModelHasChild()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount,		"Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount,	"Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount,	"Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount,		"Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount,	"Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount,	"Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(brush.TopNode, model.Node[0]);
        }

        [UnityTest]
        public IEnumerator Model_AddChildComposite_ModelHasChild()
        {
            var model					= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject			= model.gameObject;

            var composite               = TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject     = composite.gameObject;
            composite.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(composite.Node, model.Node[0]);
        }

        [UnityTest]
        public IEnumerator Model_AddChildModel_ModelHasNoChildren()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;

            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            
            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(0, model1.Node.Count);
        }

        [UnityTest]
        public IEnumerator ModelWithChildBrush_DestroyChildGameObject_ModelHasNoChildren()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, brush.TopNode.Tree);
            
            Undo.DestroyObjectImmediate(brushGameObject);
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.False(brushGameObject);
            Assert.False(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }

        [UnityTest]
        public IEnumerator ModelWithChildComposite_DestroyChildGameObject_ModelHasNoChildren()
        {
            var model					= TestUtility.CreateUndoableGameObjectWithModel();
            var modelameObject			= model.gameObject;

            var composite               = TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject     = composite.gameObject;
            composite.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, composite.Node.Tree);
            
            Undo.DestroyObjectImmediate(compositeGameObject);
            yield return null;
            
            Assert.True(modelameObject);
            Assert.True(model);
            Assert.False(compositeGameObject);
            Assert.False(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist"); 
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }
        
        [UnityTest]
        public IEnumerator ModelWithChildBrush_DestroyChildComponent_ModelHasNoChildren()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, brush.TopNode.Tree);
            
            Undo.DestroyObjectImmediate(brush);
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.False(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }

        [UnityTest]
        public IEnumerator ModelWithChildComposite_DestroyChildComponent_ModelHasNoChildren()
        {
            var model					= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject			= model.gameObject;

            var composite				= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject		= composite.gameObject;
            composite.transform.parent	= model.transform;
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, composite.Node.Tree);
            
            Undo.DestroyObjectImmediate(composite);
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.False(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist"); 
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }
        
        [UnityTest]
        public IEnumerator ModelWithChildBrush_DisableChildComponent_ModelHasNoChildren()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, brush.TopNode.Tree);

            brush.enabled = false;
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModelWithChildComposite_DisableChildComponent_ModelHasNoChildren()
        {
            var model					= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject			= model.gameObject;

            var composite				= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject		= composite.gameObject;
            composite.transform.parent	= model.transform;
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, composite.Node.Tree);
            
            composite.enabled = false;
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist"); 
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }

        [UnityTest]
        public IEnumerator ModelWithChildBrush_DeactivateChildGameObject_ModelHasNoChildren()
        {
            var model				= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject		= model.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, brush.TopNode.Tree);

            brushGameObject.SetActive(false);
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); 
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }

        [UnityTest]
        public IEnumerator ModelWithChildComposite_DeactivateChildGameObject_ModelHasNoChildren()
        {
            var model					= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject			= model.gameObject;

            var composite				= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject		= composite.gameObject;
            composite.transform.parent	= model.transform;
            
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node, composite.Node.Tree);

            compositeGameObject.SetActive(false);
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject);
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist"); 
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, model.Node.Count);
        }
    }
}*/