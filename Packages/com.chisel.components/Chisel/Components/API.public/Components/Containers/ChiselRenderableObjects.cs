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
    public struct ChiselRenderObjectUpdate
    {
        public int                  meshIndex;
        public ChiselRenderObjects  instance;
        public Material             materialOverride;
        public bool                 meshIsModified;
        public ChiselModel          model;
    }

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
        public int[]            triangleBrushes = Array.Empty<int>();
        
        public ulong            geometryHashValue;
        public ulong            surfaceHashValue;

        public bool             debugHelperRenderer;
        [NonSerialized] public float uvLightmapUpdateTime;

        internal ChiselRenderObjects() { }
        public static ChiselRenderObjects Create(string name, Transform parent, GameObjectState state, LayerUsageFlags query, bool debugHelperRenderer = false)
        {
            var renderContainer = ChiselObjectUtility.CreateGameObject(name, parent, state, debugHelperRenderer: debugHelperRenderer);
            var meshFilter      = renderContainer.AddComponent<MeshFilter>();
            var meshRenderer    = renderContainer.AddComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            var renderObjects = new ChiselRenderObjects
            {
                invalid             = false,            
                query               = query,
                container           = renderContainer,
                meshFilter          = meshFilter,
                meshRenderer        = meshRenderer,
                renderMaterials     = new Material[0],
                debugHelperRenderer = debugHelperRenderer
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
            invalid = true;
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
            if (!debugHelperRenderer)
            { 
                meshRenderer.receiveShadows	= ((query & LayerUsageFlags.ReceiveShadows) == LayerUsageFlags.ReceiveShadows);
                switch (query & (LayerUsageFlags.Renderable | LayerUsageFlags.CastShadows))
                {
                    case LayerUsageFlags.None:				meshRenderer.enabled = false; break;
                    case LayerUsageFlags.Renderable:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;			break;
                    case LayerUsageFlags.CastShadows:		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;   break;
                    case LayerUsageFlags.RenderCastShadows:	meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;			break;
                }

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetSelectedRenderState(meshRenderer, UnityEditor.EditorSelectedRenderState.Hidden);
                ChiselGeneratedComponentManager.SetHasLightmapUVs(sharedMesh, false);
#endif
            } else
            {
                meshRenderer.allowOcclusionWhenDynamic = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.scaleInLightmap = 0.0f;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void UpdateSettings(ChiselModel model, GameObjectState state, bool meshIsModified)
        {
#if UNITY_EDITOR
            Profiler.BeginSample("CheckIfFullMeshNeedsToBeHidden");
            // If we need to render partial meshes (where some brushes are hidden) then we should show the full mesh
            ChiselGeneratedComponentManager.CheckIfFullMeshNeedsToBeHidden(model, this);
            Profiler.EndSample();
            if (meshIsModified)
            {
                // Setting the sharedMesh to ensure the meshFilter knows it needs to be updated
                Profiler.BeginSample("OverrideMesh");
                meshFilter.sharedMesh = meshFilter.sharedMesh;
                Profiler.EndSample();
                Profiler.BeginSample("SetDirty");
                UnityEditor.EditorUtility.SetDirty(meshFilter);
                UnityEditor.EditorUtility.SetDirty(model);
                Profiler.EndSample();
                Profiler.BeginSample("SetHasLightmapUVs");
                ChiselGeneratedComponentManager.SetHasLightmapUVs(sharedMesh, false);
                Profiler.EndSample();
                Profiler.BeginSample("ClearLightmapData");
                ChiselGeneratedComponentManager.ClearLightmapData(state, this);
                Profiler.EndSample();
            }
#endif 
        }

        public static void UpdateProperties(ChiselModel model, MeshRenderer[] meshRenderers)
        {
            if (meshRenderers == null || meshRenderers.Length == 0)
                return;
            
            var renderSettings = model.RenderSettings;
            Profiler.BeginSample("serializedObject");
#if UNITY_EDITOR
            if (renderSettings.serializedObjectFieldsDirty)
            {
                renderSettings.serializedObjectFieldsDirty = false;
                // These SerializedObject settings can *only* be modified in the inspector, 
                //      so we should only be calling this on creation / 
                //      when something in inspector changed.

                // Warning: calling new UnityEditor.SerializedObject with an empty array crashes Unity
                using (var serializedObject = new UnityEditor.SerializedObject(meshRenderers))
                {
                    serializedObject.SetPropertyValue("m_ImportantGI",                      renderSettings.ImportantGI);
                    serializedObject.SetPropertyValue("m_PreserveUVs",                      renderSettings.OptimizeUVs);
                    serializedObject.SetPropertyValue("m_IgnoreNormalsForChartDetection",   renderSettings.IgnoreNormalsForChartDetection);
                    serializedObject.SetPropertyValue("m_AutoUVMaxDistance",                renderSettings.AutoUVMaxDistance);
                    serializedObject.SetPropertyValue("m_AutoUVMaxAngle",                   renderSettings.AutoUVMaxAngle);
                    serializedObject.SetPropertyValue("m_MinimumChartSize",                 renderSettings.MinimumChartSize);
                }
            }
            Profiler.EndSample();
#endif

            Profiler.BeginSample("meshRenderers");
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var meshRenderer = meshRenderers[i];
                var isRenderable = meshRenderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
                meshRenderer.lightProbeProxyVolumeOverride	= !isRenderable ? null : renderSettings.lightProbeProxyVolumeOverride;
                meshRenderer.probeAnchor					= !isRenderable ? null : renderSettings.probeAnchor;
                meshRenderer.motionVectorGenerationMode		= !isRenderable ? MotionVectorGenerationMode.ForceNoMotion : renderSettings.motionVectorGenerationMode;
                meshRenderer.reflectionProbeUsage			= !isRenderable ? ReflectionProbeUsage.Off : renderSettings.reflectionProbeUsage;
                meshRenderer.lightProbeUsage				= !isRenderable ? LightProbeUsage.Off : renderSettings.lightProbeUsage;
                meshRenderer.allowOcclusionWhenDynamic		= renderSettings.allowOcclusionWhenDynamic;
                meshRenderer.renderingLayerMask				= renderSettings.renderingLayerMask;
#if UNITY_EDITOR
                meshRenderer.stitchLightmapSeams            = isRenderable && renderSettings.stitchLightmapSeams;
                meshRenderer.scaleInLightmap                = !isRenderable ? 0.0f : renderSettings.scaleInLightmap;
                meshRenderer.receiveGI                      = renderSettings.receiveGI;
#endif
            }
            Profiler.EndSample();
        }

        public void Clear(ChiselModel model, GameObjectState state)
        {
            bool meshIsModified = false;
            {
                Profiler.BeginSample("Clear");
                triangleBrushes = Array.Empty<int>();

                if (sharedMesh.vertexCount > 0)
                {
                    meshIsModified = true;
                    sharedMesh.Clear(keepVertexLayout: true);
                }
                Profiler.EndSample();

                Profiler.BeginSample("SetSharedMesh");
                if (meshFilter.sharedMesh != sharedMesh)
                {
                    meshFilter.sharedMesh = sharedMesh;
                    meshIsModified = true;
                }
                Profiler.EndSample();

                Profiler.BeginSample("SetMaterialsIfModified");
                renderMaterials = Array.Empty<Material>();
                SetMaterialsIfModified(meshRenderer, renderMaterials);
                Profiler.EndSample();

                Profiler.BeginSample("Enable");
                var expectedEnabled = sharedMesh.vertexCount > 0;
                if (meshRenderer.enabled != expectedEnabled)
                    meshRenderer.enabled = expectedEnabled;
                Profiler.EndSample();
            }
            Profiler.BeginSample("UpdateSettings");
            UpdateSettings(model, state, meshIsModified);
            Profiler.EndSample();
        }

        public static void UpdateMaterials(List<ChiselMeshUpdate> meshUpdates, List<ChiselRenderObjectUpdate> objectUpdates, ref VertexBufferContents vertexBufferContents)
        {
            Profiler.BeginSample("SetTriangleBrushes");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var meshUpdate          = meshUpdates[u];
                var objectUpdate        = objectUpdates[u];
                var instance            = objectUpdate.instance;
                var brushIndicesArray   = vertexBufferContents.triangleBrushIndices[meshUpdate.contentsIndex].AsArray();
                if (instance.triangleBrushes.Length < brushIndicesArray.Length)
                    instance.triangleBrushes = new int[brushIndicesArray.Length];
                NativeArray<int>.Copy(brushIndicesArray, instance.triangleBrushes, brushIndicesArray.Length);
            }
            Profiler.EndSample();

            Profiler.BeginSample("UpdateMaterials");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var meshUpdate          = meshUpdates[u];
                var objectUpdate        = objectUpdates[u];
                var instance            = objectUpdate.instance;
                var contentsIndex       = meshUpdate.contentsIndex;
                var materialOverride    = objectUpdate.materialOverride;
                var startIndex          = vertexBufferContents.subMeshSections[contentsIndex].startIndex;
                var endIndex            = vertexBufferContents.subMeshSections[contentsIndex].endIndex;
                var desiredCapacity = endIndex - startIndex;
                if (instance.renderMaterials == null || instance.renderMaterials.Length != desiredCapacity)
                    instance.renderMaterials = new Material[desiredCapacity];
                if (materialOverride)
                {
                    for (int i = 0; i < instance.renderMaterials.Length; i++)
                        instance.renderMaterials[i] = materialOverride;
                } else
                {
                    for (int i = 0; i < desiredCapacity; i++)
                    {
                        var meshDescription = vertexBufferContents.meshDescriptions[startIndex + i];
                        var renderMaterial  = ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(meshDescription.surfaceParameter);

                        instance.renderMaterials[i] = renderMaterial;
                    }
                }
                instance.SetMaterialsIfModified(instance.meshRenderer, instance.renderMaterials);
            }
            Profiler.EndSample();
        }


        public static void UpdateSettings(List<ChiselMeshUpdate> meshUpdates, List<ChiselRenderObjectUpdate> objectUpdates, Dictionary<ChiselModel, GameObjectState> gameObjectStates, ref VertexBufferContents vertexBufferContents)
        {
            Profiler.BeginSample("UpdateSettings");
            for (int u = 0; u < objectUpdates.Count; u++)
            {
                var objectUpdate    = objectUpdates[u];
                var meshUpdate      = meshUpdates[u];
                var instance        = objectUpdate.instance;
                var contentsIndex   = meshUpdate.contentsIndex;
                var sharedMesh      = instance.sharedMesh;

                if (sharedMesh.subMeshCount > 0)
                {
                    var bounds = sharedMesh.GetSubMesh(0).bounds;
                    for (int s = 1; s < sharedMesh.subMeshCount; s++)
                        bounds.Encapsulate(sharedMesh.GetSubMesh(s).bounds);
                    sharedMesh.bounds = bounds;
                }

                if (instance.meshFilter.sharedMesh != sharedMesh)
                {
                    instance.meshFilter.sharedMesh = sharedMesh;
                    objectUpdate.meshIsModified = true;
                    objectUpdates[u] = objectUpdate;
                }

                var expectedEnabled = vertexBufferContents.triangleBrushIndices[contentsIndex].Length > 0;
                if (instance.meshRenderer.enabled != expectedEnabled)
                    instance.meshRenderer.enabled = expectedEnabled;

                var gameObjectState = gameObjectStates[objectUpdate.model];
                instance.UpdateSettings(objectUpdate.model, gameObjectState, objectUpdate.meshIsModified);
            }
            Profiler.EndSample();
        }

        static List<Material> sSharedMaterials = new List<Material>();

        private void SetMaterialsIfModified(MeshRenderer meshRenderer, Material[] renderMaterials)
        {
            meshRenderer.GetSharedMaterials(sSharedMaterials);
            if (sSharedMaterials != null &&
                sSharedMaterials.Count == renderMaterials.Length)
            {
                for (int i = 0; i < renderMaterials.Length; i++)
                {
                    if (renderMaterials[i] != sSharedMaterials[i])
                        goto SetMaterials;
                }
                sSharedMaterials.Clear(); // prevent dangling references
                return;
            }
            sSharedMaterials.Clear(); // prevent dangling references
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
        internal void UpdateVisibilityMesh(bool showMesh)
        {
            EnsureMeshesAllocated();
            var srcMesh = sharedMesh;
            var dstMesh = partialMesh;

            dstMesh.Clear(keepVertexLayout: true);
            if (!showMesh)
                return;
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
                    if (n < triangleBrushes.Length)
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