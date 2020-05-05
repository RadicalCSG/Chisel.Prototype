using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using LightProbeUsage = UnityEngine.Rendering.LightProbeUsage;
using ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage;

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


    // TODO: give model an icon
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

        [HideInInspector, SerializeField] ChiselGeneratedColliderSettings   colliderSettings;
        [HideInInspector, SerializeField] ChiselGeneratedRenderSettings     renderSettings;
        [HideInInspector, SerializeField] SerializableUnwrapParam           uvGenerationSettings;

        [HideInInspector] public CSGTree Node;
        [HideInInspector] internal GeneratedMeshContents generatedMeshContents;

        // TODO: put all bools in flags (makes it harder to work with in the ModelEditor though)
        public bool CreateRenderComponents      = true;
        public bool CreateColliderComponents    = true;
        public bool AutoRebuildUVs              = true;
        public VertexChannelFlags VertexChannelMask = VertexChannelFlags.All; // NOTE: do not rename, name is used directly in CSGModelEditor



        public ChiselGeneratedColliderSettings  ColliderSettings        { get { return colliderSettings; } }
        public ChiselGeneratedRenderSettings    RenderSettings          { get { return renderSettings; } }
        public SerializableUnwrapParam          UVGenerationSettings    { get { return uvGenerationSettings; } internal set { uvGenerationSettings = value; } }

        [HideInInspector, NonSerialized]
        public readonly Dictionary<Material, List<ChiselRenderComponents>>         generatedRenderComponents = new Dictionary<Material, List<ChiselRenderComponents>>();
        [HideInInspector, NonSerialized]
        public readonly Dictionary<PhysicMaterial, List<ChiselColliderComponents>> generatedMeshColliders    = new Dictionary<PhysicMaterial, List<ChiselColliderComponents>>();
        [HideInInspector, NonSerialized]
        public readonly HashSet<Transform>                  generatedComponents = new HashSet<Transform>();
        [SerializeField] public ChiselGeneratedModelMesh[]  generatedMeshes     = new ChiselGeneratedModelMesh[0];

        // TODO: make these private + properties, these show up as settable default settings when selecting CSGModel.cs in unity
        public GameObject   GeneratedDataContainer { get { return generatedDataContainer; } internal set { generatedDataContainer = value; } }
        public Transform    GeneratedDataTransform { get { return generatedDataTransform; } internal set { generatedDataTransform = value; } }
        [HideInInspector, SerializeField] GameObject    generatedDataContainer;
        [HideInInspector, SerializeField] Transform     generatedDataTransform;
        [HideInInspector, SerializeField] bool          initialized = false;

        public bool             IsInitialized       { get { return initialized; } }
        public override int     NodeID              { get { return Node.NodeID; } }
        public override bool    CanHaveChildNodes   { get { return !SkipThisNode; } }

        public override void OnInitialize()
        {
            if (!generatedDataContainer)
            {
                generatedDataContainer = ChiselGeneratedComponentManager.FindContainerGameObject(this);
                if (generatedDataContainer != null)
                    generatedDataTransform = generatedDataContainer.transform;
            }

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
#endif

            initialized = true;
        }
        public ChiselModel() : base() { }

        protected override void OnDisable() { base.OnDisable(); if (generatedMeshContents != null) generatedMeshContents.Dispose(); }

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

        internal override void CollectChildNodesForParent(List<CSGTreeNode> childNodes)
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
    }
}