using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
    public class SceneHandleMaterialManager : ScriptableObject
    {
        #region Instance
        static SceneHandleMaterialManager _instance;
        public static SceneHandleMaterialManager Instance
        {
            get
            {
                if (_instance)
                    return _instance;
                
                _instance = ScriptableObject.CreateInstance<SceneHandleMaterialManager>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;  
            }
        }
        #endregion

        internal const string ShaderNameHandlesRoot	= "Hidden/Chisel/internal/";

        static readonly Dictionary<string, Material>	editorMaterials = new Dictionary<string, Material>();
        
        static bool _shadersInitialized;		//= false;
        static int	_pixelsPerPointId			= -1;
        static int	_lineThicknessMultiplierId	= -1; 
        static int	_lineDashMultiplierId		= -1; 
        static int	_lineAlphaMultiplierId		= -1;

        static void ShaderInit()
        {
            _shadersInitialized = true;
    
            _pixelsPerPointId			= Shader.PropertyToID("_pixelsPerPoint");
            _lineThicknessMultiplierId	= Shader.PropertyToID("_thicknessMultiplier");
            _lineDashMultiplierId		= Shader.PropertyToID("_dashMultiplier");
            _lineAlphaMultiplierId		= Shader.PropertyToID("_alphaMultiplier");
        }
        
        static Material _defaultMaterial;
        public static Material DefaultMaterial
        {
            get
            {
                // TODO: make this work with HDRP
                if (!_defaultMaterial)
                    _defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
                return _defaultMaterial;
            }
        }
        
        public static void InitGenericLineMaterial(Material genericLineMaterial)
        {
            if (!genericLineMaterial)
                return;
            
            if (!_shadersInitialized) ShaderInit();
            if (_pixelsPerPointId != -1)
            {
#if UNITY_5_4_OR_NEWER
                genericLineMaterial.SetFloat(_pixelsPerPointId, EditorGUIUtility.pixelsPerPoint);
#else
                genericLineMaterial.SetFloat(_pixelsPerPointId, 1.0f);
#endif
            }
            if (_lineThicknessMultiplierId != -1) genericLineMaterial.SetFloat(_lineThicknessMultiplierId, _lineThicknessMultiplier * EditorGUIUtility.pixelsPerPoint);
            if (_lineDashMultiplierId      != -1) genericLineMaterial.SetFloat(_lineDashMultiplierId,      _lineDashMultiplier);
            if (_lineAlphaMultiplierId	   != -1) genericLineMaterial.SetFloat(_lineAlphaMultiplierId,     _lineAlphaMultiplier);
        }
        

        static float _lineThicknessMultiplier = 1.0f;
        public static float LineThicknessMultiplier		{ get { return _lineThicknessMultiplier; } set { if (Mathf.Abs(_lineThicknessMultiplier - value) < 0.0001f) return; _lineThicknessMultiplier = value; } }

        static float _lineDashMultiplier = 1.0f;
        public static float LineDashMultiplier			{ get { return _lineDashMultiplier; } set { if (Mathf.Abs(_lineDashMultiplier - value) < 0.0001f) return; _lineDashMultiplier = value; } }

        static float _lineAlphaMultiplier = 1.0f;
        public static float LineAlphaMultiplier			{ get { return _lineAlphaMultiplier; } set { if (Mathf.Abs(_lineAlphaMultiplier - value) < 0.0001f) return; _lineAlphaMultiplier = value; } }
        
        static Material _zTestGenericLine;
        public static Material ZTestGenericLine			{ get { if (!_zTestGenericLine) _zTestGenericLine = GenerateDebugMaterial(ShaderNameHandlesRoot + "ZTestGenericLine"); return _zTestGenericLine; } }

        static Material _noZTestGenericLine;
        public static Material NoZTestGenericLine		{ get { if (!_noZTestGenericLine) _noZTestGenericLine = GenerateDebugMaterial(ShaderNameHandlesRoot + "NoZTestGenericLine"); return _noZTestGenericLine; } }

        static Material _coloredPolygonMaterial;
        public static Material ColoredPolygonMaterial	{ get { if (!_coloredPolygonMaterial) _coloredPolygonMaterial = GenerateDebugMaterial(ShaderNameHandlesRoot + "customSurface"); return _coloredPolygonMaterial; } }

        static Material customDotMaterial;
        public static Material CustomDotMaterial		{ get { if (!customDotMaterial) customDotMaterial = GenerateDebugMaterial(ShaderNameHandlesRoot + "customDot"); return customDotMaterial; } }

        static Material surfaceNoDepthMaterial;
        public static Material SurfaceNoDepthMaterial	{ get { if (!surfaceNoDepthMaterial) surfaceNoDepthMaterial = GenerateDebugMaterial(ShaderNameHandlesRoot + "customNoDepthSurface"); return surfaceNoDepthMaterial; } }

        static Material gridMaterial;
        public static Material GridMaterial				{ get { if (!gridMaterial) gridMaterial = GenerateDebugMaterial(ShaderNameHandlesRoot + "Grid"); return gridMaterial; } }


        internal static Material GenerateDebugMaterial(string shaderName)
        {
            Material material;
            var name = shaderName;
            if (editorMaterials.TryGetValue(name, out material))
            {
                // just in case one of many unity bugs destroyed the material
                if (!material)
                {
                    editorMaterials.Remove(name);
                } else
                    return material;
            }

            var materialName = name.Replace(':', '_');


            var shader = Shader.Find(shaderName);
            if (!shader)
            {
                Debug.LogWarning("Could not find internal shader: " + shaderName);
                return null;
            }

            material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
            editorMaterials.Add(name, material);
            return material;
        }	
    }
}
