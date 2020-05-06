using UnityEngine;
using UnityEditor;

using System;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Chisel.Editors
{
    public sealed class ChiselModelDetails : ChiselNodeDetails<ChiselModel>
    {
        const string kModelIconName = "csg_model";

        public override GUIContent GetHierarchyIcon(ChiselModel node)
        {
            return ChiselEditorResources.GetIconContent(kModelIconName, node.NodeTypeName)[0];
        }

        public override bool HasValidState(ChiselModel node)
        {
            return node.HasValidState();
        }
    }

    [CustomEditor(typeof(ChiselModel))]
    [CanEditMultipleObjects]
    public sealed class ChiselModelEditor : ChiselNodeEditor<ChiselModel>
    {
        const string kModelHasNoChildren = "This model has no chisel nodes as children and will not generate any geometry.\nAdd some chisel nodes to see something.";

        [MenuItem("GameObject/Chisel/Create/" + ChiselModel.kNodeTypeName, false, 0)]
        internal static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselModel.kNodeTypeName); }


        [ContextMenu("Set Active Model", false)]
        internal static void SetActiveModel(MenuCommand menuCommand)
        {
            var model = (menuCommand.context as GameObject).GetComponent<ChiselModel>();
            if (model)
                ChiselModelManager.ActiveModel = model;
        }

        [ContextMenu("Set Active Model", true)]
        internal static bool ValidateActiveModel(MenuCommand menuCommand)
        {
            var model = (menuCommand.context as GameObject).GetComponent<ChiselModel>();
            return model;
        }

        static readonly GUIContent LightingContent                        = new GUIContent("Lighting");
        static readonly GUIContent ProbesContent                          = new GUIContent("Probes");
        static readonly GUIContent AdditionalSettingsContent              = new GUIContent("Additional Settings");
        static readonly GUIContent GenerationSettingsContent              = new GUIContent("Geometry Output");
        static readonly GUIContent ColliderSettingsContent                = new GUIContent("Collider");
        static readonly GUIContent CreateRenderComponentsContents         = new GUIContent("Renderable");
        static readonly GUIContent CreateColliderComponentsContents       = new GUIContent("Collidable");
        static readonly GUIContent UnwrapParamsContents                   = new GUIContent("UV Generation");

        static readonly GUIContent ForceBuildUVsContents                  = new GUIContent("Build", "Manually build lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent ForceRebuildUVsContents                = new GUIContent("Rebuild", "Manually rebuild lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent AutoRebuildUVsContents                 = new GUIContent("Auto UV Generation", "Automatically lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent NeedsLightmapBuildContents             = new GUIContent("In order for lightmapping to work properly the lightmap UVs need to be build.");
        static readonly GUIContent NeedsLightmapRebuildContents           = new GUIContent("In order for lightmapping to work properly the lightmap UVs need to be rebuild.");

        static readonly GUIContent MotionVectorsContent                   = new GUIContent("Motion Vectors", "Specifies whether the Model renders 'Per Object Motion', 'Camera Motion', or 'No Motion' vectors to the Camera Motion Vector Texture.");
        static readonly GUIContent LightmappingContents                   = new GUIContent("Lightmapping");
        static readonly GUIContent GINotEnabledInfoContents               = new GUIContent("Lightmapping settings are currently disabled. Enable Baked Global Illumination or Realtime Global Illumination to display these settings.");
        static readonly GUIContent UVChartingContents                     = new GUIContent("UV Charting Control");
        static readonly GUIContent ImportantGIContents                    = new GUIContent("Prioritize Illumination", "When enabled, the object will be marked as a priority object and always included in lighting calculations. Useful for objects that will be strongly emissive to make sure that other objects will be illuminated by this object.");
        static readonly GUIContent ScaleInLightmapContents                = new GUIContent("Scale In Lightmap", "Specifies the relative size of object's UVs within a lightmap. A value of 0 will result in the object not being light mapped, but still contribute lighting to other objects in the Scene.");
        static readonly GUIContent OptimizeRealtimeUVsContents            = new GUIContent("Optimize Realtime UVs", "Specifies whether the generated model UVs get optimized for Realtime Global Illumination or not. When enabled, the UVs can get merged, scaled, and packed for optimization purposes. When disabled, the UVs will get scaled and packed, but not merged.");
        static readonly GUIContent AutoUVMaxDistanceContents              = new GUIContent("Max Distance", "Specifies the maximum worldspace distance to be used for UV chart simplification. If charts are within this distance they will be simplified for optimization purposes.");
        static readonly GUIContent AutoUVMaxAngleContents                 = new GUIContent("Max Angle", "Specifies the maximum angle in degrees between faces sharing a UV edge. If the angle between the faces is below this value, the UV charts will be simplified.");
        static readonly GUIContent IgnoreNormalsForChartDetectionContents = new GUIContent("Ignore Normals", "When enabled, prevents the UV charts from being split during the precompute process for Realtime Global Illumination lighting.");
        static readonly GUIContent LightmapParametersContents             = new GUIContent("Lightmap Parameters", "Allows the adjustment of advanced parameters that affect the process of generating a lightmap for an object using global illumination.");
        static readonly GUIContent DynamicOccludeeContents                = new GUIContent("Dynamic Occluded", "Controls if dynamic occlusion culling should be performed for this model.");
        static readonly GUIContent ProbeAnchorContents                    = new GUIContent("Anchor Override", "Specifies the Transform ` that will be used for sampling the light probes and reflection probes.");
        static readonly GUIContent ReflectionProbeUsageContents           = new GUIContent("Reflection Probes", "Specifies if or how the object is affected by reflections in the Scene. This property cannot be disabled in deferred rendering modes.");
        static readonly GUIContent LightProbeUsageContents                = new GUIContent("Light Probes", "Specifies how Light Probes will handle the interpolation of lighting and occlusion. Disabled if the object is set to Lightmap Static.");
        static readonly GUIContent LightProbeVolumeOverrideContents       = new GUIContent("Proxy Volume Override", "If set, the Model will use the Light Probe Proxy Volume component from another GameObject.");
        static readonly GUIContent LightProbeCustomContents               = new GUIContent("The Custom Provided mode is not supported.");
        static readonly GUIContent LightProbeVolumeContents               = new GUIContent("A valid Light Probe Proxy Volume component could not be found.");
        static readonly GUIContent LightProbeVolumeUnsupportedContents    = new GUIContent("The Light Probe Proxy Volume feature is unsupported by the current graphics hardware or API configuration. Simple 'Blend Probes' mode will be used instead.");
        static readonly GUIContent RenderingLayerMaskStyle                = new GUIContent("Rendering Layer Mask", "Mask that can be used with SRP DrawRenderers command to filter renderers outside of the normal layering system.");
        static readonly GUIContent StaticBatchingWarningContents          = new GUIContent("This model is statically batched and uses an instanced shader at the same time. Instancing will be disabled in such a case. Consider disabling static batching if you want it to be instanced.");
        static readonly GUIContent NoNormalsNoLightmappingContents        = new GUIContent("VertexChannels is set to not have any normals. Normals are needed for lightmapping.");
        static readonly GUIContent LightmapInfoBoxContents                = new GUIContent("To enable generation of lightmaps for this Model, please enable the 'Lightmap Static' property.");
        static readonly GUIContent ClampedPackingResolutionContents       = new GUIContent("Object's size in the realtime lightmap has reached the maximum size. Try dividing large brushes into smaller pieces.");
        static readonly GUIContent UVOverlapContents                      = new GUIContent("This model has overlapping UVs. This is caused by Unity's own code.");
        static readonly GUIContent ClampedSizeContents                    = new GUIContent("Object's size in lightmap has reached the max atlas size.", "If you need higher resolution for this object, try dividing large brushes into smaller pieces or set higher max atlas size via the LightmapEditorSettings class.");
        static readonly GUIContent IsTriggerContents                      = new GUIContent("Is Trigger", "Is this model a trigger? Triggers are only supported on convex models.");
        static readonly GUIContent ConvextContents                        = new GUIContent("Convex", "Create a convex collider for this model?");
        static readonly GUIContent VertexChannelMaskContents              = new GUIContent("Vertex Channel Mask", "Select which vertex channels will be used in the generated meshes");
        static readonly GUIContent SkinWidthContents                      = new GUIContent("Skin Width", "How far out to inflate the mesh when building collision mesh.");
        static readonly GUIContent CookingOptionsContents                 = new GUIContent("Cooking Options", "Options affecting the result of the mesh processing by the physics engine.");
        static readonly GUIContent DefaultModelContents                   = new GUIContent("This model is the default model, all nodes that are not part of a model are automatically added to this model.");
        static readonly GUIContent StitchLightmapSeamsContents            = new GUIContent("Stitch Seams", "When enabled, seams in baked lightmaps will get smoothed.");
        static readonly GUIContent ContributeGIContents                   = new GUIContent("Contribute Global Illumination", "When enabled, this GameObject influences lightmaps and Light Probes. If you want this object itself to be lightmapped, you must enable this property.");
        static readonly GUIContent MinimumChartSizeContents               = new GUIContent("Min Chart Size", "Specifies the minimum texel size used for a UV chart. If stitching is required, a value of 4 will create a chart of 4x4 texels to store lighting and directionality. If stitching is not required, a value of 2 will reduce the texel density and provide better lighting build times and run time performance.");
        static readonly GUIContent ReceiveGITitle                         = new GUIContent("Receive Global Illumination", "If enabled, this GameObject receives global illumination from lightmaps or Light Probes. To use lightmaps, Contribute Global Illumination must be enabled.");
        
        static readonly int[] MinimumChartSizeValues        = { 2, 4 };
        static readonly GUIContent[] MinimumChartSizeStrings =
        {
            new GUIContent("2 (Minimum)"),
            new GUIContent("4 (Stitchable)"),
        };

        public static readonly int[]        ReceiveGILightmapValues = { (int)ReceiveGI.Lightmaps, (int)ReceiveGI.LightProbes };
        public static readonly GUIContent[] ReceiveGILightmapStrings =
        {
                new GUIContent("Lightmaps"),
                new GUIContent("Light Probes")
            };


        static GUIContent[] ReflectionProbeUsageOptionsContents;



        const string kDisplayLightingKey            = "ChiselModelEditor.ShowLightingSettings";
        const string kDisplayProbesKey              = "ChiselModelEditor.ShowProbeSettings";
        const string kDisplayAdditionalSettingsKey  = "ChiselModelEditor.ShowAdditionalSettings";
        const string kDisplayGenerationSettingsKey  = "ChiselModelEditor.ShowGenerationSettings";
        const string kDisplayColliderSettingsKey    = "ChiselModelEditor.ShowColliderSettings";
        const string kDisplayLightmapKey            = "ChiselModelEditor.ShowLightmapSettings";
        const string kDisplayChartingKey            = "ChiselModelEditor.ShowChartingSettings";
        const string kDisplayUnwrapParamsKey        = "ChiselModelEditor.ShowUnwrapParams";


        SerializedProperty vertexChannelMaskProp;
        SerializedProperty createRenderComponentsProp;
        SerializedProperty createColliderComponentsProp;
        SerializedProperty autoRebuildUVsProp;
        SerializedProperty angleErrorProp;
        SerializedProperty areaErrorProp;
        SerializedProperty hardAngleProp;
        SerializedProperty packMarginPixelsProp;
        SerializedProperty motionVectorsProp;
        SerializedProperty importantGIProp;
        SerializedProperty receiveGIProp;
        SerializedProperty lightmapScaleProp;
        SerializedProperty preserveUVsProp;
        SerializedProperty autoUVMaxDistanceProp;
        SerializedProperty ignoreNormalsForChartDetectionProp;
        SerializedProperty autoUVMaxAngleProp;
        SerializedProperty minimumChartSizeProp;
        SerializedProperty lightmapParametersProp;
        SerializedProperty allowOcclusionWhenDynamicProp;
        SerializedProperty renderingLayerMaskProp;
        SerializedProperty reflectionProbeUsageProp;
        SerializedProperty lightProbeUsageProp;
        SerializedProperty lightProbeVolumeOverrideProp;
        SerializedProperty probeAnchorProp;
        SerializedProperty stitchLightmapSeamsProp;

        SerializedObject gameObjectsSerializedObject;
        SerializedProperty staticEditorFlagsProp;


        SerializedProperty convexProp;
        SerializedProperty isTriggerProp;
        SerializedProperty cookingOptionsProp;
        SerializedProperty skinWidthProp;

        bool showLighting;
        bool showProbes;
        bool showAdditionalSettings;
        bool showGenerationSettings;
        bool showColliderSettings;
        bool showLightmapSettings;
        bool showChartingSettings;
        bool showUnwrapParams;


        delegate bool LightmapParametersGUIDelegate(SerializedProperty prop, GUIContent content);
        delegate float GetCachedMeshSurfaceAreaDelegate(MeshRenderer meshRenderer);
        delegate bool HasClampedResolutionDelegate(Renderer renderer);
        delegate bool HasUVOverlapsDelegate(Renderer renderer);
        delegate bool HasInstancingDelegate(Shader s);

#if UNITY_2020_1_OR_NEWER
        static LightmapParametersGUIDelegate	LightmapParametersGUI   = ReflectionExtensions.CreateDelegate<LightmapParametersGUIDelegate>("UnityEditor.SharedLightingSettingsEditor", "LightmapParametersGUI");
        static HasClampedResolutionDelegate     HasClampedResolution    = typeof(Lightmapping).CreateDelegate<HasClampedResolutionDelegate>("HasClampedResolution");
        static HasUVOverlapsDelegate            HasUVOverlaps           = typeof(Lightmapping).CreateDelegate<HasUVOverlapsDelegate>("HasUVOverlaps");
#else
        static LightmapParametersGUIDelegate    LightmapParametersGUI   = ReflectionExtensions.CreateDelegate<LightmapParametersGUIDelegate>("UnityEditor.LightingSettingsInspector", "LightmapParametersGUI");
        static HasClampedResolutionDelegate     HasClampedResolution    = typeof(LightmapEditorSettings).CreateDelegate<HasClampedResolutionDelegate>("HasClampedResolution");
        static HasUVOverlapsDelegate            HasUVOverlaps           = typeof(LightmapEditorSettings).CreateDelegate<HasUVOverlapsDelegate>("HasUVOverlaps");
#endif
        static GetCachedMeshSurfaceAreaDelegate GetCachedMeshSurfaceArea    = ReflectionExtensions.CreateDelegate<GetCachedMeshSurfaceAreaDelegate>("UnityEditor.InternalMeshUtil", "GetCachedMeshSurfaceArea");
        static HasInstancingDelegate            HasInstancing               = typeof(ShaderUtil).CreateDelegate<HasInstancingDelegate>("HasInstancing");
         
        internal void OnEnable()
        {
            if (ReflectionProbeUsageOptionsContents == null)
                ReflectionProbeUsageOptionsContents = (Enum.GetNames(typeof(ReflectionProbeUsage)).Select(x => ObjectNames.NicifyVariableName(x)).ToArray()).Select(x => new GUIContent(x)).ToArray();

            showLighting            = EditorPrefs.GetBool(kDisplayLightingKey, false);
            showProbes              = EditorPrefs.GetBool(kDisplayProbesKey, false);
            showAdditionalSettings  = EditorPrefs.GetBool(kDisplayAdditionalSettingsKey, false);
            showGenerationSettings  = SessionState.GetBool(kDisplayGenerationSettingsKey, false);
            showColliderSettings    = SessionState.GetBool(kDisplayColliderSettingsKey, false);
            showLightmapSettings    = SessionState.GetBool(kDisplayLightmapKey, true);
            showChartingSettings    = SessionState.GetBool(kDisplayChartingKey, true);
            showUnwrapParams        = SessionState.GetBool(kDisplayUnwrapParamsKey, true);

            if (!target)
                return;

            vertexChannelMaskProp        = serializedObject.FindProperty($"{ChiselModel.kVertexChannelMaskName}");
            createRenderComponentsProp   = serializedObject.FindProperty($"{ChiselModel.kCreateRenderComponentsName}");
            createColliderComponentsProp = serializedObject.FindProperty($"{ChiselModel.kCreateColliderComponentsName}");
            autoRebuildUVsProp           = serializedObject.FindProperty($"{ChiselModel.kAutoRebuildUVsName}");
            angleErrorProp               = serializedObject.FindProperty($"{ChiselModel.kUVGenerationSettingsName}.{SerializableUnwrapParam.kAngleErrorName}");
            areaErrorProp                = serializedObject.FindProperty($"{ChiselModel.kUVGenerationSettingsName}.{SerializableUnwrapParam.kAreaErrorName}");
            hardAngleProp                = serializedObject.FindProperty($"{ChiselModel.kUVGenerationSettingsName}.{SerializableUnwrapParam.kHardAngleName}");
            packMarginPixelsProp         = serializedObject.FindProperty($"{ChiselModel.kUVGenerationSettingsName}.{SerializableUnwrapParam.kPackMarginPixelsName}");


            motionVectorsProp                   = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kMotionVectorGenerationModeName}");
            importantGIProp                     = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kImportantGIName}");
            receiveGIProp                       = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kReceiveGIName}");
            lightmapScaleProp                   = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kScaleInLightmapName}");
            preserveUVsProp                     = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kOptimizeUVsName}");
            autoUVMaxDistanceProp               = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kAutoUVMaxDistanceName}");
            autoUVMaxAngleProp                  = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kAutoUVMaxAngleName}");
            ignoreNormalsForChartDetectionProp  = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kIgnoreNormalsForChartDetectionName}");
            minimumChartSizeProp                = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kMinimumChartSizeName}");
            lightmapParametersProp              = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kLightmapParametersName}");
            allowOcclusionWhenDynamicProp       = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kAllowOcclusionWhenDynamicName}");
            renderingLayerMaskProp              = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kRenderingLayerMaskName}");
            reflectionProbeUsageProp            = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kReflectionProbeUsageName}");
            lightProbeUsageProp                 = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kLightProbeUsageName}");
            lightProbeVolumeOverrideProp        = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kLightProbeVolumeOverrideName}");
            probeAnchorProp                     = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kProbeAnchorName}");
            stitchLightmapSeamsProp             = serializedObject.FindProperty($"{ChiselModel.kRenderSettingsName}.{ChiselGeneratedRenderSettings.kStitchLightmapSeamsName}");

            convexProp          = serializedObject.FindProperty($"{ChiselModel.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kConvexName}");
            isTriggerProp       = serializedObject.FindProperty($"{ChiselModel.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kIsTriggerName}");
            cookingOptionsProp  = serializedObject.FindProperty($"{ChiselModel.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kCookingOptionsName}");
            skinWidthProp       = serializedObject.FindProperty($"{ChiselModel.kColliderSettingsName}.{ChiselGeneratedColliderSettings.kSkinWidthName}");

            gameObjectsSerializedObject = new SerializedObject(serializedObject.targetObjects.Select(t => ((Component)t).gameObject).ToArray());
            staticEditorFlagsProp = gameObjectsSerializedObject.FindProperty("m_StaticEditorFlags");


            for (int t = 0; t < targets.Length; t++)
            {
                var modelTarget = targets[t] as ChiselModel;
                if (!modelTarget)
                    continue;

                if (!modelTarget.IsInitialized)
                    modelTarget.OnInitialize();
            }

            ChiselEditGeneratorTool.OnEditSettingsGUI = OnEditSettingsGUI;
            ChiselEditGeneratorTool.CurrentEditorName = "Model";
        }

        internal void OnDisable()
        {
            ChiselEditGeneratorTool.OnEditSettingsGUI = null;
            ChiselEditGeneratorTool.CurrentEditorName = null;
        }


        bool IsPrefabAsset
        {
            get
            {
                if (serializedObject == null || serializedObject.targetObject == null)
                    return false;

                var type = PrefabUtility.GetPrefabAssetType(serializedObject.targetObject);
                return (type == PrefabAssetType.Regular || type == PrefabAssetType.Model);
            }
        }

        bool GIEnabled
        {
            get { return (Lightmapping.bakedGI || Lightmapping.realtimeGI) || IsPrefabAsset; }
        }

        bool ContributeGI
        {
            get
            {
                if (staticEditorFlagsProp == null)
                    return false;
                return (staticEditorFlagsProp.intValue & (int)StaticEditorFlags.ContributeGI) != 0;
            }
            set
            {
                if (gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                    return;
                SceneModeUtility.SetStaticFlags(gameObjectsSerializedObject.targetObjects, (int)StaticEditorFlags.ContributeGI, value);
            }
        }


        ReflectedInstanceProperty<int> HasMultipleDifferentValuesBitwise = typeof(SerializedProperty).GetProperty<int>("hasMultipleDifferentValuesBitwise");

        bool MixedGIValue
        {
            get
            {
                if (staticEditorFlagsProp == null)
                    return false;
                return (HasMultipleDifferentValuesBitwise.GetValue(staticEditorFlagsProp) & (int)StaticEditorFlags.ContributeGI) != 0;
            }
        }


        bool BatchingStatic
        {
            get
            {
                return !staticEditorFlagsProp.hasMultipleDifferentValues && ((StaticEditorFlags)staticEditorFlagsProp.intValue & StaticEditorFlags.BatchingStatic) != 0;
            }
        }

        bool ShowEnlightenSettings
        {
            get
            {
                return Lightmapping.realtimeGI || (Lightmapping.bakedGI && Lightmapping.lightingSettings.lightmapper == (LightingSettings.Lightmapper)0) || IsPrefabAsset;
            }
        }

        bool ShowProgressiveSettings
        {
            get
            {
                return (Lightmapping.bakedGI && Lightmapping.lightingSettings.lightmapper != (LightingSettings.Lightmapper)0) || IsPrefabAsset;
            }
        }

        float GetLargestCachedMeshSurfaceAreaForTargets(float defaultValue)
        {
            if (target == null || GetCachedMeshSurfaceArea == null)
                return defaultValue;
            float largestSurfaceArea = -1;
            foreach(var target in targets)
            {
                var model = target as ChiselModel;
                if (!model)
                    continue;
                var renderComponents = model.generated.renderComponents;
                for (int r = 0; r < renderComponents.Count; r++)
                {
                    var meshRenderer = renderComponents[r].meshRenderer;
                    if (!meshRenderer)
                        continue;
                    largestSurfaceArea = Mathf.Max(largestSurfaceArea, GetCachedMeshSurfaceArea(meshRenderer));
                }
            }
            if (largestSurfaceArea >= 0)
                return largestSurfaceArea;
            return defaultValue;
        }

        bool TargetsHaveClampedResolution()
        {
            if (target == null || HasClampedResolution == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as ChiselModel;
                if (!model)
                    continue;
                var renderComponents = model.generated.renderComponents;
                for (int r = 0; r < renderComponents.Count; r++)
                {
                    var meshRenderer = renderComponents[r].meshRenderer;
                    if (!meshRenderer)
                        continue;
                    if (HasClampedResolution(meshRenderer))
                        return true;
                }
            }
            return false;
        }

        bool TargetsHaveUVOverlaps()
        {
            if (target == null || HasUVOverlaps == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as ChiselModel;
                if (!model)
                    continue;
                var renderComponents = model.generated.renderComponents;
                for (int r = 0; r < renderComponents.Count; r++)
                {
                    var meshRenderer = renderComponents[r].meshRenderer;
                    if (!meshRenderer)
                        continue;
                    if (HasUVOverlaps(meshRenderer))
                        return true;
                }
            }
            return false;
        }

        bool TargetsUseInstancingShader()
        {
            if (target == null || HasInstancing == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as ChiselModel;
                if (!model)
                    continue;
                var renderComponents = model.generated.materials;
                foreach (var material in renderComponents)
                {
                    if (material != null && material.enableInstancing && material.shader != null && HasInstancing(material.shader))
                        return true;
                }
            }
            return false;
        }

        bool NeedLightmapRebuild()
        {
            if (target == null)
                return false;

            foreach(var target in targets)
            {
                var model = target as ChiselModel;
                if (!model)
                    continue;

                if (ChiselGeneratedComponentManager.NeedUVGeneration(model))
                    return true;
            }
            return false;
        }

        internal bool IsUsingLightProbeProxyVolume()
        {
            bool isUsingLightProbeVolumes =
                ((targets.Length == 1) && (lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume)) ||
                ((targets.Length > 1) && !lightProbeUsageProp.hasMultipleDifferentValues && (lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume));

            return isUsingLightProbeVolumes;
        }

        internal bool HasLightProbeProxyOrOverride()
        {/*
            LightProbeProxyVolume lightProbeProxyVol = renderer.GetComponent<LightProbeProxyVolume>();
            bool invalidProxyVolumeOverride = (renderer.lightProbeProxyVolumeOverride == null) ||
                (renderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>() == null);
            */
            return false;
            // TODO: figure out how to set up LightProxyVolumes
            /*
            var lightProbeProxyVol = renderer.GetComponent<LightProbeProxyVolume>();
            bool invalidProxyVolumeOverride = (renderer.lightProbeProxyVolumeOverride == null) ||
                                                  (renderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>() == null);
            return lightProbeProxyVol == null && invalidProxyVolumeOverride;
            */
        }

        static internal bool AreLightProbesAllowed(ChiselModel model)
        {
            // TODO: return false if lightmapped or dynamic lightmapped

            /*
            if (!renderer) 
                return false;

            bool isLightmapped = IsLightmappedOrDynamicLightmappedForRendering(renderer->GetLightmapIndices());

            UInt32 lodGroupIndex;
            UInt8 lodMask;
            renderer->GetLODGroupIndexAndMask(&lodGroupIndex, &lodMask);

            return (isLightmapped == false) || ((lodMask & ~1) && GetLightmapSettings().GetGISettings().GetEnableRealtimeLightmaps());
            */
            return true;
        }

        internal bool AreLightProbesAllowed()
        {
            if (targets == null)
                return false;

            bool lightmapStatic = ContributeGI;

            if (lightmapStatic)
                return false;

            foreach (UnityEngine.Object obj in targets)
                if (AreLightProbesAllowed((ChiselModel)obj) == false)
                    return false;
            return true;
        }

        //int selectionCount, Renderer renderer, 
        internal void RenderLightProbeUsage(bool lightProbeAllowed)
        {
            using (new EditorGUI.DisabledScope(!lightProbeAllowed))
            {
                if (lightProbeAllowed)
                {
                    // LightProbeUsage has non-sequential enum values. Extra care is to be taken.
                    Rect r = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.popup);
                    EditorGUI.BeginProperty(r, LightProbeUsageContents, lightProbeUsageProp);
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.EnumPopup(r, LightProbeUsageContents, (LightProbeUsage)lightProbeUsageProp.intValue);
                    if (EditorGUI.EndChangeCheck())
                        lightProbeUsageProp.intValue = (int)(LightProbeUsage)newValue;
                    EditorGUI.EndProperty();

                    if (!lightProbeUsageProp.hasMultipleDifferentValues)
                    {
                        if (SupportedRenderingFeatures.active.lightProbeProxyVolumes &&
                            lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(lightProbeVolumeOverrideProp, LightProbeVolumeOverrideContents);
                            EditorGUI.indentLevel--;
                        } else 
                        if (lightProbeUsageProp.intValue == (int)LightProbeUsage.CustomProvided)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.HelpBox(LightProbeCustomContents.text, MessageType.Error);
                            EditorGUI.indentLevel--;
                        }
                    }
                } else
                {
                    EditorGUILayout.EnumPopup(LightProbeUsageContents, LightProbeUsage.Off);
                }
            }
        }

        internal void RenderLightProbeProxyVolumeWarningNote()
        {
            if (IsUsingLightProbeProxyVolume())
            {
                if (SupportedRenderingFeatures.active.lightProbeProxyVolumes &&
                    LightProbeProxyVolume.isFeatureSupported)
                {
                    bool hasLightProbeProxyOrOverride = HasLightProbeProxyOrOverride();
                    if (hasLightProbeProxyOrOverride && AreLightProbesAllowed())
                    {
                        EditorGUILayout.HelpBox(LightProbeVolumeContents.text, MessageType.Warning);
                    }
                } else
                {
                    EditorGUILayout.HelpBox(LightProbeVolumeUnsupportedContents.text, MessageType.Warning);
                }
            }
        }

        internal void RenderReflectionProbeUsage(bool isDeferredRenderingPath, bool isDeferredReflections)
        {
            if (!SupportedRenderingFeatures.active.reflectionProbes)
                return;

            using (new EditorGUI.DisabledScope(isDeferredRenderingPath))
            {
                // reflection probe usage field; UI disabled when using deferred reflections
                if (isDeferredReflections)
                {
                    EditorGUILayout.EnumPopup(ReflectionProbeUsageContents, (reflectionProbeUsageProp.intValue != (int)ReflectionProbeUsage.Off) ? ReflectionProbeUsage.Simple : ReflectionProbeUsage.Off);
                } else
                {
                    Rect r = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.popup);
                    ChiselEditorUtility.Popup(r, reflectionProbeUsageProp, ReflectionProbeUsageOptionsContents, ReflectionProbeUsageContents);
                }
            }
        }

        internal bool RenderProbeAnchor()
        {
            bool useReflectionProbes	= !reflectionProbeUsageProp.hasMultipleDifferentValues && (ReflectionProbeUsage)reflectionProbeUsageProp.intValue != ReflectionProbeUsage.Off;
            bool lightProbesEnabled		= !lightProbeUsageProp.hasMultipleDifferentValues && (LightProbeUsage)lightProbeUsageProp.intValue != LightProbeUsage.Off;
            bool needsRendering			= useReflectionProbes || lightProbesEnabled;

            if (needsRendering)
                EditorGUILayout.PropertyField(probeAnchorProp, ProbeAnchorContents);

            return needsRendering;
        }


        void RenderProbeFieldsGUI(bool isDeferredRenderingPath)
        {
            bool isDeferredReflections = isDeferredRenderingPath && ChiselEditorUtility.IsDeferredReflections();
            bool areLightProbesAllowed = AreLightProbesAllowed();

            RenderLightProbeUsage(areLightProbesAllowed);

            RenderLightProbeProxyVolumeWarningNote();

            RenderReflectionProbeUsage(isDeferredRenderingPath, isDeferredReflections);

            RenderProbeAnchor();
        }

        float LightmapScaleGUI(float lodScale)
        {
            var lightmapScaleValue = lodScale * lightmapScaleProp.floatValue;

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, ScaleInLightmapContents, lightmapScaleProp);
            EditorGUI.BeginChangeCheck();
            {
                lightmapScaleValue = EditorGUI.FloatField(rect, ScaleInLightmapContents, lightmapScaleValue);
            }
            if (EditorGUI.EndChangeCheck())
                lightmapScaleProp.floatValue = Mathf.Max(lightmapScaleValue / Mathf.Max(lodScale, float.Epsilon), 0.0f);
            EditorGUI.EndProperty();

            return lightmapScaleValue;
        }

        void ShowClampedSizeInLightmapGUI(float lightmapScale)
        {
            var cachedSurfaceArea = GetLargestCachedMeshSurfaceAreaForTargets(defaultValue: 1.0f);
            var sizeInLightmap = Mathf.Sqrt(cachedSurfaceArea) * Lightmapping.lightingSettings.lightmapResolution * lightmapScale;
            
            if (sizeInLightmap > Lightmapping.lightingSettings.lightmapMaxSize)
                EditorGUILayout.HelpBox(ClampedSizeContents.text, MessageType.Info);
        }

        void RendererUVSettings()
        {
            EditorGUI.indentLevel++;

            var optimizeRealtimeUVs = !preserveUVsProp.boolValue;
            EditorGUI.BeginChangeCheck();
            optimizeRealtimeUVs = EditorGUILayout.Toggle(OptimizeRealtimeUVsContents, optimizeRealtimeUVs);

            if (EditorGUI.EndChangeCheck())
                preserveUVsProp.boolValue = !optimizeRealtimeUVs;

            EditorGUI.indentLevel++;
            {
                var disabledAutoUVs = preserveUVsProp.boolValue;
                using (new EditorGUI.DisabledScope(disabledAutoUVs))
                {
                    EditorGUILayout.PropertyField(autoUVMaxDistanceProp, AutoUVMaxDistanceContents);
                    if (autoUVMaxDistanceProp.floatValue < 0.0f)
                        autoUVMaxDistanceProp.floatValue = 0.0f;
                    EditorGUILayout.Slider(autoUVMaxAngleProp, 0, 180, AutoUVMaxAngleContents);
                    if (autoUVMaxAngleProp.floatValue < 0.0f)
                        autoUVMaxAngleProp.floatValue = 0.0f;
                    if (autoUVMaxAngleProp.floatValue > 180.0f)
                        autoUVMaxAngleProp.floatValue = 180.0f;
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(ignoreNormalsForChartDetectionProp, IgnoreNormalsForChartDetectionContents);

            EditorGUILayout.IntPopup(minimumChartSizeProp, MinimumChartSizeStrings, MinimumChartSizeValues, MinimumChartSizeContents);
            EditorGUI.indentLevel--;
        }

        void RenderAdditionalSettingsGUI()
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            if (SupportedRenderingFeatures.active.motionVectors && motionVectorsProp != null)
            {
                EditorGUILayout.IntPopup(motionVectorsProp, new[] { new GUIContent("Camera Motion Only"), new GUIContent("Per Object Motion"), new GUIContent("Force No Motion") }, new[] { 0, 1, 2 }, MotionVectorsContent);
            }

            if (allowOcclusionWhenDynamicProp != null)
                EditorGUILayout.PropertyField(allowOcclusionWhenDynamicProp, DynamicOccludeeContents);

            RenderRenderingLayer();
        }
        

        void RenderGenerationSettingsGUI()
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            // TODO: Make Position show up instead of "None" when nothing is selected
            ChiselEditorUtility.EnumFlagsField(VertexChannelMaskContents, vertexChannelMaskProp, typeof(VertexChannelFlags), EditorStyles.popup);
        }


        bool ContributeGISettings()
        {
            bool contributeGI   = ContributeGI;
            bool mixedValue     = MixedGIValue;
            EditorGUI.showMixedValue = mixedValue;

            EditorGUI.BeginChangeCheck();
            contributeGI = EditorGUILayout.Toggle(ContributeGIContents, contributeGI);

            if (EditorGUI.EndChangeCheck())
            {
                ContributeGI = contributeGI;
                gameObjectsSerializedObject.SetIsDifferentCacheDirty();
                gameObjectsSerializedObject.Update();
            }

            EditorGUI.showMixedValue = false;

            return contributeGI && !mixedValue;
        }

        void RenderMeshSettingsGUI(ReceiveGI receiveGI)
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            var contributeGI            = ContributeGISettings();

            var showMixedGIValue        = MixedGIValue;
            var showEnlightenSettings   = ShowEnlightenSettings;

            if (!GIEnabled)
            {
                EditorGUILayout.HelpBox(GINotEnabledInfoContents.text, MessageType.Info);
                return;
            }

            if (contributeGI)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUI.showMixedValue = showMixedGIValue;
                receiveGI = (ReceiveGI)EditorGUILayout.IntPopup(ReceiveGITitle, (int)receiveGI, ReceiveGILightmapStrings, ReceiveGILightmapValues);
                EditorGUI.showMixedValue = false;

                if (EditorGUI.EndChangeCheck())
                    receiveGIProp.intValue = (int)receiveGI;

                if (showEnlightenSettings) EditorGUILayout.PropertyField(importantGIProp, ImportantGIContents);

                if (receiveGI == ReceiveGI.LightProbes && !showMixedGIValue)
                {
                    //LightmapScaleGUI(true, AlbedoScale, true);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.showMixedValue = showMixedGIValue;
                    EditorGUILayout.IntPopup(ReceiveGITitle, (int)ReceiveGI.LightProbes, ReceiveGILightmapStrings, ReceiveGILightmapValues);
                    EditorGUI.showMixedValue = false;
                }
            }
            /*
            if (!ContributeGI)
            {
                EditorGUILayout.HelpBox(LightmapInfoBoxContents.text, MessageType.Info);
            }*/
        }

        static string[] __layerNameCache;
        static string[] LayerNames
        {
            get
            {
                if (__layerNameCache == null)
                {
                    __layerNameCache = new string[32];
                    for (int i = 0; i < __layerNameCache.Length; ++i)
                    {
                        __layerNameCache[i] = string.Format("Layer{0}", i + 1);
                    }
                }
                return __layerNameCache;
            }
        }

        void RenderRenderingLayer()
        {
            if (target == null || Tools.current != Tool.Custom || !ChiselEditGeneratorTool.IsActive())
                return;

            // TODO: why are we doing this again?
            bool usingSRP = GraphicsSettings.renderPipelineAsset != null;
            if (!usingSRP)
                return;

            EditorGUI.showMixedValue = renderingLayerMaskProp.hasMultipleDifferentValues;

            var model		= (ChiselModel)target;
            var renderer	= target;
            var mask		= (int)model.RenderSettings.renderingLayerMask;

            EditorGUI.BeginChangeCheck();

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, RenderingLayerMaskStyle, renderingLayerMaskProp);
            mask = EditorGUI.MaskField(rect, RenderingLayerMaskStyle, mask, LayerNames);
            EditorGUI.EndProperty();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Set rendering layer mask");
                foreach (var t in targets)
                {
                    var m = t as ChiselModel;
                    if (m != null)
                    {
                        m.RenderSettings.renderingLayerMask = (uint)mask;
                        EditorUtility.SetDirty(t);
                    }
                }
            }
            EditorGUI.showMixedValue = false;
        }

        delegate Texture2D GetHelpIconDelegate(MessageType type);

        GetHelpIconDelegate GetHelpIcon = ReflectionExtensions.CreateDelegate<GetHelpIconDelegate>(typeof(EditorGUIUtility), "GetHelpIcon");

        void MeshRendererLightingGUI()
        {
            var oldShowLighting				= showLighting;
            var oldShowProbes				= showProbes;
            var oldShowAdditionalSettings   = showAdditionalSettings;
            var oldShowLightmapSettings		= showLightmapSettings;
            var oldShowChartingSettings		= showChartingSettings;
            var oldShowUnwrapParams			= showUnwrapParams;

            if (TargetsUseInstancingShader())
            {

                if (BatchingStatic)
                {
                    EditorGUILayout.HelpBox(StaticBatchingWarningContents.text, MessageType.Warning, true);
                }
            }

            var receiveGI           = (ReceiveGI)receiveGIProp.intValue;
            var showMixedGIValue    = MixedGIValue;
            var haveLightmaps       = GIEnabled && ContributeGI && receiveGI == ReceiveGI.Lightmaps && !showMixedGIValue;


            bool isDeferredRenderingPath = ChiselEditorUtility.IsUsingDeferredRenderingPath();


            if (haveLightmaps)
            {
                var needLightmapRebuild = NeedLightmapRebuild();
                if (!autoRebuildUVsProp.boolValue && needLightmapRebuild)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox); 
                    var messageContents = needLightmapRebuild ? NeedsLightmapBuildContents : NeedsLightmapRebuildContents;
                    GUILayout.Label(EditorGUIUtility.TrTextContent(messageContents.text, GetHelpIcon(MessageType.Warning)), EditorStyles.wordWrappedLabel);
                    GUILayout.Space(3);
                    var buttonContents = needLightmapRebuild ? ForceBuildUVsContents : ForceRebuildUVsContents;
                    if (GUILayout.Button(buttonContents, GUILayout.ExpandWidth(false)))
                    {
                        ChiselGeneratedComponentManager.DelayedUVGeneration(force: true);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }


            showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(showLighting, LightingContent);
            if (showLighting)
            {
                EditorGUI.indentLevel++;
                RenderMeshSettingsGUI(receiveGI);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (haveLightmaps)
            {
                showUnwrapParams = EditorGUILayout.BeginFoldoutHeaderGroup(showUnwrapParams, UnwrapParamsContents);
                if (showUnwrapParams)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(autoRebuildUVsProp, AutoRebuildUVsContents);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (autoRebuildUVsProp.boolValue)
                            ChiselGeneratedComponentManager.ForceUpdateDelayedUVGeneration();
                    }

                    EditorGUILayout.PropertyField(angleErrorProp);
                    EditorGUILayout.PropertyField(areaErrorProp);
                    EditorGUILayout.PropertyField(hardAngleProp);
                    EditorGUILayout.PropertyField(packMarginPixelsProp);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                showLightmapSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showLightmapSettings, LightmappingContents);
                if (showLightmapSettings)
                {
                    EditorGUI.indentLevel++;
                    var showEnlightenSettings   = ShowEnlightenSettings;
                    var showProgressiveSettings = ShowProgressiveSettings;
                    
                    if (showEnlightenSettings)
                    {
                        showChartingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showChartingSettings, UVChartingContents);
                        if (showChartingSettings)
                            RendererUVSettings();
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }

                    float lightmapScale = LightmapScaleGUI(1.0f);

                    ShowClampedSizeInLightmapGUI(lightmapScale);

                    if (showEnlightenSettings) EditorGUILayout.PropertyField(importantGIProp, ImportantGIContents);

                    if (showProgressiveSettings && stitchLightmapSeamsProp != null)
                        EditorGUILayout.PropertyField(stitchLightmapSeamsProp, StitchLightmapSeamsContents);

                    if (LightmapParametersGUI != null && lightmapParametersProp != null)
                        LightmapParametersGUI(lightmapParametersProp, LightmapParametersContents);


                    EditorGUILayout.Space();

                    if (TargetsHaveClampedResolution())
                        EditorGUILayout.HelpBox(ClampedPackingResolutionContents.text, MessageType.Warning);

                    if ((vertexChannelMaskProp.intValue & (int)VertexChannelFlags.Normal) != (int)VertexChannelFlags.Normal)
                        EditorGUILayout.HelpBox(NoNormalsNoLightmappingContents.text, MessageType.Warning);

                    if (TargetsHaveUVOverlaps())
                        EditorGUILayout.HelpBox(UVOverlapContents.text, MessageType.Warning);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }



            showProbes = EditorGUILayout.BeginFoldoutHeaderGroup(showProbes, ProbesContent);
            if (showProbes)
            {
                EditorGUI.indentLevel++;
                RenderProbeFieldsGUI(isDeferredRenderingPath);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showAdditionalSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAdditionalSettings, AdditionalSettingsContent);
            if (showAdditionalSettings)
            {
                EditorGUI.indentLevel++;
                RenderAdditionalSettingsGUI();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            serializedObject.ApplyModifiedProperties();


            if (showLighting            != oldShowLighting          ) EditorPrefs.SetBool(kDisplayLightingKey, showLighting);
            if (showProbes              != oldShowProbes            ) EditorPrefs.SetBool(kDisplayProbesKey, showProbes);
            if (showAdditionalSettings  != oldShowAdditionalSettings) EditorPrefs.SetBool(kDisplayAdditionalSettingsKey, showAdditionalSettings);
            if (showLightmapSettings    != oldShowLightmapSettings  ) SessionState.SetBool(kDisplayLightmapKey, showLightmapSettings);
            if (showChartingSettings    != oldShowChartingSettings  ) SessionState.SetBool(kDisplayChartingKey, showChartingSettings);
            if (showUnwrapParams        != oldShowUnwrapParams      ) SessionState.SetBool(kDisplayUnwrapParamsKey, showUnwrapParams);
        }

        bool IsDefaultModel()
        {
            if (serializedObject.targetObjects == null)
                return false;

            for (int i = 0; i < serializedObject.targetObjects.Length; i++)
            {
                if (ChiselGeneratedComponentManager.IsDefaultModel(serializedObject.targetObjects[i]))
                    return true;
            }
            return false;
        }
        

        protected override void OnEditSettingsGUI(SceneView sceneView)
        {
            if (Tools.current != Tool.Custom)
                return;


        }

        public override void OnInspectorGUI()
        {
            CheckForTransformationChanges(serializedObject);
            
            var oldShowGenerationSettings   = showGenerationSettings;
            var oldShowColliderSettings     = showColliderSettings;

            if (gameObjectsSerializedObject != null) gameObjectsSerializedObject.Update();
            if (serializedObject != null) serializedObject.Update();

            if (IsDefaultModel())
                EditorGUILayout.HelpBox(DefaultModelContents.text, MessageType.Warning);

            bool hasNoChildren = false;
            foreach (var target in serializedObject.targetObjects)
            {
                var operation = target as ChiselModel;
                if (!operation)
                    continue;
                if (operation.transform.childCount == 0)
                {
                    hasNoChildren = true;
                }
            }
            if (hasNoChildren)
            {
                EditorGUILayout.HelpBox(kModelHasNoChildren, MessageType.Warning, true);
            }

            EditorGUI.BeginChangeCheck();
            {
                showGenerationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showGenerationSettings, GenerationSettingsContent);
                if (showGenerationSettings)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(createColliderComponentsProp, CreateColliderComponentsContents);
                    EditorGUILayout.PropertyField(createRenderComponentsProp, CreateRenderComponentsContents);

                    EditorGUI.BeginDisabledGroup(!createRenderComponentsProp.boolValue);
                    {
                        RenderGenerationSettingsGUI();
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                if (createRenderComponentsProp.boolValue)
                {
                    MeshRendererLightingGUI();
                }

                if (createColliderComponentsProp.boolValue)
                {
                    showColliderSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showColliderSettings, ColliderSettingsContent);
                    if (showColliderSettings)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(convexProp, ConvextContents);
                        using (new EditorGUI.DisabledScope(!convexProp.boolValue))
                        {
                            EditorGUILayout.PropertyField(isTriggerProp, IsTriggerContents);
                        }
                        {
                            ChiselEditorUtility.EnumFlagsField(CookingOptionsContents, cookingOptionsProp, typeof(MeshColliderCookingOptions), EditorStyles.popup);
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (gameObjectsSerializedObject != null)
                    gameObjectsSerializedObject.ApplyModifiedProperties();
                if (serializedObject != null)
                    serializedObject.ApplyModifiedProperties();
                ForceUpdateNodeContents(serializedObject); 
            }
            
            if (showGenerationSettings  != oldShowGenerationSettings) SessionState.SetBool(kDisplayGenerationSettingsKey, showGenerationSettings);
            if (showColliderSettings    != oldShowColliderSettings  ) SessionState.SetBool(kDisplayColliderSettingsKey, showColliderSettings);
        }
    }
}
