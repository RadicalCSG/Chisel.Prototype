using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using System.Transactions;
using UnityEngine.Profiling;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;

namespace Chisel.Components
{        
    public enum DrawModeFlags
    {
        None            = 0,
        Default         = None,
        HideRenderables = 1,
        ShowColliders   = 2,
        ShowCasters     = 4,
        ShowShadowOnly  = 8,
        ShowReceivers   = 16,
        ShowCulled      = 32,
        ShowDiscarded   = 64,
    }

    //
    // 1. figure out what you where trying to do here, and remove need for the dictionary
    // 2. then do the same for the rendering equiv.
    // 3. separate building/updating the components from building a combined (phsyics)materials/mesh/rendersetting
    // 4. move colliders to single gameobject
    // 5. build meshes with submeshes, have a fixed number of meshes. one mesh per renderingsetting type (shadow-only etc.)
    // 6. have a secondary version of these fixed number of meshes that has partial meshes & use those for rendering in chiselmodel
    // 7. have a way to identify which triangles belong to which brush. so we can build partial meshes
    // 8. profit!
    //
    [Serializable]
    public class ChiselGeneratedObjects
    {
        public const string kGeneratedContainerName     = "‹[generated]›";
        public const int kGeneratedMeshRenderCount = 8;
        public const int kGeneratedMeshRendererCount = 5;
        public static readonly string[] kGeneratedMeshRendererNames = new string[]
        {
            null,                                                   // 0 (invalid option)
            "‹[generated-Renderable]›",                             // 1
            "‹[generated-CastShadows]›",                            // 2 (Shadow-Only)
            "‹[generated-Renderable|CastShadows]›",                 // 3
            null,                                                   // 4 (invalid option)
            "‹[generated-Renderable|ReceiveShadows]›",              // 5
            null,                                                   // 6 (invalid option)
            "‹[generated-Renderable|CastShadows|ReceiveShadows]›"   // 7
        };

        public const int kDebugHelperCount = 6;
        public static readonly string[] kGeneratedDebugRendererNames = new string[kDebugHelperCount]
        {
            "‹[debug-Discarded]›",                                  // LayerUsageFlags.None
            "‹[debug-CastShadows]›",                                // LayerUsageFlags.RenderableCastShadows
            "‹[debug-ShadowOnly]›",                                 // LayerUsageFlags.CastShadows
            "‹[debug-ReceiveShadows]›",                             // LayerUsageFlags.RenderableReceiveShadows
            "‹[debug-Collidable]›",                                 // LayerUsageFlags.Collidable
            "‹[debug-Culled]›"                                      // LayerUsageFlags.Culled
        };
        public static readonly (LayerUsageFlags, LayerUsageFlags)[] kGeneratedDebugRendererFlags = new (LayerUsageFlags, LayerUsageFlags)[kDebugHelperCount]
        {
            ( LayerUsageFlags.None                  , LayerUsageFlags.Renderable),              // is explicitly set to "not visible"
            ( LayerUsageFlags.RenderCastShadows     , LayerUsageFlags.RenderCastShadows),       // casts Shadows and is renderered
            ( LayerUsageFlags.CastShadows           , LayerUsageFlags.RenderCastShadows),       // casts Shadows and is NOT renderered (shadowOnly)
            ( LayerUsageFlags.RenderReceiveShadows  , LayerUsageFlags.RenderReceiveShadows),    // any surface that receives shadows (must be rendered)
            ( LayerUsageFlags.Collidable            , LayerUsageFlags.Collidable),              // collider surfaces
            ( LayerUsageFlags.Culled                , LayerUsageFlags.Culled)                   // all surfaces removed by the CSG algorithm
        };
        public static readonly DrawModeFlags[] kGeneratedDebugShowFlags = new DrawModeFlags[kDebugHelperCount]
        {
            DrawModeFlags.ShowDiscarded,
            DrawModeFlags.ShowCasters,
            DrawModeFlags.ShowShadowOnly,
            DrawModeFlags.ShowReceivers,
            DrawModeFlags.ShowColliders,
            DrawModeFlags.ShowCulled
        };
        public const string kGeneratedMeshColliderName	= "‹[generated-Collider]›";

