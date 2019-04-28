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
        
        public static Material DefaultFloorMaterial		{ get { return Instance.defaultFloorMaterial; } }
        public static Material DefaultStepMaterial		{ get { return Instance.defaultStepMaterial; } }
        public static Material DefaultTreadMaterial		{ get { return Instance.defaultTreadMaterial; } }
        public static Material DefaultWallMaterial		{ get { return Instance.defaultWallMaterial; } }
        
        static readonly Dictionary<string, Material>	editorMaterials = new Dictionary<string, Material>();
        static readonly Dictionary<Color,Material>		colorMaterials	= new Dictionary<Color, Material>();
        
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

        static PhysicMaterial _defaultPhysicsMaterial;
        public static PhysicMaterial DefaultPhysicsMaterial
        {
            get
            {
                if (!_defaultPhysicsMaterial)
                    _defaultPhysicsMaterial = new PhysicMaterial("Default");
                return _defaultPhysicsMaterial;
            }
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
