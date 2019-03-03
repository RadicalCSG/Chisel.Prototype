using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Components;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;


namespace HierarchyTests
{
    public partial class TestUtility
    {
        public const string tempFilename = "temp.unity";
        public const string tempFilename2 = "temp2.unity";
        public static Scene defaultScene;

        public static void ClearScene()
        {
            defaultScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            CSGManager.Clear();
            CSGNodeHierarchyManager.Reset();
            CSGNodeHierarchyManager.Update();
        }

        public static Scene CreateAdditionalSceneAndActivate()
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SceneManager.SetActiveScene(newScene);
            return newScene;
        }

        public static CSGModel CreateUndoableGameObjectWithModel(string name = "model", HideFlags flags = HideFlags.None)
        {
            var modelGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            var model = modelGameObject.AddComponent<CSGModel>();
            Undo.RegisterCreatedObjectUndo(modelGameObject, "Created " + name + " gameObject");
            return model;
        }

        public static CSGOperation CreateUndoableGameObjectWithOperation(string name = "operation", HideFlags flags = HideFlags.None)
        {
            var operationGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            var operation = operationGameObject.AddComponent<CSGOperation>();
            Undo.RegisterCreatedObjectUndo(operationGameObject, "Created " + name + " gameObject");
            return operation;
        }
        /*
                public static CSGMirror CreateUndoableGameObjectWithMirror(string name = "mirror", HideFlags flags = HideFlags.None)
                {
                    var mirrorGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
                    var mirror = mirrorGameObject.AddComponent<CSGMirror>();
                    Undo.RegisterCreatedObjectUndo(mirrorGameObject, "Created " + name + " gameObject");
                    return mirror;
                }
        */
        public static CSGBrush CreateUndoableGameObjectWithBrush(string name = "brush", HideFlags flags = HideFlags.None)
        {
            var brushGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            var brush = brushGameObject.AddComponent<CSGBrush>();
            Undo.RegisterCreatedObjectUndo(brushGameObject, "Created " + name + " gameObject");
            return brush;
        }

        public static CSGModel CreateGameObjectWithUndoableModelComponent(string name = "model", HideFlags flags = HideFlags.None)
        {
            var modelGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            var model = modelGameObject.AddComponent<CSGModel>();
            Undo.RegisterCreatedObjectUndo(model, "Created " + name + " component");
            return model;
        }

        public static CSGOperation CreateGameObjectWithUndoableOperationComponent(string name = "operation", HideFlags flags = HideFlags.None)
        {
            var operationGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            var operation = operationGameObject.AddComponent<CSGOperation>();
            Undo.RegisterCreatedObjectUndo(operation, "Created " + name + " component");
            return operation;
        }

        public static CSGBrush CreateGameObjectWithUndoableBrushComponent(string name = "brush", HideFlags flags = HideFlags.None)
        {
            var brushGameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            var brush = brushGameObject.AddComponent<CSGBrush>();
            Undo.RegisterCreatedObjectUndo(brush, "Created " + name + " component");
            return brush;
        }



        public static GameObject CreateGameObject(string name = "gameObject", HideFlags flags = HideFlags.None)
        {
            var gameObject = EditorUtility.CreateGameObjectWithHideFlags(name, flags);
            Undo.RegisterCreatedObjectUndo(gameObject, "Created " + name + " gameObject");
            return gameObject;
        }


        public static CSGModel CreateUndoableModelComponent(GameObject modelGameObject, string name = "model", HideFlags flags = HideFlags.None)
        {
            var model = modelGameObject.AddComponent<CSGModel>();
            Undo.RegisterCreatedObjectUndo(model, "Created " + name + " component");
            return model;
        }

        public static CSGOperation CreateUndoableOperationComponent(GameObject operationGameObject, string name = "operation", HideFlags flags = HideFlags.None)
        {
            var operation = operationGameObject.AddComponent<CSGOperation>();
            Undo.RegisterCreatedObjectUndo(operation, "Created " + name + " component");
            return operation;
        }

        public static CSGBrush CreateUndoableBrushComponent(GameObject brushGameObject, string name = "brush", HideFlags flags = HideFlags.None)
        {
            var brush = brushGameObject.AddComponent<CSGBrush>();
            Undo.RegisterCreatedObjectUndo(brush, "Created " + name + " component");
            return brush;
        }
    }
}