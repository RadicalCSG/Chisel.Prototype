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
    public partial class Model_Parenting
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }

        // TODO: test models inside models

        [UnityTest]
        public IEnumerator ModelWithChildBrush_ChildHasModelAsTree()
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
            Assert.AreEqual(model.Node.NodeID, brush.TopNode.Tree.NodeID);
        }

        [UnityTest]
        public IEnumerator ModelWithChildComposite_ChildHasModelAsTree()
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
            Assert.AreEqual(model.Node.NodeID, composite.Node.Tree.NodeID);
        }

        [UnityTest]
        public IEnumerator ModelWithWithGameObjectWithBrush_BrushHasModelAsTree()
        {
            var model			= TestUtility.CreateUndoableGameObjectWithModel("model");
            var modelGameObject = model.gameObject;

            var plainGameObject		= TestUtility.CreateGameObject("gameObject");
            plainGameObject.transform.parent = modelGameObject.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
            var brushGameObject		= brush.gameObject;
            brush.transform.parent = plainGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(plainGameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node.NodeID, brush.TopNode.Tree.NodeID);
        }

        [UnityTest]
        public IEnumerator CreateModel_AddChildBrush_ChildHasModelAsTree()
        {
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Tree.NodeID);

            brush.transform.parent = model.transform;
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject); 
            Assert.True(brush);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node.NodeID, brush.TopNode.Tree.NodeID); 
        }

        [UnityTest]
        public IEnumerator CreateModel_AddChildComposite_ChildHasModelAsTree()
        {
            var model			= TestUtility.CreateUndoableGameObjectWithModel();
            var modelGameObject = model.gameObject;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");

            var composite			= TestUtility.CreateUndoableGameObjectWithComposite();
            var compositeGameObject	= composite.gameObject;
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, composite.Node.Tree.NodeID);

            composite.transform.parent = model.transform;
            yield return null;
            
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(compositeGameObject); 
            Assert.True(composite);
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node.NodeID, composite.Node.Tree.NodeID); 
        }

        [UnityTest]
        public IEnumerator Model1WithDisabledModel2WithChildBrush_AddChildBrushToModel1_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;
            
            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;
            model2.enabled			= false;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist"); 
            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);	
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID); 		
        }

        [UnityTest]
        public IEnumerator Model1WithChildBrush_MoveChildToModel2_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel("model1");
            var model1GameObject	= model1.gameObject;

            var model2				= TestUtility.CreateUndoableGameObjectWithModel("model2");
            var model2GameObject	= model2.gameObject;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model1.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
                
            yield return null;

            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(0, model2.Node.Count);
                
            brush.transform.parent	= model2.transform;
            yield return null;
                
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Model1WithChildBrush_MoveChildOutOfAnyModel_ChildHasNoTreeNodeSet()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel("model1");
            var model1GameObject	= model1.gameObject;

            var plainGameObject		= TestUtility.CreateGameObject("gameObject");

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model1.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(plainGameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
                
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
                
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(1, model1.Node.Count);
                
            brush.transform.parent	= plainGameObject.transform;
            yield return null;
                
            Assert.AreEqual(0, model1.Node.Count);
            var defaultModel = brush.hierarchyItem.sceneHierarchy.DefaultModel;
            Assert.AreEqual(defaultModel.NodeID, brush.TopNode.Tree.NodeID);
        }

        [UnityTest]
        public IEnumerator Model1WithChildBrush_MoveChildToGameObjectChildOfModel2_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel("model1");
            var model1GameObject	= model1.gameObject;

            var model2				= TestUtility.CreateUndoableGameObjectWithModel("model2");
            var model2GameObject	= model2.gameObject;
                
            var plainGameObject		= TestUtility.CreateGameObject("gameObject");
            plainGameObject.transform.parent = model2.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model1.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
                
            yield return null;

            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(0, model2.Node.Count);
                
            brush.transform.parent	= plainGameObject.transform;
            yield return null;
                
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Model1WithChildGameObjectWithChildBrush_MoveChildToModel2_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel("model1");
            var model1GameObject	= model1.gameObject;

            var model2				= TestUtility.CreateUndoableGameObjectWithModel("model2");
            var model2GameObject	= model2.gameObject;
                
            var plainGameObject		= TestUtility.CreateGameObject("gameObject");
            plainGameObject.transform.parent = model1.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= plainGameObject.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
                
            yield return null;

            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model1.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
                
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(0, model2.Node.Count);
                
            brush.transform.parent	= model2.transform;
            yield return null;
                
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Model1WithWithGameObjectWithBrush_AddModel2ToGameObject_BrushHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel("model1");
            var model1GameObject	= model1.gameObject;

            var model2GameObject	= TestUtility.CreateGameObject("model2");
            model2GameObject.transform.parent = model1GameObject.transform;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2GameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);
             
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID);

            var model2			= TestUtility.CreateUndoableModelComponent(model2GameObject);
            yield return null;
            
            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);
        }

        [UnityTest]
        public IEnumerator GameObjectWithBrush_AddModelToGameObject_BrushHasModelAsTree()
        {
            var modelGameObject		= TestUtility.CreateGameObject("model");

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush("brush");
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= modelGameObject.transform;

            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;

            Assert.True(modelGameObject);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); // default model
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.Parent.NodeID);

            var model				= TestUtility.CreateUndoableModelComponent(modelGameObject);
            yield return null;
            Assert.True(modelGameObject);
            Assert.True(model);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist"); // new model only, default model should've been destroyed
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, model.Node.Count);
            Assert.AreEqual(model.Node.NodeID, brush.TopNode.Tree.NodeID);
        }

        [UnityTest]
        public IEnumerator Model1WithDisabledModel2WithChildBrush_EnableModel2_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel("model1");
            var model1GameObject	= model1.gameObject;

            var model2				= TestUtility.CreateUndoableGameObjectWithModel("model2");
            var model2GameObject	= model2.gameObject;
            model2.transform.parent = model1.transform;
            model2.enabled = false;

            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject);
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
                
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Assert.AreEqual(1, model1.Node.Count);

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID);

            model2.enabled = true;
            yield return null;

            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");

            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            Assert.AreNotEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID);
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);

            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);
        }

        [UnityTest]
        public IEnumerator Model1WithModel2WithChildBrush_DisableModel2Component_ChildHasModel1AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;
            
            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;
            model2.enabled			= false;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");	

            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
        }

        [UnityTest]
        public IEnumerator Model1WithModel2WithChildBrush_DestroyModel2Component_ChildHasModel1AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;
            
            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	

            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID); 
            
            Undo.DestroyObjectImmediate(model2);
            yield return null;
                        
            Assert.False(model2);

            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");	

            Assert.AreEqual(1, model1.Node.Count);
            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID); 
        }

        [UnityTest]
        public IEnumerator Model1WithModel2WithChildBrush_EnableModel2Component_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;
            
            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;
            model2.enabled			= false;
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");	

            Assert.AreEqual(model1.Node.NodeID, brush.TopNode.Tree.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            Assert.AreEqual(1, model1.Node.Count);
            
            model2.enabled				= true;
            yield return null;	
            
            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");	
            
            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);
            Assert.AreEqual(1, model2.Node.Count);
            Assert.AreEqual(0, model1.Node.Count);
        }

        [UnityTest]
        public IEnumerator Model1WithDeactivatedModel2WithChildBrush_ActivateModel2GameObject_ChildHasModel2AsTree()
        {
            var model1				= TestUtility.CreateUndoableGameObjectWithModel();
            var model1GameObject	= model1.gameObject;
            
            var model2				= TestUtility.CreateUndoableGameObjectWithModel();
            var model2GameObject	= model2.gameObject;
            model2.transform.parent	= model1.transform;
            model2GameObject.SetActive(false);
            
            var brush				= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject		= brush.gameObject;
            brush.transform.parent	= model2.transform;

            Assert.True(model1GameObject);
            Assert.True(model1);
            Assert.True(model2GameObject);
            Assert.True(model2);
            Assert.True(brushGameObject); 
            Assert.True(brush);

            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            yield return null;
            
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");	

            Assert.AreEqual(0, model1.Node.Count);

            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, brush.TopNode.NodeID); 
            Assert.AreEqual(CSGTreeNode.InvalidNode.NodeID, model2.Node.NodeID);
            
            model2GameObject.SetActive(true); 
            yield return null;	
            
            Assert.AreEqual(2, CSGManager.TreeCount, "Expected 2 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(0, model1.Node.Count);
            Assert.AreEqual(1, model2.Node.Count);

            Assert.AreEqual(model2.Node.NodeID, brush.TopNode.Tree.NodeID);
        }
    }
}*/