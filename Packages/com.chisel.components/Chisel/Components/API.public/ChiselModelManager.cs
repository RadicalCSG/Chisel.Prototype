using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Pool;

namespace Chisel.Components
{
    public class ChiselModelManager : ScriptableObject, ISerializationCallbackReceiver
    {
        const string kDefaultModelName = "Model";

        #region Instance
        static ChiselModelManager _instance;
        public static ChiselModelManager Instance
        {
            get
            {
                if (_instance)
                    return _instance;
                 
#if UNITY_2023_1_OR_NEWER
                var foundInstances = UnityEngine.Object.FindObjectsByType<ChiselModelManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
                var foundInstances = UnityEngine.Object.FindObjectsOfType<ChiselModelManager>();
#endif
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<ChiselModelManager>();
                    _instance.hideFlags = HideFlags.DontSaveInBuild;
                    return _instance;
                }

                if (foundInstances.Length > 1)
                {
                    for (int i = 1; i < foundInstances.Length; i++)
                        ChiselObjectUtility.SafeDestroy(foundInstances[i]);
                }

                _instance = foundInstances[0];
                return _instance;
            }
        }
#endregion

        // TODO: potentially have a history per scene, so when one model turns out to be invalid, go back to the previously selected model
        readonly Dictionary<Scene, ChiselModel> activeModels = new Dictionary<Scene, ChiselModel>();

        #region ActiveModel Serialization
        [Serializable] public struct SceneModelPair { public Scene Key; public ChiselModel Value; }
        [SerializeField] SceneModelPair[] activeModelsArray;

        public void OnBeforeSerialize()
        {
            var foundModels = ListPool<SceneModelPair>.Get();
            //if (foundModels != null)
            {
                if (foundModels.Capacity < activeModels.Count)
                    foundModels.Capacity = activeModels.Count;
                foreach (var pair in activeModels)
                    foundModels.Add(new SceneModelPair { Key = pair.Key, Value = pair.Value });
                if (activeModelsArray != null && activeModelsArray.Length == foundModels.Count)
                {
                    foundModels.CopyTo(activeModelsArray);
                }
                else
                    activeModelsArray = foundModels.ToArray();
                ListPool<SceneModelPair>.Release(foundModels);
            }
        }

        public void OnAfterDeserialize()
        {
            if (activeModelsArray == null)
                return;
            foreach (var pair in activeModelsArray)
                activeModels[pair.Key] = pair.Value;
            activeModelsArray = null;
        }
        #endregion



        public static bool IsSelectable(ChiselModel model)
        {
            if (!model || !model.isActiveAndEnabled)
                return false;

            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            var visible         = !sceneVisibilityManager.IsHidden(model.gameObject);
            var pickingEnabled  = !sceneVisibilityManager.IsPickingDisabled(model.gameObject);
            return visible && pickingEnabled;
        }


        public static ChiselModel ActiveModel
        { 
            get
            {
                // Find our active model for the current active scene
                var activeScene = SceneManager.GetActiveScene();
                Instance.activeModels.TryGetValue(activeScene, out var activeModel);

                // Check if the activeModel is valid & if it's scene actually points to the active Scene
                if (ReferenceEquals(activeModel, null) ||
                    !activeModel || activeModel.gameObject.scene != activeScene)
                {
                    // If active model is invalid or missing, find another model the active model
                    Instance.activeModels[activeScene] = FindModelInScene(activeScene);
                    return null;
                }

                // If we have an active model, but it's actually disabled, do not use it
                // This prevents users from accidentally adding generators to a model that is inactive, 
                // and then be confused why nothing is visible.
                if (!IsSelectable(activeModel))
                    return null;
                return activeModel;
            }
            set
            {
                // When we set a model to be active, make sure we use the scene of its gameobject
                var modelScene = value.gameObject.scene;
                Instance.activeModels[modelScene] = value;

                // And then make sure that scene is active
                if (modelScene != SceneManager.GetActiveScene())
                    SceneManager.SetActiveScene(modelScene);
            }
        }

        public static ChiselModel GetActiveModelOrCreate(ChiselModel overrideModel = null)
        {
            if (overrideModel)
            {
                ChiselModelManager.ActiveModel = overrideModel;
                return overrideModel;
            }

            var activeModel = ChiselModelManager.ActiveModel;
            if (!activeModel)
            {
                // TODO: handle scene being locked by version control
                activeModel = CreateNewModel();
                ChiselModelManager.ActiveModel = activeModel; 
            }
            return activeModel;
        }

