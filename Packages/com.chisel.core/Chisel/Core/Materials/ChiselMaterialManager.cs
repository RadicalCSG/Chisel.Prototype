using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Chisel.Core
{
    public sealed class ChiselMaterialManager : ScriptableObject
    {
        #region Instance
        static ChiselMaterialManager _instance;
        public static ChiselMaterialManager Instance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_instance)
                    return _instance;
                
                _instance = ScriptableObject.CreateInstance<ChiselMaterialManager>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                _instance.Initialize();
                return _instance;  
            }
        }
        #endregion

        public Material defaultFloorMaterial;
        public Material defaultStepMaterial;
        public Material defaultTreadMaterial;
        public Material defaultWallMaterial;
        public PhysicMaterial defaultPhysicMaterial;
        
        public Material defaultHiddenMaterial;
        public Material defaultCastMaterial;
        public Material defaultShadowOnlyMaterial;
        public Material defaultReceiveMaterial;
        public Material defaultColliderMaterial;
        public Material defaultCulledMaterial;

        Material[] helperMaterials;


        public static Material DefaultFloorMaterial		    { get { return Instance.defaultFloorMaterial; } }
        public static Material DefaultStepMaterial		    { get { return Instance.defaultStepMaterial; } }
        public static Material DefaultTreadMaterial		    { get { return Instance.defaultTreadMaterial; } }
        public static Material DefaultWallMaterial		    { get { return Instance.defaultWallMaterial; } }
        public static Material DefaultMaterial              { get { return Instance.defaultWallMaterial; } }
        public static PhysicMaterial DefaultPhysicsMaterial { get { return Instance.defaultPhysicMaterial; } }
        public static Material[] HelperMaterials            { get { return Instance.helperMaterials; } }
        
        static readonly Dictionary<string, Material>	editorMaterials = new Dictionary<string, Material>();
        static readonly Dictionary<Color,Material>		colorMaterials	= new Dictionary<Color, Material>();
        
        internal void Initialize()
        {
            // TODO: add check to ensure this matches ChiselGeneratedObjects.kGeneratedDebugRendererNames
            helperMaterials = new Material[6]
            {
                defaultHiddenMaterial,
                defaultCastMaterial,
                defaultShadowOnlyMaterial,
                defaultReceiveMaterial,
                defaultColliderMaterial,
                defaultCulledMaterial
            };
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
#if UNITY_EDITOR
                return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
#else
                return null;
#endif

            colorMaterials.Add(color, material);
            return material;
        }


        readonly Dictionary<int, Material> idToMaterial = new Dictionary<int, Material>();
        readonly Dictionary<Material, int> materialToID = new Dictionary<Material, int>();
        readonly Dictionary<int, PhysicMaterial> idToPhysicMaterial = new Dictionary<int, PhysicMaterial>();
        readonly Dictionary<PhysicMaterial, int> physicMaterialToID = new Dictionary<PhysicMaterial, int>();

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(Material material)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(material);
            return objectID.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(PhysicMaterial physicMaterial)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(physicMaterial);
            return objectID.GetHashCode();
        }
#else
        // TODO: Implement a runtime solution
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(Material material)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(PhysicMaterial physicMaterial)
        {
            throw new NotImplementedException();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SurfaceLayers GetLayerDefinition(ChiselBrushMaterial brushMaterial)
        {
            return new SurfaceLayers
            {
                layerUsage = brushMaterial.layerUsage,
                layerParameter1 = brushMaterial.renderMaterial == null ? 0 : GetID(brushMaterial.renderMaterial),
                layerParameter2 = brushMaterial.physicsMaterial == null ? 0 : GetID(brushMaterial.physicsMaterial)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetID(Material material)
        {
            if (!material)
                return 0;
            if (!materialToID.TryGetValue(material, out var id))
            {
                id = ToID(material);
                materialToID[material] = id;
                idToMaterial[id] = material;
            }
            return id;
        }

        public Material GetMaterial(int id)
        {
            if (idToMaterial.TryGetValue(id, out var material))
            {
                if (material)
                    return material;
                idToMaterial.Remove(id);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetID(PhysicMaterial physicMaterial)
        {
            if (!physicMaterial)
                return 0;
            if (!physicMaterialToID.TryGetValue(physicMaterial, out var id))
            {
                id = ToID(physicMaterial);
                physicMaterialToID[physicMaterial] = id;
                idToPhysicMaterial[id] = physicMaterial;
            }
            return id;
        }

        public PhysicMaterial GetPhysicMaterial(int id)
        {
            if (idToPhysicMaterial.TryGetValue(id, out var physicMaterial))
            {
                if (physicMaterial)
                    return physicMaterial;
                idToPhysicMaterial.Remove(id);
            }
            return null;
        }
    }
}
