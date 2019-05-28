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
    public sealed class CSGGeneratedColliderSettings
    {
        public bool     isTrigger;
        public bool		convex;
        public MeshColliderCookingOptions cookingOptions = (MeshColliderCookingOptions)(2|4|8);
        public float	skinWidth	= 0.01f;

        public void Reset()
        {
            isTrigger       = false;
            convex          = false;
            cookingOptions	= (MeshColliderCookingOptions)(2|4|8);
            skinWidth       = 0.01f;
        }
    }

    [Serializable]
    public sealed class CSGGeneratedRenderSettings
    {
        public GameObject                       lightProbeProxyVolumeOverride;
        public Transform                        probeAnchor;
        public MotionVectorGenerationMode		motionVectorGenerationMode		= MotionVectorGenerationMode.Object;
        public ReflectionProbeUsage				reflectionProbeUsage			= ReflectionProbeUsage.BlendProbes;
        public LightProbeUsage					lightProbeUsage					= LightProbeUsage.Off;
        #if UNITY_2017_2_OR_ABOVE || UNITY_EDITOR
        public bool                             dynamicOccludee                 = true;
        #endif
        #if UNITY_2018_2_OR_ABOVE || UNITY_EDITOR
        public uint                             renderingLayerMask              = ~(uint)0;
        #endif
#if UNITY_EDITOR
///		public UnityEditor.LightmapParameters   lightmapParameters				= null;		// TODO: figure out how to apply this, safely, using SerializedObject
        public bool								importantGI						= false;
        public bool								optimizeUVs                     = false;	// "Preserve UVs"
        public bool								ignoreNormalsForChartDetection  = false;
        public float							scaleInLightmap                 = 1.0f;
        public float							autoUVMaxDistance				= 0.5f;
        public float							autoUVMaxAngle					= 89;
        public int								minimumChartSize				= 4;

#if UNITY_2017_2_OR_ABOVE
        public bool								stitchLightmapSeams				= false;
#endif
#endif

        public void Reset()
        {
            motionVectorGenerationMode		= MotionVectorGenerationMode.Object;
            reflectionProbeUsage			= ReflectionProbeUsage.BlendProbes;
            lightProbeUsage					= LightProbeUsage.Off;
            dynamicOccludee					= true;
#if UNITY_EDITOR
//			lightmapParameters				= new UnityEditor.LightmapParameters();
            importantGI						= false;
            optimizeUVs						= false;
            ignoreNormalsForChartDetection  = false;
            scaleInLightmap                 = 1.0f;
            autoUVMaxDistance				= 0.5f;
            autoUVMaxAngle					= 89;
            minimumChartSize				= 4;
            renderingLayerMask              = ~(uint)0;
#if UNITY_2017_2_OR_ABOVE
            stitchLightmapSeams				= false;
#endif
#endif
        }
    }


    // TODO: give model an icon
    [ExecuteInEditMode]
    public sealed class CSGModel : ChiselNode
    {
        public override string NodeTypeName { get { return "Model"; } }


        [HideInInspector, SerializeField] CSGGeneratedColliderSettings  colliderSettings    = new CSGGeneratedColliderSettings();
        [HideInInspector, SerializeField] CSGGeneratedRenderSettings    renderSettings      = new CSGGeneratedRenderSettings();
        [HideInInspector, SerializeField] SerializableUnwrapParam       uvGenerationSettings = new SerializableUnwrapParam();
        [HideInInspector] public CSGTree Node;
        [HideInInspector] internal GeneratedMeshContents generatedMeshContents;

        // TODO: put all bools in flags (makes it harder to work with in the ModelEditor though)
        public bool CreateRenderComponents      = true;
        public bool CreateColliderComponents    = true;
        public bool AutoRebuildUVs              = true;
        public VertexChannelFlags VertexChannelMask = VertexChannelFlags.All; // NOTE: do not rename, name is used directly in CSGModelEditor



        public CSGGeneratedColliderSettings ColliderSettings { get { return colliderSettings; } }
        public CSGGeneratedRenderSettings RenderSettings { get { return renderSettings; } }
        public SerializableUnwrapParam UVGenerationSettings { get { return uvGenerationSettings; } internal set { uvGenerationSettings = value; } }

        [HideInInspector, NonSerialized]
        public readonly Dictionary<Material, List<CSGRenderComponents>>         generatedRenderComponents = new Dictionary<Material, List<CSGRenderComponents>>();
        [HideInInspector, NonSerialized]
        public readonly Dictionary<PhysicMaterial, List<CSGColliderComponents>> generatedMeshColliders    = new Dictionary<PhysicMaterial, List<CSGColliderComponents>>();
        [HideInInspector, NonSerialized]
        public readonly HashSet<Transform>                  generatedComponents = new HashSet<Transform>();
        [SerializeField] internal CSGGeneratedModelMesh[]   generatedMeshes     = new CSGGeneratedModelMesh[0];

        // TODO: make these private + properties, these show up as settable default settings when selecting CSGModel.cs in unity
        public GameObject GeneratedDataContainer { get { return generatedDataContainer; } internal set { generatedDataContainer = value; } }
        public Transform GeneratedDataTransform { get { return generatedDataTransform; } internal set { generatedDataTransform = value; } }
        [HideInInspector, SerializeField] GameObject    generatedDataContainer;
        [HideInInspector, SerializeField] Transform     generatedDataTransform;
        [HideInInspector, SerializeField] bool          initialized = false;

        public bool IsInitialized { get { return initialized; } }
        public override int NodeID { get { return Node.NodeID; } }
        public override bool CanHaveChildNodes { get { return !SkipThisNode; } }

        public override void OnInitialize()
        {
            if (!generatedDataContainer)
            {
                generatedDataContainer = CSGGeneratedComponentManager.FindContainerGameObject(this);
                if (generatedDataContainer != null)
                    generatedDataTransform = generatedDataContainer.transform;
            }

            colliderSettings = new CSGGeneratedColliderSettings();
            colliderSettings.Reset();

            renderSettings = new CSGGeneratedRenderSettings();
            renderSettings.Reset();

#if UNITY_EDITOR
            UnityEditor.UnwrapParam defaults;
            UnityEditor.UnwrapParam.SetDefaults(out defaults);
            uvGenerationSettings.angleError = defaults.angleError;
            uvGenerationSettings.areaError = defaults.areaError;
            uvGenerationSettings.hardAngle = defaults.hardAngle;
            uvGenerationSettings.packMarginPixels = defaults.packMargin * 256;
#endif

            initialized = true;
        }
        public CSGModel() : base() { }

        internal override void ClearTreeNodes(bool clearCaches = false) { Node.SetInvalid(); }
        internal override CSGTreeNode[] CreateTreeNodes()
        {
            if (Node.Valid)
                Debug.LogWarning("CSGModel already has a treeNode, but trying to create a new one?", this);
            var userID = GetInstanceID();
            Node = CSGTree.Create(userID: userID);
            return new CSGTreeNode[] { Node };
        }


        internal override void SetChildren(List<CSGTreeNode> childNodes)
        {
            if (!Node.Valid)
            {
                Debug.LogWarning("SetChildren called on a CSGModel that isn't properly initialized", this);
                return;
            }
            if (!Node.SetChildren(childNodes.ToArray()))
                Debug.LogError("Failed to assign list of children to tree node");
        }

        internal override void CollectChildNodesForParent(List<CSGTreeNode> childNodes)
        {
            // No parent can hold a model as a child, so we don't add anything
        }

        public override void SetDirty() { if (Node.Valid) Node.SetDirty(); }

        protected override void OnCleanup()
        {
            CSGGeneratedComponentManager.RemoveContainerFlags(this);
        }

        public override int GetAllTreeBrushCount()
        {
            return 0;
        }

        // Get all brushes directly contained by this CSGNode (not its children)
        public override void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes)
        {
            // A Model doesn't contain a CSGTreeBrush node
        }
        
        // TODO: cache this
        public override Bounds CalculateBounds()
        {
            var bounds = CSGHierarchyItem.EmptyBounds;
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