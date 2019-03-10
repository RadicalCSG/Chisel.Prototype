﻿using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Chisel.Assets;

namespace Chisel.Components
{
    // TODO: fix adding "generated" gameObject causing TransformChanged events that dirty model, which rebuilds components
    // TODO: Modifying a lightmap index *should also be undoable*
    public sealed class CSGGeneratedComponentManager
    {
        const string GeneratedDefaultModelName  = "‹[default-model]›";
        const string GeneratedContainerName     = "‹[generated]›";
        const string GeneratedMeshRendererName	= "‹[generated-MeshRenderer]›";
        const string GeneratedMeshColliderName	= "‹[generated-MeshCollider]›";

        static readonly HashSet<CSGModel> models = new HashSet<CSGModel>();
        static readonly List<MeshRenderer> updateMeshRenderers = new List<MeshRenderer>();
        static readonly List<MeshCollider> updateMeshColliders = new List<MeshCollider>();

#if UNITY_EDITOR
        const float kGenerateUVDelayTime = 1.0f;

        static bool haveUVsToUpdate = false;
#endif

        public void Clear()
        {
            models.Clear();
            updateMeshRenderers.Clear();
            updateMeshColliders.Clear();
        }

        public void Register(CSGModel model)
        {
            // Destroy leftover components in model lookups
            DestroyAllRegisteredGeneratedComponentsInModel(model);

            // Rebuild component lookup tables used by generatedMeshes
            BuildGeneratedComponentLookupTables(model);

            models.Add(model);
        }

        public void Unregister(CSGModel model)
        {
            DestroyAllRegisteredGeneratedComponentsInModel(model);
            RemoveContainerGameObject(model);
            
            models.Remove(model);
        }

        public void RemoveAllGeneratedComponents(CSGModel model)
        {
            DestroyAllRegisteredGeneratedComponentsInModel(model);
            RemoveContainerGameObject(model);
        }

        public static void ForceUpdateDelayedUVGeneration()
        {
            haveUVsToUpdate = true;
        }

        public static bool NeedUVGeneration(CSGModel model)
        {
            haveUVsToUpdate = false;

            if (!model)
                return false;

            var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(model.gameObject);
            if ((staticFlags & UnityEditor.StaticEditorFlags.LightmapStatic) != UnityEditor.StaticEditorFlags.LightmapStatic)
                return false;

            for (int i = 0; i < model.generatedMeshes.Length; i++)
            {
                var generatedMesh		= model.generatedMeshes[i];

                // Avoid light mapping multiple times, when the same mesh is used on multiple MeshRenderers
                if (!CSGSharedUnityMeshManager.HasLightmapUVs(generatedMesh.sharedMesh))
                    return true;
            }
            return false;
        }
        
        public static void DelayedUVGeneration(bool force = false)
        {
#if UNITY_EDITOR
            if (!haveUVsToUpdate && !force)
                return;

            float currentTime = Time.realtimeSinceStartup;

            haveUVsToUpdate = false;
            foreach (var model in models)
            {
                if (!model)
                    continue;

                var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(model.gameObject);
                if ((!model.AutoRebuildUVs && !force) ||
                    (staticFlags & UnityEditor.StaticEditorFlags.LightmapStatic) != UnityEditor.StaticEditorFlags.LightmapStatic)
                    continue;

                for (int i = 0; i < model.generatedMeshes.Length; i++)
                {
                    var generatedMesh = model.generatedMeshes[i];
                    var renderComponents = generatedMesh.renderComponents;
                    if (renderComponents == null || 
                        (!force && renderComponents.uvLightmapUpdateTime == 0))
                        continue;

                    if (force || 
                        (currentTime - renderComponents.uvLightmapUpdateTime) > kGenerateUVDelayTime)
                    {
                        renderComponents.uvLightmapUpdateTime = 0;
                        GenerateLightmapUVsForInstance(model, generatedMesh.renderComponents, generatedMesh.sharedMesh);
                    } else
                        haveUVsToUpdate = true;
                }
            }
#endif
        }

