using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Assets
{
    public delegate void OnSurfaceAssetDelegate(CSGSurfaceAsset surfaceAsset);
    
    public static class CSGSurfaceAssetManager
    {
        public static event OnSurfaceAssetDelegate OnSurfaceAssetChanged;
        public static event OnSurfaceAssetDelegate OnSurfaceAssetRemoved;
        public static event OnSurfaceAssetDelegate OnSurfaceAssetAdded;

        class RenderMaterialInstance
        {
            public RenderMaterialInstance(Material renderMaterial) { this.renderMaterial = renderMaterial; }
            public Material renderMaterial;
            public int		refCount;
        }

        class PhysicsMaterialInstance
        {
            public PhysicsMaterialInstance(PhysicMaterial physicsMaterial) { this.physicsMaterial = physicsMaterial; }
            public PhysicMaterial physicsMaterial;
            public int			  refCount;
        }

        static Dictionary<int, RenderMaterialInstance>  renderMaterialLookup    = new Dictionary<int, RenderMaterialInstance>();
        static Dictionary<int, PhysicsMaterialInstance> physicsMaterialLookup	= new Dictionary<int, PhysicsMaterialInstance>();
        
        static Dictionary<CSGSurfaceAsset, RenderMaterialInstance>  surfaceRenderMaterialLookup		= new Dictionary<CSGSurfaceAsset, RenderMaterialInstance>();
        static Dictionary<CSGSurfaceAsset, PhysicsMaterialInstance> surfacePhysicsMaterialLookup	= new Dictionary<CSGSurfaceAsset, PhysicsMaterialInstance>();

        static readonly HashSet<CSGSurfaceAsset> registeredLookup       = new HashSet<CSGSurfaceAsset>();
        
        static readonly HashSet<int> removeRenderMaterials  = new HashSet<int>();
        static readonly HashSet<int> removePhysicsMaterials = new HashSet<int>();

        static readonly HashSet<int> unknownRenderMaterialInstanceIDs	= new HashSet<int>();
        static readonly HashSet<int> unknownPhysicsMaterialInstanceIDs	= new HashSet<int>();
        

        static void Clear()
        {
            renderMaterialLookup.Clear();
            physicsMaterialLookup.Clear();
            surfaceRenderMaterialLookup.Clear();
            surfacePhysicsMaterialLookup.Clear();
            registeredLookup.Clear();
            removeRenderMaterials.Clear();
            removePhysicsMaterials.Clear();
        }

        public static void Reset()
        {
            Clear();
            CSGBrushMeshAssetManager.RegisterAllSurfaces();
        }


        public static Material GetRenderMaterialByInstanceID(int instanceID, bool reregisterAllIfNotFound = true)
        {
            RenderMaterialInstance renderMaterialInstance;
            if (renderMaterialLookup.TryGetValue(instanceID, out renderMaterialInstance))
                return renderMaterialInstance.renderMaterial;
            
            // See if we already, unsuccessfully, tried finding this instanceID
            if (unknownRenderMaterialInstanceIDs.Contains(instanceID))
            {
                Debug.LogError("Could not find Material with instanceID " + instanceID + ".");
                return null;
            }

            // See if we've, somehow, passed the instanceID of a PhysicMaterial (we're expecting a Material)
            if (physicsMaterialLookup.ContainsKey(instanceID))
            {
                Debug.LogError("Trying to use PhysicMaterial with instanceID " + instanceID + " as a Material.");
                return null;
            } else
            if (reregisterAllIfNotFound)
            {
                // This should never happen, but just in case it does, we try finding all surface assets and reregister all of them (slow)
                Reset();
                Debug.LogWarning("Performance warning: Could not find Material with instanceID " + instanceID + ". Reregistering all surfaces to find it.");
                if (renderMaterialLookup.TryGetValue(instanceID, out renderMaterialInstance))
                    return renderMaterialInstance.renderMaterial;
            }
            unknownRenderMaterialInstanceIDs.Add(instanceID);
            Debug.LogError("Could not find Material with instanceID " + instanceID + ".");
            return null;
        }

        public static PhysicMaterial GetPhysicsMaterialByInstanceID(int instanceID, bool reregisterAllIfNotFound = true)
        {
            PhysicsMaterialInstance physicsMaterialInstance;
            if (physicsMaterialLookup.TryGetValue(instanceID, out physicsMaterialInstance))
                return physicsMaterialInstance.physicsMaterial;
            
            // See if we already, unsuccessfully, tried finding this instanceID
            if (unknownPhysicsMaterialInstanceIDs.Contains(instanceID))
            {
                Debug.LogError("Could not find PhysicMaterial with instanceID " + instanceID + ".");
                return null;
            }

            // See if we've, somehow, passed the instanceID of a Material (we're expecting a PhysicMaterial)
            if (renderMaterialLookup.ContainsKey(instanceID))
            {
                Debug.LogError("Trying to use Material with instanceID " + instanceID + " as a PhysicMaterial.");
                return null;
            } else
            if (reregisterAllIfNotFound)
            {
                // This should never happen, but just in case it does, we try finding all surface assets and reregister all of them (slow)
                Reset();
                Debug.LogWarning("Performance warning: Could not find PhysicMaterial with instanceID " + instanceID + ". Reregistering all surfaces to find it.");
                if (physicsMaterialLookup.TryGetValue(instanceID, out physicsMaterialInstance))
                    return physicsMaterialInstance.physicsMaterial;
            }

            unknownPhysicsMaterialInstanceIDs.Add(instanceID);
            Debug.LogError("Could not find PhysicMaterial with instanceID " + instanceID + ".");
            return null;
        }

        public static int? GetRenderMaterialRefCountByInstanceID(int instanceID)
        {
            RenderMaterialInstance renderMaterialInstance;
            if (!renderMaterialLookup.TryGetValue(instanceID, out renderMaterialInstance))
                return null;
            return renderMaterialInstance.refCount;
        }

        public static int? GetPhysicsMaterialRefCountByInstanceID(int instanceID)
        {
            PhysicsMaterialInstance physicsMaterialInstance;
            if (!physicsMaterialLookup.TryGetValue(instanceID, out physicsMaterialInstance))
                return null;
            return physicsMaterialInstance.refCount;
        }

        public static bool IsRegistered(CSGSurfaceAsset surfaceAsset)
        {
            if (surfaceAsset == null)
                return false;
            return registeredLookup.Contains(surfaceAsset);
        }

        public static void Register(CSGSurfaceAsset surfaceAsset)
        {
            if (surfaceAsset == null || !registeredLookup.Add(surfaceAsset)) return;

            surfaceRenderMaterialLookup [surfaceAsset] = IncRefCount(surfaceAsset.RenderMaterial);
            surfacePhysicsMaterialLookup[surfaceAsset] = IncRefCount(surfaceAsset.PhysicsMaterial);

            if (OnSurfaceAssetAdded != null)
                OnSurfaceAssetAdded(surfaceAsset);
        }

        public static void Unregister(CSGSurfaceAsset surfaceAsset)
        {
            if (!registeredLookup.Remove(surfaceAsset)) return;

            surfaceRenderMaterialLookup .Remove(surfaceAsset);
            surfacePhysicsMaterialLookup.Remove(surfaceAsset);

            DecRefCount(surfaceAsset.RenderMaterial);
            DecRefCount(surfaceAsset.PhysicsMaterial);		

            if (OnSurfaceAssetRemoved != null)
                OnSurfaceAssetRemoved(surfaceAsset);
        }

        static RenderMaterialInstance IncRefCount(Material renderMaterial)
        {
            if (!renderMaterial)
                return null;
            var instanceID = renderMaterial.GetInstanceID();
            RenderMaterialInstance instance;
            if (!renderMaterialLookup.TryGetValue(instanceID, out instance))
            {
                instance = new RenderMaterialInstance(renderMaterial);
                renderMaterialLookup[instanceID] = instance;
                unknownRenderMaterialInstanceIDs.Remove(instanceID);
            }
            instance.refCount++;
            return instance;
        }

        static PhysicsMaterialInstance IncRefCount(PhysicMaterial physicsMaterial)
        {
            if (!physicsMaterial)
                return null;
            var instanceID = physicsMaterial.GetInstanceID();
            PhysicsMaterialInstance instance;
            if (!physicsMaterialLookup.TryGetValue(instanceID, out instance))
            {
                instance = new PhysicsMaterialInstance(physicsMaterial);
                physicsMaterialLookup[instanceID] = instance;
                unknownPhysicsMaterialInstanceIDs.Remove(instanceID);
            }
            instance.refCount++;
            return instance;
        }

        static void DecRefCount(Material renderMaterial)
        {
            if (!renderMaterial)
                return;
            var instanceID = renderMaterial.GetInstanceID();
            RenderMaterialInstance instance;
            if (!renderMaterialLookup.TryGetValue(instanceID, out instance))
                return;
            instance.refCount--;
            if (instance.refCount <= 0)
                removeRenderMaterials.Add(instanceID);
        }

        static void DecRefCount(PhysicMaterial physicsMaterial)
        {
            if (!physicsMaterial)
                return;
            var instanceID = physicsMaterial.GetInstanceID();
            PhysicsMaterialInstance instance;
            if (!physicsMaterialLookup.TryGetValue(instanceID, out instance))
                return;
            instance.refCount--;
            if (instance.refCount <= 0)
            {
                removePhysicsMaterials.Add(instanceID);
            }
        }

        public static void OnLayerUsageFlagsChanged(CSGSurfaceAsset surfaceAsset, LayerUsageFlags prevValue, LayerUsageFlags value)
        {
            if (!registeredLookup.Contains(surfaceAsset) || (prevValue == value))
                return;
            
            if (OnSurfaceAssetChanged != null)
                OnSurfaceAssetChanged(surfaceAsset);
        }

        public static void OnRenderMaterialChanged(CSGSurfaceAsset surfaceAsset, Material prevValue, Material value)
        {
            if (!registeredLookup.Contains(surfaceAsset) || (prevValue == value))
                return;
            
            DecRefCount(prevValue);
            IncRefCount(value);

            if (OnSurfaceAssetChanged != null)
                OnSurfaceAssetChanged(surfaceAsset);
        }

        public static void OnPhysicsMaterialChanged(CSGSurfaceAsset surfaceAsset, PhysicMaterial prevValue, PhysicMaterial value)
        {
            if (!registeredLookup.Contains(surfaceAsset) || (prevValue == value))
                return;

            DecRefCount(prevValue);
            IncRefCount(value);

            if (OnSurfaceAssetChanged != null)
                OnSurfaceAssetChanged(surfaceAsset);
        }

        public static void NotifyContentsModified(CSGSurfaceAsset surfaceAsset)
        {
            if (surfaceAsset == null || !registeredLookup.Contains(surfaceAsset))
                return;

            RenderMaterialInstance renderMaterialInstance;
            if (surfaceRenderMaterialLookup.TryGetValue(surfaceAsset, out renderMaterialInstance))
            {
                if ((renderMaterialInstance == null && surfaceAsset.RenderMaterial) ||
                    (renderMaterialInstance != null && renderMaterialInstance.renderMaterial != surfaceAsset.RenderMaterial))
                {
                    if (renderMaterialInstance != null)
                        DecRefCount(renderMaterialInstance.renderMaterial);
                    surfaceRenderMaterialLookup[surfaceAsset] = IncRefCount(surfaceAsset.RenderMaterial);
                }
            }
            PhysicsMaterialInstance physicsMaterialInstance;
            if (surfacePhysicsMaterialLookup.TryGetValue(surfaceAsset, out physicsMaterialInstance))
            {
                if ((physicsMaterialInstance == null && surfaceAsset.PhysicsMaterial) ||
                    (physicsMaterialInstance != null && physicsMaterialInstance.physicsMaterial != surfaceAsset.PhysicsMaterial))
                {
                    if (physicsMaterialInstance != null)
                        DecRefCount(physicsMaterialInstance.physicsMaterial);
                    surfacePhysicsMaterialLookup[surfaceAsset] = IncRefCount(surfaceAsset.PhysicsMaterial);
                }
            }
            
            if (OnSurfaceAssetChanged != null)
                OnSurfaceAssetChanged(surfaceAsset);
        }

        public static void Update()
        {
            if (removeRenderMaterials.Count == 0 &&
                removePhysicsMaterials.Count == 0)
                return;

            foreach(var instanceID in removeRenderMaterials)
            {
                RenderMaterialInstance instance;
                if (renderMaterialLookup.TryGetValue(instanceID, out instance))
                {
                    if (instance.refCount <= 0) // it might have been re-added in the meantime
                        renderMaterialLookup.Remove(instanceID);
                }
            }
            removeRenderMaterials.Clear();

            foreach (var instanceID in removePhysicsMaterials)
            {
                PhysicsMaterialInstance instance;
                if (physicsMaterialLookup.TryGetValue(instanceID, out instance))
                {
                    if (instance.refCount <= 0) // it might have been re-added in the meantime
                        physicsMaterialLookup.Remove(instanceID);
                }
            }
            removePhysicsMaterials.Clear();
        }
    }
}
