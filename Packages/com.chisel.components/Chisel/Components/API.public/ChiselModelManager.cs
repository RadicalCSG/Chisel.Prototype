using System;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    public class ChiselModelManager : ScriptableObject
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
                
                var foundInstances = UnityEngine.Object.FindObjectsOfType<ChiselModelManager>();
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

        [SerializeField]
        public ChiselModel activeModel;
        
        public static ChiselModel ActiveModel
        { 
            get
            {
                var activeModel = Instance.activeModel;
                if (ReferenceEquals(activeModel, null))
                {
                    // TODO: handle not having an active model (try and find one in the scene?)
                    return null;
                }
                if (!activeModel)
                {
                    // TODO: handle active model being invalid
                    return null;
                }
                // TODO: handle active model not being in active scene
                return activeModel;
            }
            set
            {
                Instance.activeModel = value;
            }
        }

        // TODO: fix this so we keep the active scene in mind, as in, when the active scene changes, 
        // we should be using a model in the active scene, not in another scene.
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
                // TODO: ensure we create this in the active scene
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

#if UNITY_EDITOR
        static ChiselModel GetSelectedModel()
        {
            var selectedGameObjects = UnityEditor.Selection.gameObjects;
            if (selectedGameObjects == null ||
                selectedGameObjects.Length == 1)
            { 
                var selection   = selectedGameObjects[0];
                return selection.GetComponent<ChiselModel>();
            }
            return null;
        }

        const string SetActiveModelMenuName = "GameObject/Set Active Model";
        [UnityEditor.MenuItem(SetActiveModelMenuName, false, -100000)]
        static void SetActiveModel()
        {
            var model = GetSelectedModel();
            if (!model)
                return;
            ChiselModelManager.ActiveModel = model;
        }

        [UnityEditor.MenuItem(SetActiveModelMenuName, true, -100000)]
        static bool ValidateSetActiveModel()
        {
            var model = GetSelectedModel();
            return (model != null);
        }
#endif
    }
}