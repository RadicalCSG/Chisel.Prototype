using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Chisel.Core
{
    public sealed class ChiselDefaultMaterials : ScriptableObject
    {
        #region Instance
        static ChiselDefaultMaterials _instance;
        public static ChiselDefaultMaterials Instance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_instance)
                    return _instance;
                
                _instance = ScriptableObject.CreateInstance<ChiselDefaultMaterials>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                _instance.Initialize();
                return _instance;  
            }
        }
		#endregion

		// We use the default values that are set in the inspector when selecting this .cs file within unity 
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
		public static Material CollisionOnlyMaterial        { get { return Instance.defaultColliderMaterial; } }
		public static Material DiscardedMaterial            { get { return Instance.defaultHiddenMaterial; } }
		public static Material ShadowOnlyMaterial           { get { return Instance.defaultShadowOnlyMaterial; } }
		public static PhysicMaterial DefaultPhysicsMaterial { get { return Instance.defaultPhysicMaterial; } }
        public static Material[] HelperMaterials            { get { return Instance.helperMaterials; } }
        
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
    }
}
