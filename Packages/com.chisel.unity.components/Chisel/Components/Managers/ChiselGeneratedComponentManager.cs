using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Chisel.Components
{
    // TODO: fix adding "generated" gameObject causing TransformChanged events that dirty model, which rebuilds components
    // TODO: Modifying a lightmap index *should also be undoable*
    public sealed class ChiselGeneratedComponentManager
    {
        public const string kGeneratedDefaultModelName  = "‹[default-model]›";
        public const string kGeneratedContainerName     = "‹[generated]›";

        static readonly HashSet<ChiselModelComponent> s_Models = new();

#if UNITY_EDITOR
        const float kGenerateUVDelayTime = 1.0f;

        static bool haveUVsToUpdate = false;
#endif

        public void Register(ChiselModelComponent model)
        {
            s_Models.Add(model);
        }

        public void Unregister(ChiselModelComponent model)
        {
            // If we removed our model component, we should remove the containers
            if (!model && model.hierarchyItem.GameObject)
                RemoveContainerGameObjectWithUndo(model);

            s_Models.Remove(model);
        }

        public static void ForceUpdateDelayedUVGeneration()
        {
#if UNITY_EDITOR
            haveUVsToUpdate = true;
#endif
        }

        public static bool NeedUVGeneration(ChiselModelComponent model)
        {
#if UNITY_EDITOR
            haveUVsToUpdate = false;

            if (!model)
                return false;

            var staticFlags = GameObjectUtility.GetStaticEditorFlags(model.gameObject);
            if ((staticFlags & StaticEditorFlags.ContributeGI) != StaticEditorFlags.ContributeGI)
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
            foreach (var model in s_Models)
            {
                if (!model)
                    continue;

                var staticFlags = GameObjectUtility.GetStaticEditorFlags(model.gameObject);
                var lightmapStatic = (staticFlags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI;
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

        public void Rebuild(ChiselModelComponent model)
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
        
        // Get all brushes directly contained by this CSGNode (not its children)
        public static void GetAllTreeBrushes(ChiselGeneratorComponent component, HashSet<CSGTreeBrush> foundBrushes)
        {
            if (foundBrushes == null ||
                !component.TopTreeNode.Valid)
                return;

            var brush = (CSGTreeBrush)component.TopTreeNode;
            if (brush.Valid)
            {
                foundBrushes.Add(brush);
            } else
            {
                var nodes = new List<CSGTreeNode>();
                nodes.Add(component.TopTreeNode);
                while (nodes.Count > 0)
                {
                    var lastIndex = nodes.Count - 1;
                    var current = nodes[lastIndex];
                    nodes.RemoveAt(lastIndex);
                    var nodeType = current.Type;
                    if (nodeType == CSGNodeType.Brush)
                    {
                        brush = (CSGTreeBrush)current;
                            foundBrushes.Add(brush);
                    } else
                    {
                        for (int i = current.Count - 1; i >= 0; i--)
                            nodes.Add(current[i]);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private static readonly Dictionary<CompactNodeID, VisibilityState> s_VisibilityStateLookup = new();
        public static bool IsBrushVisible(CompactNodeID brushID) 
        { 
            return s_VisibilityStateLookup.TryGetValue(brushID, out VisibilityState state) && state == VisibilityState.AllVisible; 
        }

        static bool updateVisibilityFlag = true;
        public static void OnVisibilityChanged()
        {
            updateVisibilityFlag = true;
            EditorApplication.delayCall -= OnUnityIndeterministicMessageOrderingWorkAround;
            EditorApplication.delayCall += OnUnityIndeterministicMessageOrderingWorkAround;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        static void OnUnityIndeterministicMessageOrderingWorkAround()
        {
            EditorApplication.delayCall -= OnUnityIndeterministicMessageOrderingWorkAround;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        public static DrawModeFlags UpdateHelperSurfaceState(DrawModeFlags helperStateFlags, bool ignoreBrushVisibility = true)
        {
            foreach (var model in s_Models)
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
                foreach (var model in go.GetComponentsInChildren<ChiselModelComponent>())
                {
                    if (!model || !model.isActiveAndEnabled || model.generated == null)
                        continue;
                    model.generated.RemoveHelperSurfaces();
                }
            }
        }

        public static void RemoveHelperSurfaces()
        {
            var scene = SceneManager.GetActiveScene();
            foreach(var go in scene.GetRootGameObjects())
            {
                foreach (var model in go.GetComponentsInChildren<ChiselModelComponent>())
                {
                    if (!model || !model.isActiveAndEnabled || model.generated == null)
                        continue;
                    model.generated.RemoveHelperSurfaces();
                }
            }
        }

        public static void OnRenderModels(Camera camera, DrawModeFlags helperStateFlags)
        {
            foreach (var model in s_Models)
            {
                if (model == null)
                    continue;
                model.OnRenderModel(camera, helperStateFlags);
            }
        }

        public static VisibilityState UpdateVisibilityState(SceneVisibilityManager instance, ChiselGeneratorComponent generator)
        {
            var resultState     = VisibilityState.Unknown;
            var visible         = !instance.IsHidden(generator.gameObject);
            var pickingEnabled  = !instance.IsPickingDisabled(generator.gameObject);
            var topNode         = generator.TopTreeNode;
            if (topNode.Valid)
            {
                topNode.Visible         = visible;
                topNode.PickingEnabled  = pickingEnabled;

                if (visible)
                    resultState |= VisibilityState.AllVisible;
                else
                    resultState |= VisibilityState.AllInvisible;
            }
            return resultState;
        }

        public static bool HasVisibilityInitialized(ChiselGeneratorComponent node)
        {
            if (!node.TopTreeNode.Valid)
                return false;

            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(node.TopTreeNode);
            foreach (var childCompactNodeID in CompactHierarchyManager.GetAllChildren(compactNodeID))
            {
                if (!s_VisibilityStateLookup.ContainsKey(childCompactNodeID))
                    return false;
            }
            return true;
        } 

        public static void EnsureVisibilityInitialized(ChiselGeneratorComponent node)
        {
            if (HasVisibilityInitialized(node))
                return;
            UpdateVisibility(node);
        }

        public static void UpdateVisibility(ChiselGeneratorComponent node)
        {
            var sceneVisibilityManager = SceneVisibilityManager.instance;
            UpdateVisibility(sceneVisibilityManager, node);
        }

        static void UpdateVisibility(SceneVisibilityManager sceneVisibilityManager, ChiselGeneratorComponent node)
        {
            var treeNode = node.TopTreeNode;
            if (!treeNode.Valid)
                return;

            var model = node.hierarchyItem.Model;
            if (model == null)
                Debug.LogError($"{node.hierarchyItem.Component} model {model} == null", node.hierarchyItem.Component);
            if (!model)
                return;

            var modelNode = model.TopTreeNode;
            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(treeNode);
            var modelCompactNodeID = CompactHierarchyManager.GetCompactNodeID(modelNode);
            if (!s_VisibilityStateLookup.TryGetValue(modelCompactNodeID, out VisibilityState prevState))
                prevState = VisibilityState.Unknown;
            var state = UpdateVisibilityState(sceneVisibilityManager, node);

            foreach (var childCompactNodeID in CompactHierarchyManager.GetAllChildren(compactNodeID))
                s_VisibilityStateLookup[childCompactNodeID] = state;
            s_VisibilityStateLookup[modelCompactNodeID] = state | prevState;
        }

        public static void UpdateVisibility(bool force = false)
        { 
            if (!updateVisibilityFlag && !force)
                return;

            updateVisibilityFlag = false;
            // TODO: 1. turn off rendering regular meshes when we have partial visibility of model contents
            //       2. find a way to render partial mesh instead
            //          A. needs to show lightmap of original mesh, even when modified
            //          B. updating lightmaps needs to still work as if original mesh is changed
            s_VisibilityStateLookup.Clear();
            var sceneVisibilityManager = SceneVisibilityManager.instance;
            foreach (var node in ChiselGeneratedModelMeshManager.s_RegisteredNodeLookup)
            {
                if (!node || !node.isActiveAndEnabled)
                    continue;

                var generatorComponent = node as ChiselGeneratorComponent;
                if (generatorComponent)
                {
                    UpdateVisibility(sceneVisibilityManager, generatorComponent);
                }
            }

            foreach (var model in s_Models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated == null)
                    continue;
                var modelNode = model.TopTreeNode;
                if (!modelNode.Valid)
                    continue;
                var modelCompactNodeID  = CompactHierarchyManager.GetCompactNodeID(modelNode);
                if (!s_VisibilityStateLookup.TryGetValue(modelCompactNodeID, out VisibilityState state))
                {
                    s_VisibilityStateLookup[modelCompactNodeID] = VisibilityState.AllVisible;
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
        
        internal static bool IsDefaultModel(GameObject gameObject)
        {
            if (!gameObject)
                return false;
            var model = gameObject.GetComponent<ChiselModelComponent>();
            if (!model)
                return false;
            return (model.IsDefaultModel);
        }

        internal static bool IsDefaultModel(Component component)	
        {
            if (!component)
                return false;
            ChiselModelComponent model = component as ChiselModelComponent;
            if (!model)
            {
                model = component.GetComponent<ChiselModelComponent>();
                if (!model)
                    return false;
            }
            return (model.IsDefaultModel);
        }

        internal static bool IsDefaultModel(ChiselModelComponent model)
        {
            if (!model)
                return false;
            return (model.IsDefaultModel);
        }

        internal static ChiselModelComponent CreateDefaultModel(ChiselSceneHierarchy sceneHierarchy)
        {
            var currentScene = sceneHierarchy.Scene;
            var rootGameObjects = ListPool<GameObject>.Get();
            currentScene.GetRootGameObjects(rootGameObjects);
            for (int i = 0; i < rootGameObjects.Count; i++)
            {
                if (!IsDefaultModel(rootGameObjects[i]))
                    continue;
                
                var gameObject = rootGameObjects[i];
                var model = gameObject.GetComponent<ChiselModelComponent>();
                if (model)
                    return model;

                var transform = gameObject.GetComponent<Transform>();
                ChiselObjectUtility.ResetTransform(transform);

                model = gameObject.AddComponent<ChiselModelComponent>();
                UpdateModelFlags(model);
                return model;
            }
            ListPool<GameObject>.Release(rootGameObjects);


            var oldActiveScene = SceneManager.GetActiveScene();
            if (currentScene != oldActiveScene)
                SceneManager.SetActiveScene(currentScene);
            
            try
            {
                var model = ChiselComponentFactory.Create<ChiselModelComponent>(kGeneratedDefaultModelName);
                model.IsDefaultModel = true;
                UpdateModelFlags(model);
                return model;
            }
            finally
            {
                if (currentScene != oldActiveScene)
                    SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        private static void UpdateModelFlags(ChiselModelComponent model)
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

        private void RemoveContainerGameObjectWithUndo(ChiselModelComponent model)
        {
            if (model.generated != null)
                model.generated.DestroyWithUndo();
        }

        private void RemoveContainerGameObject(ChiselModelComponent model)
        {
            if (model.generated != null)
            {
                model.generated.Destroy();
                model.generated = null;
            }
        }

        public static void RemoveContainerFlags(ChiselModelComponent model)
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

        public static void CheckIfFullMeshNeedsToBeHidden(ChiselModelComponent model, ChiselRenderObjects renderable)
        {
            var shouldHideMesh = (model.generated.visibilityState != VisibilityState.AllVisible && model.generated.visibilityState != VisibilityState.Unknown);
            if (renderable.meshRenderer.forceRenderingOff != shouldHideMesh)
                renderable.meshRenderer.forceRenderingOff = shouldHideMesh;
        }
        public static void ClearLightmapData(GameObjectState state, ChiselRenderObjects renderable)
        {
            var lightmapStatic = (state.staticFlags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI;
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
        // Note: tried storing this in name of mesh, but getting the current mesh name allocates a lot of memory 
        public static bool HasLightmapUVs(UnityEngine.Mesh sharedMesh)
        {
            if (!sharedMesh)
                return true;
            return (sharedMesh.hideFlags & HideFlags.NotEditable) == HideFlags.NotEditable;
        }

        public static void SetHasLightmapUVs(UnityEngine.Mesh sharedMesh, bool haveLightmapUVs)
        {
            HideFlags hideFlags     = sharedMesh.hideFlags;
            HideFlags newHideFlags  = hideFlags;
            if (!haveLightmapUVs)
            {
                newHideFlags &= ~HideFlags.NotEditable;
            } else
            {
                newHideFlags |= HideFlags.NotEditable;
            }

            if (newHideFlags == hideFlags)
                return;
            sharedMesh.hideFlags = newHideFlags;
        }

        private static void GenerateLightmapUVsForInstance(ChiselModelComponent model, ChiselRenderObjects renderable, bool force = false)
        {
            // Avoid light mapping multiple times, when the same mesh is used on multiple MeshRenderers
            if (!force && renderable.HasLightmapUVs)
                return;

            if (renderable == null ||
                !renderable.meshFilter ||
                !renderable.meshRenderer)
                return;
            
            UnwrapParam.SetDefaults(out UnwrapParam param);
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
            
            var lightmapGenerationTime = EditorApplication.timeSinceStartup;
            Unwrapping.GenerateSecondaryUVSet(tempMesh, param);
            lightmapGenerationTime = EditorApplication.timeSinceStartup - lightmapGenerationTime; 
            
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
            EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
        }
#endif


        public class HideFlagsState
        {
            public Dictionary<UnityEngine.GameObject, ChiselModelComponent>	generatedComponents;
            public Dictionary<UnityEngine.Object, HideFlags>	    hideFlags;
#if UNITY_EDITOR
            public Dictionary<Renderer, bool>	                    rendererOff;
            public Dictionary<Renderer, bool>	                    rendererDisabled;
            public Dictionary<UnityEngine.GameObject, bool>	        hierarchyHidden;
            public Dictionary<UnityEngine.GameObject, bool>	        hierarchyDisabled;
#endif
        }

        // TODO: find a better place for this
        public static bool IsValidModelToBeSelected(ChiselModelComponent model)
        {
            if (!model || !model.isActiveAndEnabled || model.generated == null)
                return false;
#if UNITY_EDITOR
            var gameObject = model.gameObject;
            var sceneVisibilityManager = SceneVisibilityManager.instance;
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
                generatedComponents = new Dictionary<UnityEngine.GameObject, ChiselModelComponent>(),
                hideFlags           = new Dictionary<UnityEngine.Object, HideFlags>(),
#if UNITY_EDITOR
                rendererOff         = new Dictionary<Renderer, bool>(),
                rendererDisabled    = new Dictionary<Renderer, bool>(),
                hierarchyHidden     = new Dictionary<UnityEngine.GameObject, bool>(),
                hierarchyDisabled   = new Dictionary<UnityEngine.GameObject, bool>(),
#endif
            };

#if UNITY_EDITOR
            var sceneVisibilityManager = SceneVisibilityManager.instance;
            s_IgnoreVisibility = true;
            BeginDrawModeForCamera(ignoreBrushVisibility: true);
#endif

            foreach (var model in s_Models)
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
                        if (debugHelper.visible)
                        {
                            state.rendererDisabled[debugHelper.meshRenderer] = true;
                            debugHelper.meshRenderer.forceRenderingOff = false;
                            debugHelper.meshRenderer.enabled = true;
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
        
        public static bool EndPicking(HideFlagsState state, UnityEngine.Object pickedObject, out ChiselModelComponent model)
        {
            model = null;
            if (state == null || state.hideFlags == null)
                return false;
            
            foreach (var pair in state.hideFlags)
                pair.Key.hideFlags = pair.Value;

#if UNITY_EDITOR
            var sceneVisibilityManager = SceneVisibilityManager.instance;
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
            foreach (var pair in state.rendererDisabled)
            {
                pair.Key.enabled = false;
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

#if UNITY_EDITOR
        static readonly Dictionary<Camera, DrawModeFlags> s_CameraDrawMode = new();
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
#endif
    }
}
