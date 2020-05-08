using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using System.Transactions;

namespace Chisel.Components
{        
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
                null,//ChiselRenderObjects Create(kGeneratedMeshRendererNames[0], containerTransform, modelState, LayerUsageFlags.None),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[1], containerTransform, modelState, LayerUsageFlags.Renderable                               ),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[2], containerTransform, modelState,                              LayerUsageFlags.CastShadows ),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[3], containerTransform, modelState, LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows ),
                null,//ChiselRenderObjects.Create(kGeneratedMeshRendererNames[4], containerTransform, modelState,                                                     LayerUsageFlags.ReceiveShadows),
                ChiselRenderObjects.Create(kGeneratedMeshRendererNames[5], containerTransform, modelState, LayerUsageFlags.Renderable |                               LayerUsageFlags.ReceiveShadows),
                null,//ChiselRenderObjects.Create(kGeneratedMeshRendererNames[6], containerTransform, modelState,                       LayerUsageFlags.CastShadows | LayerUsageFlags.ReceiveShadows),
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

            return new ChiselModelGeneratedObjects
            {
                generatedDataContainer  = container,
                colliderContainer       = colliderContainer,
                colliders               = new ChiselColliderObjects[0],
                renderables             = renderables,
                meshRenderers           = meshRenderers
            };
        }

        public void Destroy()
        {
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

                // These queries are invalid, and should always be null
                satelliteObjects.renderables[0] != null ||
                satelliteObjects.renderables[4] != null ||
                satelliteObjects.renderables[6] != null ||

                // These queries are valid, and should never be null
                satelliteObjects.renderables[1] == null ||
                satelliteObjects.renderables[2] == null ||
                satelliteObjects.renderables[3] == null ||
                satelliteObjects.renderables[5] == null ||
                satelliteObjects.renderables[7] == null)
                return false;

            for (int i = 0; i < satelliteObjects.renderables.Length; i++)
            {
                if (satelliteObjects.renderables[i] == null)
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
                for (int i = 0; i < renderables.Length; i++)
                {
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
                if (renderables[i] == null)
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

                        if (colliders[i].surfaceParameter != meshDescription.surfaceParameter)
                        {
                            rebuild = true;
                            break;
                        }
                    }
                }
                if (rebuild)
                {
                    var newColliders = new ChiselColliderObjects[colliderCount];
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


        public void UpdateVisibilityMeshes()
        {
            if (!needVisibilityMeshUpdate)
                return;
            
            var shouldHideMesh  = visibilityState != VisibilityState.AllVisible &&
                                  visibilityState != VisibilityState.Unknown;
            for (int i = 0; i < renderables.Length; i++)
            {
                var renderable = renderables[i];
                if (renderable == null)
                    continue;

                if (renderable.meshRenderer)
                    renderable.meshRenderer.forceRenderingOff = shouldHideMesh;
            }

            if (visibilityState == VisibilityState.Mixed)
            {
                for (int i = 0; i < renderables.Length; i++)
                {
                    var renderable = renderables[i];
                    if (renderable == null)
                        continue;

                    renderable.UpdateVisibilityMesh();
                }
            }

            needVisibilityMeshUpdate = false;
        }
    }
}