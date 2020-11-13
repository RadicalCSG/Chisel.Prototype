using System;
using System.Collections.Generic;
using UnityEngine;
using Chisel.Core;
using UnityEngine.Profiling;
using System.Linq;

namespace Chisel.Components
{
    public delegate void OnBrushContainerAssetDelegate(ChiselBrushContainerAsset brushContainerAsset);
 
    public static class ChiselBrushContainerAssetManager
    {
        public static event OnBrushContainerAssetDelegate   OnBrushMeshInstanceChanged;
        public static event OnBrushContainerAssetDelegate   OnBrushMeshInstanceDestroyed;

        static readonly HashSet<ChiselBrushContainerAsset>  registeredLookup        = new HashSet<ChiselBrushContainerAsset>();

        static readonly HashSet<ChiselBrushContainerAsset>  unregisterQueueLookup   = new HashSet<ChiselBrushContainerAsset>();
        static readonly List<ChiselBrushContainerAsset>     unregisterQueue         = new List<ChiselBrushContainerAsset>();

        static readonly HashSet<ChiselBrushContainerAsset>  updateQueueLookup       = new HashSet<ChiselBrushContainerAsset>();
        static readonly List<ChiselBrushContainerAsset>     updateQueue             = new List<ChiselBrushContainerAsset>();

        // Dictionaries used to keep track which surfaces are used by which brushMeshes, which is necessary to update the right brushMeshes when a used brushMaterial has been changed
        static readonly Dictionary<ChiselBrushContainerAsset, List<ChiselBrushMaterial>>    brushMeshSurfaces  = new Dictionary<ChiselBrushContainerAsset, List<ChiselBrushMaterial>>();
        static readonly Dictionary<ChiselBrushMaterial, List<ChiselBrushContainerAsset>>    surfaceBrushMeshes = new Dictionary<ChiselBrushMaterial, List<ChiselBrushContainerAsset>>();
        

        static ChiselBrushContainerAssetManager()
        {
            ChiselBrushMaterialManager.OnBrushMaterialChanged -= OnChiselBrushMaterialChanged;
            ChiselBrushMaterialManager.OnBrushMaterialChanged += OnChiselBrushMaterialChanged;

            ChiselBrushMaterialManager.OnBrushMaterialRemoved -= OnChiselBrushMaterialRemoved;
            ChiselBrushMaterialManager.OnBrushMaterialRemoved += OnChiselBrushMaterialRemoved;

            ChiselBrushMaterialManager.OnBrushMaterialsReset -= OnChiselBrushMaterialsReset;
            ChiselBrushMaterialManager.OnBrushMaterialsReset += OnChiselBrushMaterialsReset;
        }

        static List<ChiselBrushContainerAsset>  sRemoveChiselBrushContainerAsset   = new List<ChiselBrushContainerAsset>();
        static List<ChiselBrushMaterial>        sRemoveChiselBrushMaterial         = new List<ChiselBrushMaterial>();
        static void Clear()
        {
            registeredLookup        .Clear();

            unregisterQueueLookup   .Clear();
            unregisterQueue         .Clear();

            updateQueueLookup       .Clear();
            updateQueue             .Clear();

            if (sRemoveChiselBrushContainerAsset.Capacity < brushMeshSurfaces.Count)
                sRemoveChiselBrushContainerAsset.Capacity = brushMeshSurfaces.Count;
            foreach (var item in brushMeshSurfaces)
            {
                if (item.Key == null)
                {
                    sRemoveChiselBrushContainerAsset.Add(item.Key);
                    continue;
                }
                item.Value.Clear();
            }
            foreach (var item in sRemoveChiselBrushContainerAsset)
                brushMeshSurfaces.Remove(item);
            sRemoveChiselBrushContainerAsset.Clear();

            if (sRemoveChiselBrushMaterial.Capacity < surfaceBrushMeshes.Count)
                sRemoveChiselBrushMaterial.Capacity = surfaceBrushMeshes.Count;
            foreach (var item in surfaceBrushMeshes)
            {
                if (item.Key == null)
                {
                    sRemoveChiselBrushMaterial.Add(item.Key);
                    continue;
                }
                item.Value.Clear();
            }
            foreach (var item in sRemoveChiselBrushMaterial)
                surfaceBrushMeshes.Remove(item);
            sRemoveChiselBrushMaterial.Clear();
        }

