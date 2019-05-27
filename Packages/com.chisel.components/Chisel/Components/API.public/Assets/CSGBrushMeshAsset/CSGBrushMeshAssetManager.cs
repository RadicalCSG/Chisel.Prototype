using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chisel.Assets
{
    public delegate void OnBrushMeshAssetDelegate(CSGBrushMeshAsset brushMeshAsset);
 
    public static class CSGBrushMeshAssetManager
    {
        public static event OnBrushMeshAssetDelegate OnBrushMeshInstanceChanged;
        public static event OnBrushMeshAssetDelegate OnBrushMeshInstanceDestroyed;

        static readonly HashSet<CSGBrushMeshAsset>  registeredLookup        = new HashSet<CSGBrushMeshAsset>();

        static readonly HashSet<CSGBrushMeshAsset>  unregisterQueueLookup   = new HashSet<CSGBrushMeshAsset>();
        static readonly List<CSGBrushMeshAsset>     unregisterQueue         = new List<CSGBrushMeshAsset>();

        static readonly HashSet<CSGBrushMeshAsset>  updateQueueLookup       = new HashSet<CSGBrushMeshAsset>();
        static readonly List<CSGBrushMeshAsset>     updateQueue             = new List<CSGBrushMeshAsset>();

        // Dictionaries used to keep track which surfaces are used by which brushMeshes, which is necessary to update the right brushMeshes when a used brushMaterial has been changed
        static readonly Dictionary<CSGBrushMeshAsset, HashSet<ChiselBrushMaterial>> brushMeshSurfaces  = new Dictionary<CSGBrushMeshAsset, HashSet<ChiselBrushMaterial>>();
        static readonly Dictionary<ChiselBrushMaterial, HashSet<CSGBrushMeshAsset>> surfaceBrushMeshes = new Dictionary<ChiselBrushMaterial, HashSet<CSGBrushMeshAsset>>();
        

        static CSGBrushMeshAssetManager()
        {
            ChiselBrushMaterialManager.OnBrushMaterialChanged -= OnChiselBrushMaterialChanged;
            ChiselBrushMaterialManager.OnBrushMaterialChanged += OnChiselBrushMaterialChanged;

            ChiselBrushMaterialManager.OnBrushMaterialRemoved -= OnChiselBrushMaterialRemoved;
            ChiselBrushMaterialManager.OnBrushMaterialRemoved += OnChiselBrushMaterialRemoved;

            ChiselBrushMaterialManager.OnBrushMaterialsReset -= OnChiselBrushMaterialsReset;
            ChiselBrushMaterialManager.OnBrushMaterialsReset += OnChiselBrushMaterialsReset;
        }

        static void Clear()
        {
            registeredLookup        .Clear();

            unregisterQueueLookup   .Clear();
            unregisterQueue         .Clear();

            updateQueueLookup       .Clear();
            updateQueue             .Clear();

            brushMeshSurfaces		.Clear();
            surfaceBrushMeshes		.Clear();	

        }

        public static void Reset()
        {
            Clear();
            var brushMeshAssets = Resources.FindObjectsOfTypeAll<CSGBrushMeshAsset>();
            foreach (var brushMeshAsset in brushMeshAssets)
            {
                Register(brushMeshAsset);
                brushMeshAsset.CreateInstances();
            }
        }

        #region Lifetime
        public static bool SetDirty(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!registeredLookup    .Contains(brushMeshAsset) ||
                unregisterQueueLookup.Contains(brushMeshAsset))
                return false;
            
            if (updateQueueLookup.Add(brushMeshAsset))
                updateQueue.Add(brushMeshAsset);
            return true;
        }

        public static bool IsDirty(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!brushMeshAsset)
                return false;
            return updateQueueLookup.Contains(brushMeshAsset);
        }

        public static void UnregisterAllSurfaces(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!brushMeshAsset || brushMeshAsset.SubMeshes == null)
                return;
            foreach (var subMesh in brushMeshAsset.SubMeshes)
            {
                if (subMesh.Polygons == null)
                    continue;
                foreach (var polygon in subMesh.Polygons)
                {
                    ChiselBrushMaterialManager.Unregister(polygon.brushMaterial);
                }
            }
        }

        public static void RegisterAllSurfaces(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!brushMeshAsset || brushMeshAsset.SubMeshes == null)
                return;
            foreach (var subMesh in brushMeshAsset.SubMeshes)
            {
                if (subMesh.Polygons == null)
                    continue;
                foreach (var polygon in subMesh.Polygons)
                {
                    ChiselBrushMaterialManager.Register(polygon.brushMaterial);
                }
            }
        }

        public static void RegisterAllSurfaces()
        {
            foreach (var brushMeshAsset in registeredLookup)
            {
                RegisterAllSurfaces(brushMeshAsset);
            }
        }

        public static bool IsBrushMeshUnique(CSGBrushMeshAsset currentBrushMesh)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(currentBrushMesh.GetInstanceID());
            if (!string.IsNullOrEmpty(path))
                return false;
