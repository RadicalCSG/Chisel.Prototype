using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using LightProbeUsage = UnityEngine.Rendering.LightProbeUsage;
using ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;

namespace Chisel.Components
{
    [Serializable]
    public class ChiselRenderObjects
    {
        [HideInInspector] [SerializeField] internal bool invalid = false;
        public bool Valid 
        { 
            get
            {
                return (this != null) && !invalid;
            }
        }
        public LayerUsageFlags  query;
        public GameObject       container;
        public Mesh             sharedMesh;
#if UNITY_EDITOR
        public Mesh             partialMesh;
#endif
        public MeshFilter       meshFilter;
        public MeshRenderer     meshRenderer;
        public Material[]       renderMaterials;
        public readonly List<int> triangleBrushes = new List<int>();
        
        public ulong            geometryHashValue;
        public ulong            surfaceHashValue;

        [NonSerialized] public float uvLightmapUpdateTime;

        internal ChiselRenderObjects() { }
        public static ChiselRenderObjects Create(string name, Transform parent, GameObjectState state, LayerUsageFlags query)
        {
            var renderContainer = ChiselObjectUtility.CreateGameObject(name, parent, state);
            var meshFilter      = renderContainer.AddComponent<MeshFilter>();
            var meshRenderer    = renderContainer.AddComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            var renderObjects = new ChiselRenderObjects
            {
                invalid         = false,            
                query           = query,
                container       = renderContainer,
                meshFilter      = meshFilter,
                meshRenderer    = meshRenderer,
                renderMaterials = new Material[0]
            };
            renderObjects.EnsureMeshesAllocated();
            renderObjects.Initialize();
            return renderObjects;
        }


        void EnsureMeshesAllocated()
        {
            if (sharedMesh == null) sharedMesh = new Mesh { name = meshFilter.gameObject.name };
#if UNITY_EDITOR
            if (partialMesh == null)
            {
                partialMesh = new Mesh { name = meshFilter.gameObject.name };
                partialMesh.hideFlags = HideFlags.DontSave;
            }
#endif
        }

        public void Destroy()
        {
            if (invalid)
                return;
#if UNITY_EDITOR
            ChiselObjectUtility.SafeDestroy(partialMesh);
            partialMesh     = null;
#endif
            ChiselObjectUtility.SafeDestroy(sharedMesh);
            ChiselObjectUtility.SafeDestroy(container, ignoreHierarchyEvents: true);
            container       = null;
            sharedMesh      = null;
            meshFilter      = null;
            meshRenderer    = null;
            renderMaterials = null;
        }

        public void DestroyWithUndo()
        {
            if (invalid)
                return;
#if UNITY_EDITOR
            ChiselObjectUtility.SafeDestroyWithUndo(partialMesh);
#endif
            ChiselObjectUtility.SafeDestroyWithUndo(sharedMesh);
            ChiselObjectUtility.SafeDestroyWithUndo(container, ignoreHierarchyEvents: true);
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
            ChiselGeneratedComponentManager.SetHasLightmapUVs(sharedMesh, false);
#endif
        }

        void UpdateSettings(ChiselModel model, GameObjectState state, bool meshIsModified)
        {
#if UNITY_EDITOR
            // If we need to render partial meshes (where some brushes are hidden) then we should show the full mesh
            ChiselGeneratedComponentManager.CheckIfFullMeshNeedsToBeHidden(model, this);
            if (meshIsModified)
            {
                // Setting the sharedMesh to ensure the meshFilter knows it needs to be updated
                meshFilter.sharedMesh = meshFilter.sharedMesh;
                UnityEditor.EditorUtility.SetDirty(meshFilter);
                UnityEditor.EditorUtility.SetDirty(model);
                ChiselGeneratedComponentManager.SetHasLightmapUVs(sharedMesh, false);
                ChiselGeneratedComponentManager.ClearLightmapData(state, this);
            }
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
#if UNITY_EDITOR
                meshRenderer.stitchLightmapSeams            = renderSettings.stitchLightmapSeams;
                meshRenderer.scaleInLightmap                = renderSettings.scaleInLightmap;
                meshRenderer.receiveGI                      = renderSettings.receiveGI;
#endif
            }
        }

        public void Clear(ChiselModel model, GameObjectState state)
        {
            bool meshIsModified = false;
            try
            {
                triangleBrushes.Clear();

                if (sharedMesh.vertexCount > 0)
                {
                    meshIsModified = true;
                    sharedMesh.Clear(keepVertexLayout: true);
                }
                renderMaterials = Array.Empty<Material>();
                if (meshFilter.sharedMesh != sharedMesh)
                {
                    meshFilter.sharedMesh = sharedMesh;
                    meshIsModified = true;
                }
                var expectedEnabled = sharedMesh.vertexCount > 0;
                SetMaterialsIfModified(meshRenderer, renderMaterials);
                if (meshRenderer.enabled != expectedEnabled)
                    meshRenderer.enabled = expectedEnabled;
            }
            finally
            {
                __foundMaterials.Clear();
            }
            UpdateSettings(model, state, meshIsModified);
        }