        static readonly List<ChiselBrushContainerAsset>     s_NodeBrushes       = new List<ChiselBrushContainerAsset>();
        static readonly HashSet<ChiselBrushContainerAsset>  s_UsedContainers    = new HashSet<ChiselBrushContainerAsset>();
        public static void Reset()
        {
            Clear();
            s_UsedContainers.Clear();
            var chiselNodes     = Resources.FindObjectsOfTypeAll<ChiselNode>();
            foreach (var chiselNode in chiselNodes)
            {
                s_NodeBrushes.Clear();
                if (!chiselNode.GetUsedGeneratedBrushes(s_NodeBrushes))
                    continue;
                foreach(var asset in s_NodeBrushes)
                    s_UsedContainers.Add(asset);
            }
            var brushContainerAssets = Resources.FindObjectsOfTypeAll<ChiselBrushContainerAsset>();
            foreach (var brushContainerAsset in brushContainerAssets)
            {
                if (!s_UsedContainers.Contains(brushContainerAsset))
                {
#if UNITY_EDITOR
                    var path = UnityEditor.AssetDatabase.GetAssetPath(brushContainerAsset);
                    if (string.IsNullOrEmpty(path))
#endif
                    {
                        ChiselObjectUtility.SafeDestroy(brushContainerAsset);
                        continue;
                    }
                }
                Register(brushContainerAsset);
                brushContainerAsset.CreateInstances();
            }
        }

        #region Lifetime
        public static bool SetDirty(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!registeredLookup    .Contains(brushContainerAsset) ||
                unregisterQueueLookup.Contains(brushContainerAsset))
                return false;
            
            if (updateQueueLookup.Add(brushContainerAsset))
                updateQueue.Add(brushContainerAsset);
            return true;
        }

        public static bool IsDirty(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!brushContainerAsset)
                return false;
            return updateQueueLookup.Contains(brushContainerAsset);
        }

        public static void UnregisterAllSurfaces(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!brushContainerAsset || brushContainerAsset.SubMeshCount == 0)
                return;
            foreach (var brushMesh in brushContainerAsset.BrushMeshes)
            {
                if (brushMesh.polygons == null)
                    continue;
                foreach (var polygon in brushMesh.polygons)
                {
                    if (polygon.surface == null ||
                        polygon.surface.brushMaterial == null)
                        continue;
                    ChiselBrushMaterialManager.Unregister(polygon.surface.brushMaterial);
                }
            }
        }

        public static void RegisterAllSurfaces(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!brushContainerAsset || brushContainerAsset.SubMeshCount == 0)
                return;
            foreach (var brushMesh in brushContainerAsset.BrushMeshes)
            {
                if (brushMesh.polygons == null)
                    continue;
                foreach (var polygon in brushMesh.polygons)
                {
                    if (polygon.surface == null)
                        continue;
                    if (polygon.surface.brushMaterial == null)
                    {
                        polygon.surface.brushMaterial = ChiselBrushMaterial.CreateInstance();
                        if (polygon.surface.brushMaterial != null)
                        {
                            polygon.surface.brushMaterial.LayerUsage        = polygon.surface.brushMaterial.LayerUsage;
                            polygon.surface.brushMaterial.PhysicsMaterial   = polygon.surface.brushMaterial.PhysicsMaterial;
                            polygon.surface.brushMaterial.RenderMaterial    = polygon.surface.brushMaterial.RenderMaterial;
                        }
                    }
                    ChiselBrushMaterialManager.Register(polygon.surface.brushMaterial);
                }
            }
        }

        public static void RegisterAllSurfaces()
        {
            foreach (var brushContainerAsset in registeredLookup)
            {
                RegisterAllSurfaces(brushContainerAsset);
            }
        }

        public static bool IsBrushMeshUnique(ChiselBrushContainerAsset currentBrushMesh)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(currentBrushMesh.GetInstanceID());
            if (!string.IsNullOrEmpty(path))
                return false;
