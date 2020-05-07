using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using LightProbeUsage = UnityEngine.Rendering.LightProbeUsage;
using ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage;
using UnityEngine.Rendering;

namespace Chisel.Components
{
    [Serializable]
    public class ChiselRenderObjects
    {
        public LayerUsageFlags  query;
        public GameObject       container;
        public Mesh             sharedMesh;
        public MeshFilter       meshFilter;
        public MeshRenderer     meshRenderer;
        public Material[]       renderMaterials;
        [NonSerialized] public float uvLightmapUpdateTime;

        private ChiselRenderObjects() { }
        public static ChiselRenderObjects Create(string name, Transform parent, GameObjectState state, LayerUsageFlags query)
        {
            var renderContainer = ChiselObjectUtility.CreateGameObject(name, parent, state);
            var sharedMesh      = new Mesh { name = name };
            var meshFilter      = renderContainer.AddComponent<MeshFilter>();
            var meshRenderer    = renderContainer.AddComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            var renderObjects = new ChiselRenderObjects
            {
                query           = query,
                container       = renderContainer,
                meshFilter      = meshFilter,
                meshRenderer    = meshRenderer,
                sharedMesh      = sharedMesh,
                renderMaterials = new Material[0]
            };
            renderObjects.Initialize();
            return renderObjects;
        }

        public void Destroy()
        {
            ChiselObjectUtility.SafeDestroy(container, ignoreHierarchyEvents: true);
            ChiselObjectUtility.SafeDestroy(sharedMesh);
            container       = null;
            sharedMesh      = null;
            meshFilter      = null;
            meshRenderer    = null;
            renderMaterials = null;
        }

        public void RemoveContainerFlags()
        {
            ChiselObjectUtility.RemoveContainerFlags(meshFilter);
            ChiselObjectUtility.RemoveContainerFlags(meshRenderer);
            ChiselObjectUtility.RemoveContainerFlags(container);
        }

        public static bool IsValid(ChiselRenderObjects renderObjects)
        {
            if (renderObjects == null)
                return false;

            if (!renderObjects.container  ||
                !renderObjects.sharedMesh ||
                !renderObjects.meshFilter ||
                !renderObjects.meshRenderer)
                return false;

            return true;
        }

        public bool HasLightmapUVs
        {
            get
            {
#if UNITY_EDITOR
                // Avoid light mapping multiple times, when the same mesh is used on multiple MeshRenderers
                if (!ChiselGeneratedComponentManager.HasLightmapUVs(sharedMesh))
                    return true;
#endif
                return false;
            }
        }