        static readonly List<Material>              __foundMaterials    = new List<Material>(); // static to avoid allocations
        public void Update(ChiselModel model, GameObjectState state, ref VertexBufferContents vertexBufferContents, int contentsIndex)
        {
            bool meshIsModified = false;
            // Retrieve the generatedMeshes and its materials, combine them into a single Unity Mesh/Material array
            try
            {
                Profiler.BeginSample("CopyMeshFrom");
                meshIsModified = vertexBufferContents.CopyToMesh(contentsIndex, sharedMesh, __foundMaterials, triangleBrushes);
                Profiler.EndSample();
            
                Profiler.BeginSample("UpdateMaterials");
                if (renderMaterials != null && renderMaterials.Length == __foundMaterials.Count)
                    __foundMaterials.CopyTo(renderMaterials);
                else
                    renderMaterials = __foundMaterials.ToArray();
                SetMaterialsIfModified(meshRenderer, renderMaterials);
                Profiler.EndSample();

                Profiler.BeginSample("UpdateSharedMesh");
                if (meshFilter.sharedMesh != sharedMesh)
                {
                    meshFilter.sharedMesh = sharedMesh;
                    meshIsModified = true;
                }
                var expectedEnabled = sharedMesh.vertexCount > 0;
                if (meshRenderer.enabled != expectedEnabled)
                    meshRenderer.enabled = expectedEnabled;
                Profiler.EndSample();
            }
            finally
            {
                __foundMaterials.Clear();
            }
            UpdateSettings(model, state, meshIsModified);
        }

        private void SetMaterialsIfModified(MeshRenderer meshRenderer, Material[] renderMaterials)
        {
            var currentSharedMaterials = meshRenderer.sharedMaterials;
            if (currentSharedMaterials != null &&
                currentSharedMaterials.Length == renderMaterials.Length)
            {
                for (int i = 0; i < renderMaterials.Length; i++)
                {
                    if (renderMaterials[i] != currentSharedMaterials[i])
                        goto SetMaterials;
                }
                return;
            }
            SetMaterials:
            meshRenderer.sharedMaterials = renderMaterials;
        }

#if UNITY_EDITOR
        static readonly List<Vector3>   sVertices       = new List<Vector3>();
        static readonly List<Vector3>   sNormals        = new List<Vector3>();
        static readonly List<Vector4>   sTangents       = new List<Vector4>();
        static readonly List<Vector2>   sUV0            = new List<Vector2>();
        static readonly List<int>       sSrcTriangles   = new List<int>();
        static readonly List<int>       sDstTriangles   = new List<int>();
        internal void UpdateVisibilityMesh()
        {
            EnsureMeshesAllocated();
            var srcMesh = sharedMesh;
            var dstMesh = partialMesh;

            dstMesh.Clear(keepVertexLayout: true);
            srcMesh.GetVertices(sVertices);
            dstMesh.SetVertices(sVertices);

            srcMesh.GetNormals(sNormals);
            dstMesh.SetNormals(sNormals);

            srcMesh.GetTangents(sTangents);
            dstMesh.SetTangents(sTangents);

            srcMesh.GetUVs(0, sUV0);
            dstMesh.SetUVs(0, sUV0);

            dstMesh.subMeshCount = srcMesh.subMeshCount;
            for (int subMesh = 0, n = 0; subMesh < srcMesh.subMeshCount; subMesh++)
            {
                bool calculateBounds    = false;
                int baseVertex          = (int)srcMesh.GetBaseVertex(subMesh);
                srcMesh.GetTriangles(sSrcTriangles, subMesh, applyBaseVertex: false);
                sDstTriangles.Clear();
                for (int i = 0; i < sSrcTriangles.Count; i += 3, n++)
                {
                    if (n < triangleBrushes.Count)
                    { 
                        int     brushID         = triangleBrushes[n];
                        bool    isBrushVisible  = ChiselGeneratedComponentManager.IsBrushVisible(brushID);
                        if (!isBrushVisible)
                            continue;
                    }
                    sDstTriangles.Add(sSrcTriangles[i + 0]);
                    sDstTriangles.Add(sSrcTriangles[i + 1]);
                    sDstTriangles.Add(sSrcTriangles[i + 2]);
                }
                dstMesh.SetTriangles(sDstTriangles, subMesh, calculateBounds, baseVertex);
            }
            dstMesh.RecalculateBounds();
        }
#endif
    }
}