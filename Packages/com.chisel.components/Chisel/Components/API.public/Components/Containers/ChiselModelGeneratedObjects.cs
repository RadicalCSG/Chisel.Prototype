using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using System.Transactions;

namespace Chisel.Components
{        
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
    public class ChiselModelGeneratedObjects
    {
        public const string kGeneratedContainerName     = "�[generated]�";
        public static readonly string[] kGeneratedMeshRendererNames = new string[]
        {
            null,                                                   // 0 (invalid option)
            "�[generated-Renderable]�",                             // 1
            "�[generated-CastShadows]�",                            // 2
            "�[generated-Renderable|CastShadows]�",                 // 3
            null,                                                   // 4 (invalid option)
            "�[generated-Renderable|ReceiveShadows]�",              // 5
            null,                                                   // 6 (invalid option)
            "�[generated-Renderable|CastShadows|ReceiveShadows]�"   // 7
        };
        public const string kGeneratedMeshColliderName	= "�[generated-Collider]�";

        public GameObject               generatedDataContainer;
        public GameObject               colliderContainer;
        public ChiselColliderObjects[]  colliders;
        public ChiselRenderObjects[]    renderables;
        public MeshRenderer[]           meshRenderers;
        public readonly List<Material>  renderMaterials             = new List<Material>();
        public VisibilityState          visibilityState             = VisibilityState.Unknown;
        public bool                     needVisibilityMeshUpdate    = false;

        private ChiselModelGeneratedObjects() { }