#endif
            // TODO: implement

            return true;
        }

        public static void Register(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!brushMeshAsset)
                return;
            
            if (!registeredLookup.Add(brushMeshAsset))
                return;
            
            if (updateQueueLookup    .Add   (brushMeshAsset)) updateQueue    .Add   (brushMeshAsset);
            if (unregisterQueueLookup.Remove(brushMeshAsset)) unregisterQueue.Remove(brushMeshAsset);
        }

        public static void Unregister(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!registeredLookup.Remove(brushMeshAsset))
                return;
            
            if (updateQueueLookup    .Remove(brushMeshAsset)) updateQueue    .Remove(brushMeshAsset);
            if (unregisterQueueLookup.Add   (brushMeshAsset)) unregisterQueue.Add   (brushMeshAsset);
        }

        public static bool IsRegistered(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!brushMeshAsset)
                return false;
            return registeredLookup.Contains(brushMeshAsset);
        }
        #endregion

        public static void NotifyContentsModified(CSGBrushMeshAsset brushMeshAsset)
        {
            if (!brushMeshAsset || !registeredLookup.Contains(brushMeshAsset))
                return;
            
            updateQueue.Add(brushMeshAsset);
        }

        static void OnChiselBrushMaterialsReset()
        {
            CSGBrushMeshAssetManager.RegisterAllSurfaces();
        }

        static void OnChiselBrushMaterialChanged(ChiselBrushMaterial brushMaterial)
        {
            if (brushMaterial == null)
                return;

            HashSet<CSGBrushMeshAsset> brushMeshAssets;
            if (!surfaceBrushMeshes.TryGetValue(brushMaterial, out brushMeshAssets))
                return;
            
            foreach (var brushMeshAsset in brushMeshAssets)
            {
                if (brushMeshAsset)
                    updateQueue.Add(brushMeshAsset);
            }
        }

        static void OnChiselBrushMaterialRemoved(ChiselBrushMaterial brushMaterial)
        {
            HashSet<CSGBrushMeshAsset> brushMeshAssets;
            if (surfaceBrushMeshes.TryGetValue(brushMaterial, out brushMeshAssets))
            {
                foreach (var brushMeshAsset in brushMeshAssets)
                {
                    HashSet<ChiselBrushMaterial> uniqueSurfaces;
                    if (brushMeshSurfaces.TryGetValue(brushMeshAsset, out uniqueSurfaces))
                    {
                        uniqueSurfaces.Remove(brushMaterial);
                        if (brushMeshAsset)
                            updateQueue.Add(brushMeshAsset);
                    }
                } 
            }
            surfaceBrushMeshes.Remove(brushMaterial);
        }

        // TODO: shouldn't be public
        public static void UpdateSurfaces(CSGBrushMeshAsset brushMeshAsset)
        {
            HashSet<ChiselBrushMaterial> uniqueSurfaces;
            if (brushMeshSurfaces.TryGetValue(brushMeshAsset, out uniqueSurfaces))
            {
                // Remove previously set surfaces for this brushMesh
                foreach (var brushMaterial in uniqueSurfaces)
                {
                    if (Equals(null, brushMaterial))
                        continue;

                    HashSet<CSGBrushMeshAsset> brushMeshAssets;
                    if (surfaceBrushMeshes.TryGetValue(brushMaterial, out brushMeshAssets))
                        brushMeshAssets.Remove(brushMeshAsset);
                }
                uniqueSurfaces.Clear();
            } else
                uniqueSurfaces = new HashSet<ChiselBrushMaterial>();
            
            var polygons		= brushMeshAsset.Polygons;
            if (polygons != null)
            {
                for (int i = 0; i < polygons.Length; i++)
                {
                    var brushMaterial = polygons[i].brushMaterial;
                    if (Equals(null, brushMaterial))
                        continue;

                    // Add current surfaces of this brushMesh
                    if (uniqueSurfaces.Add(brushMaterial))
                    {
                        HashSet<CSGBrushMeshAsset> brushMeshAssets;
                        if (!surfaceBrushMeshes.TryGetValue(brushMaterial, out brushMeshAssets))
                        {
                            brushMeshAssets = new HashSet<CSGBrushMeshAsset>();
                            surfaceBrushMeshes[brushMaterial] = brushMeshAssets;
                        }
                        brushMeshAssets.Add(brushMeshAsset);
                    }
                }
            }
            brushMeshSurfaces[brushMeshAsset] = uniqueSurfaces;
        }

        static void RemoveSurfaces(CSGBrushMeshAsset brushMeshAsset)
        {
            // NOTE: brushMeshAsset is likely destroyed at this point, it can still be used as a lookup key however.
            
            HashSet<ChiselBrushMaterial> uniqueSurfaces;
            if (brushMeshSurfaces.TryGetValue(brushMeshAsset, out uniqueSurfaces))
            {
                // Remove previously set surfaces for this brushMesh
                foreach (var brushMaterial in uniqueSurfaces)
                {
                    HashSet<CSGBrushMeshAsset> brushMeshAssets;
                    if (surfaceBrushMeshes.TryGetValue(brushMaterial, out brushMeshAssets))
                        brushMeshAssets.Remove(brushMeshAsset);
                }
                uniqueSurfaces.Clear();
            }
            brushMeshSurfaces.Remove(brushMeshAsset);
        }

        public static void Update()
        {
            if (unregisterQueue.Count == 0 &&
                updateQueue.Count == 0)
                return;

            for (int i = 0; i < unregisterQueue.Count; i++)
            {
                var brushMeshAsset = unregisterQueue[i];
                RemoveSurfaces(brushMeshAsset);
                UnregisterAllSurfaces(brushMeshAsset);

                if (brushMeshAsset)
                {
                    var instances = brushMeshAsset.Instances;
                    if (instances != null && instances[0].Valid)
                        brushMeshAsset.DestroyInstances();
                }

                if (OnBrushMeshInstanceDestroyed != null)
                    OnBrushMeshInstanceDestroyed(brushMeshAsset);
            }
            unregisterQueue.Clear();
            unregisterQueueLookup.Clear();
            
            for (int i = 0; i < updateQueue.Count; i++)
            {
                var brushMeshAsset = updateQueue[i];
                if (!brushMeshAsset)
                    continue;

                try
                {
                    if (!brushMeshAsset.HasInstances)
                    {
                        brushMeshAsset.CreateInstances();
                    } else
                    {
                        //UnregisterAllSurfaces(brushMeshAsset); // TODO: should we?
                        brushMeshAsset.UpdateInstances();
                    }

                    RegisterAllSurfaces(brushMeshAsset);
                    UpdateSurfaces(brushMeshAsset);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                if (OnBrushMeshInstanceChanged != null)
                    OnBrushMeshInstanceChanged(brushMeshAsset);
            }
            updateQueue.Clear();
            updateQueueLookup.Clear();
        }
    }
}
