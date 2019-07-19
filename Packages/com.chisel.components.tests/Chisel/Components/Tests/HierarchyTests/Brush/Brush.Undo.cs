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
    public partial class Brush_Undo
    {
        [SetUp] public void Setup() { TestUtility.ClearScene(); }


        [UnityTest]
        public IEnumerator CreateBrush_UndoCreateGameObject_BrushDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject = brush.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.False(brushGameObject);
            Assert.False(brush);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateBrush_UndoCreateComponent_BrushDoesNotExist()
        {
            var scene			= TestUtility.defaultScene;
            var brush			= TestUtility.CreateGameObjectWithUndoableBrushComponent();
            var brushGameObject = brush.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");

            Undo.PerformUndo();
            yield return null;

            Assert.True(brushGameObject);
            Assert.False(brush);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateAndDestroyBrushGameObject_Undo_BrushExists()
        {
            var scene			= TestUtility.defaultScene;
            var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject = brush.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            
            Undo.DestroyObjectImmediate(brushGameObject);
            yield return null;

            Assert.False(brushGameObject);
            Assert.False(brush);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));

            Undo.PerformUndo();
            brush = Object.FindObjectsOfType<ChiselBrush>()[0];
            brushGameObject = brush.gameObject;
            yield return null;

            Assert.True(brushGameObject);
            Assert.True(brush);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateAndDestroyBrushComponent_Undo_BrushExists()
        {
            var scene			= TestUtility.defaultScene;
            var brush			= TestUtility.CreateGameObjectWithUndoableBrushComponent();
            var brushGameObject = brush.gameObject;

            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist"); 
            
            Undo.DestroyObjectImmediate(brush);
            yield return null;

            Assert.True(brushGameObject);
            Assert.False(brush);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGNodeHierarchyManager.RootCount(scene));

            Undo.PerformUndo();
            brush = brushGameObject.GetComponent<ChiselBrush>();
            yield return null; 

            Assert.True(brushGameObject);
            Assert.True(brush);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGNodeHierarchyManager.RootCount(scene));
        }

        [UnityTest]
        public IEnumerator CreateBrush_UndoCreateGameObject_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);

            var brush			= TestUtility.CreateUndoableGameObjectWithBrush();
            var brushGameObject = brush.gameObject;
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            Assert.AreEqual(brushGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.False(brushGameObject);
            Assert.False(brush);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }

        [UnityTest]
        public IEnumerator CreateBrush_UndoCreateComponent_DoesNotDirtyAnyScene()
        {
            var currentScene	= SceneManager.GetActiveScene();
            var	newScene		= TestUtility.CreateAdditionalSceneAndActivate();
            Assert.False(currentScene.isDirty);
            Assert.False(newScene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return null;

            var	brush			= TestUtility.CreateGameObjectWithUndoableBrushComponent();
            var brushGameObject = brush.gameObject;

            Assert.AreEqual(brushGameObject.scene, newScene);
            Assert.True(newScene.isDirty);
            Assert.False(currentScene.isDirty);

            Undo.PerformUndo();
            yield return null;

            Assert.True(brushGameObject);
            Assert.False(brush);
            Assert.False(newScene.isDirty);
            Assert.False(currentScene.isDirty);
        }
    }
}