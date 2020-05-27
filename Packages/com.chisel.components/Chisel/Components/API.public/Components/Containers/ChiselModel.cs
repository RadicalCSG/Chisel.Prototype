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
    // Can't use UnityEditor.UnwrapParam since it's not marked as Serializable
    [Serializable]
    public sealed class SerializableUnwrapParam
    {
        public const string kAngleErrorName         = nameof(angleError);
        public const string kAreaErrorName          = nameof(areaError);
        public const string kHardAngleName          = nameof(hardAngle);
        public const string kPackMarginPixelsName   = nameof(packMarginPixels);

        public const float minAngleError	= 0.001f;
        public const float maxAngleError    = 1.000f;

        public const float minAreaError		= 0.001f;
        public const float maxAreaError		= 1.000f;

        public const float minHardAngle     = 1;
        public const float maxHardAngle     = 179;

        public const float minPackMargin	= 1;
        public const float maxPackMargin	= 256;

        [Range(minAngleError, maxAngleError)] public float angleError;
        [Range(minAreaError,  maxAreaError )] public float areaError;
        [Range(minHardAngle,  maxHardAngle )] public float hardAngle;
        [Range(minPackMargin, maxPackMargin)] public float packMarginPixels;
    }

    [Serializable]
    public sealed class ChiselGeneratedColliderSettings
    {
        public const string kIsTriggerName      = nameof(isTrigger);
        public const string kConvexName         = nameof(convex);
        public const string kCookingOptionsName = nameof(cookingOptions);
        public const string kSkinWidthName      = nameof(skinWidth);

        public bool                         isTrigger;
        public bool		                    convex;
        public MeshColliderCookingOptions   cookingOptions;
        public float	                    skinWidth;

        public void Reset()
        {
            isTrigger       = false;
            convex          = false;
            cookingOptions	= (MeshColliderCookingOptions)(2|4|8);
            skinWidth       = 0.01f;
        }
    }

    [Serializable]
    public sealed class ChiselGeneratedRenderSettings
    {
        public const string kMotionVectorGenerationModeName     = nameof(motionVectorGenerationMode);
        public const string kAllowOcclusionWhenDynamicName      = nameof(allowOcclusionWhenDynamic);
        public const string kRenderingLayerMaskName             = nameof(renderingLayerMask);
        public const string kReflectionProbeUsageName           = nameof(reflectionProbeUsage);
        public const string kLightProbeUsageName                = nameof(lightProbeUsage);
        public const string kLightProbeVolumeOverrideName       = nameof(lightProbeProxyVolumeOverride);
        public const string kProbeAnchorName                    = nameof(probeAnchor);
        public const string kReceiveGIName                      = nameof(receiveGI);
        
#if UNITY_EDITOR
        public const string kLightmapParametersName             = nameof(lightmapParameters);
        public const string kImportantGIName                    = nameof(importantGI);
        public const string kOptimizeUVsName                    = nameof(optimizeUVs);
        public const string kIgnoreNormalsForChartDetectionName = nameof(ignoreNormalsForChartDetection);
        public const string kScaleInLightmapName                = nameof(scaleInLightmap);
        public const string kAutoUVMaxDistanceName              = nameof(autoUVMaxDistance);
        public const string kAutoUVMaxAngleName                 = nameof(autoUVMaxAngle);
        public const string kMinimumChartSizeName               = nameof(minimumChartSize);
        public const string kStitchLightmapSeamsName            = nameof(stitchLightmapSeams);
#endif


        // TODO: store lightmap information in settings so it can't get lost?
        //renderComponents.meshRenderer.lightmapIndex
        //renderComponents.meshRenderer.lightmapScaleOffset
        //renderComponents.meshRenderer.lightmapTilingOffset
        //renderComponents.meshRenderer.lightProbeUsage
        //renderComponents.meshRenderer.realtimeLightmapIndex
        //renderComponents.meshRenderer.realtimeLightmapScaleOffset
        //renderComponents.meshRenderer.lightProbeProxyVolumeOverride
        //renderComponents.meshRenderer.probeAnchor


        public GameObject                       lightProbeProxyVolumeOverride;
        public Transform                        probeAnchor;
        public MotionVectorGenerationMode		motionVectorGenerationMode		= MotionVectorGenerationMode.Object;
        public ReflectionProbeUsage				reflectionProbeUsage			= ReflectionProbeUsage.BlendProbes;
        public LightProbeUsage					lightProbeUsage					= LightProbeUsage.BlendProbes;
        public bool                             allowOcclusionWhenDynamic       = true;
        public uint                             renderingLayerMask              = ~(uint)0;
        public ReceiveGI						receiveGI						= ReceiveGI.LightProbes;

#if UNITY_EDITOR
    	public UnityEditor.LightmapParameters   lightmapParameters				= null;		// TODO: figure out how to apply this, safely, using SerializedObject
        public bool								importantGI						= false;
        public bool								optimizeUVs                     = false;	// "Preserve UVs"
        public bool								ignoreNormalsForChartDetection  = false;
        public float							scaleInLightmap                 = 1.0f;
        public float							autoUVMaxDistance				= 0.5f;
        public float							autoUVMaxAngle					= 89;
        public int								minimumChartSize				= 4;
        public bool								stitchLightmapSeams				= false;
#endif

        public void Reset()
        {
            lightProbeProxyVolumeOverride   = null;
            probeAnchor                     = null;
            motionVectorGenerationMode		= MotionVectorGenerationMode.Object;
            reflectionProbeUsage			= ReflectionProbeUsage.BlendProbes;
            lightProbeUsage					= LightProbeUsage.Off;
            allowOcclusionWhenDynamic		= true;
            renderingLayerMask              = ~(uint)0;
            receiveGI                       = ReceiveGI.LightProbes;
#if UNITY_EDITOR
    		lightmapParameters				= new UnityEditor.LightmapParameters();
            importantGI						= false;
            optimizeUVs						= false;
            ignoreNormalsForChartDetection  = false;
            scaleInLightmap                 = 1.0f;
            autoUVMaxDistance				= 0.5f;
            autoUVMaxAngle					= 89;
            minimumChartSize				= 4;
            stitchLightmapSeams				= false;
#endif
        }
    }


    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselModel : ChiselNode
    {
        public const string kRenderSettingsName             = nameof(renderSettings);
        public const string kColliderSettingsName           = nameof(colliderSettings);
        public const string kUVGenerationSettingsName       = nameof(uvGenerationSettings);
        public const string kCreateRenderComponentsName     = nameof(CreateRenderComponents);
        public const string kCreateColliderComponentsName   = nameof(CreateColliderComponents);
        public const string kAutoRebuildUVsName             = nameof(AutoRebuildUVs);
        public const string kVertexChannelMaskName          = nameof(VertexChannelMask);


        public const string kNodeTypeName = "Model";
        public override string NodeTypeName { get { return kNodeTypeName; } }


        public ChiselGeneratedColliderSettings  ColliderSettings        { get { return colliderSettings; } }
        public ChiselGeneratedRenderSettings    RenderSettings          { get { return renderSettings; } }
        public SerializableUnwrapParam          UVGenerationSettings    { get { return uvGenerationSettings; } internal set { uvGenerationSettings = value; } }
        public bool                 IsInitialized               { get { return initialized; } }
        public override int         NodeID                      { get { return Node.NodeID; } }
        public override bool        CanHaveChildNodes           { get { return !SkipThisNode; } }

        // TODO: put all bools in flags (makes it harder to work with in the ModelEditor though)
        public bool                 CreateRenderComponents      = true;
        public bool                 CreateColliderComponents    = true;
        public bool                 AutoRebuildUVs              = true;
        public VertexChannelFlags   VertexChannelMask           = VertexChannelFlags.All;

        public ChiselGeneratedColliderSettings    colliderSettings;
        public ChiselGeneratedRenderSettings      renderSettings;
        public SerializableUnwrapParam            uvGenerationSettings;

        
        [HideInInspector] public CSGTree                    Node;

        [HideInInspector] bool                              initialized = false;

        [HideInInspector] public ChiselModelGeneratedObjects generated;


        public override void OnInitialize()
        {
            if (generated != null &&
                !generated.generatedDataContainer)
                generated.Destroy();

            if (generated == null)
                generated = ChiselModelGeneratedObjects.Create(this);

            if (colliderSettings == null)
            {
                colliderSettings = new ChiselGeneratedColliderSettings();
                colliderSettings.Reset();
            }

            if (renderSettings == null)
            {
                renderSettings = new ChiselGeneratedRenderSettings();
                renderSettings.Reset();
            }

#if UNITY_EDITOR
            if (uvGenerationSettings == null)
            {
                uvGenerationSettings = new SerializableUnwrapParam();
                UnityEditor.UnwrapParam defaults;
                UnityEditor.UnwrapParam.SetDefaults(out defaults);
                uvGenerationSettings.angleError = defaults.angleError;
                uvGenerationSettings.areaError = defaults.areaError;
                uvGenerationSettings.hardAngle = defaults.hardAngle;
                uvGenerationSettings.packMarginPixels = defaults.packMargin * 256;
            }
#else
            if (generated != null && generated.meshRenderers != null)
            {
                foreach(var renderable in generated.renderables)
                {                    
                    renderable.meshRenderer.forceRenderingOff = true;
                    renderable.meshRenderer.enabled = renderable.sharedMesh.vertexCount == 0;
                }
            }
#endif

            initialized = true;
        }

        public ChiselModel() : base() { }
        protected override void OnDisable() { base.OnDisable(); }


        internal override void ClearTreeNodes(bool clearCaches = false) { Node.SetInvalid(); }
        internal override CSGTreeNode[] CreateTreeNodes()
        {
            if (Node.Valid)
                Debug.LogWarning("ChiselModel already has a treeNode, but trying to create a new one?", this);
            var userID = GetInstanceID();
            Node = CSGTree.Create(userID: userID);
            return new CSGTreeNode[] { Node };
        }


        internal override void SetChildren(List<CSGTreeNode> childNodes)
        {
            if (!Node.Valid)
            {
                Debug.LogWarning("SetChildren called on a ChiselModel that isn't properly initialized", this);
                return;
            }
            if (!Node.SetChildren(childNodes.ToArray()))
                Debug.LogError("Failed to assign list of children to tree node");
        }

        public override void CollectCSGTreeNodes(List<CSGTreeNode> childNodes)
        {
            // No parent can hold a model as a child, so we don't add anything
        }


        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (!Node.Valid)
                return false;
            // A model makes no sense without any children
            return (transform.childCount > 0);
        }

        public override void SetDirty() { if (Node.Valid) Node.SetDirty(); }

        protected override void OnCleanup()
        {
            ChiselGeneratedComponentManager.RemoveContainerFlags(this);
            generated.Destroy();
        }

        public override int GetAllTreeBrushCount()
        {
            return 0;
        }

        // Get all brushes directly contained by this ChiselNode (not its children)
        public override void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes)
        {
            // A Model doesn't contain a CSGTreeBrush node
        }
        
        // TODO: cache this
        public override Bounds CalculateBounds()
        {
            var bounds = ChiselHierarchyItem.EmptyBounds;
            var haveBounds = false;
            for (int c = 0; c < hierarchyItem.Children.Count; c++)
            {
                var child = hierarchyItem.Children[c];
                if (!child.Component)
                    continue;
                var assetBounds = child.Component.CalculateBounds();
                if (assetBounds.size.sqrMagnitude == 0)
                    continue;
                if (!haveBounds)
                {
                    bounds = assetBounds;
                    haveBounds = true;
                } else
                    bounds.Encapsulate(assetBounds);
            }
            return bounds;
        }

