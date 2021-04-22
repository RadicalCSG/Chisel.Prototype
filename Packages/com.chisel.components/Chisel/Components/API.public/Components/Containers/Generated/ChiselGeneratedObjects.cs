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
using Unity.Burst;
using UnityEditor;
using System.Runtime.InteropServices;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;

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
                debugHelpers[i] = ChiselRenderObjects.Create(kGeneratedDebugRendererNames[i], containerTransform, gameObjectState, AssignMeshesJob.kGeneratedDebugRendererFlags[i].Item1, debugHelperRenderer: true);
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

        
        readonly Dictionary<ChiselModel, GameObjectState>   gameObjectStates        = new Dictionary<ChiselModel, GameObjectState>();
        readonly List<ChiselColliderObjectUpdate>           colliderObjectUpdates   = new List<ChiselColliderObjectUpdate>();
        readonly List<ChiselMeshUpdate>                     renderMeshUpdates       = new List<ChiselMeshUpdate>();
        readonly List<ChiselRenderObjectUpdate>             renderObjectUpdates     = new List<ChiselRenderObjectUpdate>();
        readonly List<ChiselColliderObjects>                colliderObjects         = new List<ChiselColliderObjects>();
        readonly List<Mesh>                                 foundMeshes             = new List<Mesh>();

        // in between UpdateMeshes and FinishMeshUpdates our jobs should be force completed, so we can now upload our meshes to unity Meshes

        public int FinishMeshUpdates(ChiselModel model, GameObject  parentGameObject, 
                                     Mesh.MeshDataArray             meshDataArray, 
                                     ref VertexBufferContents       vertexBufferContents, 
                                     NativeList<ChiselMeshUpdate>   colliderMeshUpdates,
                                     NativeList<ChiselMeshUpdate>   debugHelperMeshes,
                                     NativeList<ChiselMeshUpdate>   renderMeshes,
                                     JobHandle dependencies)
        {
            gameObjectStates.Clear();
            colliderObjectUpdates.Clear();
            renderMeshUpdates.Clear();
            renderObjectUpdates.Clear();
            colliderObjects.Clear();
            foundMeshes.Clear();

            GameObjectState gameObjectState;
            { 
                Profiler.BeginSample("Setup");
                var parentTransform     = parentGameObject.transform;
                gameObjectState         = GameObjectState.Create(parentGameObject);
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

                    bool isRenderable = (renderables[i].query & LayerUsageFlags.Renderable) == LayerUsageFlags.Renderable;
                    var renderableContainer = renderables[i].container;
                    ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState, isRenderable: isRenderable);
                    ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
                }
            
                for (int i = 0; i < debugHelpers.Length; i++)
                {
                    if (debugHelpers[i] == null || debugHelpers[i].invalid)
                        continue;
                    var renderableContainer = debugHelpers[i].container;
                    ChiselObjectUtility.UpdateContainerFlags(renderableContainer, gameObjectState, isRenderable: true, debugHelperRenderer: true);
                    ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
                }
                gameObjectStates.Add(model, gameObjectState);
                Profiler.EndSample();
            }

            Debug.Assert(LayerParameterIndex.LayerParameter1 < LayerParameterIndex.LayerParameter2);
            Debug.Assert((LayerParameterIndex.LayerParameter1 + 1) == LayerParameterIndex.LayerParameter2);

            dependencies.Complete();

            Debug.Assert(!vertexBufferContents.meshDescriptions.IsCreated ||
                         vertexBufferContents.meshDescriptions.Length == 0 ||
                         vertexBufferContents.meshDescriptions[0].meshQuery.LayerParameterIndex >= LayerParameterIndex.None);


            Profiler.BeginSample("Init");
            var colliderCount = colliderMeshUpdates.Length;
            if (colliderObjects.Capacity < colliderCount)
                colliderObjects.Capacity = colliderCount;
            for (int i = 0; i < colliderCount; i++)
                colliderObjects.Add(null);

            for (int i = 0; i < renderMeshes.Length; i++)
                renderMeshUpdates.Add(renderMeshes[i]);

            for (int i = 0; i < debugHelperMeshes.Length; i++)
                renderMeshUpdates.Add(debugHelperMeshes[i]);
            renderMeshUpdates.Sort(delegate (ChiselMeshUpdate x, ChiselMeshUpdate y)
            {
                return x.contentsIndex - y.contentsIndex;
            });
            Profiler.EndSample();



            // Now do all kinds of book-keeping code that we might as well do while our jobs are running on other threads
            Profiler.BeginSample("new_ChiselRenderObjectUpdate");

            var usedDebugHelpers = new HashSet<int>();
            for (int i = 0; i < debugHelperMeshes.Length; i++)
            {
                var debugHelperMeshUpdate   = debugHelperMeshes[i];
                usedDebugHelpers.Add(debugHelperMeshUpdate.objectIndex);
                var instance                = debugHelpers[debugHelperMeshUpdate.objectIndex];
                foundMeshes.Add(instance.sharedMesh);
                renderObjectUpdates.Add(new ChiselRenderObjectUpdate
                {
                    meshIndex           = debugHelperMeshUpdate.meshIndex,
                    materialOverride    = ChiselMaterialManager.HelperMaterials[debugHelperMeshUpdate.objectIndex],
                    instance            = instance,
                    model               = model
                });
            }
            Profiler.EndSample();

            Profiler.BeginSample("new_ChiselRenderObjectUpdate");
            var usedRenderMeshes = new HashSet<int>();
            for (int i = 0; i < renderMeshes.Length; i++)
            {
                var renderMeshUpdate    = renderMeshes[i];
                usedRenderMeshes.Add(renderMeshUpdate.objectIndex);
                var instance            = renderables[renderMeshUpdate.objectIndex];
                foundMeshes.Add(instance.sharedMesh);
                renderObjectUpdates.Add(new ChiselRenderObjectUpdate
                {
                    meshIndex           = renderMeshUpdate.meshIndex,
                    materialOverride    = null,
                    instance            = instance,
                    model               = model
                });
            }
            Profiler.EndSample();

            Profiler.BeginSample("new_ChiselPhysicsObjectUpdate");
            for (int i = 0; i < colliderMeshUpdates.Length; i++)
            {
                var colliderMeshUpdate  = colliderMeshUpdates[i];

                var surfaceParameter    = colliderMeshUpdate.objectIndex;
                var colliderIndex       = colliderMeshUpdate.contentsIndex;

                // TODO: optimize
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] == null)
                        continue;
                    if (colliders[j].surfaceParameter != surfaceParameter)
                        continue;

                    colliderObjects[colliderIndex] = colliders[j];
                    colliders[j] = null;
                    break;
                }

                Profiler.BeginSample("Create.Colliders");
                if (colliderObjects[colliderIndex] == null)
                    colliderObjects[colliderIndex] = ChiselColliderObjects.Create(colliderContainer, surfaceParameter);
                Profiler.EndSample();

                var instance            = colliderObjects[colliderIndex];
                foundMeshes.Add(instance.sharedMesh);
                colliderObjectUpdates.Add(new ChiselColliderObjectUpdate
                {
                    meshIndex   = colliderMeshUpdate.meshIndex
                });
            }
            Profiler.EndSample();

            Profiler.BeginSample("Renderers.UpdateMaterials");
            ChiselRenderObjects.UpdateMaterials(renderMeshUpdates, renderObjectUpdates, ref vertexBufferContents);
            Profiler.EndSample();


            Profiler.BeginSample("CleanUp.Colliders");
            for (int j = 0; j < colliders.Length; j++)
            {
                if (colliders[j] != null)
                    colliders[j].Destroy();
            }
            Profiler.EndSample();

            Profiler.BeginSample("Assign.Colliders");
            if (colliders.Length != colliderCount)
                colliders = new ChiselColliderObjects[colliderCount];
            for (int i = 0; i < colliderCount; i++)
                colliders[i] = colliderObjects[i];
            Profiler.EndSample();

            Profiler.BeginSample("Renderers.Update");
            ChiselRenderObjects.UpdateSettings(this.renderMeshUpdates, this.renderObjectUpdates, this.gameObjectStates, ref vertexBufferContents);
            Profiler.EndSample();

            Debug.Assert(foundMeshes.Count <= meshDataArray.Length);

            var realMeshDataArraySize = meshDataArray.Length;
            {
                // This is a hack to ensure foundMeshes is the same exact length as meshDataArray
                // (All these need to be set to empty anyway)

                // TODO: figure out why the maximum meshDataArray.Length does not match the maximum used meshes?

                int meshDataArrayOffset = foundMeshes.Count;
                for (int i = 0; foundMeshes.Count < meshDataArray.Length && i < renderables.Length; i++)
                {
                    if (usedRenderMeshes.Contains(i))
                        continue;

                    var instance = renderables[i];
                    var sharedMesh = instance.sharedMesh;
                    if (!sharedMesh || foundMeshes.Contains(sharedMesh))
                        continue;
                    foundMeshes.Add(sharedMesh);
                    meshDataArray[meshDataArrayOffset].SetIndexBufferParams(0, IndexFormat.UInt32);
                    meshDataArray[meshDataArrayOffset].SetVertexBufferParams(0, VertexBufferContents.s_RenderDescriptors);
                    meshDataArrayOffset++;
                }

                for (int i = 0; foundMeshes.Count < meshDataArray.Length && i < debugHelpers.Length; i++)
                {
                    if (usedDebugHelpers.Contains(i))
                        continue;

                    var instance = debugHelpers[i];
                    var sharedMesh = instance.sharedMesh;
                    if (!sharedMesh || foundMeshes.Contains(sharedMesh))
                        continue;

                    foundMeshes.Add(sharedMesh);
                    meshDataArray[meshDataArrayOffset].SetIndexBufferParams(0, IndexFormat.UInt32);
                    meshDataArray[meshDataArrayOffset].SetVertexBufferParams(0, VertexBufferContents.s_RenderDescriptors);
                    meshDataArrayOffset++;
                }
            }

            Profiler.BeginSample("ApplyAndDisposeWritableMeshData");
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, foundMeshes,
                                                 UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);

            // Unfortunately the MeshData API is a big bag of buggy bullshit and it doesn't actually update the bounds (no matter what flags you use)
            for (int i = 0; i < realMeshDataArraySize; i++)
                foundMeshes[i].RecalculateBounds();
            Profiler.EndSample();

            Profiler.BeginSample("UpdateProperties");
            ChiselRenderObjects.UpdateProperties(model, this.meshRenderers);
            Profiler.EndSample();

            Profiler.BeginSample("UpdateColliders");
            ChiselColliderObjects.UpdateProperties(model, this.colliders);
            Profiler.EndSample();

            this.needVisibilityMeshUpdate = true;
            this.gameObjectStates.Clear();
            this.renderMeshUpdates.Clear();
            this.renderObjectUpdates.Clear();
            this.colliderObjects.Clear();
            
            var foundMeshCount = foundMeshes.Count;
            foundMeshes.Clear();
            return foundMeshCount;
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