        public static ChiselModel CreateNewModel(Transform parent = null)
        {
            return ChiselComponentFactory.Create<ChiselModel>(kDefaultModelName, parent);
        }


        public static ChiselModel FindModelInScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            return FindModelInScene(activeScene);
        }

        public static ChiselModel FindModelInScene(Scene scene)
        {
            if (!scene.isLoaded ||
                !scene.IsValid())
                return null;

            var allRootGameObjects = scene.GetRootGameObjects();
            if (allRootGameObjects == null)
                return null;

            // We prever last model (more likely last created), so we iterate backwards
            for (int n = allRootGameObjects.Length - 1; n >= 0; n--)
            {
                var rootGameObject = allRootGameObjects[n];
                // Skip all gameobjects that are disabled
                if (!rootGameObject.activeInHierarchy)
                    continue;

                // Go through all it's models, this method returns the top most models first
                var models = rootGameObject.GetComponentsInChildren<ChiselModel>(includeInactive: false);
                foreach (var model in models)
                {
                    // Skip all inactive models
                    if (!ChiselModelManager.IsSelectable(model))
                        continue;

                    return model;
                }
            }
            return null;
        }

        public static void CheckActiveModels()
        {
            // Go through all activeModels, which we store per scene, and make sure they still make sense

            var removeScenes = ListPool<Scene>.Get();
            var setSceneModels = DictionaryPool<Scene, ChiselModel>.Get();
            try
            {
                foreach (var pair in Instance.activeModels)
                {
                    // If the scene is no longer loaded, remove it from our list
                    if (!pair.Key.isLoaded || !pair.Key.IsValid())
                    {
                        removeScenes.Add(pair.Key);
                    }

                    // Check if a current activeModel still exists
                    var sceneActiveModel = pair.Value;
                    if (!sceneActiveModel)
                    {
                        setSceneModels[pair.Key] = FindModelInScene(pair.Key);
                        //Instance.activeModels[pair.Key] = FindModelInScene(pair.Key);
                        continue;
                    }

                    // Check if a model has been moved to another scene, and correct this if it has
                    var gameObjectScene = sceneActiveModel.gameObject.scene;
                    if (gameObjectScene != pair.Key)
                    {
                        setSceneModels[pair.Key] = FindModelInScene(pair.Key);
                        setSceneModels[gameObjectScene] = FindModelInScene(pair.Key);
                    }
                }


                foreach (var scene in removeScenes)
                    Instance.activeModels.Remove(scene);


                foreach (var pair in setSceneModels)
                    Instance.activeModels[pair.Key] = pair.Value;
            }
            finally
            {
                ListPool<Scene>.Release(removeScenes);
                DictionaryPool<Scene, ChiselModel>.Release(setSceneModels);
            }
        }

#if UNITY_EDITOR
        public static void OnWillFlushUndoRecord()
        {
            // Called on Undo, which happens when moving model to another scene
            CheckActiveModels();
        }
         
        public static void OnActiveSceneChanged(Scene _, Scene newScene)
        {
            if (Instance.activeModels.TryGetValue(newScene, out var activeModel) && IsSelectable(activeModel))
                return;

            Instance.activeModels[newScene] = FindModelInScene(newScene);
            CheckActiveModels();
        }

        static ChiselModel GetSelectedModel()
        {
            var selectedGameObjects = UnityEditor.Selection.gameObjects;
            if (selectedGameObjects == null ||
                selectedGameObjects.Length == 1)
            { 
                var selection = selectedGameObjects[0];
                return selection.GetComponent<ChiselModel>();
            }
            return null;
        }

        const string SetActiveModelMenuName = "GameObject/Set Active Model";
        [UnityEditor.MenuItem(SetActiveModelMenuName, false, -100000)]
        internal static void SetActiveModel()
        {
            var model = GetSelectedModel();
            if (!model)
                return;
            ChiselModelManager.ActiveModel = model;
        }

        [UnityEditor.MenuItem(SetActiveModelMenuName, true, -100000)]
        internal static bool ValidateSetActiveModel()
        {
            var model = GetSelectedModel();
            return (model != null);
        }
#endif
    }
}