#if UNITY_EDITOR
        public void Update()
        {
            if (generated == null)
                return;

            generated.UpdateVisibilityMeshes();

            // TODO: figure out why this can happen
            Debug.Assert(generated.visibilityState != VisibilityState.Unknown, "Unknown Visibility state");
            if (generated.visibilityState != VisibilityState.Mixed)
                return;

            // When we toggle visibility on brushes in the editor hierarchy, we want to render a different mesh
            // but still have the same lightmap, and keep lightmap support.
            // We do this by setting forceRenderingOff to true on all MeshRenderers.
            // This makes them behave like before, except that they don't render. This means they are still 
            // part of things such as lightmap generation. At the same time we use Graphics.DrawMesh to
            // render the sub-mesh with the exact same settings as the MeshRenderer.

            var layer   = gameObject.layer; 
            var matrix  = transform.localToWorldMatrix;
            var camera  = Camera.current;
            foreach (var renderable in generated.renderables)
            {
                if (renderable == null)
                    continue;

                var mesh = (Mesh)renderable.partialMesh;
                if (mesh == null || mesh.vertexCount == 0)
                    continue;

                var meshRenderer = renderable.meshRenderer;
                if (!meshRenderer || !meshRenderer.enabled || !meshRenderer.forceRenderingOff)
                    continue;

                var properties = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(properties);

                var castShadows             = (ShadowCastingMode)meshRenderer.shadowCastingMode;
                var receiveShadows          = (bool)meshRenderer.receiveShadows;
                var probeAnchor             = (Transform)meshRenderer.probeAnchor;
                var lightProbeUsage         = (LightProbeUsage)meshRenderer.lightProbeUsage;
                var lightProbeProxyVolume   = meshRenderer.lightProbeProxyVolumeOverride == null ? null : meshRenderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>();
                    
                for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
                    Graphics.DrawMesh(mesh, matrix, renderable.renderMaterials[submeshIndex], layer, camera, submeshIndex, properties, castShadows, receiveShadows, probeAnchor, lightProbeUsage, lightProbeProxyVolume);
            }
        }
#endif
    }
}