        public GameObject               generatedDataContainer;
        public GameObject               colliderContainer;
        public ChiselColliderObjects[]  colliders;

        public ChiselRenderObjects[]    renderables;
        public MeshRenderer[]           meshRenderers;

        public ChiselRenderObjects[]    debugHelpers;
        public MeshRenderer[]           debugMeshRenderers;

        public VisibilityState          visibilityState             = VisibilityState.Unknown;
        public bool                     needVisibilityMeshUpdate    = false;
        
        private ChiselGeneratedObjects() { }

        public static ChiselGeneratedObjects Create(GameObject parentGameObject)
        {
            var parentTransform     = parentGameObject.transform;

            // Make sure there's not a dangling container out there from a previous version
            var existingContainer   = parentTransform.FindChildByName(kGeneratedContainerName);
            ChiselObjectUtility.SafeDestroy(existingContainer, ignoreHierarchyEvents: true);

            var gameObjectState     = GameObjectState.Create(parentGameObject);
            var container           = ChiselObjectUtility.CreateGameObject(kGeneratedContainerName, parentTransform, gameObjectState);
            var containerTransform  = container.transform;
            var colliderContainer   = ChiselObjectUtility.CreateGameObject(kGeneratedMeshColliderName, containerTransform, gameObjectState);

            Debug.Assert((int)LayerUsageFlags.Renderable     == 1);
            Debug.Assert((int)LayerUsageFlags.CastShadows    == 2);
            Debug.Assert((int)LayerUsageFlags.ReceiveShadows == 4);
            Debug.Assert((int)LayerUsageFlags.RenderReceiveCastShadows == (1|2|4));

            var renderables = new ChiselRenderObjects[]
            {
                new ChiselRenderObjects() { invalid = true },
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[1], containerTransform, gameObjectState, LayerUsageFlags.Renderable                               ),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[2], containerTransform, gameObjectState, LayerUsageFlags.CastShadows                              ),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[3], containerTransform, gameObjectState, LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows ),
                new ChiselRenderObjects() { invalid = true },
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[5], containerTransform, gameObjectState, LayerUsageFlags.Renderable |                               LayerUsageFlags.ReceiveShadows),
                new ChiselRenderObjects() { invalid = true },
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[7], containerTransform, gameObjectState, LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows | LayerUsageFlags.ReceiveShadows),
            };

            var meshRenderers = new MeshRenderer[]
            {
                renderables[1].meshRenderer,
                renderables[2].meshRenderer,
                renderables[3].meshRenderer,
                renderables[5].meshRenderer,
                renderables[7].meshRenderer
            };

            renderables[1].invalid = false;
            renderables[2].invalid = false;
            renderables[3].invalid = false;
            renderables[5].invalid = false;
            renderables[7].invalid = false;

            var debugHelpers = new ChiselRenderObjects[kDebugHelperCount];
            var debugMeshRenderers = new MeshRenderer[kDebugHelperCount];
            for (int i = 0; i < kDebugHelperCount; i++)
            {
                debugHelpers[i] = ChiselRenderObjects.Create(kGeneratedDebugRendererNames[i], containerTransform, gameObjectState, kGeneratedDebugRendererFlags[i].Item1, debugHelperRenderer: true);
                debugMeshRenderers[i] = debugHelpers[0].meshRenderer;
                debugHelpers[i].invalid = false;
            }

            var result = new ChiselGeneratedObjects
            {
                generatedDataContainer  = container,
                colliderContainer       = colliderContainer,
                colliders               = new ChiselColliderObjects[0],
                renderables             = renderables,
                meshRenderers           = meshRenderers,
                debugHelpers            = debugHelpers,
                debugMeshRenderers      = debugMeshRenderers
            };

            Debug.Assert(IsValid(result));

            return result;
        }

        public void Destroy()
        {
            if (!generatedDataContainer)
                return;

            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    if (collider != null)
                        collider.Destroy();
                }
                colliders = null;
            }
            if (renderables != null)
            {
                foreach (var renderable in renderables)
                {
                    if (renderable != null)
                        renderable.Destroy();
                }
                renderables = null;
            }
            if (debugHelpers != null)
            {
                foreach (var debugHelper in debugHelpers)
                {
                    if (debugHelper != null)
                        debugHelper.Destroy();
                }
                debugHelpers = null;
            }
            ChiselObjectUtility.SafeDestroy(colliderContainer, ignoreHierarchyEvents: true);
            ChiselObjectUtility.SafeDestroy(generatedDataContainer, ignoreHierarchyEvents: true);
            generatedDataContainer  = null;
            colliderContainer       = null;

            meshRenderers       = null;
            debugMeshRenderers  = null;
        }

        public void DestroyWithUndo()
        {
            if (!generatedDataContainer)
                return;

            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    if (collider != null)
                        collider.DestroyWithUndo();
                }
            }
            if (renderables != null)
            {
                foreach (var renderable in renderables)
                {
                    if (renderable != null)
                        renderable.DestroyWithUndo();
                }
            }
            if (debugHelpers != null)
            {
                foreach (var debugHelper in debugHelpers)
                {
                    if (debugHelper != null)
                        debugHelper.DestroyWithUndo();
                }
            }
            ChiselObjectUtility.SafeDestroyWithUndo(colliderContainer, ignoreHierarchyEvents: true);
            ChiselObjectUtility.SafeDestroyWithUndo(generatedDataContainer, ignoreHierarchyEvents: true);
        }

        public void RemoveContainerFlags()
        {
            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    if (collider != null)
                        collider.RemoveContainerFlags();
                }
            }
            if (renderables != null)
            {
                foreach (var renderable in renderables)
                {
                    if (renderable != null)
                        renderable.RemoveContainerFlags();
                }
            }
            if (debugHelpers != null)
            {
                foreach (var debugHelper in debugHelpers)
                {
                    if (debugHelper != null)
                        debugHelper.RemoveContainerFlags();
                }
            }
            ChiselObjectUtility.RemoveContainerFlags(colliderContainer);
            ChiselObjectUtility.RemoveContainerFlags(generatedDataContainer);
        }

        public static bool IsValid(ChiselGeneratedObjects satelliteObjects)
        {
            if (satelliteObjects == null)
                return false;

            if (!satelliteObjects.generatedDataContainer)
                return false;

            if (!satelliteObjects.colliderContainer ||
                satelliteObjects.colliders == null)   // must be an array, even if 0 length
                return false;

            if (satelliteObjects.renderables == null ||
                satelliteObjects.renderables.Length != kGeneratedMeshRenderCount ||
                satelliteObjects.meshRenderers == null ||
                satelliteObjects.meshRenderers.Length != kGeneratedMeshRendererCount)
                return false;

            if (satelliteObjects.debugHelpers == null ||
                satelliteObjects.debugHelpers.Length != kDebugHelperCount ||
                satelliteObjects.debugMeshRenderers == null ||
                satelliteObjects.debugMeshRenderers.Length != kDebugHelperCount)
                return false;

            // These queries are valid, and should never be null (We don't care about the other queries)
            if (satelliteObjects.renderables[1] == null ||
                satelliteObjects.renderables[2] == null ||
                satelliteObjects.renderables[3] == null ||
                satelliteObjects.renderables[5] == null ||
                satelliteObjects.renderables[7] == null)
                return false;

            // These queries are valid, and should never be null (We don't care about the other queries)
            for (int i = 0; i < kDebugHelperCount;i++)
            { 
                if (satelliteObjects.debugHelpers[i] == null)
                    return false;
            }
            
            satelliteObjects.renderables[0].invalid = true;
            satelliteObjects.renderables[1].invalid = false;
            satelliteObjects.renderables[2].invalid = false;
            satelliteObjects.renderables[3].invalid = false;
            satelliteObjects.renderables[4].invalid = true;
            satelliteObjects.renderables[5].invalid = false;
            satelliteObjects.renderables[6].invalid = true;
            satelliteObjects.renderables[7].invalid = false;

            for (int i = 0; i < kDebugHelperCount; i++)
                satelliteObjects.debugHelpers[i].invalid = false;

            for (int i = 0; i < satelliteObjects.renderables.Length; i++)
            {
                if (satelliteObjects.renderables[i] == null ||
                    satelliteObjects.renderables[i].invalid)
                    continue;
                if (!ChiselRenderObjects.IsValid(satelliteObjects.renderables[i]))
                    return false;
            }

            for (int i = 0; i < satelliteObjects.debugHelpers.Length; i++)
            {
                if (satelliteObjects.debugHelpers[i] == null)
                    continue;
                if (!ChiselRenderObjects.IsValid(satelliteObjects.debugHelpers[i]))
                    return false;
            }

            for (int i = 0; i < satelliteObjects.colliders.Length; i++)
            {
                if (!ChiselColliderObjects.IsValid(satelliteObjects.colliders[i]))
                    return false;
            }

            return true;
        }

        public bool HasLightmapUVs
        {
            get
            {
#if UNITY_EDITOR
                if (renderables == null)
                    return false;

                for (int i = 0; i < renderables.Length; i++)
                {
                    if (renderables[i] == null || 
                        renderables[i].invalid)
                        continue;
                    if (renderables[i].HasLightmapUVs)
                        return true;
                }
#endif
                return false;
            }
        }

        static bool[] meshUpdated = null;

        static List<ChiselColliderObjects> sColliderObjects = new List<ChiselColliderObjects>();

        public void Update(ChiselModel model, GameObject parentGameObject, VertexBufferContents vertexBufferContents)
        {
            Profiler.BeginSample("Setup");
            var parentTransform     = parentGameObject.transform;
            var gameObjectState     = GameObjectState.Create(parentGameObject);
            ChiselObjectUtility.UpdateContainerFlags(generatedDataContainer, gameObjectState);

            var containerTransform  = generatedDataContainer.transform;
            var colliderTransform   = colliderContainer.transform;

            // Make sure we're always a child of the model
            ChiselObjectUtility.ResetTransform(containerTransform, requiredParent: parentTransform);
            ChiselObjectUtility.ResetTransform(colliderTransform, requiredParent: containerTransform);
            ChiselObjectUtility.UpdateContainerFlags(colliderContainer, gameObjectState);

            for (int i = 0; i < renderables.Length; i++)
            {
                if (renderables[i] == null || renderables[i].invalid)
                    continue;
                var renderableContainer = renderables[i].container;
                ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState);
                ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
            }
            
            for (int i = 0; i < debugHelpers.Length; i++)
            {
                if (debugHelpers[i] == null || debugHelpers[i].invalid)
                    continue;
                var renderableContainer = debugHelpers[i].container;
                ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState, debugHelperRenderer: true);
                ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
            }
            Profiler.EndSample();

            Profiler.BeginSample("Update.Components");
            ref var meshDescriptions = ref vertexBufferContents.meshDescriptions;

            Debug.Assert(LayerParameterIndex.LayerParameter1 < LayerParameterIndex.LayerParameter2);
            Debug.Assert((LayerParameterIndex.LayerParameter1 + 1) == LayerParameterIndex.LayerParameter2);

            Debug.Assert(!meshDescriptions.IsCreated ||
                         meshDescriptions.Length == 0 ||
                         meshDescriptions[0].meshQuery.LayerParameterIndex >= LayerParameterIndex.None);

            // TODO: would love to use something like MeshDataArray here, but it seems to be impossible to use without stalling the pipeline

            // Loop through all meshDescriptions with LayerParameter1, and create renderable meshes from them
            if (!meshDescriptions.IsCreated || meshDescriptions.Length == 0)
            {
                Profiler.BeginSample("ClearAll");
                for (int renderIndex = 0; renderIndex < renderables.Length; renderIndex++)
                {
                    if (renderables[renderIndex].Valid)
                        renderables[renderIndex].Clear(model, gameObjectState);
                }

                for (int helperIndex = 0; helperIndex < debugHelpers.Length; helperIndex++)
                {
                    if (debugHelpers[helperIndex].Valid)
                        debugHelpers[helperIndex].Clear(model, gameObjectState);
                }

                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] != null)
                        colliders[j].Destroy();
                }
                Profiler.EndSample();
            } else
            {
                Profiler.BeginSample("meshUpdated");
                if (meshUpdated == null || meshUpdated.Length < debugHelpers.Length)
                    meshUpdated = new bool[debugHelpers.Length];
                Array.Clear(meshUpdated, 0, meshUpdated.Length);
                Profiler.EndSample();

                int colliderCount = 0;
                for (int i = 0; i < vertexBufferContents.subMeshSections.Length; i++)
                {
                    var subMeshSection = vertexBufferContents.subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.None)
                    {
                        int helperIndex = Array.IndexOf(kGeneratedDebugRendererFlags, (subMeshSection.meshQuery.LayerQuery, subMeshSection.meshQuery.LayerQueryMask));
                        //Debug.Log($"helperIndex (query: {subMeshSection.meshQuery.LayerQuery}, mask: {subMeshSection.meshQuery.LayerQueryMask})");

                        if (helperIndex == -1)
                        {
                            Debug.Assert(false, $"Invalid helper query used (query: {subMeshSection.meshQuery.LayerQuery}, mask: {subMeshSection.meshQuery.LayerQueryMask})");
                            continue;
                        }

                        // Group by all meshDescriptions with same query
                        Profiler.BeginSample("Update");
                        if (!debugHelpers[helperIndex].invalid)
                            debugHelpers[helperIndex].Update(model, gameObjectState, ref vertexBufferContents, i, materialOverride: ChiselMaterialManager.HelperMaterials[helperIndex]);
                        meshUpdated[helperIndex] = true;
                        Profiler.EndSample();
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                    {
                        var renderIndex = (int)(subMeshSection.meshQuery.LayerQuery & LayerUsageFlags.RenderReceiveCastShadows);
                        // Group by all meshDescriptions with same query
                        Profiler.BeginSample("Update");
                        renderables[renderIndex].Update(model, gameObjectState, ref vertexBufferContents, i);
                        Profiler.EndSample();
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                        colliderCount++;
                }

                Profiler.BeginSample("debugHelpers.Clear");
                for (int helperIndex = 0; helperIndex < debugHelpers.Length; helperIndex++)
                {
                    if (meshUpdated[helperIndex])
                        continue;
                    if (!debugHelpers[helperIndex].invalid)
                        debugHelpers[helperIndex].Clear(model, gameObjectState);
                }
                Profiler.EndSample();

                Profiler.BeginSample("sColliderObjects.Clear");
                sColliderObjects.Clear();
                if (sColliderObjects.Capacity < colliderCount)
                    sColliderObjects.Capacity = colliderCount;
                for (int i = 0; i < colliderCount; i++)
                    sColliderObjects.Add(null);
                Profiler.EndSample();

                Profiler.BeginSample("Update.Colliders");
                int colliderIndex = 0;
                for (int i = 0; i < vertexBufferContents.subMeshSections.Length; i++)
                {
                    var subMeshSection = vertexBufferContents.subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex != LayerParameterIndex.PhysicsMaterial)
                        continue;

                    var surfaceParameter = vertexBufferContents.meshDescriptions[subMeshSection.startIndex].surfaceParameter;

                    // TODO: optimize
                    for (int j = 0; j < colliders.Length; j++)
                    {
                        if (colliders[j] == null)
                            continue;
                        if (colliders[j].surfaceParameter != surfaceParameter)
                            continue;

                        sColliderObjects[colliderIndex] = colliders[j];
                        colliders[j] = null;
                        break;
                    }

                    Profiler.BeginSample("Create.Colliders");
                    if (sColliderObjects[colliderIndex] == null)
                        sColliderObjects[colliderIndex] = ChiselColliderObjects.Create(colliderContainer, surfaceParameter);
                    Profiler.EndSample();

                    Profiler.BeginSample("DoUpdate.Colliders");
                    sColliderObjects[colliderIndex].Update(model, ref vertexBufferContents, i);
                    Profiler.EndSample();
                    colliderIndex++;
                }
                Profiler.BeginSample("CleanUp.Colliders");
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] != null)
                        colliders[j].Destroy();
                }
                Profiler.EndSample();
                Profiler.BeginSample("Assign.Colliders");
                if (colliders.Length != sColliderObjects.Count)
                    colliders = new ChiselColliderObjects[sColliderObjects.Count];
                for (int i = 0; i < sColliderObjects.Count; i++)
                    colliders[i] = sColliderObjects[i];
                Profiler.EndSample();
                Profiler.EndSample();
            }
            Profiler.EndSample();

            Profiler.BeginSample("UpdateProperties");
            ChiselRenderObjects.UpdateProperties(model, meshRenderers);
            Profiler.EndSample();
            Profiler.BeginSample("UpdateColliders");
            ChiselColliderObjects.UpdateColliders(model, colliders);
            Profiler.EndSample();
            needVisibilityMeshUpdate = true;
        }

