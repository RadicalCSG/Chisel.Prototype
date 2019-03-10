using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnityEditor;

namespace Chisel.Components
{
	public class CSGMaterialManager : ScriptableObject
	{
		#region Instance
		static CSGMaterialManager _instance;
		public static CSGMaterialManager Instance
		{
			get
			{
				if (_instance)
					return _instance;
				
				_instance = ScriptableObject.CreateInstance<CSGMaterialManager>();
				_instance.hideFlags = HideFlags.HideAndDontSave;
				return _instance;  
			}
		}
		#endregion

		[SerializeField] public Material defaultFloorMaterial;
		[SerializeField] public Material defaultStepMaterial;
		[SerializeField] public Material defaultTreadMaterial;
		[SerializeField] public Material defaultWallMaterial;
        [SerializeField] public PhysicMaterial defaultPhysicsMaterial;

        public static Material DefaultFloorMaterial		        { get { return Instance.defaultFloorMaterial; } }
		public static Material DefaultStepMaterial		        { get { return Instance.defaultStepMaterial; } }
		public static Material DefaultTreadMaterial		        { get { return Instance.defaultTreadMaterial; } }
		public static Material DefaultWallMaterial		        { get { return Instance.defaultWallMaterial; } }
		public static PhysicMaterial DefaultPhysicsMaterial		{ get { return Instance.defaultPhysicsMaterial; } }
		

		const string ShaderNameCSGRoot		= "Hidden/CSG/internal/";
		const string ShaderNameHandlesRoot	= "Hidden/UnitySceneExtensions/internal/";

		static readonly Dictionary<string, Material>	editorMaterials = new Dictionary<string, Material>();
		static readonly Dictionary<Color,Material>		colorMaterials	= new Dictionary<Color, Material>();
		
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
		public static Material ZTestGenericLine			{ get { if (!_zTestGenericLine) _zTestGenericLine = GenerateDebugMaterial(ShaderNameCSGRoot + "ZTestGenericLine"); return _zTestGenericLine; } }

		static Material _noZTestGenericLine;
		public static Material NoZTestGenericLine		{ get { if (!_noZTestGenericLine) _noZTestGenericLine = GenerateDebugMaterial(ShaderNameCSGRoot + "NoZTestGenericLine"); return _noZTestGenericLine; } }

		static Material _coloredPolygonMaterial;
		public static Material ColoredPolygonMaterial	{ get { if (!_coloredPolygonMaterial) _coloredPolygonMaterial = GenerateDebugMaterial(ShaderNameCSGRoot + "customSurface"); return _coloredPolygonMaterial; } }

		static Material customDotMaterial;
		public static Material CustomDotMaterial		{ get { if (!customDotMaterial) customDotMaterial = GenerateDebugMaterial(ShaderNameHandlesRoot + "customDot"); return customDotMaterial; } }

		static Material surfaceNoDepthMaterial;
		public static Material SurfaceNoDepthMaterial	{ get { if (!surfaceNoDepthMaterial) surfaceNoDepthMaterial = GenerateDebugMaterial(ShaderNameHandlesRoot + "customNoDepthSurface"); return surfaceNoDepthMaterial; } }


		internal static Material GenerateDebugMaterial(string shaderName, string textureName = null, string materialName = null)
		{
			Material material;
			var name = shaderName + ":" + textureName;
			if (editorMaterials.TryGetValue(name, out material))
			{
				// just in case one of many unity bugs destroyed the material
				if (!material)
				{
					editorMaterials.Remove(name);
				} else
					return material;
			}

			if (materialName == null)
				materialName = name.Replace(':', '_');


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
			if (textureName != null)
			{
				string filename = "Textures/Chisel/" + textureName + ".png";
				material.mainTexture = EditorGUIUtility.Load(filename) as Texture2D;
				if (!material.mainTexture)
					Debug.LogWarning("Could not find internal texture: " + filename);
			}
			editorMaterials.Add(name, material);
			return material;
		}

		internal static Material GenerateDebugColorMaterial(Color color)
		{
			var name = "Color: " + color;
			Material material;
			if (editorMaterials.TryGetValue(name, out material))
			{
				// just in case one of many unity bugs destroyed the material
				if (!material)
				{
					editorMaterials.Remove(name);
				} else
					return material;
			}

			var shader = Shader.Find("Unlit/Color");
			if (!shader)
				return null;

			material = new Material(shader)
			{
				name		= name.Replace(':', '_'),
				hideFlags	= HideFlags.HideAndDontSave
			};
			material.SetColor("_Color", color);

			editorMaterials.Add(name, material);
			return material;
		}
		
		public static Material GetColorMaterial(Color color)
		{
			Material material;
			if (colorMaterials.TryGetValue(color, out material))
			{
				// just in case one of many unity bugs destroyed the material
				if (!material)
				{
					colorMaterials.Remove(color);
				} else
					return material;
			}
			
			material = GenerateDebugColorMaterial(color);
			if (!material)
				return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");

			colorMaterials.Add(color, material);
			return material;
		}		
	}
}