        static readonly ModelState __modelState = new ModelState(); // static to avoid allocations
        public void Rebuild(CSGModel model)
        {
            if (model.generatedMeshes == null ||
                model.generatedMeshes.Length == 0)
            {
                // Destroy leftover components in model lookups
                DestroyAllRegisteredGeneratedComponentsInModel(model);
                RemoveContainerGameObject(model);
            } else
            {
                if (!model.IsInitialized)
                {
                    model.OnInitialize(); 
                }

                if (!model.GeneratedDataTransform)
                {
                    DestroyAllRegisteredGeneratedComponentsInModel(model);
                    CreateContainerGameObject(model);
                } 

                var modelGameObject = model.gameObject;
    #if UNITY_EDITOR
                __modelState.staticFlags				= UnityEditor.GameObjectUtility.GetStaticEditorFlags(modelGameObject);
    #endif	
                __modelState.modelGameObject			= modelGameObject;
                __modelState.modelTransform				= model.hierarchyItem.Transform;
                __modelState.layer						= modelGameObject.layer;
                __modelState.containerGameObject		= model.GeneratedDataContainer;
                __modelState.containerTransform			= model.GeneratedDataTransform;
                __modelState.existingRenderComponents	= model.generatedRenderComponents;
                __modelState.existingMeshColliders		= model.generatedMeshColliders;

                UpdateModelFlags(model);
                UpdateContainerFlags(model, __modelState);
                // Build components for generatedMesh, re-use existing components if they're available (& remove from lookups)


                updateMeshRenderers.Clear();
                updateMeshColliders.Clear();
                for (int i = 0; i < model.generatedMeshes.Length; i++)
                {
                    var generatedMesh = model.generatedMeshes[i];
                    BuildComponents(model, __modelState, generatedMesh);
                }
                UpdateComponents(model, updateMeshRenderers, updateMeshColliders);
                updateMeshRenderers.Clear();
                updateMeshColliders.Clear();


                // Destroy leftover components in model lookups
                DestroyAllRegisteredGeneratedComponentsInModel(model);

                // Rebuild component lookup tables used by generatedMeshes
                BuildGeneratedComponentLookupTables(model);

                var containerTransform = __modelState.containerTransform;
                for (int c = containerTransform.childCount - 1; c >= 0; c--)
                {
                    var child = containerTransform.GetChild(c);
                    if (!model.generatedComponents.Contains(child))
                        CSGObjectUtility.SafeDestroy(child.gameObject);
                }
            }

        
            // to avoid dangling memory
            __modelState.modelGameObject		= null;
            __modelState.modelTransform			= null;
            __modelState.containerGameObject	= null;
            __modelState.containerTransform		= null;
        }

        static readonly HashSet<GameObject> __uniqueGameObjects = new HashSet<GameObject>();
        internal void DestroyAllRegisteredGeneratedComponentsInModel(CSGModel model)
        {
            GameObject modelGameObject = null;
            if (model)
                modelGameObject = model.gameObject;

            __uniqueGameObjects.Clear();
            foreach(var components in model.generatedRenderComponents.Values)
            {
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    if (component.gameObject)
                    {
                        var gameObject = component.gameObject;
                        if (gameObject != modelGameObject)
                            __uniqueGameObjects.Add(gameObject);
                    }
                }
            }
            foreach (var components in model.generatedMeshColliders.Values)
            {
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    if (component.gameObject)
                    {
                        var gameObject = component.gameObject;
                        if (gameObject != modelGameObject)
                            __uniqueGameObjects.Add(gameObject);
                    }
                }
            }
            foreach (var gameObject in __uniqueGameObjects)
            {
                CSGObjectUtility.SafeDestroy(gameObject);
            }

