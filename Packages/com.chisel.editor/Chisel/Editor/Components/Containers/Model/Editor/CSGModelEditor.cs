using UnityEngine;
using UnityEditor;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEngine.Rendering;

namespace Chisel.Editors
{
    [CustomEditor(typeof(CSGModel))]
    [CanEditMultipleObjects]
    public sealed class CSGModelEditor : Editor
    {
        const int kSingleLineHeight = 16;

        static readonly int RebuildButtonHashCode = "rebuild".GetHashCode();

        static readonly GUIContent	LightingContent							= new GUIContent("Lighting");
        static readonly GUIContent	CreateRenderComponentsContents			= new GUIContent("Create Render Components");
        static readonly GUIContent	CreateColliderComponentsContents		= new GUIContent("Create Collider Components");
        static readonly GUIContent	UnwrapParamsContents					= new GUIContent("UV Generation Settings");

        static readonly GUIContent	LightmapUVsContents						= new GUIContent("Lightmap UV", "Manually rebuild lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent  ForceBuildUVsContents					= new GUIContent("Build", "Manually build lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent	ForceRebuildUVsContents                 = new GUIContent("Rebuild", "Manually rebuild lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent	AutoRebuildUVsContents                  = new GUIContent("Auto UV Generation", "Automatically lightmap UVs for generated meshes. This operation can be slow for more complicated meshes");
        static readonly GUIContent  NeedsLightmapRebuildContents			= new GUIContent("The lightmap UVs need to be rebuild in order for lightmapping to work properly.");

        static readonly GUIContent	MotionVectorsContent					= new GUIContent("Motion Vectors", "Specifies whether the Model renders 'Per Object Motion', 'Camera Motion', or 'No Motion' vectors to the Camera Motion Vector Texture.");
        static readonly GUIContent	LightmapSettingContents					= new GUIContent("Lightmap Settings");
        static readonly GUIContent	GINotEnabledInfoContents				= new GUIContent("Lightmapping settings are currently disabled. Enable Baked Global Illumination or Realtime Global Illumination to display these settings.");
        static readonly GUIContent	UVChartingContents						= new GUIContent("UV Charting Control");
        static readonly GUIContent	ImportantGIContents						= new GUIContent("Prioritize Illumination", "When enabled, the object will be marked as a priority object and always included in lighting calculations. Useful for objects that will be strongly emissive to make sure that other objects will be illuminated by this object.");
        static readonly GUIContent	LightmapStaticContents					= new GUIContent("Lightmap Static", "Controls whether the geometry will be marked as Static for lightmapping purposes. When enabled, this model will be present in lightmap calculations.");
        static readonly GUIContent	ScaleInLightmapContents					= new GUIContent("Scale In Lightmap", "Specifies the relative size of object's UVs within a lightmap. A value of 0 will result in the object not being light mapped, but still contribute lighting to other objects in the Scene.");
        static readonly GUIContent	OptimizeRealtimeUVsContents				= new GUIContent("Optimize Realtime UVs", "Specifies whether the generated model UVs get optimized for Realtime Global Illumination or not. When enabled, the UVs can get merged, scaled, and packed for optimization purposes. When disabled, the UVs will get scaled and packed, but not merged.");
        static readonly GUIContent	AutoUVMaxDistanceContents				= new GUIContent("Max Distance", "Specifies the maximum worldspace distance to be used for UV chart simplification. If charts are within this distance they will be simplified for optimization purposes.");
        static readonly GUIContent	AutoUVMaxAngleContents					= new GUIContent("Max Angle", "Specifies the maximum angle in degrees between faces sharing a UV edge. If the angle between the faces is below this value, the UV charts will be simplified.");
        static readonly GUIContent	IgnoreNormalsForChartDetectionContents	= new GUIContent("Ignore Normals", "When enabled, prevents the UV charts from being split during the precompute process for Realtime Global Illumination lighting.");
        static readonly GUIContent	LightmapParametersContents				= new GUIContent("Lightmap Parameters", "Allows the adjustment of advanced parameters that affect the process of generating a lightmap for an object using global illumination.");
        static readonly GUIContent	DynamicOccludeeContents					= new GUIContent("Dynamic Occluded", "Controls if dynamic occlusion culling should be performed for this model.");
        static readonly GUIContent	ProbeAnchorContents						= new GUIContent("Anchor Override", "Specifies the Transform ` that will be used for sampling the light probes and reflection probes.");
        static readonly GUIContent	ReflectionProbeUsageContents			= new GUIContent("Reflection Probes", "Specifies if or how the object is affected by reflections in the Scene. This property cannot be disabled in deferred rendering modes.");
        static readonly GUIContent	LightProbeUsageContents					= new GUIContent("Light Probes", "Specifies how Light Probes will handle the interpolation of lighting and occlusion. Disabled if the object is set to Lightmap Static.");
        static readonly GUIContent	LightProbeVolumeOverrideContents		= new GUIContent("Proxy Volume Override", "If set, the Model will use the Light Probe Proxy Volume component from another GameObject.");
        static readonly GUIContent	LightProbeCustomContents				= new GUIContent("The Custom Provided mode is not supported.");
        static readonly GUIContent	LightProbeVolumeContents				= new GUIContent("A valid Light Probe Proxy Volume component could not be found.");
        static readonly GUIContent	LightProbeVolumeUnsupportedContents		= new GUIContent("The Light Probe Proxy Volume feature is unsupported by the current graphics hardware or API configuration. Simple 'Blend Probes' mode will be used instead.");
        static readonly GUIContent	RenderingLayerMaskStyle                 = new GUIContent("Rendering Layer Mask", "Mask that can be used with SRP DrawRenderers command to filter renderers outside of the normal layering system.");
        static readonly GUIContent	StaticBatchingWarningContents			= new GUIContent("This model is statically batched and uses an instanced shader at the same time. Instancing will be disabled in such a case. Consider disabling static batching if you want it to be instanced.");
        static readonly GUIContent	NoNormalsNoLightmappingContents         = new GUIContent("VertexChannels is set to not have any normals. Normals are needed for lightmapping.");
        static readonly GUIContent	LightmapInfoBoxContents                 = new GUIContent("To enable generation of lightmaps for this Model, please enable the 'Lightmap Static' property.");
        static readonly GUIContent	ClampedPackingResolutionContents        = new GUIContent("Object's size in the realtime lightmap has reached the maximum size. Try dividing large brushes into smaller pieces.");
        static readonly GUIContent	UVOverlapContents                       = new GUIContent("This model has overlapping UVs. This is caused by Unity's own code.");
        static readonly GUIContent	ClampedSizeContents                     = new GUIContent("Object's size in lightmap has reached the max atlas size.", "If you need higher resolution for this object, try dividing large brushes into smaller pieces or set higher max atlas size via the LightmapEditorSettings class.");
        static readonly GUIContent	IsTriggerContents						= new GUIContent("Is Trigger", "Is this model a trigger? Triggers are only supported on convex models.");
        static readonly GUIContent	ConvextContents							= new GUIContent("Convex", "Create a convex collider for this model?");
        static readonly GUIContent  VertexChannelMaskContents               = new GUIContent("Vertex Channel Mask", "Select which vertex channels will be used in the generated meshes");
        static readonly GUIContent	SkinWidthContents						= new GUIContent("Skin Width", "How far out to inflate the mesh when building collision mesh.");
        static readonly GUIContent	CookingOptionsContents					= new GUIContent("Cooking Options", "Options affecting the result of the mesh processing by the physics engine.");
        static readonly GUIContent	DefaultModelContents					= new GUIContent("This model is the default model, all nodes that are not part of a model are automatically added to this model.");
        
        static readonly GUIContent	MinimumChartSizeContents				= new GUIContent("Min Chart Size", "Specifies the minimum texel size used for a UV chart. If stitching is required, a value of 4 will create a chart of 4x4 texels to store lighting and directionality. If stitching is not required, a value of 2 will reduce the texel density and provide better lighting build times and run time performance.");
        static readonly int[]		MinimumChartSizeValues	= { 2, 4 };
        static readonly GUIContent[] MinimumChartSizeStrings =
        {
            new GUIContent("2 (Minimum)"),
            new GUIContent("4 (Stitchable)"),
        };
        
        static GUIContent[] ReflectionProbeUsageOptionsContents;


#if UNITY_2017_2_OR_ABOVE
        static GUIContent   StitchLightmapSeamsContents             = new GUIContent("Stitch Seams", "When enabled, seams in baked lightmaps will get smoothed.");
#endif

        const string kDisplayLightingKey			= "CSGModelEditor.ShowSettings";
        const string kDisplayLightmapKey			= "CSGModelEditor.ShowLightmapSettings";
        const string kDisplayChartingKey			= "CSGModelEditor.ShowChartingSettings";
        const string kDisplayUnwrapParamsKey		= "CSGModelEditor.ShowUnwrapParams";


        SerializedProperty  vertexChannelMaskProp;
        SerializedProperty	createRenderComponentsProp;
        SerializedProperty	createColliderComponentsProp;
        SerializedProperty  autoRebuildUVsProp;
        SerializedProperty  angleErrorProp;
        SerializedProperty  areaErrorProp;
        SerializedProperty  hardAngleProp;
        SerializedProperty  packMarginPixelsProp;
        SerializedProperty	motionVectorsProp;
        SerializedProperty	importantGIProp;
        SerializedProperty  lightmapScaleProp;
        SerializedProperty  preserveUVsProp;
        SerializedProperty  autoUVMaxDistanceProp;
        SerializedProperty  ignoreNormalsForChartDetectionProp;
        SerializedProperty  autoUVMaxAngleProp;
        SerializedProperty  minimumChartSizeProp;
        SerializedProperty  lightmapParametersProp;
        SerializedProperty  dynamicOccludeeProp;
        SerializedProperty  renderingLayerMaskProp;
        SerializedProperty  reflectionProbeUsageProp;
        SerializedProperty  lightProbeUsageProp;
        SerializedProperty  lightProbeVolumeOverrideProp;
        SerializedProperty  probeAnchorProp;

#if UNITY_2017_2_OR_ABOVE
        SerializedProperty stitchLightmapSeamsProp;
#endif

        SerializedObject    gameObjectsSerializedObject;
        SerializedProperty	staticEditorFlagsProp;


        SerializedProperty	convexProp;
        SerializedProperty	isTriggerProp;
        SerializedProperty	cookingOptionsProp;
        SerializedProperty	skinWidthProp;

        bool showLighting;
        bool showLightmapSettings;
        bool showChartingSettings;
        bool showUnwrapParams;


        delegate bool LightmapParametersGUIDelegate(SerializedProperty prop, GUIContent content);
        delegate float GetCachedMeshSurfaceAreaDelegate(MeshRenderer meshRenderer);
        delegate bool HasClampedResolutionDelegate(Renderer renderer);
        delegate bool HasUVOverlapsDelegate(Renderer renderer);
        delegate bool HasInstancingDelegate(Shader s);


        static LightmapParametersGUIDelegate	LightmapParametersGUI;
        static GetCachedMeshSurfaceAreaDelegate	GetCachedMeshSurfaceArea;
        static HasClampedResolutionDelegate		HasClampedResolution;
        static HasUVOverlapsDelegate			HasUVOverlaps;
        static HasInstancingDelegate			HasInstancing;
        static bool	reflectionInitialized = false;

        void InitTypes()
        {
            if (reflectionInitialized)
                return;
            reflectionInitialized = true;

            var unityEditorTypes = typeof(UnityEditor.SceneView).Assembly.GetTypes();
            var meshRendererEditorType = unityEditorTypes.FirstOrDefault(t => t.FullName == "UnityEditor.MeshRendererEditor");
            var internalMeshUtilType = unityEditorTypes.FirstOrDefault(t => t.FullName == "UnityEditor.InternalMeshUtil");
            var lightmapEditorSettingsType = typeof(LightmapEditorSettings);
            var shaderUtilType = typeof(ShaderUtil);

            if (meshRendererEditorType != null)
            {
                //static private bool LightmapParametersGUI(SerializedProperty prop, GUIContent content)
                var lightmapParametersGUIMethod = meshRendererEditorType.GetMethod("LightmapParametersGUI", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (lightmapParametersGUIMethod != null)
                    LightmapParametersGUI = (LightmapParametersGUIDelegate)Delegate.CreateDelegate(typeof(LightmapParametersGUIDelegate), null, lightmapParametersGUIMethod, true);
            }

            if (internalMeshUtilType != null)
            {
                //public static float GetCachedMeshSurfaceArea(MeshRenderer meshRenderer);
                var getCachedMeshSurfaceAreaMethod = internalMeshUtilType.GetMethod("GetCachedMeshSurfaceArea", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (getCachedMeshSurfaceAreaMethod != null)
                    GetCachedMeshSurfaceArea = (GetCachedMeshSurfaceAreaDelegate)Delegate.CreateDelegate(typeof(GetCachedMeshSurfaceAreaDelegate), null, getCachedMeshSurfaceAreaMethod, true);
            }

            {
                //static internal bool HasClampedResolution(Renderer renderer);
                var hasClampedResolutionMethod = lightmapEditorSettingsType.GetMethod("HasClampedResolution", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (hasClampedResolutionMethod != null)
                    HasClampedResolution = (HasClampedResolutionDelegate)Delegate.CreateDelegate(typeof(HasClampedResolutionDelegate), null, hasClampedResolutionMethod, true);
            }

            { 
                //static internal bool HasUVOverlaps(Renderer renderer);
                var hasUVOverlapsMethod = lightmapEditorSettingsType.GetMethod("HasUVOverlaps", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (hasUVOverlapsMethod != null)
                    HasUVOverlaps = (HasUVOverlapsDelegate)Delegate.CreateDelegate(typeof(HasUVOverlapsDelegate), null, hasUVOverlapsMethod, true);
            }

            { 
                //internal static bool HasInstancing(Shader s);
                var hasInstancingMethod = shaderUtilType.GetMethod("HasInstancing", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (hasInstancingMethod != null)
                    HasInstancing = (HasInstancingDelegate)Delegate.CreateDelegate(typeof(HasInstancingDelegate), null, hasInstancingMethod, true);
            }
        }

        internal void OnEnable()
        {
            if (ReflectionProbeUsageOptionsContents == null)
                ReflectionProbeUsageOptionsContents	= (Enum.GetNames(typeof(ReflectionProbeUsage)).Select(x => ObjectNames.NicifyVariableName(x)).ToArray()).Select(x => new GUIContent(x)).ToArray();

            showLighting				= EditorPrefs.GetBool(kDisplayLightingKey, false);
            showLightmapSettings		= SessionState.GetBool(kDisplayLightmapKey, true);
            showChartingSettings		= SessionState.GetBool(kDisplayChartingKey, true);
            showUnwrapParams			= SessionState.GetBool(kDisplayUnwrapParamsKey, true);

            if (!target)
                return;
            
            vertexChannelMaskProp				= serializedObject.FindProperty("VertexChannelMask");
            createRenderComponentsProp			= serializedObject.FindProperty("CreateRenderComponents");
            createColliderComponentsProp		= serializedObject.FindProperty("CreateColliderComponents");
            autoRebuildUVsProp					= serializedObject.FindProperty("AutoRebuildUVs");			
            angleErrorProp						= serializedObject.FindProperty("uvGenerationSettings.angleError");
            areaErrorProp						= serializedObject.FindProperty("uvGenerationSettings.areaError");
            hardAngleProp						= serializedObject.FindProperty("uvGenerationSettings.hardAngle");
            packMarginPixelsProp				= serializedObject.FindProperty("uvGenerationSettings.packMarginPixels");
            
            motionVectorsProp					= serializedObject.FindProperty("renderSettings.motionVectorGenerationMode");
            importantGIProp						= serializedObject.FindProperty("renderSettings.importantGI");
            lightmapScaleProp					= serializedObject.FindProperty("renderSettings.scaleInLightmap");
            preserveUVsProp						= serializedObject.FindProperty("renderSettings.optimizeUVs");
            autoUVMaxDistanceProp				= serializedObject.FindProperty("renderSettings.autoUVMaxDistance");
            autoUVMaxAngleProp					= serializedObject.FindProperty("renderSettings.autoUVMaxAngle");
            ignoreNormalsForChartDetectionProp	= serializedObject.FindProperty("renderSettings.ignoreNormalsForChartDetection");
            minimumChartSizeProp				= serializedObject.FindProperty("renderSettings.minimumChartSize");
            lightmapParametersProp				= serializedObject.FindProperty("renderSettings.lightmapParameters");
            dynamicOccludeeProp					= serializedObject.FindProperty("renderSettings.dynamicOccludee");
            renderingLayerMaskProp				= serializedObject.FindProperty("renderSettings.renderingLayerMask");
            reflectionProbeUsageProp			= serializedObject.FindProperty("renderSettings.reflectionProbeUsage");
            lightProbeUsageProp					= serializedObject.FindProperty("renderSettings.lightProbeUsage");
            lightProbeVolumeOverrideProp		= serializedObject.FindProperty("renderSettings.lightProbeVolumeOverride");
            probeAnchorProp						= serializedObject.FindProperty("renderSettings.probeAnchor");
#if UNITY_2017_2_OR_ABOVE
            stitchLightmapSeamsProp				= serializedObject.FindProperty("renderSettings.stitchLightmapSeams");
#endif			

            convexProp							= serializedObject.FindProperty("colliderSettings.convex");
            isTriggerProp						= serializedObject.FindProperty("colliderSettings.isTrigger");
            cookingOptionsProp					= serializedObject.FindProperty("colliderSettings.cookingOptions");
            skinWidthProp						= serializedObject.FindProperty("colliderSettings.skinWidth");

            gameObjectsSerializedObject			= new SerializedObject(serializedObject.targetObjects.Select(t => ((Component)t).gameObject).ToArray());
            staticEditorFlagsProp				= gameObjectsSerializedObject.FindProperty("m_StaticEditorFlags");


            for (int t = 0; t < targets.Length; t++)
            {
                var modelTarget = targets[t] as CSGModel;
                if (!modelTarget)
                    continue;

                if (!modelTarget.IsInitialized)
                    modelTarget.OnInitialize();
            }
        }
        
        public Bounds OnGetFrameBounds() { return CSGNodeEditor.CalculateBounds(targets); }
        public bool HasFrameBounds() { return true; }


        bool IsPrefabAsset
        {
            get
            {
                if (serializedObject == null || serializedObject.targetObject == null)
                    return false;

#if UNITY_2018_3_OR_NEWER
                var type = PrefabUtility.GetPrefabAssetType( serializedObject.targetObject );
                return ( type == PrefabAssetType.Regular || type == PrefabAssetType.Model );                
#else
                var type = PrefabUtility.GetPrefabType(serializedObject.targetObject);
                return (type == PrefabType.Prefab || type == PrefabType.ModelPrefab);
#endif
            }
        }

        float GetLargestCachedMeshSurfaceAreaForTargets(float defaultValue)
        {
            if (target == null || GetCachedMeshSurfaceArea == null)
                return defaultValue;
            float largestSurfaceArea = -1;
            foreach(var target in targets)
            {
                var model = target as CSGModel;
                if (!model)
                    continue;
                var renderComponents = model.generatedRenderComponents.Values;
                foreach (var renderComponentList in renderComponents)
                {
                    for (int r = 0; r < renderComponentList.Count; r++)
                    {
                        var meshRenderer = renderComponentList[r].meshRenderer;
                        if (!meshRenderer)
                            continue;
                        largestSurfaceArea = Mathf.Max(largestSurfaceArea, GetCachedMeshSurfaceArea(meshRenderer));
                    }
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
                var model = target as CSGModel;
                if (!model)
                    continue;
                var renderComponents = model.generatedRenderComponents.Values;
                foreach (var renderComponentList in renderComponents)
                {
                    for (int r = 0; r < renderComponentList.Count; r++)
                    {
                        var meshRenderer = renderComponentList[r].meshRenderer;
                        if (!meshRenderer)
                            continue;
                        if (HasClampedResolution(meshRenderer))
                            return true;
                    }
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
                var model = target as CSGModel;
                if (!model)
                    continue;
                var renderComponents = model.generatedRenderComponents.Values;
                foreach (var renderComponentList in renderComponents)
                {
                    for (int r = 0; r < renderComponentList.Count; r++)
                    {
                        var meshRenderer = renderComponentList[r].meshRenderer;
                        if (!meshRenderer)
                            continue;
                        if (HasUVOverlaps(meshRenderer))
                            return true;
                    }
                }
            }
            return false;
        }

        bool TargetsHaveInstancingShader()
        {
            if (target == null || HasInstancing == null)
                return false;
            foreach (var target in targets)
            {
                var model = target as CSGModel;
                if (!model)
                    continue;
                var renderComponents = model.generatedRenderComponents.Keys;
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
                var model = target as CSGModel;
                if (!model)
                    continue;
                if (CSGGeneratedComponentManager.NeedUVGeneration(model))
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
        {
            return false;
            // TODO: figure out how to set up LightProxyVolumes
            /*
            var lightProbeProxyVol = renderer.GetComponent<LightProbeProxyVolume>();
            bool invalidProxyVolumeOverride = (renderer.lightProbeProxyVolumeOverride == null) ||
                                                  (renderer.lightProbeProxyVolumeOverride.GetComponent<LightProbeProxyVolume>() == null);
            return lightProbeProxyVol == null && invalidProxyVolumeOverride;
            */
        }

        static internal bool AreLightProbesAllowed(CSGModel model)
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

            bool lightmapStatic = (staticEditorFlagsProp.intValue & (int)StaticEditorFlags.LightmapStatic) != 0;
            if (lightmapStatic)
                return false;

            foreach (UnityEngine.Object obj in targets)
                if (AreLightProbesAllowed((CSGModel)obj) == false)
                    return false;
            return true;
        }

        void LightmapStaticSettings()
        {
            bool lightmapStatic = (staticEditorFlagsProp.intValue & (int)StaticEditorFlags.LightmapStatic) != 0;

            EditorGUI.BeginChangeCheck();
            lightmapStatic = EditorGUILayout.Toggle(LightmapStaticContents, lightmapStatic);

            if (EditorGUI.EndChangeCheck())
            {
                SceneModeUtility.SetStaticFlags(gameObjectsSerializedObject.targetObjects, (int)StaticEditorFlags.LightmapStatic, lightmapStatic);
                gameObjectsSerializedObject.Update();
            }
        }

        //int selectionCount, Renderer renderer, 
        internal void RenderLightProbeUsage(bool lightProbeAllowed)
        {
            using (new EditorGUI.DisabledScope(!lightProbeAllowed))
            {
                if (lightProbeAllowed)
                {
                    // LightProbeUsage has non-sequential enum values. Extra care is to be taken.
                    Rect r = EditorGUILayout.GetControlRect(true, kSingleLineHeight, EditorStyles.popup);
                    EditorGUI.BeginProperty(r, LightProbeUsageContents, lightProbeUsageProp);
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.EnumPopup(r, LightProbeUsageContents, (LightProbeUsage)lightProbeUsageProp.intValue);
                    if (EditorGUI.EndChangeCheck())
                        lightProbeUsageProp.intValue = (int)(LightProbeUsage)newValue;
                    EditorGUI.EndProperty();

                    if (!lightProbeUsageProp.hasMultipleDifferentValues)
                    {
                        if (
#if UNITY_2018_1_OR_ABOVE
                            SupportedRenderingFeatures.active.rendererSupportsLightProbeProxyVolumes &&
#endif
                            lightProbeUsageProp.intValue == (int)LightProbeUsage.UseProxyVolume)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(lightProbeVolumeOverrideProp, LightProbeVolumeOverrideContents);
                            EditorGUI.indentLevel--;
                        } else 
                        if (lightProbeUsageProp.intValue == (int)4)//LightProbeUsage.CustomProvided
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
                if (
#if UNITY_2018_1_OR_ABOVE
                    SupportedRenderingFeatures.active.rendererSupportsLightProbeProxyVolumes &&
#endif
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
#if UNITY_2018_1_OR_ABOVE
            if (!SupportedRenderingFeatures.active.rendererSupportsReflectionProbes)
                return;
#endif

            using (new EditorGUI.DisabledScope(isDeferredRenderingPath))
            {
                // reflection probe usage field; UI disabled when using deferred reflections
                if (isDeferredReflections)
                {
                    EditorGUILayout.EnumPopup(ReflectionProbeUsageContents, (reflectionProbeUsageProp.intValue != (int)ReflectionProbeUsage.Off) ? ReflectionProbeUsage.Simple : ReflectionProbeUsage.Off);
                } else
                {
                    Rect r = EditorGUILayout.GetControlRect(true, kSingleLineHeight, EditorStyles.popup);
                    CSGEditorUtility.Popup(r, reflectionProbeUsageProp, ReflectionProbeUsageOptionsContents, ReflectionProbeUsageContents);
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


        void RenderProbeFieldsGUI()
        {
            bool isDeferredRenderingPath = CSGEditorUtility.IsUsingDeferredRenderingPath();
            bool isDeferredReflections = isDeferredRenderingPath && CSGEditorUtility.IsDeferredReflections();
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
            var sizeInLightmap = Mathf.Sqrt(cachedSurfaceArea) * LightmapEditorSettings.bakeResolution * lightmapScale;
            
            if (sizeInLightmap > LightmapEditorSettings.maxAtlasSize)
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

        void RenderMeshSettingsGUI()
        {
            if (serializedObject == null || gameObjectsSerializedObject == null || gameObjectsSerializedObject.targetObjects.Length == 0)
                return;

            gameObjectsSerializedObject.Update(); 

#if UNITY_2018_2_OR_ABOVE
            if (SupportedRenderingFeatures.active.rendererSupportsMotionVectors)
#endif
            EditorGUILayout.PropertyField(motionVectorsProp, MotionVectorsContent, true);

            EditorGUILayout.Space();
            LightmapStaticSettings();

            if (!(Lightmapping.bakedGI || Lightmapping.realtimeGI) && !IsPrefabAsset)
            {
                EditorGUILayout.HelpBox(GINotEnabledInfoContents.text, MessageType.Info);
                return;
            }

            bool enableSettings = (staticEditorFlagsProp.intValue & (int)StaticEditorFlags.LightmapStatic) != 0;
            if (enableSettings)
            {
                EditorGUILayout.Space();
                var needLightmapRebuild = NeedLightmapRebuild();
                if (!autoRebuildUVsProp.boolValue && needLightmapRebuild)
                    EditorGUILayout.HelpBox(NeedsLightmapRebuildContents.text, MessageType.Warning);

                var buttonRect		= EditorGUILayout.GetControlRect();
                int buttonId		= EditorGUIUtility.GetControlID(RebuildButtonHashCode, FocusType.Keyboard, buttonRect);
                var buttonPropRect	= EditorGUI.PrefixLabel(buttonRect, buttonId, LightmapUVsContents);
                var buttonContents	= needLightmapRebuild ? ForceBuildUVsContents : ForceRebuildUVsContents;
                if (GUI.Button(buttonPropRect, buttonContents))
                {
                    CSGGeneratedComponentManager.DelayedUVGeneration(force: true);
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(autoRebuildUVsProp, AutoRebuildUVsContents);
                if (EditorGUI.EndChangeCheck())
                {
                    if (autoRebuildUVsProp.boolValue)
                        CSGGeneratedComponentManager.ForceUpdateDelayedUVGeneration();
                }

                showUnwrapParams = EditorGUILayout.Foldout(showUnwrapParams, UnwrapParamsContents, true);
                if (showUnwrapParams)
                {
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.PropertyField(angleErrorProp);
                    EditorGUILayout.PropertyField(areaErrorProp);
                    EditorGUILayout.PropertyField(hardAngleProp);
                    EditorGUILayout.PropertyField(packMarginPixelsProp);
                    EditorGUI.indentLevel -= 1;
                }

#if UNITY_2017_2_OR_ABOVE
                showEnlightenSettings		= IsPrefabAsset || Lightmapping.realtimeGI || (Lightmapping.bakedGI && LightmapEditorSettings.lightmapper == (Lightmapper)0);
                var showProgressiveSettings	= IsPrefabAsset ||                            (Lightmapping.bakedGI && LightmapEditorSettings.lightmapper != (Lightmapper)0);
#else
                bool showEnlightenSettings	= IsPrefabAsset || Lightmapping.realtimeGI || Lightmapping.bakedGI;
#endif

                if (showEnlightenSettings)
                {
                    showChartingSettings = EditorGUILayout.Foldout(showChartingSettings, UVChartingContents, true);
                    if (showChartingSettings)
                        RendererUVSettings();
                }
                
                showLightmapSettings = EditorGUILayout.Foldout(showLightmapSettings, LightmapSettingContents, true);
                if (showLightmapSettings)
                {
                    EditorGUI.indentLevel += 1;

                    float lightmapScale		= LightmapScaleGUI(1.0f);

                    ShowClampedSizeInLightmapGUI(lightmapScale);

                    if (showEnlightenSettings) EditorGUILayout.PropertyField(importantGIProp, ImportantGIContents);

#if UNITY_2017_2_OR_ABOVE
                    if (showProgressiveSettings)
                        EditorGUILayout.PropertyField(stitchLightmapSeamsProp, StitchLightmapSeamsContents);
#endif

                    if (LightmapParametersGUI != null)
                        LightmapParametersGUI(lightmapParametersProp, LightmapParametersContents);

                    EditorGUI.indentLevel -= 1;
                }

                if (TargetsHaveClampedResolution())
                    EditorGUILayout.HelpBox(ClampedPackingResolutionContents.text, MessageType.Warning);

                if ((vertexChannelMaskProp.intValue & (int)VertexChannelFlags.Normal) != (int)VertexChannelFlags.Normal)
                    EditorGUILayout.HelpBox(NoNormalsNoLightmappingContents.text, MessageType.Warning);

                if (TargetsHaveUVOverlaps())
                    EditorGUILayout.HelpBox(UVOverlapContents.text, MessageType.Warning);
                
                serializedObject.ApplyModifiedProperties();
            } else
                EditorGUILayout.HelpBox(LightmapInfoBoxContents.text, MessageType.Info);
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
            bool usingSRP = GraphicsSettings.renderPipelineAsset != null;
            if (!usingSRP || target == null|| CSGEditModeManager.EditMode != CSGEditMode.ShapeEdit)
                return;

            EditorGUI.showMixedValue = renderingLayerMaskProp.hasMultipleDifferentValues;

            var model		= (CSGModel)target;
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
                    var m = t as CSGModel;
                    if (m != null)
                    {
                        m.RenderSettings.renderingLayerMask = (uint)mask;
                        EditorUtility.SetDirty(t);
                    }
                }
            }
            EditorGUI.showMixedValue = false;
        }

        void CullDynamicFieldGUI()
        {
            EditorGUILayout.PropertyField(dynamicOccludeeProp, DynamicOccludeeContents);
        }

        void MeshRendererLightingGUI()
        {
            var oldShowLighting				= showLighting;
            var oldShowLightmapSettings		= showLightmapSettings;
            var oldShowChartingSettings		= showChartingSettings;
            var oldShowUnwrapParams			= showUnwrapParams;
            

            showLighting = EditorGUILayout.Foldout(showLighting, LightingContent, true);
            if (showLighting)
            {
                EditorGUI.indentLevel += 1;
                RenderProbeFieldsGUI();
                RenderMeshSettingsGUI();

                if (TargetsHaveInstancingShader())
                {
                    gameObjectsSerializedObject.Update();

                    if (!staticEditorFlagsProp.hasMultipleDifferentValues && ((StaticEditorFlags)staticEditorFlagsProp.intValue & StaticEditorFlags.BatchingStatic) != 0)
                    {
                        EditorGUILayout.HelpBox(StaticBatchingWarningContents.text, MessageType.Warning, true);
                    }
                }

                EditorGUI.indentLevel -= 1;
            }

            if (showLighting         != oldShowLighting        ) EditorPrefs.SetBool(kDisplayLightingKey, showLighting);
            if (showLightmapSettings != oldShowLightmapSettings) SessionState.SetBool(kDisplayLightmapKey, showLightmapSettings);
            if (showChartingSettings != oldShowChartingSettings) SessionState.SetBool(kDisplayChartingKey, showChartingSettings);
            if (showUnwrapParams     != oldShowUnwrapParams    ) SessionState.SetBool(kDisplayUnwrapParamsKey, showUnwrapParams);
        }

        bool IsDefaultModel()
        {
            if (serializedObject.targetObjects == null)
                return false;

            for (int i = 0; i < serializedObject.targetObjects.Length; i++)
            {
                if (CSGGeneratedComponentManager.IsDefaultModel(serializedObject.targetObjects[i]))
                    return true;
            }
            return false;
        }

        
        public override void OnInspectorGUI()
        {
            InitTypes();

            CSGNodeEditor.CheckForTransformationChanges(serializedObject);

            if (IsDefaultModel())
                EditorGUILayout.HelpBox(DefaultModelContents.text, MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.PropertyField(createRenderComponentsProp, CreateRenderComponentsContents);
                EditorGUI.BeginDisabledGroup(!createRenderComponentsProp.boolValue);
                {
                    EditorGUI.indentLevel++;

                    MeshRendererLightingGUI();
                    RenderRenderingLayer();
                    CullDynamicFieldGUI();
                    // TODO: Make Position show up instead of "None" when nothing is selected
                    CSGEditorUtility.EnumFlagsField(VertexChannelMaskContents, vertexChannelMaskProp, typeof(VertexChannelFlags), EditorStyles.popup);
                    
                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(createColliderComponentsProp, CreateColliderComponentsContents);
                EditorGUI.BeginDisabledGroup(!createColliderComponentsProp.boolValue);
                {
                    EditorGUI.indentLevel++;

                    EditorGUI.indentLevel++;
                    using (new EditorGUI.DisabledScope(!convexProp.boolValue))
                    {
                        EditorGUILayout.PropertyField(isTriggerProp, IsTriggerContents);
                    }
                    EditorGUI.indentLevel--;

                    {
                        CSGEditorUtility.EnumFlagsField(CookingOptionsContents, cookingOptionsProp, typeof(MeshColliderCookingOptions), EditorStyles.popup);
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();
            }
            if (EditorGUI.EndChangeCheck())
            {
                CSGNodeEditor.ForceUpdateNodeContents(serializedObject);
            }
        }
    }
}
