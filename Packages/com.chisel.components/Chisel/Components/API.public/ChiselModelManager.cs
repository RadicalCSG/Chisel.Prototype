using System;
using System.Linq;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Components
{
    // TODO: have concept of active model
    // TODO: reduce allocations, reuse existing arrays when possible
    // TODO: make it possible to generate brushes in specific scenes/specific parents
    // TODO: clean up
    // TODO: rename
    public class ChiselModelManager : ScriptableObject
    {
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
                        UnityEngine.Object.DestroyImmediate(foundInstances[i]);
                }
                
                _instance = foundInstances[0];
                return _instance;
            }
        }
        #endregion

        [SerializeField]
        public ChiselModel activeModel;

        // TODO: make this part of Undo stack ...
        public static ChiselModel ActiveModel
        { 
            get
            {
                return Instance.activeModel;
            }
            set
            {
                Instance.activeModel = value;
            }
        }

        // TODO: improve naming
        public static ChiselModel GetModelForNode(ChiselModel overrideModel = null)
        {
            if (overrideModel)
            {
                ChiselModelManager.ActiveModel = overrideModel;
                return overrideModel;
            }

            var activeModel = ChiselModelManager.ActiveModel;
            if (!activeModel)
            {
                activeModel = ChiselComponentFactory.Create<ChiselModel>("Model");
                ChiselModelManager.ActiveModel = activeModel; 
            }
            return activeModel;
        }
    }
}