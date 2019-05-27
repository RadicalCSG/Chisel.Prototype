using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Assets
{
    public delegate void OnBrushMaterialDelegate(ChiselBrushMaterial brushMaterial);
    
    public static class ChiselBrushMaterialManager
    {
        public static event OnBrushMaterialDelegate OnBrushMaterialChanged;
        public static event OnBrushMaterialDelegate OnBrushMaterialRemoved;
        public static event OnBrushMaterialDelegate OnBrushMaterialAdded;
        public static event Action OnBrushMaterialsReset;

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

        static Dictionary<int, RenderMaterialInstance>  instanceIdToRenderMaterialLookup    = new Dictionary<int, RenderMaterialInstance>();
        static Dictionary<int, PhysicsMaterialInstance> instanceIdToPhysicsMaterialLookup	= new Dictionary<int, PhysicsMaterialInstance>();
        
        static Dictionary<ChiselBrushMaterial, RenderMaterialInstance>  brushMaterialToRenderMaterialLookup		= new Dictionary<ChiselBrushMaterial, RenderMaterialInstance>();
        static Dictionary<ChiselBrushMaterial, PhysicsMaterialInstance> brushMaterialToPhysicsMaterialLookup	= new Dictionary<ChiselBrushMaterial, PhysicsMaterialInstance>();

        static readonly HashSet<ChiselBrushMaterial> registeredLookup       = new HashSet<ChiselBrushMaterial>();
        
        static readonly HashSet<int> removeRenderMaterials  = new HashSet<int>();
        static readonly HashSet<int> removePhysicsMaterials = new HashSet<int>();

        static readonly HashSet<int> unknownRenderMaterialInstanceIDs	= new HashSet<int>();
        static readonly HashSet<int> unknownPhysicsMaterialInstanceIDs	= new HashSet<int>();
        

        static void Clear()
        {
            instanceIdToRenderMaterialLookup.Clear();
            instanceIdToPhysicsMaterialLookup.Clear();
            brushMaterialToRenderMaterialLookup.Clear();
            brushMaterialToPhysicsMaterialLookup.Clear();
            registeredLookup.Clear();
            removeRenderMaterials.Clear();
            removePhysicsMaterials.Clear();
        }

        public static void Reset()
        {
            Clear();
            OnBrushMaterialsReset?.Invoke();
        }


        public static Material GetRenderMaterialByInstanceID(int instanceID, bool reregisterAllIfNotFound = true)
        {
            RenderMaterialInstance renderMaterialInstance;
            if (instanceIdToRenderMaterialLookup.TryGetValue(instanceID, out renderMaterialInstance))
                return renderMaterialInstance.renderMaterial;
            
            // See if we already, unsuccessfully, tried finding this instanceID
            if (unknownRenderMaterialInstanceIDs.Contains(instanceID))
            {
                Debug.LogError("Could not find Material with instanceID " + instanceID + ".");
                return null;
            }

            // See if we've, somehow, passed the instanceID of a PhysicMaterial (we're expecting a Material)
            if (instanceIdToPhysicsMaterialLookup.ContainsKey(instanceID))
            {
                Debug.LogError("Trying to use PhysicMaterial with instanceID " + instanceID + " as a Material.");
                return null;
            } else
            if (reregisterAllIfNotFound)
            {
                // This should never happen, but just in case it does, we try finding all brush materials and reregister all of them (slow)
                Reset();
                Debug.LogWarning("Performance warning: Could not find Material with instanceID " + instanceID + ". Reregistering all brush materials to find it.");
                if (instanceIdToRenderMaterialLookup.TryGetValue(instanceID, out renderMaterialInstance))
                    return renderMaterialInstance.renderMaterial;
            }
            unknownRenderMaterialInstanceIDs.Add(instanceID);
            Debug.LogError("Could not find Material with instanceID " + instanceID + ".");
            return null;
        }

        public static PhysicMaterial GetPhysicsMaterialByInstanceID(int instanceID, bool reregisterAllIfNotFound = true)
        {
            PhysicsMaterialInstance physicsMaterialInstance;
            if (instanceIdToPhysicsMaterialLookup.TryGetValue(instanceID, out physicsMaterialInstance))
                return physicsMaterialInstance.physicsMaterial;
            
            // See if we already, unsuccessfully, tried finding this instanceID
            if (unknownPhysicsMaterialInstanceIDs.Contains(instanceID))
            {
                Debug.LogError("Could not find PhysicMaterial with instanceID " + instanceID + ".");
                return null;
            }

            // See if we've, somehow, passed the instanceID of a Material (we're expecting a PhysicMaterial)
            if (instanceIdToRenderMaterialLookup.ContainsKey(instanceID))
            {
                Debug.LogError("Trying to use Material with instanceID " + instanceID + " as a PhysicMaterial.");
                return null;
            } else
            if (reregisterAllIfNotFound)
            {
                // This should never happen, but just in case it does, we try finding all brush materials and reregister all of them (slow)
                Reset();
                Debug.LogWarning("Performance warning: Could not find PhysicMaterial with instanceID " + instanceID + ". Reregistering all brush materials to find it.");
                if (instanceIdToPhysicsMaterialLookup.TryGetValue(instanceID, out physicsMaterialInstance))
                    return physicsMaterialInstance.physicsMaterial;
            }

            unknownPhysicsMaterialInstanceIDs.Add(instanceID);
            Debug.LogError("Could not find PhysicMaterial with instanceID " + instanceID + ".");
            return null;
        }

        public static int? GetRenderMaterialRefCountByInstanceID(int instanceID)
        {
            RenderMaterialInstance renderMaterialInstance;
            if (!instanceIdToRenderMaterialLookup.TryGetValue(instanceID, out renderMaterialInstance))
                return null;
            return renderMaterialInstance.refCount;
        }

        public static int? GetPhysicsMaterialRefCountByInstanceID(int instanceID)
        {
            PhysicsMaterialInstance physicsMaterialInstance;
            if (!instanceIdToPhysicsMaterialLookup.TryGetValue(instanceID, out physicsMaterialInstance))
                return null;
            return physicsMaterialInstance.refCount;
        }

        public static bool IsRegistered(ChiselBrushMaterial brushMaterial)
        {
            if (brushMaterial == null)
                return false;
            return registeredLookup.Contains(brushMaterial);
        }

        public static void Register(ChiselBrushMaterial brushMaterial)
        {
            if (brushMaterial == null || !registeredLookup.Add(brushMaterial)) return;

            brushMaterialToRenderMaterialLookup [brushMaterial] = IncRefCount(brushMaterial.RenderMaterial);
            brushMaterialToPhysicsMaterialLookup[brushMaterial] = IncRefCount(brushMaterial.PhysicsMaterial);

            OnBrushMaterialAdded?.Invoke(brushMaterial);
        }

        public static void Unregister(ChiselBrushMaterial brushMaterial)
        {
            if (!registeredLookup.Remove(brushMaterial)) return;

            brushMaterialToRenderMaterialLookup .Remove(brushMaterial);
            brushMaterialToPhysicsMaterialLookup.Remove(brushMaterial);

            DecRefCount(brushMaterial.RenderMaterial);
            DecRefCount(brushMaterial.PhysicsMaterial);

            OnBrushMaterialRemoved?.Invoke(brushMaterial);
        }

        static RenderMaterialInstance IncRefCount(Material renderMaterial)
        {
            if (!renderMaterial)
                return null;
            var instanceID = renderMaterial.GetInstanceID();
            RenderMaterialInstance instance;
            if (!instanceIdToRenderMaterialLookup.TryGetValue(instanceID, out instance))
            {
                instance = new RenderMaterialInstance(renderMaterial);
                instanceIdToRenderMaterialLookup[instanceID] = instance;
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
            if (!instanceIdToPhysicsMaterialLookup.TryGetValue(instanceID, out instance))
            {
                instance = new PhysicsMaterialInstance(physicsMaterial);
                instanceIdToPhysicsMaterialLookup[instanceID] = instance;
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
            if (!instanceIdToRenderMaterialLookup.TryGetValue(instanceID, out instance))
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
            if (!instanceIdToPhysicsMaterialLookup.TryGetValue(instanceID, out instance))
                return;
            instance.refCount--;
            if (instance.refCount <= 0)
            {
                removePhysicsMaterials.Add(instanceID);
            }
        }

        public static void OnLayerUsageFlagsChanged(ChiselBrushMaterial brushMaterial, LayerUsageFlags prevValue, LayerUsageFlags value)
        {
            if (!registeredLookup.Contains(brushMaterial) || (prevValue == value))
                return;

            OnBrushMaterialChanged?.Invoke(brushMaterial);
        }

        public static void OnRenderMaterialChanged(ChiselBrushMaterial brushMaterial, Material prevValue, Material value)
        {
            if (!registeredLookup.Contains(brushMaterial) || (prevValue == value))
                return;
            
            DecRefCount(prevValue);
            IncRefCount(value);

            OnBrushMaterialChanged?.Invoke(brushMaterial);
        }

        public static void OnPhysicsMaterialChanged(ChiselBrushMaterial brushMaterial, PhysicMaterial prevValue, PhysicMaterial value)
        {
            if (!registeredLookup.Contains(brushMaterial) || (prevValue == value))
                return;

            DecRefCount(prevValue);
            IncRefCount(value);

            OnBrushMaterialChanged?.Invoke(brushMaterial);
        }

        public static void NotifyContentsModified(ChiselBrushMaterial brushMaterial)
        {
            if (brushMaterial == null || !registeredLookup.Contains(brushMaterial))
                return;

            RenderMaterialInstance renderMaterialInstance;
            if (brushMaterialToRenderMaterialLookup.TryGetValue(brushMaterial, out renderMaterialInstance))
            {
                if ((renderMaterialInstance == null && brushMaterial.RenderMaterial) ||
                    (renderMaterialInstance != null && renderMaterialInstance.renderMaterial != brushMaterial.RenderMaterial))
                {
                    if (renderMaterialInstance != null)
                        DecRefCount(renderMaterialInstance.renderMaterial);
                    brushMaterialToRenderMaterialLookup[brushMaterial] = IncRefCount(brushMaterial.RenderMaterial);
                }
            }
            PhysicsMaterialInstance physicsMaterialInstance;
            if (brushMaterialToPhysicsMaterialLookup.TryGetValue(brushMaterial, out physicsMaterialInstance))
            {
                if ((physicsMaterialInstance == null && brushMaterial.PhysicsMaterial) ||
                    (physicsMaterialInstance != null && physicsMaterialInstance.physicsMaterial != brushMaterial.PhysicsMaterial))
                {
                    if (physicsMaterialInstance != null)
                        DecRefCount(physicsMaterialInstance.physicsMaterial);
                    brushMaterialToPhysicsMaterialLookup[brushMaterial] = IncRefCount(brushMaterial.PhysicsMaterial);
                }
            }

            OnBrushMaterialChanged?.Invoke(brushMaterial);
        }

        public static void Update()
        {
            if (removeRenderMaterials.Count == 0 &&
                removePhysicsMaterials.Count == 0)
                return;

            foreach(var instanceID in removeRenderMaterials)
            {
                RenderMaterialInstance instance;
                if (instanceIdToRenderMaterialLookup.TryGetValue(instanceID, out instance))
                {
                    if (instance.refCount <= 0) // it might have been re-added in the meantime
                        instanceIdToRenderMaterialLookup.Remove(instanceID);
                }
            }
            removeRenderMaterials.Clear();

            foreach (var instanceID in removePhysicsMaterials)
            {
                PhysicsMaterialInstance instance;
                if (instanceIdToPhysicsMaterialLookup.TryGetValue(instanceID, out instance))
                {
                    if (instance.refCount <= 0) // it might have been re-added in the meantime
                        instanceIdToPhysicsMaterialLookup.Remove(instanceID);
                }
            }
            removePhysicsMaterials.Clear();
        }
    }
}