#if UNITY_EDITOR
        public void RemoveHelperSurfaces()
        {
            for (int i = 0; i < renderables.Length; i++)
            {
                var renderable = renderables[i];
                if (renderable == null ||
                    renderable.invalid ||
                    !renderable.meshRenderer)
                {
                    if (renderable.container)
                        UnityEngine.Object.DestroyImmediate(renderable.container);
                    continue;
                }

                renderable.meshRenderer.forceRenderingOff = false;
            }

            for (int i = 0; i < debugHelpers.Length; i++)
            {
                if (debugHelpers[i].container)
                    UnityEngine.Object.DestroyImmediate(debugHelpers[i].container);
            }
        }

        public void UpdateHelperSurfaceState(DrawModeFlags helperStateFlags, bool ignoreBrushVisibility = false)
        {
            if (!ignoreBrushVisibility)
                ChiselGeneratedComponentManager.UpdateVisibility();
            
            var shouldHideMesh  = !ignoreBrushVisibility &&
                                  visibilityState != VisibilityState.AllVisible &&
                                  visibilityState != VisibilityState.Unknown;
                                  
            var showRenderables = (helperStateFlags & DrawModeFlags.HideRenderables) == DrawModeFlags.None;
            for (int i = 0; i < renderables.Length; i++)
            {
                var renderable = renderables[i];
                if (renderable == null ||
                    renderable.invalid)
                    continue;

                if (renderable.meshRenderer != null)
                    renderable.meshRenderer.forceRenderingOff = shouldHideMesh || !showRenderables;
            }

            for (int i = 0; i < debugHelpers.Length; i++)
            {
                var showState    = (helperStateFlags & kGeneratedDebugShowFlags[i]) != DrawModeFlags.None;
                if (debugHelpers[i].meshRenderer != null)
                    debugHelpers[i].meshRenderer.forceRenderingOff = shouldHideMesh || !showState;
            }

            if (ignoreBrushVisibility || !needVisibilityMeshUpdate)
                return;

            if (visibilityState == VisibilityState.Mixed)
            {
                for (int i = 0; i < renderables.Length; i++)
                {
                    var renderable = renderables[i];
                    if (renderable == null ||
                        renderable.invalid)
                        continue;

                    renderable.UpdateVisibilityMesh(showRenderables);
                }

                for (int i = 0; i < debugHelpers.Length; i++)
                {
                    var show = (helperStateFlags & kGeneratedDebugShowFlags[i]) != DrawModeFlags.None;
                    var debugHelper = debugHelpers[i];
                    if (debugHelper == null)
                        continue;

                    debugHelper.UpdateVisibilityMesh(show);
                }
            }

            needVisibilityMeshUpdate = false;
        }
#endif
    }
}