            __uniqueGameObjects.Clear();
            model.generatedRenderComponents.Clear();
            model.generatedMeshColliders.Clear();
            model.generatedComponents.Clear();
        }

        internal void BuildGeneratedComponentLookupTables(CSGModel model)
        {
            for (int i = 0; i < model.generatedMeshes.Length; i++)
            {
                var generatedMesh = model.generatedMeshes[i];
                if (generatedMesh.renderComponents != null &&
                    generatedMesh.renderComponents.meshRenderer &&
                    generatedMesh.renderComponents.meshFilter)
                {
                    var material = generatedMesh.renderComponents.meshRenderer.sharedMaterial;
                    List<CSGRenderComponents> components;
                    if (!model.generatedRenderComponents.TryGetValue(material, out components))
                    {
                        components = new List<CSGRenderComponents>();
                        model.generatedRenderComponents[material] = components;
                    }
                    components.Add(generatedMesh.renderComponents);
                    model.generatedComponents.Add(generatedMesh.renderComponents.transform);
                }

                if (generatedMesh.colliderComponents != null &&
                    generatedMesh.colliderComponents.meshCollider)
                {
                    var meshCollider = generatedMesh.colliderComponents.meshCollider; 
                    var material = meshCollider.sharedMaterial;
                    List<CSGColliderComponents> components;
                    if (!model.generatedMeshColliders.TryGetValue(material, out components))
                    {
                        components = new List<CSGColliderComponents>();
                        model.generatedMeshColliders[material] = components;
                    }
                    components.Add(generatedMesh.colliderComponents);
                    model.generatedComponents.Add(generatedMesh.colliderComponents.transform);
                }
            }
        }

        internal class ModelState
        {
            public GameObject		modelGameObject;
            public Transform		modelTransform;
            public GameObject		containerGameObject;
            public Transform		containerTransform;
            public int				layer;
            public Dictionary<Material,       List<CSGRenderComponents  >> existingRenderComponents	= new Dictionary<Material,       List<CSGRenderComponents  >>();
            public Dictionary<PhysicMaterial, List<CSGColliderComponents>> existingMeshColliders	= new Dictionary<PhysicMaterial, List<CSGColliderComponents>>();

#if UNITY_EDITOR
            public UnityEditor.StaticEditorFlags	staticFlags;
#endif
        }

        internal void BuildComponents(CSGModel			model, 
                                      ModelState		modelState,
                                      CSGGeneratedModelMesh	generatedMesh)
        {
            Material		renderMaterial	= null;
            PhysicMaterial	physicsMaterial = null;
            if (generatedMesh.meshDescription.surfaceParameter != 0)
            {
                var type		= generatedMesh.meshDescription.meshQuery.LayerParameterIndex;
                var parameter	= generatedMesh.meshDescription.surfaceParameter;
                switch (type)
                {
                    case LayerParameterIndex.LayerParameter1: renderMaterial  = CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(parameter);  break;
                    case LayerParameterIndex.LayerParameter2: physicsMaterial = CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(parameter); break;
                }
            } else
            {
                var type		= generatedMesh.meshDescription.meshQuery.LayerParameterIndex;
                switch (type)
                {
                    case LayerParameterIndex.LayerParameter1: renderMaterial  = CSGMaterialManager.DefaultMaterial; break;
                    case LayerParameterIndex.LayerParameter2: physicsMaterial = CSGMaterialManager.DefaultPhysicsMaterial; break;
                }
            }
            
            if (renderMaterial != null)
            {
                generatedMesh.renderComponents = null;
                List<CSGRenderComponents> components;
                if (modelState.existingRenderComponents.TryGetValue(renderMaterial, out components))
                {
                    while (components.Count > 0)
                    {
                        var curComponents = components[0];
                        components.RemoveAt(0);

                        if (components.Count == 0)
                        {
                            modelState.existingRenderComponents.Remove(renderMaterial);
                            model.generatedComponents.Remove(curComponents.transform);
                        }

                        if (curComponents.meshRenderer && curComponents.meshFilter)
                        {
                            generatedMesh.renderComponents = curComponents;
                            break;
                        }
                    } 
                }

                var forceUpdate = UpdateOrCreateRenderComponents(model, modelState, generatedMesh.meshDescription, ref generatedMesh.renderComponents);
                if (generatedMesh.renderComponents.meshRenderer.sharedMaterial != renderMaterial)
                    generatedMesh.renderComponents.meshRenderer.sharedMaterial =  renderMaterial;
                if (generatedMesh.renderComponents.meshFilter  .sharedMesh     != generatedMesh.sharedMesh)
                    generatedMesh.renderComponents.meshFilter  .sharedMesh     =  generatedMesh.sharedMesh;
                if (generatedMesh.needsUpdate || forceUpdate)
                {
#if UNITY_EDITOR
                    if ((modelState.staticFlags & UnityEditor.StaticEditorFlags.LightmapStatic) == UnityEditor.StaticEditorFlags.LightmapStatic)
                    {
                        generatedMesh.renderComponents.meshRenderer.realtimeLightmapIndex = -1;
                        generatedMesh.renderComponents.meshRenderer.lightmapIndex         = -1;
                        generatedMesh.renderComponents.uvLightmapUpdateTime = Time.realtimeSinceStartup;
                        haveUVsToUpdate = true;
                    }
#endif
                }
            } else
            if (physicsMaterial != null)
            {
                generatedMesh.colliderComponents = null;
                List<CSGColliderComponents> components;
                if (modelState.existingMeshColliders.TryGetValue(physicsMaterial, out components))
                {
                    while (components.Count > 0)
                    {
                        var curComponents = components[0];
                        components.RemoveAt(0);
                        if (components.Count == 0)
                        {
                            modelState.existingMeshColliders.Remove(physicsMaterial);
                            model.generatedComponents.Remove(curComponents.transform);
                        }

                        if (curComponents.meshCollider)
                        {
                            generatedMesh.colliderComponents = curComponents;
                            break;
                        }
                    }
                }

                UpdateOrCreateColliderComponents(model, modelState, generatedMesh.meshDescription, ref generatedMesh.colliderComponents);
                if (generatedMesh.colliderComponents.meshCollider.sharedMesh     != generatedMesh.sharedMesh)
                    generatedMesh.colliderComponents.meshCollider.sharedMesh	 =  generatedMesh.sharedMesh;
                if (generatedMesh.colliderComponents.meshCollider.sharedMaterial != physicsMaterial)
                    generatedMesh.colliderComponents.meshCollider.sharedMaterial =  physicsMaterial;
            }
            generatedMesh.needsUpdate = false;
        }

        private void UpdateContainerFlags(CSGModel model, ModelState modelState)
        {
            const HideFlags GameObjectHideFlags = HideFlags.NotEditable;
            const HideFlags TransformHideFlags	= HideFlags.NotEditable;// | HideFlags.HideInInspector;
            
            var gameObject	= modelState.containerGameObject;
            var transform	= modelState.containerTransform;
            if (gameObject.name != GeneratedContainerName)
                gameObject.name = GeneratedContainerName;

            // Make sure we're always a child of the model
            if (transform.parent != modelState.modelTransform)
            {
                transform.SetParent(modelState.modelTransform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale    = Vector3.one;
            }
            
            if (gameObject.layer     != modelState.layer   ) gameObject.layer     = modelState.layer;
            if (gameObject.hideFlags != GameObjectHideFlags) gameObject.hideFlags = GameObjectHideFlags;
            if (transform .hideFlags != TransformHideFlags ) transform .hideFlags = TransformHideFlags;

#if UNITY_EDITOR
            var prevStaticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(gameObject);
            if (prevStaticFlags != modelState.staticFlags)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(gameObject, modelState.staticFlags);
#endif
        }

        void UpdateComponentFlags(CSGModel model, ModelState modelState, Component component, GameObject componentGameObject, Transform componentTransform, string componentName, bool notEditable)
        {
            const HideFlags GameObjectHideFlags = HideFlags.NotEditable;
            const HideFlags TransformHideFlags	= HideFlags.NotEditable;// | HideFlags.HideInInspector;

            if (componentGameObject.name != componentName)
                componentGameObject.name = componentName;

            // TODO: make components turn off this flag when its gameObject is directly selected?
            HideFlags ComponentHideFlags = HideFlags.HideInHierarchy | (notEditable ? HideFlags.NotEditable : HideFlags.None); // Avoids MeshCollider showing wireframe

            // Some components could theoretically just be put on the model itself, so we don't modify any flags then
            if (componentGameObject == modelState.modelGameObject)
                return;

            // Make sure we're always a child of the current data container
            if (componentTransform.parent != modelState.containerTransform)
            {
                componentTransform.SetParent(modelState.containerTransform, false);
                componentTransform.localPosition = Vector3.zero;
                componentTransform.localRotation = Quaternion.identity;
                componentTransform.localScale    = Vector3.one;
            }
            
            if (componentGameObject.layer     != modelState.layer   ) componentGameObject.layer     = modelState.layer;
            if (componentGameObject.hideFlags != GameObjectHideFlags) componentGameObject.hideFlags = GameObjectHideFlags;
            if (componentTransform .hideFlags != TransformHideFlags ) componentTransform .hideFlags = TransformHideFlags;
            if (component          .hideFlags != ComponentHideFlags ) component          .hideFlags = ComponentHideFlags;

#if UNITY_EDITOR
            var prevStaticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(componentGameObject);
            if (prevStaticFlags != modelState.staticFlags)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(componentGameObject, modelState.staticFlags);
#endif
        }
        
        public static bool IsDefaultModel(UnityEngine.Object obj)
        {
            var component = obj as Component;
            if (!Equals(component, null))
                return IsDefaultModel(component);
            var gameObject = obj as GameObject;
            if (!Equals(gameObject, null))
                return IsDefaultModel(gameObject);
            return false;
        }
        
        internal static bool IsDefaultModel(GameObject gameObject)	{ return gameObject && (gameObject.name == GeneratedDefaultModelName) && (gameObject.GetComponent<CSGModel>()); }
        internal static bool IsDefaultModel(Component component)	{ return component  && (component.name  == GeneratedDefaultModelName) && (component is CSGModel || component.GetComponent<CSGModel>()); }

        static List<GameObject> __rootGameObjects = new List<GameObject>(); // static to avoid allocations
        internal static CSGModel CreateDefaultModel(CSGSceneHierarchy sceneHierarchy)
        {
            var currentScene = sceneHierarchy.Scene;
            currentScene.GetRootGameObjects(__rootGameObjects);
            for (int i = 0; i < __rootGameObjects.Count; i++)
            {
                if (!IsDefaultModel(__rootGameObjects[i]))
                    continue;
                
                var gameObject = __rootGameObjects[i];
                var model = gameObject.GetComponent<CSGModel>();
                if (model)
                    return model;

                var transform = gameObject.GetComponent<Transform>();
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale	= Vector3.one;
                    
                model = gameObject.AddComponent<CSGModel>();
                UpdateModelFlags(model);
                return model;
            }


            var oldActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(currentScene);
            
            try
            {
                var gameObject = new GameObject(GeneratedDefaultModelName);
                var transform = gameObject.GetComponent<Transform>();
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale	= Vector3.one;
                
                var model = gameObject.AddComponent<CSGModel>();
                UpdateModelFlags(model);
                return model;
            }
            finally
            {
                if (currentScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        private static void UpdateModelFlags(CSGModel model)
        {
            if (!IsDefaultModel(model))
                return;
            
            const HideFlags DefaultGameObjectHideFlags	= HideFlags.NotEditable;
            const HideFlags DefaultTransformHideFlags	= HideFlags.NotEditable;// | HideFlags.HideInInspector;
            
            var gameObject = model.gameObject;
            var transform  = model.transform;
            if (gameObject.hideFlags != DefaultGameObjectHideFlags) gameObject.hideFlags = DefaultGameObjectHideFlags;
            if (transform .hideFlags != DefaultTransformHideFlags ) transform .hideFlags = DefaultTransformHideFlags;

            if (transform.parent != null)
            {
                transform.SetParent(null, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale    = Vector3.one;
            }
        }
                
        private void CreateContainerGameObject(CSGModel model)
        {
            var modelScene		= model.gameObject.scene;
            var oldActiveScene	= UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (modelScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(modelScene);
            
            try
            {
                CSGNodeHierarchyManager.ignoreNextChildrenChanged = true;
                var gameObject = new GameObject(GeneratedContainerName);
                var transform  = gameObject.GetComponent<Transform>();
                CSGNodeHierarchyManager.ignoreNextChildrenChanged = true;
                transform.SetParent(model.transform, false);
                CSGNodeHierarchyManager.ignoreNextChildrenChanged = false;
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                model.GeneratedDataContainer = gameObject;
                model.GeneratedDataTransform = transform;
                model.OnInitialize();
            }
            finally
            {
                if (modelScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        private void RemoveContainerGameObject(CSGModel model)
        {

            if (model.GeneratedDataTransform)
            {
                model.GeneratedDataContainer.hideFlags = HideFlags.None;
                model.GeneratedDataTransform.hideFlags = HideFlags.None;
                CSGNodeHierarchyManager.ignoreNextChildrenChanged = true;
                CSGObjectUtility.SafeDestroy(model.GeneratedDataContainer);
                CSGNodeHierarchyManager.ignoreNextChildrenChanged = false;
                model.GeneratedDataContainer = null;
                model.GeneratedDataTransform = null;
            }
        }

        public static GameObject FindContainerGameObject(CSGModel model)
        {
            if (!model)
                return null;
            var transform = model.transform;
            for (int i = 0, childCount = transform.childCount; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == GeneratedContainerName)
                    return child.gameObject;
            }
            return null;
        }

        public static void RemoveContainerFlags(CSGModel model)
        {
            if (model.GeneratedDataContainer) model.GeneratedDataContainer.hideFlags = HideFlags.None;
            foreach (var lists in model.generatedRenderComponents.Values)
            {
                foreach (var item in lists)
                {
                    if (item.gameObject  ) item.gameObject  .hideFlags = HideFlags.None;
                    if (item.transform   ) item.transform   .hideFlags = HideFlags.None;
                    if (item.meshFilter  ) item.meshFilter  .hideFlags = HideFlags.None;
                    if (item.meshRenderer) item.meshRenderer.hideFlags = HideFlags.None;
                }
            }
            foreach (var lists in model.generatedMeshColliders.Values)
            {
                foreach (var item in lists)
                {
                    if (item.gameObject  ) item.gameObject  .hideFlags = HideFlags.None;
                    if (item.transform   ) item.transform   .hideFlags = HideFlags.None;
                    if (item.meshCollider) item.meshCollider.hideFlags = HideFlags.None;
                }
            }
        }

        public static bool IsObjectGenerated(UnityEngine.Object obj)
        {
            if (!obj)
                return false;

            var gameObject = obj as GameObject;
            if (Equals(gameObject, null))
            {
                var component = obj as GameObject;
                if (Equals(component, null))
                    return false;

                gameObject = component.gameObject;
            }

            if (gameObject.name == GeneratedContainerName)
                return true;

            var parent = gameObject.transform.parent;
            if (Equals(parent, null))
                return false;

            return parent.name == GeneratedContainerName;
        }
    

        private GameObject CreateComponentGameObject(CSGModel model, string name, params Type[] components)
        {
            var modelScene		= model.gameObject.scene;
            var oldActiveScene	= UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (modelScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(modelScene);			
            try
            {
                var container  = model.GeneratedDataContainer;
                var gameObject = new GameObject(name, components);
                var transform  = gameObject.GetComponent<Transform>();
                transform.SetParent(container.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                return gameObject;
            }
            finally
            {
                if (modelScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }


        private CSGRenderComponents CreateRenderComponents(CSGModel model, GeneratedMeshDescription meshDescription)
        {
            var gameObject	= CreateComponentGameObject(model, GeneratedMeshRendererName, typeof(MeshFilter), typeof(MeshRenderer));
            var renderComponents = new CSGRenderComponents
            {
                meshFilter		= gameObject.GetComponent<MeshFilter>(),
                meshRenderer	= gameObject.GetComponent<MeshRenderer>(),
                gameObject		= gameObject,
                transform		= gameObject.transform
            };
            return renderComponents;
        }

        private CSGColliderComponents CreateColliderComponents(CSGModel model, GeneratedMeshDescription meshDescription)
        {
            var gameObject		= CreateComponentGameObject(model, GeneratedMeshColliderName, typeof(MeshCollider));
            var colliderComponents = new CSGColliderComponents
            {
                meshCollider	= gameObject.GetComponent<MeshCollider>(),
                gameObject		= gameObject,
                transform		= gameObject.transform
            };
            return colliderComponents;
        }

        private void UpdateComponents(CSGModel model, List<MeshRenderer> meshRenderers, List<MeshCollider> meshColliders)
        {
            if (meshRenderers.Count > 0)
            { 
                var renderSettings = model.RenderSettings;
#if UNITY_EDITOR
                // Warning: calling new UnityEditor.SerializedObject with an empty array crashes Unity
                var serializedObject = new UnityEditor.SerializedObject(meshRenderers.ToArray());
                #if !UNITY_2017_2_OR_ABOVE
                var dynamicOccludeeProp						 = serializedObject.FindProperty("m_DynamicOccludee");
                if (dynamicOccludeeProp != null)
                    dynamicOccludeeProp.boolValue			 = renderSettings.dynamicOccludee;
                #endif
                #if !UNITY_2018_2_OR_ABOVE
                var renderingLayerMaskProp					 = serializedObject.FindProperty("m_RenderingLayerMask");
                if (renderingLayerMaskProp != null)
                    renderingLayerMaskProp.intValue			 = (int)renderSettings.renderingLayerMask;
                #endif

                var importantGIProp							 = serializedObject.FindProperty("m_ImportantGI");
                importantGIProp.boolValue					 = renderSettings.importantGI;
                var optimizeUVsProp							 = serializedObject.FindProperty("m_PreserveUVs");
                optimizeUVsProp.boolValue					 = renderSettings.optimizeUVs;
                var ignoreNormalsForChartDetectionProp		 = serializedObject.FindProperty("m_IgnoreNormalsForChartDetection");
                ignoreNormalsForChartDetectionProp.boolValue = renderSettings.ignoreNormalsForChartDetection;
                var scaleInLightmapProp						 = serializedObject.FindProperty("m_ScaleInLightmap");
                scaleInLightmapProp.floatValue				 = renderSettings.scaleInLightmap;
                var autoUVMaxDistanceProp					 = serializedObject.FindProperty("m_AutoUVMaxDistance");
                autoUVMaxDistanceProp.floatValue			 = renderSettings.autoUVMaxDistance;
                var autoUVMaxAngleProp						 = serializedObject.FindProperty("m_AutoUVMaxAngle");
                autoUVMaxAngleProp.floatValue				 = renderSettings.autoUVMaxAngle;
                var minimumChartSizeProp					 = serializedObject.FindProperty("m_MinimumChartSize");
                minimumChartSizeProp.intValue				 = renderSettings.minimumChartSize;

                #if UNITY_2017_2_OR_ABOVE
                var stitchLightmapSeamsProp					= serializedObject.FindProperty("m_StitchLightmapSeams");
                stitchLightmapSeamsProp.boolValue			= renderSettings.stitchLightmapSeams;
                #endif
#endif

                for(int i = 0; i < meshRenderers.Count; i++)
                {
                    var meshRenderer = meshRenderers[i];
                    meshRenderer.lightProbeProxyVolumeOverride	= renderSettings.lightProbeProxyVolumeOverride;
                    meshRenderer.probeAnchor					= renderSettings.probeAnchor;
                    meshRenderer.motionVectorGenerationMode		= renderSettings.motionVectorGenerationMode;
                    meshRenderer.reflectionProbeUsage			= renderSettings.reflectionProbeUsage;
                    meshRenderer.lightProbeUsage				= renderSettings.lightProbeUsage;
                    #if UNITY_2017_2_OR_ABOVE
                    meshRenderer.allowOcclusionWhenDynamic		= renderSettings.allowOcclusionWhenDynamic;
                    #endif
                    #if UNITY_2018_2_OR_ABOVE
                    meshRenderer.renderingLayerMask				= renderSettings.renderingLayerMask;
                    #endif
                }
            }

            if (meshColliders.Count > 0)
            {
                var colliderSettings = model.ColliderSettings;
                for (int i = 0; i < meshColliders.Count; i++)
                {
                    var meshCollider = meshColliders[i];

                    meshCollider.cookingOptions	= colliderSettings.cookingOptions;
                    meshCollider.convex			= colliderSettings.convex;
                    meshCollider.isTrigger		= colliderSettings.isTrigger;
                }
            }
        }
        

        private void UpdateRenderComponents(CSGModel model, ModelState modelState, GeneratedMeshDescription meshDescription, CSGRenderComponents renderComponents)
        {
            var meshRenderer = renderComponents.meshRenderer;
#if UNITY_EDITOR
            updateMeshRenderers.Add(meshRenderer);
#endif

            var query = meshDescription.meshQuery.LayerQuery;
            meshRenderer.receiveShadows		= ((query & LayerUsageFlags.ReceiveShadows) == LayerUsageFlags.ReceiveShadows);
            switch (query & (LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows))
            {
                case LayerUsageFlags.None:				meshRenderer.enabled = false; break;
                case LayerUsageFlags.Renderable:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;			meshRenderer.enabled = true; break;
                case LayerUsageFlags.CastShadows:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;	meshRenderer.enabled = true; break;
                case LayerUsageFlags.RenderCastShadows:	meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;			meshRenderer.enabled = true; break;
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetSelectedRenderState(meshRenderer, UnityEditor.EditorSelectedRenderState.Hidden);
#endif
        }

        private void UpdateColliderComponents(CSGModel model, ModelState modelState, GeneratedMeshDescription meshDescription, CSGColliderComponents colliderComponents)
        {
            var meshCollider = colliderComponents.meshCollider;
            updateMeshColliders.Add(meshCollider);
        }


        // NOTE: assumes that if a renderComponents are passed to this, they are -valid-
        //		 do any checking outside of this method, and make sure everything that 
        //		 needs to be cleaned up, IS cleaned up
        private bool UpdateOrCreateRenderComponents(CSGModel model, ModelState modelState, GeneratedMeshDescription meshDescription, ref CSGRenderComponents renderComponents)
        {
            bool updated = false;
            if (renderComponents == null)
            {
                renderComponents = CreateRenderComponents(model, meshDescription);
                updated = true;
            }

            UpdateRenderComponents(model, modelState, meshDescription, renderComponents);
            UpdateComponentFlags(model, modelState, renderComponents.meshRenderer, renderComponents.gameObject, renderComponents.transform, GeneratedMeshRendererName, notEditable: true);
            if (!renderComponents.meshRenderer.enabled) renderComponents.meshRenderer.enabled = true;
            return updated;
        }

        // NOTE: assumes that if a meshCollider is passed to this, it is -valid-
        //		 do any checking outside of this method, and make sure everything that 
        //		 needs to be cleaned up, IS cleaned up
        private bool UpdateOrCreateColliderComponents(CSGModel model, ModelState modelState, GeneratedMeshDescription meshDescription, ref CSGColliderComponents colliderComponents)
        {
            bool updated = false;
            if (colliderComponents == null)
            {
                colliderComponents = CreateColliderComponents(model, meshDescription);
                updated = true;
            }
            UpdateColliderComponents(model, modelState, meshDescription, colliderComponents);
            UpdateComponentFlags(model, modelState, colliderComponents.meshCollider, colliderComponents.gameObject, colliderComponents.transform, GeneratedMeshColliderName, notEditable: true);
            if (!colliderComponents.meshCollider.enabled) colliderComponents.meshCollider.enabled = true;
            return updated;
        }

#if UNITY_EDITOR
        private static void GenerateLightmapUVsForInstance(CSGModel model, CSGRenderComponents renderComponents, Mesh generatedMesh)
        {
            // Avoid light mapping multiple times, when the same mesh is used on multiple MeshRenderers
            if (CSGSharedUnityMeshManager.HasLightmapUVs(generatedMesh))
                return;

            if (renderComponents == null ||
                !renderComponents.meshFilter ||
                !renderComponents.meshRenderer)
                return;
            
            var uvSettings = model.UVGenerationSettings;
            var param = new UnityEditor.UnwrapParam();
            UnityEditor.UnwrapParam.SetDefaults(out param);
            param.angleError	= Mathf.Clamp(uvSettings.angleError,       SerializableUnwrapParam.minAngleError, SerializableUnwrapParam.maxAngleError);
            param.areaError		= Mathf.Clamp(uvSettings.areaError,        SerializableUnwrapParam.minAreaError,  SerializableUnwrapParam.maxAreaError );
            param.hardAngle		= Mathf.Clamp(uvSettings.hardAngle,        SerializableUnwrapParam.minHardAngle,  SerializableUnwrapParam.maxHardAngle );
            param.packMargin	= Mathf.Clamp(uvSettings.packMarginPixels, SerializableUnwrapParam.minPackMargin, SerializableUnwrapParam.maxPackMargin) / 256.0f;

            var oldVertices		= generatedMesh.vertices;
            if (oldVertices.Length == 0)
                return;

            // TODO: can we avoid creating a temporary Mesh? if not; make sure CSGSharedUnityMeshManager is handled correctly

            var oldUV			= generatedMesh.uv;
            var oldNormals		= generatedMesh.normals;
            var oldTangents		= generatedMesh.tangents;
            var oldTriangles	= generatedMesh.triangles;

            var tempMesh = new Mesh
            {
                vertices	= oldVertices,
                normals		= oldNormals,
                uv			= oldUV,
                tangents	= oldTangents,
                triangles	= oldTriangles
            };
            
            var lightmapGenerationTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.Unwrapping.GenerateSecondaryUVSet(tempMesh, param);
            lightmapGenerationTime = UnityEditor.EditorApplication.timeSinceStartup - lightmapGenerationTime;
            
            // TODO: make a nicer text here
            Debug.Log("Generating lightmap UVs (by Unity) for the mesh '" + generatedMesh.name + "' of the Model named \"" + model.name +"\"\n"+
                      "\tUV generation in " + (lightmapGenerationTime* 1000) + " ms\n", model);

            // Modify the original mesh, since it is shared
            generatedMesh.uv2 = tempMesh.uv2;	// static lightmaps
            generatedMesh.uv3 = tempMesh.uv3;   // real-time lightmaps
            CSGSharedUnityMeshManager.SetHasLightmapUVs(generatedMesh, true);

            renderComponents.meshFilter.sharedMesh = null;
            renderComponents.meshFilter.sharedMesh = generatedMesh;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
        }
#endif



        public class HideFlagsState
        {
            public Dictionary<UnityEngine.GameObject, CSGModel>	generatedComponents;
            public Dictionary<UnityEngine.Object, HideFlags>	hideFlags;
        }

        public static HideFlagsState BeginPicking()
        {
            var state = new HideFlagsState()
            {
                generatedComponents = new Dictionary<UnityEngine.GameObject, CSGModel>(),
                hideFlags = new Dictionary<UnityEngine.Object, HideFlags>()
            };

            foreach(var model in models)
            {
                if (!model.GeneratedDataContainer)
                    continue;
                var renderers	= model.GeneratedDataContainer.GetComponentsInChildren<Renderer>();
                if (renderers != null)
                {
                    foreach (var renderer in renderers)
                        state.generatedComponents[renderer.gameObject] = model;
                }

                var colliders	= model.GeneratedDataContainer.GetComponentsInChildren<Collider>();
                if (colliders != null)
                {
                    foreach (var collider in colliders)
                        state.generatedComponents[collider.gameObject] = model;
                }
            }
            if (state.generatedComponents != null)
            {
                foreach(var component in state.generatedComponents.Keys)
                {
                    var gameObject = component.gameObject;
                    var transform = component.transform;
                    state.hideFlags[gameObject] = gameObject.hideFlags;
                    state.hideFlags[transform] = transform.hideFlags;
                    state.hideFlags[component] = component.hideFlags;
                    gameObject.hideFlags = HideFlags.None;
                    transform.hideFlags = HideFlags.None;
                    component.hideFlags = HideFlags.None;
                }
            }
            return state;
        }
        
        public static bool EndPicking(HideFlagsState state, UnityEngine.Object pickedObject, out CSGModel model)
        {
            model = null;
            if (state == null || state.hideFlags == null)
                return false;
            
            foreach (var pair in state.hideFlags)
                pair.Key.hideFlags = pair.Value;
            
            if (object.Equals(pickedObject, null))
                return false;

            if (state.generatedComponents == null)
                return false;

            bool pickedGeneratedComponent = false;		
            foreach(var pair in state.generatedComponents)
            {
                if (pickedObject == pair.Key)
                {
                    model = pair.Value;
                    pickedGeneratedComponent = true;
                    break;
                }
            }

            return pickedGeneratedComponent;
        }
    }
}