        void Initialize()
        {
            meshFilter.sharedMesh       = sharedMesh;
            meshRenderer.receiveShadows	= ((query & LayerUsageFlags.ReceiveShadows) == LayerUsageFlags.ReceiveShadows);
            switch (query & (LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows))
            {
                case LayerUsageFlags.None:				meshRenderer.enabled = false; break;
                case LayerUsageFlags.Renderable:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;			break;
                case LayerUsageFlags.CastShadows:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;	break;
                case LayerUsageFlags.RenderCastShadows:	meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;			break;
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetSelectedRenderState(meshRenderer, UnityEditor.EditorSelectedRenderState.Hidden);
#endif
        }



        void UpdateSettings(ChiselModel model, GameObjectState state, bool meshIsModified)
        {
#if UNITY_EDITOR
            // If we need to render partial meshes (where some brushes are hidden) then we should show the full mesh
            ChiselGeneratedComponentManager.CheckIfFullMeshNeedsToBeHidden(model, this);
            if (meshIsModified)
                ChiselGeneratedComponentManager.ClearLightmapData(state, this);
#endif
        }

        public static void UpdateProperties(ChiselModel model, MeshRenderer[] meshRenderers)
        {
            if (meshRenderers == null || meshRenderers.Length == 0)
                return;
            
            var renderSettings = model.RenderSettings;
#if UNITY_EDITOR
            // Warning: calling new UnityEditor.SerializedObject with an empty array crashes Unity
            using (var serializedObject = new UnityEditor.SerializedObject(meshRenderers))
            { 
                serializedObject.SetPropertyValue("m_ImportantGI",                      renderSettings.importantGI);
                serializedObject.SetPropertyValue("m_PreserveUVs",                      renderSettings.optimizeUVs);
                serializedObject.SetPropertyValue("m_IgnoreNormalsForChartDetection",   renderSettings.ignoreNormalsForChartDetection);
                serializedObject.SetPropertyValue("m_AutoUVMaxDistance",                renderSettings.autoUVMaxDistance);
                serializedObject.SetPropertyValue("m_AutoUVMaxAngle",                   renderSettings.autoUVMaxAngle);
                serializedObject.SetPropertyValue("m_MinimumChartSize",                 renderSettings.minimumChartSize);
            }
#endif

            for(int i = 0; i < meshRenderers.Length; i++)
            {
                var meshRenderer = meshRenderers[i];
                meshRenderer.lightProbeProxyVolumeOverride	= renderSettings.lightProbeProxyVolumeOverride;
                meshRenderer.probeAnchor					= renderSettings.probeAnchor;
                meshRenderer.motionVectorGenerationMode		= renderSettings.motionVectorGenerationMode;
                meshRenderer.reflectionProbeUsage			= renderSettings.reflectionProbeUsage;
                meshRenderer.lightProbeUsage				= renderSettings.lightProbeUsage;
                meshRenderer.allowOcclusionWhenDynamic		= renderSettings.allowOcclusionWhenDynamic;
                meshRenderer.renderingLayerMask				= renderSettings.renderingLayerMask;
                meshRenderer.stitchLightmapSeams            = renderSettings.stitchLightmapSeams;
                meshRenderer.scaleInLightmap                = renderSettings.scaleInLightmap;
                meshRenderer.receiveGI                      = renderSettings.receiveGI;
            }
        }


        static readonly List<Material>              __foundMaterials    = new List<Material>(); // static to avoid allocations
        static readonly List<GeneratedMeshContents> __foundContents     = new List<GeneratedMeshContents>(); // static to avoid allocations
        public void Update(ChiselModel model, GeneratedMeshDescription[] meshDescriptions, int startIndex, int endIndex)
        {
            // Retrieve the generatedMeshes and its materials, combine them into a single Unity Mesh/Material array
            try
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    ref var meshDescription = ref meshDescriptions[i];
                    var generatedMeshContents = model.Node.GetGeneratedMesh(meshDescription);
                    if (generatedMeshContents == null)
                        continue;
                    if (generatedMeshContents.indices.Length == 0)
                    {
                        generatedMeshContents.Dispose();
                        continue;
                    }
                    var renderMaterial = ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(meshDescription.surfaceParameter);
                    __foundContents.Add(generatedMeshContents);
                    __foundMaterials.Add(renderMaterial);
                }
                if (__foundContents.Count == 0)
                {
                    if (sharedMesh.vertexCount > 0) sharedMesh.Clear(keepVertexLayout: true);
                } else
                {
                    sharedMesh.CopyFrom(__foundContents);
                    ChiselGeneratedComponentManager.SetHasLightmapUVs(sharedMesh, false);
                }
                if (renderMaterials != null && 
                    renderMaterials.Length == __foundMaterials.Count)
                {
                    __foundMaterials.CopyTo(renderMaterials);
                } else
                    renderMaterials = __foundMaterials.ToArray();
                if (meshFilter.sharedMesh != sharedMesh)
                    meshFilter.sharedMesh = sharedMesh;
                meshRenderer.sharedMaterials = renderMaterials;
                meshRenderer.enabled = sharedMesh.vertexCount > 0; 
            }
            finally
            {
                for (int i = 0; i < __foundContents.Count; i++)
                    __foundContents[i].Dispose();
                __foundContents.Clear();
                __foundMaterials.Clear();
            }
        }
    }
}