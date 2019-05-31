using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Components
{
    // Responsible for retrieving meshes for models, using MeshDescriptions stored in model
    //	- reuse UnityEngine.Meshes on multiple models when they're identical
    //		- MAKE SURE TO KEEP LIGHTMAP INFORMATION WHEN THERE'S NO CHANGE!
    //	- create meshes when it is unique
    //	- destroy meshes when no model is using them

    // Re-use UnityEngine.Meshes when modified (overwrite)
    //			but not when shared among multiple models
    // Re-use UnityEngine.Meshes when changed
    // Re-use UnityEngine.Meshes between models
    // Only generate meshes when we need them (helper meshes)

    internal sealed class ChiselGeneratedRefCountedMesh
    {
        public UnityEngine.Mesh	sharedMesh;
        public int				refCount;
    }
    
    internal sealed class ChiselSharedUnityMeshManager
    {
        Dictionary<GeneratedMeshKey, ChiselGeneratedRefCountedMesh> refCountedMeshes = new Dictionary<GeneratedMeshKey, ChiselGeneratedRefCountedMesh>();

        public void Clear() { refCountedMeshes.Clear(); }
        public void Register(ChiselModel model) { AddUnityMeshes(model); }
        public void Unregister(ChiselModel model) { RemoveUnityMeshes(model); }


        public void DecreaseMeshRefCount(ChiselModel model)
        {
            // First decrease the ref count on all meshes
            RemoveUnityMeshes(model);
        }

        public void ReuseExistingMeshes(ChiselModel model)
        {
            for (int i = 0; i < model.generatedMeshes.Length; i++)
            {
                var generatedMesh = model.generatedMeshes[i];
                // See if we already know a mesh that has the same description
                generatedMesh.sharedMesh = ReturnUnityMeshAndIncreaseRefCountIfExists(generatedMesh.meshKey);
                generatedMesh.needsUpdate = !generatedMesh.sharedMesh;
            }
        }

        public void CreateNewMeshes(ChiselModel model)
        {
            // Separate loop so we can re-use meshes when creating new meshes

            for (int i = 0; i < model.generatedMeshes.Length; i++)
            {
                var generatedMesh = model.generatedMeshes[i];
                if (generatedMesh.sharedMesh != null)
                    continue;

                // If not, create a new mesh ...
                generatedMesh.sharedMesh = CreateNewUnityMesh(generatedMesh.meshKey);

                RetrieveUnityMesh(model, generatedMesh.meshDescription, generatedMesh.sharedMesh);
            }
        }

        public void AddUnityMeshes(ChiselModel model)
        {
            for (int i = 0; i < model.generatedMeshes.Length; i++)
            {
                var generatedMesh = model.generatedMeshes[i];
                generatedMesh.sharedMesh = ReturnOrRegisterUnityMeshAndIncreaseRefCount(generatedMesh.meshKey, generatedMesh.sharedMesh);
            }
        }

        public void RemoveUnityMeshes(ChiselModel model)
        {
            for (int i = 0; i < model.generatedMeshes.Length; i++)
            {
                var generatedMesh	= model.generatedMeshes[i];
                DecreaseRefCount(generatedMesh.meshKey);
                generatedMesh.sharedMesh = null;
            }
        }

        public UnityEngine.Mesh ReturnOrRegisterUnityMeshAndIncreaseRefCount(GeneratedMeshKey meshKey, UnityEngine.Mesh generatedMesh)
        {
            ChiselGeneratedRefCountedMesh refCountedMesh;
            refCountedMeshes.TryGetValue(meshKey, out refCountedMesh);
            if (refCountedMesh == null)
            {
                refCountedMesh = new ChiselGeneratedRefCountedMesh();
                refCountedMeshes[meshKey] = refCountedMesh;
            }

            if (refCountedMesh.sharedMesh != null &&
                refCountedMesh.sharedMesh != generatedMesh)
            {
                garbageMeshes.Add(refCountedMesh.sharedMesh);
                refCountedMesh.refCount = 0;
            }

            refCountedMesh.sharedMesh = generatedMesh;
            refCountedMesh.refCount++;
            return refCountedMesh.sharedMesh;
        }

        public UnityEngine.Mesh ReturnUnityMeshAndIncreaseRefCountIfExists(GeneratedMeshKey meshKey)
        {
            ChiselGeneratedRefCountedMesh refCountedMesh;
            if (!refCountedMeshes.TryGetValue(meshKey, out refCountedMesh))
                return null;
            refCountedMesh.refCount++;
            return refCountedMesh.sharedMesh;
        }

        public void DecreaseRefCount(GeneratedMeshKey meshKey)
        {
            ChiselGeneratedRefCountedMesh refCountedMesh;
            if (!refCountedMeshes.TryGetValue(meshKey, out refCountedMesh))
                return;
            refCountedMesh.refCount--;
        }
        
        static List<GeneratedMeshKey> garbage		= new List<GeneratedMeshKey>();
        static List<UnityEngine.Mesh> garbageMeshes	= new List<UnityEngine.Mesh>();

        // Find all Unity Meshes that are no longer used
        public bool FindAllUnusedUnityMeshes()
        {
            garbageMeshes.Clear();
            foreach (var pair in refCountedMeshes)
            {
                if (pair.Value.refCount <= 0)
                {
                    garbage.Add(pair.Key);
                    if (pair.Value.sharedMesh)
                        garbageMeshes.Add(pair.Value.sharedMesh);
                }
            }
            return garbageMeshes.Count > 0;
        }

        // Destroy all Unity Meshes that aren't used and haven't been recycled
        public void DestroyNonRecycledUnusedUnityMeshes()
        {
            // Remove our garbage
            for (int i = 0; i < garbage.Count; i++)
                refCountedMeshes.Remove(garbage[i]);
            garbage.Clear();

            // Make sure we destroy the leftover meshes
            for (int i = 0; i < garbageMeshes.Count; i++)
                CSGObjectUtility.SafeDestroy(garbageMeshes[i]);
            garbageMeshes.Clear();
        }

        public UnityEngine.Mesh CreateNewUnityMesh(GeneratedMeshKey meshKey)
        {
            UnityEngine.Mesh sharedMesh = null;
            if (garbageMeshes.Count > 0)
            {
                var lastIndex = garbageMeshes.Count - 1;
                sharedMesh = garbageMeshes[lastIndex];
                garbageMeshes.RemoveAt(lastIndex);
                if (sharedMesh) sharedMesh.Clear();
            }
            if (!sharedMesh)
                sharedMesh = new UnityEngine.Mesh() { name = meshKey.GetHashCode().ToString() };
            return ReturnOrRegisterUnityMeshAndIncreaseRefCount(meshKey, sharedMesh);
        }

        public bool RetrieveUnityMesh(ChiselModel model, GeneratedMeshDescription meshDescription, UnityEngine.Mesh sharedMesh)
        {
            // Retrieve the generatedMesh, and store it in the Unity Mesh
            model.generatedMeshContents = model.Node.GetGeneratedMesh(meshDescription, model.generatedMeshContents);
            if (model.generatedMeshContents == null)
                return false;

            model.generatedMeshContents.CopyTo(sharedMesh);
            SetHasLightmapUVs(sharedMesh, false);
            return true;
        }

        // Hacky way to store that a mesh has lightmap UV created
        public static bool HasLightmapUVs(UnityEngine.Mesh sharedMesh)
        {
            var name = sharedMesh.name;
            if (!string.IsNullOrEmpty(name) && 
                name[name.Length - 1] == '*')
                return true;
            return false;
        }
        
        public static void SetHasLightmapUVs(UnityEngine.Mesh sharedMesh, bool haveLightmapUVs)
        {
            var name = sharedMesh.name;
            if (haveLightmapUVs)
            {
                if (!string.IsNullOrEmpty(name) &&
                    name[name.Length - 1] == '*')
                    return;
                sharedMesh.name = name + "*";
            } else
            {
                if (string.IsNullOrEmpty(name))
                    return;
                if (name[name.Length - 1] != '*')
                    return;
                int index = name.IndexOf('*');
                name = name.Remove(index);
                sharedMesh.name = name;
            }
        }
    }
}
