using Chisel.Core;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Components
{
    // TODO: fix adding "generated" gameObject causing TransformChanged events that dirty model, which rebuilds components
    // TODO: Modifying a lightmap index *should also be undoable*
    public sealed class ChiselGeneratedComponentManager
    {
        public const string kGeneratedDefaultModelName  = "‹[default-model]›";
        public const string kGeneratedContainerName     = "‹[generated]›";

        static readonly HashSet<ChiselModel> models = new HashSet<ChiselModel>();

#if UNITY_EDITOR
        const float kGenerateUVDelayTime = 1.0f;

        static bool haveUVsToUpdate = false;
#endif

        public void Register(ChiselModel model)
        {
            models.Add(model);
        }

        public void Unregister(ChiselModel model)
        {
            // If we removed our model component, we should remove the containers
            if (!model && model.hierarchyItem.GameObject)
                RemoveContainerGameObjectWithUndo(model);
            
            models.Remove(model);
        }

        public static void ForceUpdateDelayedUVGeneration()
        {
#if UNITY_EDITOR
            haveUVsToUpdate = true;
#endif
        }

        public static bool NeedUVGeneration(ChiselModel model)
        {
#if UNITY_EDITOR
            haveUVsToUpdate = false;

            if (!model)
                return false;

            var staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(model.gameObject);
            if ((staticFlags & UnityEditor.StaticEditorFlags.ContributeGI) != UnityEditor.StaticEditorFlags.ContributeGI)
                return false;

            if (!model.generated.HasLightmapUVs)
                return true;
#endif
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
                var lightmapStatic = (staticFlags & UnityEditor.StaticEditorFlags.ContributeGI) == UnityEditor.StaticEditorFlags.ContributeGI;
                if ((!model.AutoRebuildUVs && !force) || !lightmapStatic)
                    continue;

                var renderables = model.generated.renderables;
                if (renderables == null)
                    continue;

                for (int i = 0; i < renderables.Length; i++)
                {
                    var renderable  = renderables[i];
                    if (renderable == null || 
                        renderable.invalid ||
                        (!force && renderable.uvLightmapUpdateTime == 0))
                        continue;

                    if (force || 
                        (currentTime - renderable.uvLightmapUpdateTime) > kGenerateUVDelayTime)
                    {
                        renderable.uvLightmapUpdateTime = 0;
                        GenerateLightmapUVsForInstance(model, renderable, force);
                    } else
                        haveUVsToUpdate = true;
                }
            }
#endif
        }

        public void Rebuild(ChiselModel model)
        {
            if (!model.IsInitialized)
            {
                model.OnInitialize(); 
            }

            if (!ChiselGeneratedObjects.IsValid(model.generated))
            {
                if (model.generated != null)
                    model.generated.Destroy();
                model.generated = ChiselGeneratedObjects.Create(model.gameObject);
            }

            UpdateModelFlags(model);
        }

#if UNITY_EDITOR
        static Dictionary<int, VisibilityState> visibilityStateLookup = new Dictionary<int, VisibilityState>();
        public static bool IsBrushVisible(int brushID) { return visibilityStateLookup.TryGetValue(brushID, out VisibilityState state) && state == VisibilityState.AllVisible; }

        static bool updateVisibilityFlag = false;
        public static void OnVisibilityChanged()
        {
            updateVisibilityFlag = true;
            UnityEditor.EditorApplication.delayCall -= OnUnityIndeterministicMessageOrderingWorkAround;
            UnityEditor.EditorApplication.delayCall += OnUnityIndeterministicMessageOrderingWorkAround;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        static void OnUnityIndeterministicMessageOrderingWorkAround()
        {
            UnityEditor.EditorApplication.delayCall -= OnUnityIndeterministicMessageOrderingWorkAround;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        public static DrawModeFlags UpdateHelperSurfaceState(DrawModeFlags helperStateFlags, bool ignoreBrushVisibility = true)
        {
            foreach (var model in models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated == null)
                    continue;
                model.generated.UpdateHelperSurfaceState(helperStateFlags, ignoreBrushVisibility);
            }
            return helperStateFlags;
        }

        public static void InitializeOnLoad(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var model in go.GetComponentsInChildren<ChiselModel>())
                {
                    if (!model || !model.isActiveAndEnabled || model.generated == null)
                        continue;
                    model.generated.RemoveHelperSurfaces();
                }
            }
        }

        public static void RemoveHelperSurfaces()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var go in scene.GetRootGameObjects())
            {
                foreach (var model in go.GetComponentsInChildren<ChiselModel>())
                {
                    if (!model || !model.isActiveAndEnabled || model.generated == null)
                        continue;
                    model.generated.RemoveHelperSurfaces();
                }
            }
        }

        public static void OnRenderModels(Camera camera, DrawModeFlags helperStateFlags)
        {
            foreach (var model in models)
            {
                model.OnRenderModel(camera, helperStateFlags);
            }
        }

        public static void UpdateVisibility()
        {
            if (!updateVisibilityFlag)
                return;

            updateVisibilityFlag = false;
            // TODO: 1. turn off rendering regular meshes when we have partial visibility of model contents
            //       2. find a way to render partial mesh instead
            //          A. needs to show lightmap of original mesh, even when modified
            //          B. updating lightmaps needs to still work as if original mesh is changed
            visibilityStateLookup.Clear();
            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            foreach (var node in ChiselGeneratedModelMeshManager.registeredNodeLookup)
            {
                if (!node || !node.isActiveAndEnabled)
                    continue;
                var generator = node as ChiselGeneratorComponent;
                if (!generator)
                    continue;

                if (!visibilityStateLookup.TryGetValue(generator.hierarchyItem.Model.NodeID, out VisibilityState prevState))
                    prevState = VisibilityState.Unknown;
                var state = generator.UpdateVisibility(sceneVisibilityManager);
                visibilityStateLookup[node.NodeID] = state;
                visibilityStateLookup[generator.hierarchyItem.Model.NodeID] = state | prevState;
            }

            foreach (var model in models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated == null)
                    continue;
                if (!visibilityStateLookup.TryGetValue(model.NodeID, out VisibilityState state))
                {
                    visibilityStateLookup[model.NodeID] = VisibilityState.AllVisible;
                    model.generated.visibilityState = VisibilityState.AllVisible;
                    continue;
                }
                if (state == VisibilityState.Mixed ||
                    state != model.generated.visibilityState)
                    model.generated.needVisibilityMeshUpdate = true;
                model.generated.visibilityState = state;
            }
        }
