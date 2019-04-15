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

        // Dictionaries used to keep track which surfaces are used by which brushMeshes, which is necessary to update the right brushMeshes when a used surfaceAsset has been changed
        static readonly Dictionary<CSGBrushMeshAsset, HashSet<CSGSurfaceAsset>> brushMeshSurfaces  = new Dictionary<CSGBrushMeshAsset, HashSet<CSGSurfaceAsset>>();
        static readonly Dictionary<CSGSurfaceAsset, HashSet<CSGBrushMeshAsset>> surfaceBrushMeshes = new Dictionary<CSGSurfaceAsset, HashSet<CSGBrushMeshAsset>>();
        

        static CSGBrushMeshAssetManager()
        {
            CSGSurfaceAssetManager.OnSurfaceAssetChanged -= OnSurfaceAssetChanged;
            CSGSurfaceAssetManager.OnSurfaceAssetChanged += OnSurfaceAssetChanged;

            CSGSurfaceAssetManager.OnSurfaceAssetRemoved -= OnSurfaceAssetRemoved;
            CSGSurfaceAssetManager.OnSurfaceAssetRemoved += OnSurfaceAssetRemoved;
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
                    CSGSurfaceAssetManager.Unregister(polygon.surfaceAsset);
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
                    CSGSurfaceAssetManager.Register(polygon.surfaceAsset);
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

            /*
        public static bool IsSurfaceUnique(CSGSurfaceAsset currentSurface)
        {
            return true;
            if (currentSurface == null)
                return false;
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(currentSurface.GetInstanceID());
            if (!string.IsNullOrEmpty(path))
                return false;
#endif
            HashSet<CSGBrushMeshAsset> brushMeshAssetLookup;
            if (!surfaceBrushMeshes.TryGetValue(currentSurface, out brushMeshAssetLookup))
                return true;

            if (brushMeshAssetLookup.Count == 0)
                return true;
            
            if (brushMeshAssetLookup.Count > 1)
                return false;

            var brushMeshAsset = brushMeshAssetLookup.First();
            var counter = 0;
            for (int p = 0; p < brushMeshAsset.Polygons.Length; p++)
            {
                var polygon = brushMeshAsset.Polygons[p];
                if (polygon.surfaceAsset == currentSurface)
                {
                    counter++;
                    if (counter > 1)
                        return false;
                }
            }

            return true;
            
        }
*/
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


        static void OnSurfaceAssetChanged(CSGSurfaceAsset surfaceAsset)
        {
            if (surfaceAsset == null)
                return;

            HashSet<CSGBrushMeshAsset> brushMeshAssets;
            if (!surfaceBrushMeshes.TryGetValue(surfaceAsset, out brushMeshAssets))
                return;
            
            foreach (var brushMeshAsset in brushMeshAssets)
            {
                if (brushMeshAsset)
                    updateQueue.Add(brushMeshAsset);
            }
        }

        static void OnSurfaceAssetRemoved(CSGSurfaceAsset surfaceAsset)
        {
            HashSet<CSGBrushMeshAsset> brushMeshAssets;
            if (surfaceBrushMeshes.TryGetValue(surfaceAsset, out brushMeshAssets))
            {
                foreach (var brushMeshAsset in brushMeshAssets)
                {
                    HashSet<CSGSurfaceAsset> uniqueSurfaces;
                    if (brushMeshSurfaces.TryGetValue(brushMeshAsset, out uniqueSurfaces))
                    {
                        uniqueSurfaces.Remove(surfaceAsset);
                        if (brushMeshAsset)
                            updateQueue.Add(brushMeshAsset);
                    }
                } 
            }
            surfaceBrushMeshes.Remove(surfaceAsset);
        }

        // TODO: shouldn't be public
        public static void UpdateSurfaces(CSGBrushMeshAsset brushMeshAsset)
        {
            HashSet<CSGSurfaceAsset> uniqueSurfaces;
            if (brushMeshSurfaces.TryGetValue(brushMeshAsset, out uniqueSurfaces))
            {
                // Remove previously set surfaces for this brushMesh
                foreach (var surfaceAsset in uniqueSurfaces)
                {
                    if (Equals(null, surfaceAsset))
                        continue;

                    HashSet<CSGBrushMeshAsset> brushMeshAssets;
                    if (surfaceBrushMeshes.TryGetValue(surfaceAsset, out brushMeshAssets))
                        brushMeshAssets.Remove(brushMeshAsset);
                }
                uniqueSurfaces.Clear();
            } else
                uniqueSurfaces = new HashSet<CSGSurfaceAsset>();
            
            var polygons		= brushMeshAsset.Polygons;
            if (polygons != null)
            {
                for (int i = 0; i < polygons.Length; i++)
                {
                    var surfaceAsset = polygons[i].surfaceAsset;
                    if (Equals(null, surfaceAsset))
                        continue;

                    // Add current surfaces of this brushMesh
                    if (uniqueSurfaces.Add(surfaceAsset))
                    {
                        HashSet<CSGBrushMeshAsset> brushMeshAssets;
                        if (!surfaceBrushMeshes.TryGetValue(surfaceAsset, out brushMeshAssets))
                        {
                            brushMeshAssets = new HashSet<CSGBrushMeshAsset>();
                            surfaceBrushMeshes[surfaceAsset] = brushMeshAssets;
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
            
            HashSet<CSGSurfaceAsset> uniqueSurfaces;
            if (brushMeshSurfaces.TryGetValue(brushMeshAsset, out uniqueSurfaces))
            {
                // Remove previously set surfaces for this brushMesh
                foreach (var surfaceAsset in uniqueSurfaces)
                {
                    HashSet<CSGBrushMeshAsset> brushMeshAssets;
                    if (surfaceBrushMeshes.TryGetValue(surfaceAsset, out brushMeshAssets))
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