#endif
            // TODO: implement

            return true;
        }

        public static void Register(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!brushContainerAsset)
                return;
            
            if (!registeredLookup.Add(brushContainerAsset))
                return;
            
            if (updateQueueLookup    .Add   (brushContainerAsset)) updateQueue    .Add   (brushContainerAsset);
            if (unregisterQueueLookup.Remove(brushContainerAsset)) unregisterQueue.Remove(brushContainerAsset);
        }

        public static void Unregister(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!registeredLookup.Remove(brushContainerAsset))
                return;
            
            if (updateQueueLookup    .Remove(brushContainerAsset)) updateQueue    .Remove(brushContainerAsset);
            if (unregisterQueueLookup.Add   (brushContainerAsset)) unregisterQueue.Add   (brushContainerAsset);
        }

        public static bool IsRegistered(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!brushContainerAsset)
                return false;
            return registeredLookup.Contains(brushContainerAsset);
        }
        #endregion

        public static void NotifyContentsModified(ChiselBrushContainerAsset brushContainerAsset)
        {
            if (!brushContainerAsset || !registeredLookup.Contains(brushContainerAsset))
                return;

            if (updateQueueLookup.Add(brushContainerAsset)) 
                updateQueue.Add(brushContainerAsset);
        }

        static void OnChiselBrushMaterialsReset()
        {
            // TODO: what about the previously registered surfaces for this brush??

            ChiselBrushContainerAssetManager.RegisterAllSurfaces();
        }

        static void OnChiselBrushMaterialChanged(ChiselBrushMaterial brushMaterial)
        {
            if (brushMaterial == null)
                return;

            if (!surfaceBrushMeshes.TryGetValue(brushMaterial, out var brushContainerAssets))
                return;
            
            foreach (var brushContainerAsset in brushContainerAssets)
            {
                if (brushContainerAsset)
                {
                    if (updateQueueLookup.Add(brushContainerAsset))
                        updateQueue.Add(brushContainerAsset);
                }
            }
        }

        static void OnChiselBrushMaterialRemoved(ChiselBrushMaterial brushMaterial)
        {
            if (surfaceBrushMeshes.TryGetValue(brushMaterial, out var brushContainerAssets))
            {
                foreach (var brushContainerAsset in brushContainerAssets)
                {
                    if (brushMeshSurfaces.TryGetValue(brushContainerAsset, out var uniqueSurfaces))
                    {
                        uniqueSurfaces.Remove(brushMaterial);
                        if (brushContainerAsset)
                        {
                            if (updateQueueLookup.Add(brushContainerAsset))
                                updateQueue.Add(brushContainerAsset);
                        }
                    }
                } 
            }
            surfaceBrushMeshes.Remove(brushMaterial);
        }

        // TODO: shouldn't be public
        public static void UpdateSurfaces(ChiselBrushContainerAsset brushContainerAsset)
        {
            var brushMeshes = brushContainerAsset.BrushMeshes;
            if (brushMeshes == null)
                return;

            for (int m = 0; m < brushMeshes.Length; m++)
            {
                ref var brushMesh = ref brushMeshes[m];
                if (brushMesh == null)
                    continue;
                UpdateSurfaces(ref brushMesh, brushContainerAsset);
            }
        }

        static void UpdateSurfaces(ref BrushMesh brushMesh, ChiselBrushContainerAsset brushContainerAsset)
        {
            if (brushMesh == null)
                return;
            Profiler.BeginSample("uniqueSurfaces");
            if (brushMeshSurfaces.TryGetValue(brushContainerAsset, out var uniqueSurfaces))
            {
                // Remove previously set surfaces for this brushMesh
                foreach (var brushMaterial in uniqueSurfaces)
                {
                    if (Equals(null, brushMaterial))
                        continue;

                    if (surfaceBrushMeshes.TryGetValue(brushMaterial, out var surfaceGeneratedBrushes))
                        surfaceGeneratedBrushes.Remove(brushContainerAsset);
                }
                uniqueSurfaces.Clear();
            } else
            {
                uniqueSurfaces = new List<ChiselBrushMaterial>();
                brushMeshSurfaces[brushContainerAsset] = uniqueSurfaces;
            }
            Profiler.EndSample();

            Profiler.BeginSample("polygons");
            var polygons = brushMesh.polygons;
            if (polygons != null)
            {
                for (int i = 0; i < polygons.Length; i++)
                {
                    var surface = brushMesh.polygons[i].surface;
                    if (surface == null)
                        continue;

                    var brushMaterial = surface.brushMaterial;
                    if (Equals(null, brushMaterial))
                        continue;

                    // We've already processed this material in this loop
                    if (uniqueSurfaces.Contains(brushMaterial))
                        continue;

                    // Add current surfaces of this brushMesh
                    Profiler.BeginSample("uniqueSurfaces.Add");
                    uniqueSurfaces.Add(brushMaterial);
                    Profiler.EndSample();
                    
                    if (!surfaceBrushMeshes.TryGetValue(brushMaterial, out var brushContainerAssets))
                    {
                        Profiler.BeginSample("brushContainerAssets");
                        brushContainerAssets = new List<ChiselBrushContainerAsset>();
                        Profiler.EndSample();
                        Profiler.BeginSample("surfaceBrushMeshes.Set");
                        surfaceBrushMeshes.Add(brushMaterial, brushContainerAssets);
                        Profiler.EndSample();
                        Profiler.BeginSample("brushContainerAssets.Add");
                        brushContainerAssets.Add(brushContainerAsset);
                        Profiler.EndSample();
                    } else
                    {
                        if (!brushContainerAssets.Contains(brushContainerAsset))
                        {
                            Profiler.BeginSample("brushContainerAssets.Add");
                            brushContainerAssets.Add(brushContainerAsset);
                            Profiler.EndSample();
                        }
                    }
                }
            }
            Profiler.EndSample();
        }

        static void RemoveSurfaces(ChiselBrushContainerAsset brushContainerAsset)
        {
            // NOTE: brushContainerAsset is likely destroyed at this point, it can still be used as a lookup key however.
            
            if (brushMeshSurfaces.TryGetValue(brushContainerAsset, out var uniqueSurfaces))
            {
                // Remove previously set surfaces for this brushMesh
                foreach (var brushMaterial in uniqueSurfaces)
                {
                    if (surfaceBrushMeshes.TryGetValue(brushMaterial, out var brushContainerAssets))
                        brushContainerAssets.Remove(brushContainerAsset);
                }
                uniqueSurfaces.Clear();
            }
            brushMeshSurfaces.Remove(brushContainerAsset);
        }


        static HashSet<int> modifiedBrushMeshes = new HashSet<int>();
        public static void Update()
        {
            if (unregisterQueue.Count == 0 &&
                updateQueue.Count == 0)
                return;

            for (int i = 0; i < unregisterQueue.Count; i++)
            {
                var brushContainerAsset = unregisterQueue[i];
                RemoveSurfaces(brushContainerAsset);
                UnregisterAllSurfaces(brushContainerAsset);

                if (brushContainerAsset)
                {
                    var instances = brushContainerAsset.Instances;
                    if (instances != null && instances[0].Valid)
                        brushContainerAsset.DestroyInstances();
                }
            }
            Profiler.BeginSample("OnBrushMeshInstanceDestroyed");
            if (OnBrushMeshInstanceDestroyed != null)
            {
                for (int i = 0; i < unregisterQueue.Count; i++)
                {
                    var brushContainerAsset = unregisterQueue[i];
                    OnBrushMeshInstanceDestroyed(brushContainerAsset);
                }
            }
            Profiler.EndSample();

            unregisterQueue.Clear();
            unregisterQueueLookup.Clear();
            modifiedBrushMeshes.Clear();
            for (int i = 0; i < updateQueue.Count; i++)
            {
                var brushContainerAsset = updateQueue[i];
                if (!brushContainerAsset)
                    continue;

                try
                {
                    if (!brushContainerAsset.HasInstances)
                    {
                        Profiler.BeginSample("CreateInstances");
                        brushContainerAsset.CreateInstances();
                        Profiler.EndSample();
                    } else
                    {
                        //UnregisterAllSurfaces(brushContainerAsset); // TODO: should we?
                        Profiler.BeginSample("UpdateInstances");
                        brushContainerAsset.UpdateInstances(modifiedBrushMeshes);
                        Profiler.EndSample();
                    }

                    Profiler.BeginSample("RegisterAllSurfaces");
                    RegisterAllSurfaces(brushContainerAsset);
                    Profiler.EndSample();
                    Profiler.BeginSample("UpdateSurfaces");
                    UpdateSurfaces(brushContainerAsset);
                    Profiler.EndSample();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (modifiedBrushMeshes.Count > 0)
            {
                CSGManager.NotifyBrushMeshModified(modifiedBrushMeshes);
            }

            Profiler.BeginSample("OnBrushMeshInstanceChanged");
            if (OnBrushMeshInstanceChanged != null)
            {
                for (int i = 0; i < updateQueue.Count; i++)
                {
                    var brushContainerAsset = updateQueue[i];
                    if (!brushContainerAsset)
                        continue;
                    OnBrushMeshInstanceChanged(brushContainerAsset);
                }
            }
            Profiler.EndSample();

            updateQueue.Clear();
            updateQueueLookup.Clear();
        }
    }
}