#endif

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
        
        internal static bool IsDefaultModel(GameObject gameObject)	{ return gameObject && (gameObject.name == kGeneratedDefaultModelName) && (gameObject.GetComponent<ChiselModel>()); }
        internal static bool IsDefaultModel(Component component)	{ return component  && (component.name  == kGeneratedDefaultModelName) && (component is ChiselModel || component.GetComponent<ChiselModel>()); }

        static List<GameObject> __rootGameObjects = new List<GameObject>(); // static to avoid allocations
        internal static ChiselModel CreateDefaultModel(ChiselSceneHierarchy sceneHierarchy)
        {
            var currentScene = sceneHierarchy.Scene;
            currentScene.GetRootGameObjects(__rootGameObjects);
            for (int i = 0; i < __rootGameObjects.Count; i++)
            {
                if (!IsDefaultModel(__rootGameObjects[i]))
                    continue;
                
                var gameObject = __rootGameObjects[i];
                var model = gameObject.GetComponent<ChiselModel>();
                if (model)
                    return model;

                var transform = gameObject.GetComponent<Transform>();
                ChiselObjectUtility.ResetTransform(transform);

                model = gameObject.AddComponent<ChiselModel>();
                UpdateModelFlags(model);
                return model;
            }


            var oldActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(currentScene);
            
            try
            {
                var model = ChiselComponentFactory.Create<ChiselModel>(kGeneratedDefaultModelName);
                UpdateModelFlags(model);
                return model;
            }
            finally
            {
                if (currentScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        private static void UpdateModelFlags(ChiselModel model)
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
                ChiselObjectUtility.ResetTransform(transform);
            }
        }

        private void RemoveContainerGameObjectWithUndo(ChiselModel model)
        {
            if (model.generated != null)
                model.generated.DestroyWithUndo();
        }

        private void RemoveContainerGameObject(ChiselModel model)
        {
            if (model.generated != null)
            {
                model.generated.Destroy();
                model.generated = null;
            }
        }

        public static void RemoveContainerFlags(ChiselModel model)
        {
            model.generated.RemoveContainerFlags();
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

            if (gameObject.name == kGeneratedContainerName)
                return true;

            var parent = gameObject.transform.parent;
            if (Equals(parent, null))
                return false;

            return parent.name == kGeneratedContainerName;
        }
#if UNITY_EDITOR

        public static void CheckIfFullMeshNeedsToBeHidden(ChiselModel model, ChiselRenderObjects renderable)
        {
            var shouldHideMesh = (model.generated.visibilityState != VisibilityState.AllVisible && model.generated.visibilityState != VisibilityState.Unknown);
            if (renderable.meshRenderer.forceRenderingOff != shouldHideMesh)
                renderable.meshRenderer.forceRenderingOff = shouldHideMesh;
        }
        public static void ClearLightmapData(GameObjectState state, ChiselRenderObjects renderable)
        {
            var lightmapStatic = (state.staticFlags & UnityEditor.StaticEditorFlags.ContributeGI) == UnityEditor.StaticEditorFlags.ContributeGI;
            if (lightmapStatic)
            {
                renderable.meshRenderer.realtimeLightmapIndex = -1;
                renderable.meshRenderer.lightmapIndex = -1;
                renderable.uvLightmapUpdateTime = Time.realtimeSinceStartup;
                haveUVsToUpdate = true;
            }
        }
#endif

#if UNITY_EDITOR
        // Hacky way to store that a mesh has lightmap UV created
        public static bool HasLightmapUVs(UnityEngine.Mesh sharedMesh)
        {
            if (!sharedMesh)
                return true;
            var name = sharedMesh.name;
            if (!string.IsNullOrEmpty(name) &&
                name[name.Length - 1] == '*')
                return true;
            return false;
        }

        public static void SetHasLightmapUVs(UnityEngine.Mesh sharedMesh, bool haveLightmapUVs)
        {
            var name = sharedMesh.name;
            if (!haveLightmapUVs)
            {
                if (!string.IsNullOrEmpty(name) && name[name.Length - 1] == '*')
                    return;
                sharedMesh.name = name + "*";
            } else
            {
                if (string.IsNullOrEmpty(name) || name[name.Length - 1] != '*')
                    return;
                name = name.Remove(name.Length - 1);
                sharedMesh.name = name;
            }
        }

        private static void GenerateLightmapUVsForInstance(ChiselModel model, ChiselRenderObjects renderable, bool force = false)
        {
            // Avoid light mapping multiple times, when the same mesh is used on multiple MeshRenderers
            if (!force && renderable.HasLightmapUVs)
                return;

            if (renderable == null ||
                !renderable.meshFilter ||
                !renderable.meshRenderer)
                return;
            
            UnityEditor.UnwrapParam.SetDefaults(out UnityEditor.UnwrapParam param);
            var uvSettings = model.UVGenerationSettings;
            param.angleError	= Mathf.Clamp(uvSettings.angleError,       SerializableUnwrapParam.minAngleError, SerializableUnwrapParam.maxAngleError);
            param.areaError		= Mathf.Clamp(uvSettings.areaError,        SerializableUnwrapParam.minAreaError,  SerializableUnwrapParam.maxAreaError );
            param.hardAngle		= Mathf.Clamp(uvSettings.hardAngle,        SerializableUnwrapParam.minHardAngle,  SerializableUnwrapParam.maxHardAngle );
            param.packMargin	= Mathf.Clamp(uvSettings.packMarginPixels, SerializableUnwrapParam.minPackMargin, SerializableUnwrapParam.maxPackMargin) / 256.0f;

            var sharedMesh      = renderable.sharedMesh;

            var oldVertices		= sharedMesh.vertices;
            if (oldVertices.Length == 0)
                return;

            // TODO: can we avoid creating a temporary Mesh? if not; make sure ChiselSharedUnityMeshManager is handled correctly

            var oldUV			= sharedMesh.uv;
            var oldNormals		= sharedMesh.normals;
            var oldTangents		= sharedMesh.tangents;
            var oldTriangles	= sharedMesh.triangles;

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
            Debug.Log("Generating lightmap UVs (by Unity) for the mesh '" + sharedMesh.name + "' of the Model named \"" + model.name +"\"\n"+
                      "\tUV generation in " + (lightmapGenerationTime* 1000) + " ms\n", model);

            // Modify the original mesh, since it is shared
            sharedMesh.Clear(keepVertexLayout: true);
            sharedMesh.vertices  = tempMesh.vertices;
            sharedMesh.normals   = tempMesh.normals;
            sharedMesh.tangents  = tempMesh.tangents;
            sharedMesh.uv        = tempMesh.uv;
            sharedMesh.uv2       = tempMesh.uv2;	    // static lightmaps
            sharedMesh.uv3       = tempMesh.uv3;        // real-time lightmaps
            sharedMesh.triangles = tempMesh.triangles;
            SetHasLightmapUVs(sharedMesh, true);

            renderable.meshFilter.sharedMesh = null;
            renderable.meshFilter.sharedMesh = sharedMesh;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
        }
#endif


        public class HideFlagsState
        {
            public Dictionary<UnityEngine.GameObject, ChiselModel>	generatedComponents;
            public Dictionary<UnityEngine.Object, HideFlags>	    hideFlags;
#if UNITY_EDITOR
            public Dictionary<Renderer, bool>	                    rendererOff;
            public Dictionary<UnityEngine.GameObject, bool>	        hierarchyHidden;
            public Dictionary<UnityEngine.GameObject, bool>	        hierarchyDisabled;
#endif
        }

        // TODO: find a better place for this
        public static bool IsValidModelToBeSelected(ChiselModel model)
        {
            if (!model || !model.isActiveAndEnabled || model.generated == null)
                return false;
#if UNITY_EDITOR
            var gameObject = model.gameObject;
            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            if (sceneVisibilityManager.AreAllDescendantsHidden(gameObject) ||
                sceneVisibilityManager.IsPickingDisabledOnAllDescendants(gameObject))
                return false;
#endif
            return true;
        }

        public static HideFlagsState BeginPicking()
        {
            var state = new HideFlagsState()
            {
                generatedComponents = new Dictionary<UnityEngine.GameObject, ChiselModel>(),
                hideFlags           = new Dictionary<UnityEngine.Object, HideFlags>(),
#if UNITY_EDITOR
                rendererOff         = new Dictionary<Renderer, bool>(),
                hierarchyHidden     = new Dictionary<UnityEngine.GameObject, bool>(),
                hierarchyDisabled   = new Dictionary<UnityEngine.GameObject, bool>(),
#endif
            };

#if UNITY_EDITOR
            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            s_IgnoreVisibility = true;
            BeginDrawModeForCamera(ignoreBrushVisibility: true);
#endif

            foreach (var model in models)
            {
                if (!IsValidModelToBeSelected(model))
                    continue;

                var renderers	= model.generated.renderables;
                if (renderers != null)
                {
                    foreach (var renderer in renderers)
                    {
                        if (renderer == null || renderer.invalid || !renderer.container)
                            continue;
                        state.generatedComponents[renderer.container] = model;
#if UNITY_EDITOR
                        if (renderer.meshRenderer.forceRenderingOff)
                        {
                            state.rendererOff[renderer.meshRenderer] = true;
                            renderer.meshRenderer.forceRenderingOff = false;
                        }
#endif
                    }
                }

                var debugHelpers = model.generated.debugHelpers;
                if (debugHelpers != null)
                {
                    foreach (var debugHelper in debugHelpers)
                    {
                        if (debugHelper == null || debugHelper.invalid || !debugHelper.container)
                            continue;
                        state.generatedComponents[debugHelper.container] = model;
#if UNITY_EDITOR
                        if (debugHelper.meshRenderer.forceRenderingOff)
                        {
                            state.rendererOff[debugHelper.meshRenderer] = true;
                            debugHelper.meshRenderer.forceRenderingOff = false;
                        }
#endif
                    }
                }
            }
            if (state.generatedComponents != null)
            {
                foreach(var component in state.generatedComponents.Keys)
                {
                    var gameObject  = component.gameObject;
                    var transform   = component.transform;
                    state.hideFlags[gameObject] = gameObject.hideFlags;
                    state.hideFlags[transform] = transform.hideFlags;
                    state.hideFlags[component] = component.hideFlags;
                    gameObject.hideFlags = HideFlags.None;
                    transform.hideFlags = HideFlags.None;
                    component.hideFlags = HideFlags.None;
#if UNITY_EDITOR
                    state.hierarchyHidden[gameObject] = sceneVisibilityManager.IsHidden(gameObject);
                    if (state.hierarchyHidden[gameObject])
                        sceneVisibilityManager.Show(gameObject, false);
                    
                    state.hierarchyDisabled[gameObject] = sceneVisibilityManager.IsPickingDisabled(gameObject);
                    if (state.hierarchyDisabled[gameObject])
                        sceneVisibilityManager.EnablePicking(gameObject, false);
#endif
                }
            }
            return state;
        }
        
        public static bool EndPicking(HideFlagsState state, UnityEngine.Object pickedObject, out ChiselModel model)
        {
            model = null;
            if (state == null || state.hideFlags == null)
                return false;
            
            foreach (var pair in state.hideFlags)
                pair.Key.hideFlags = pair.Value;

#if UNITY_EDITOR
            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            foreach (var pair in state.hierarchyHidden)
            {
                if (pair.Value)
                    sceneVisibilityManager.Hide(pair.Key, false);
            }
            foreach (var pair in state.hierarchyDisabled)
            {
                if (pair.Value)
                    sceneVisibilityManager.DisablePicking(pair.Key, false);
            }
            foreach (var pair in state.rendererOff)
            {
                pair.Key.forceRenderingOff = pair.Value;
            }
            s_IgnoreVisibility = false;
            EndDrawModeForCamera();
#endif
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

        static readonly Dictionary<Camera, DrawModeFlags>   s_CameraDrawMode    = new Dictionary<Camera, DrawModeFlags>();

        static bool s_IgnoreVisibility = false;

        public static void ResetCameraDrawMode(Camera camera)
        {
            s_CameraDrawMode.Remove(camera);
        }

        public static void SetCameraDrawMode(Camera camera, DrawModeFlags drawModeFlags)
        {
            s_CameraDrawMode[camera] = drawModeFlags;
        }

        public static DrawModeFlags GetCameraDrawMode(Camera camera)
        {
            if (!s_CameraDrawMode.TryGetValue(camera, out var drawModeFlags))
                drawModeFlags = DrawModeFlags.Default;
            return drawModeFlags;
        }

        public static DrawModeFlags BeginDrawModeForCamera(Camera camera = null, bool ignoreBrushVisibility = false)
        {
            if (camera == null)
                camera = Camera.current;
            var currentState = GetCameraDrawMode(camera);
            return UpdateHelperSurfaceState(currentState, s_IgnoreVisibility || ignoreBrushVisibility);
        }

        public static DrawModeFlags EndDrawModeForCamera()
        {
            return UpdateHelperSurfaceState(DrawModeFlags.Default, ignoreBrushVisibility: true);
        }

        public static void Update()
        {
            UpdateHelperSurfaceState(DrawModeFlags.Default, ignoreBrushVisibility: true);
        }
    }
}