        public static ChiselModelGeneratedObjects Create(ChiselModel model)
        {
            // Make sure there's not a dangling container out there from a previous version
            var existingContainer = model.FindChildByName(kGeneratedContainerName);
            ChiselObjectUtility.SafeDestroy(existingContainer, ignoreHierarchyEvents: true);

            var modelState          = GameObjectState.Create(model);
            var parent              = model.transform;
            var container           = ChiselObjectUtility.CreateGameObject(kGeneratedContainerName, parent, modelState);
            var containerTransform  = container.transform;
            var colliderContainer   = ChiselObjectUtility.CreateGameObject(kGeneratedMeshColliderName, containerTransform, modelState);

            Debug.Assert((int)LayerUsageFlags.Renderable     == 1);
            Debug.Assert((int)LayerUsageFlags.CastShadows    == 2);
            Debug.Assert((int)LayerUsageFlags.ReceiveShadows == 4);
            Debug.Assert((int)LayerUsageFlags.RenderReceiveCastShadows == (1|2|4));

            var renderables = new ChiselRenderObjects[]
            {
                new ChiselRenderObjects() { invalid = true },
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[1], containerTransform, modelState, LayerUsageFlags.Renderable                               ),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[2], containerTransform, modelState,                              LayerUsageFlags.CastShadows ),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[3], containerTransform, modelState, LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows ),
                new ChiselRenderObjects() { invalid = true },
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[5], containerTransform, modelState, LayerUsageFlags.Renderable |                               LayerUsageFlags.ReceiveShadows),
                new ChiselRenderObjects() { invalid = true },
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[7], containerTransform, modelState, LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows | LayerUsageFlags.ReceiveShadows),
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

            var result = new ChiselModelGeneratedObjects
            {
                generatedDataContainer  = container,
                colliderContainer       = colliderContainer,
                colliders               = new ChiselColliderObjects[0],
                renderables             = renderables,
                meshRenderers           = meshRenderers
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
            ChiselObjectUtility.SafeDestroy(colliderContainer, ignoreHierarchyEvents: true);
            ChiselObjectUtility.SafeDestroy(generatedDataContainer, ignoreHierarchyEvents: true);
            generatedDataContainer  = null;
            colliderContainer       = null;
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
            ChiselObjectUtility.RemoveContainerFlags(colliderContainer);
            ChiselObjectUtility.RemoveContainerFlags(generatedDataContainer);
        }

        public static bool IsValid(ChiselModelGeneratedObjects satelliteObjects)
        {
            if (satelliteObjects == null)
                return false;

            if (!satelliteObjects.generatedDataContainer ||
                !satelliteObjects.colliderContainer ||
                satelliteObjects.colliders == null ||   // must be an array, even if 0 length
                satelliteObjects.renderables == null ||
                satelliteObjects.renderables.Length != 8 ||
                satelliteObjects.meshRenderers == null ||
                satelliteObjects.meshRenderers.Length != 5)
                return false;

            // These queries are valid, and should never be null (We don't care about the other queries)
            if (satelliteObjects.renderables[1] == null ||
                satelliteObjects.renderables[2] == null ||
                satelliteObjects.renderables[3] == null ||
                satelliteObjects.renderables[5] == null ||
                satelliteObjects.renderables[7] == null)
                return false;

            satelliteObjects.renderables[0].invalid = true;
            satelliteObjects.renderables[1].invalid = false;
            satelliteObjects.renderables[2].invalid = false;
            satelliteObjects.renderables[3].invalid = false;
            satelliteObjects.renderables[4].invalid = true;
            satelliteObjects.renderables[5].invalid = false;
            satelliteObjects.renderables[6].invalid = true;
            satelliteObjects.renderables[7].invalid = false;

            for (int i = 0; i < satelliteObjects.renderables.Length; i++)
            {
                if (satelliteObjects.renderables[i] == null ||
                    satelliteObjects.renderables[i].invalid)
                    continue;
                if (!ChiselRenderObjects.IsValid(satelliteObjects.renderables[i]))
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

        public void Update(ChiselModel model, GeneratedMeshDescription[] meshDescriptions)
        {
            var modelState = GameObjectState.Create(model);
            ChiselObjectUtility.UpdateContainerFlags(generatedDataContainer, modelState);

            var modelTransform      = model.transform;
            var containerTransform  = generatedDataContainer.transform;
            var colliderTransform   = colliderContainer.transform;

            // Make sure we're always a child of the model
            ChiselObjectUtility.ResetTransform(containerTransform, requiredParent: modelTransform);
            ChiselObjectUtility.ResetTransform(colliderTransform, requiredParent: containerTransform);
            ChiselObjectUtility.UpdateContainerFlags(colliderContainer, modelState);

            for (int i = 0; i < renderables.Length; i++)
            {
                if (renderables[i] == null || renderables[i].invalid)
                    continue;
                var renderableContainer = renderables[i].container;
                ChiselObjectUtility.UpdateContainerFlags(renderableContainer, modelState);
                ChiselObjectUtility.ResetTransform(renderableContainer.transform, requiredParent: containerTransform);
            }
                        
            Debug.Assert(LayerParameterIndex.LayerParameter1 < LayerParameterIndex.LayerParameter2);
            Debug.Assert((LayerParameterIndex.LayerParameter1 + 1) == LayerParameterIndex.LayerParameter2);
            Debug.Assert(meshDescriptions[0].meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1);

            int descriptionIndex = 0;

            ChiselRenderObjects.UpdateProperties(model, meshRenderers);
            ChiselColliderObjects.UpdateColliders(model, colliders);

            renderMaterials.Clear();

            // Loop through all meshDescriptions with LayerParameter1, and create renderable meshes from them
            if (meshDescriptions[0].meshQuery.LayerParameterIndex == LayerParameterIndex.LayerParameter1)
            {
                var prevQuery   = meshDescriptions[0].meshQuery;
                var startIndex  = 0;
                for (; descriptionIndex < meshDescriptions.Length; descriptionIndex++)
                {
                    ref var meshDescriptionIterator = ref meshDescriptions[descriptionIndex];
                    // Exit when layerParameterIndex is no longer LayerParameter1
                    if (meshDescriptionIterator.meshQuery.LayerParameterIndex != LayerParameterIndex.LayerParameter1)
                        break;

                    var currQuery = meshDescriptionIterator.meshQuery;
                    if (prevQuery == currQuery)
                        continue;

                    prevQuery = currQuery;
                    var renderIndex = (int)(prevQuery.LayerQueryMask & LayerUsageFlags.RenderReceiveCastShadows);

                    // Group by all meshDescriptions with same query
                    renderables[renderIndex].Update(model, modelState, meshDescriptions, startIndex, descriptionIndex);
                    renderMaterials.AddRange(renderables[renderIndex].renderMaterials);
                    startIndex = descriptionIndex;
                }

                {
                    var renderIndex = (int)(prevQuery.LayerQueryMask & LayerUsageFlags.RenderReceiveCastShadows);

                    // Group by all meshDescriptions with same query
                    renderables[renderIndex].Update(model, modelState, meshDescriptions, startIndex, descriptionIndex);
                    renderMaterials.AddRange(renderables[renderIndex].renderMaterials);
                }
            }
            
            if (descriptionIndex < meshDescriptions.Length &&
                meshDescriptions[descriptionIndex].meshQuery.LayerParameterIndex == LayerParameterIndex.LayerParameter2)
            {
                Debug.Assert(meshDescriptions[meshDescriptions.Length - 1].meshQuery.LayerParameterIndex == LayerParameterIndex.LayerParameter2);

                var colliderCount = meshDescriptions.Length - descriptionIndex;
                bool rebuild = true;
                if (colliderCount == colliders.Length)
                {
                    rebuild = false;
                    for (int i = 0; descriptionIndex < meshDescriptions.Length; descriptionIndex++, i++)
                    {
                        ref var meshDescription = ref meshDescriptions[descriptionIndex];
                        // Exit when layerParameterIndex is no longer LayerParameter2
                        if (meshDescription.meshQuery.LayerParameterIndex != LayerParameterIndex.LayerParameter2)
                            break;

                        if (colliders[i].surfaceParameter != meshDescription.surfaceParameter ||
                            colliders[i].geometryHashValue != meshDescription.geometryHashValue)
                        {
                            rebuild = true;
                            break;
                        }
                    }
                }
                if (rebuild)
                {
                    var newColliders = new ChiselColliderObjects[colliderCount];
                    var oldDescriptionIndex = descriptionIndex;
                    for (int i = 0; descriptionIndex < meshDescriptions.Length; descriptionIndex++, i++)
                    {
                        ref var meshDescription = ref meshDescriptions[descriptionIndex];
                        // Exit when layerParameterIndex is no longer LayerParameter2
                        if (meshDescription.meshQuery.LayerParameterIndex != LayerParameterIndex.LayerParameter2)
                            break;

                        // TODO: optimize
                        for (int j = 0; j < colliders.Length; j++)
                        {
                            if (colliders[j] == null)
                                continue;
                            if (colliders[j].surfaceParameter != meshDescription.surfaceParameter)
                                continue;
                                
                            newColliders[i] = colliders[j];
                            colliders[j] = null;
                            break;
                        }         
                        if (newColliders[i] == null)
                            newColliders[i] = ChiselColliderObjects.Create(colliderContainer, meshDescription.surfaceParameter);
                    }
                    for (int j = 0; j < colliders.Length; j++)
                    {
                        if (colliders[j] != null)
                            colliders[j].Destroy();
                    }
                    colliders = newColliders;
                    descriptionIndex = oldDescriptionIndex;
                }
                // Loop through all meshDescriptions with LayerParameter2, and create collider meshes from them
                for (int i = 0; descriptionIndex < meshDescriptions.Length; descriptionIndex++, i++)
                {
                    ref var meshDescription = ref meshDescriptions[descriptionIndex];

                    // Exit when layerParameterIndex is no longer LayerParameter2
                    if (meshDescription.meshQuery.LayerParameterIndex != LayerParameterIndex.LayerParameter2)
                        break;

                    colliders[i].Update(model, meshDescription);
                }
            }
            
            Debug.Assert(descriptionIndex == meshDescriptions.Length);
        }

#if UNITY_EDITOR
        public void UpdateVisibilityMeshes()
        {
            if (!needVisibilityMeshUpdate)
                return;
            
            var shouldHideMesh  = visibilityState != VisibilityState.AllVisible &&
                                  visibilityState != VisibilityState.Unknown;
            for (int i = 0; i < renderables.Length; i++)
            {
                var renderable = renderables[i];
                if (renderable == null ||
                    renderable.invalid)
                    continue;

                if (renderable.meshRenderer)
                    renderable.meshRenderer.forceRenderingOff = shouldHideMesh;
            }

            if (visibilityState == VisibilityState.Mixed)
            {
                for (int i = 0; i < renderables.Length; i++)
                {
                    var renderable = renderables[i];
                    if (renderable == null ||
                        renderable.invalid)
                        continue;

                    renderable.UpdateVisibilityMesh();
                }
            }

            needVisibilityMeshUpdate = false;
        }
#endif
    }
}