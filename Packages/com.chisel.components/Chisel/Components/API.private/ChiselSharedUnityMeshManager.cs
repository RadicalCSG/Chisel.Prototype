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
        Dictionary<int, ChiselGeneratedRefCountedMesh> refCountedMeshes = new Dictionary<int, ChiselGeneratedRefCountedMesh>();

        public void Clear() { refCountedMeshes.Clear(); }
        public void Register(ChiselModel model) { AddUnityMeshes(model); }
        public void Unregister(ChiselModel model) { RemoveUnityMeshes(model); }


        public void DecreaseMeshRefCount(ChiselModel model)
        {
            // First decrease the ref count on all meshes
            RemoveUnityMeshes(model);
        }


        public void UpdatePartialVisibilityMeshes(ChiselModel model)
        {
            model.generated.needVisibilityMeshUpdate = false;
        }

        public void AddUnityMeshes(ChiselModel model)
        {
            for (int i = 0; i < model.generatedRenderMeshes.Length; i++)
            {
                var generatedMesh = model.generatedRenderMeshes[i];
                generatedMesh.sharedMesh = ReturnOrRegisterUnityMeshAndIncreaseRefCount(generatedMesh.meshKey, generatedMesh.sharedMesh);
            }
            for (int i = 0; i < model.generatedColliderMeshes.Length; i++)
            {
                var generatedMesh = model.generatedColliderMeshes[i];
                generatedMesh.sharedMesh = ReturnOrRegisterUnityMeshAndIncreaseRefCount(generatedMesh.meshKey, generatedMesh.sharedMesh);
            }
        }

        public void RemoveUnityMeshes(ChiselModel model)
        {
            for (int i = 0; i < model.generatedRenderMeshes.Length; i++)
            {
                var generatedMesh = model.generatedRenderMeshes[i];
                DecreaseRefCount(generatedMesh.meshKey);
                generatedMesh.sharedMesh = null;
            }
            for (int i = 0; i < model.generatedColliderMeshes.Length; i++)
            {
                var generatedMesh = model.generatedColliderMeshes[i];
                DecreaseRefCount(generatedMesh.meshKey);
                generatedMesh.sharedMesh = null;
            }
        }

        public UnityEngine.Mesh ReturnOrRegisterUnityMeshAndIncreaseRefCount(int meshKey, UnityEngine.Mesh generatedMesh)
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

        public UnityEngine.Mesh ReturnUnityMeshAndIncreaseRefCountIfExists(int meshKey)
        {
            ChiselGeneratedRefCountedMesh refCountedMesh;
            if (!refCountedMeshes.TryGetValue(meshKey, out refCountedMesh))
                return null;
            refCountedMesh.refCount++;
            return refCountedMesh.sharedMesh;
        }

        public void DecreaseRefCount(int meshKey)
        {
            ChiselGeneratedRefCountedMesh refCountedMesh;
            if (!refCountedMeshes.TryGetValue(meshKey, out refCountedMesh))
                return;
            refCountedMesh.refCount--;
        }
        
        static List<int> garbage		= new List<int>();
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
                ChiselObjectUtility.SafeDestroy(garbageMeshes[i]);
            garbageMeshes.Clear();
        }

        public UnityEngine.Mesh CreateNewUnityMesh(int meshKey)
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

        public bool RetrieveUnityMeshPositionOnly(ChiselModel model, GeneratedMeshDescription meshDescription, UnityEngine.Mesh sharedMesh)
        {
            // Retrieve the generatedMesh, and store it in the Unity Mesh
            var generatedMeshContents = model.Node.GetGeneratedMesh(meshDescription);
            if (generatedMeshContents == null)
                return false;

            sharedMesh.CopyFromPositionOnly(generatedMeshContents);
            generatedMeshContents.Dispose();
            SetHasLightmapUVs(sharedMesh, false);
            return true;
        }

        static readonly List<GeneratedMeshContents> sGeneratedContents = new List<GeneratedMeshContents>();
        public bool RetrieveUnityMesh(ChiselModel model, GeneratedMeshDescription[] meshDescriptions, int startIndex, int endIndex, UnityEngine.Mesh sharedMesh)
        {
            // Retrieve the generatedMesh, and store it in the Unity Mesh
            for (int i = startIndex; i < endIndex; i++)
            {
                var generatedMeshContents = model.Node.GetGeneratedMesh(meshDescriptions[i]);
                if (generatedMeshContents == null)
                    continue;
                sGeneratedContents.Add(generatedMeshContents);
            }
            if (sGeneratedContents.Count == 0)
                return false;

            sharedMesh.CopyFrom(sGeneratedContents);

            for (int i = 0; i < sGeneratedContents.Count; i++)
            {
                sGeneratedContents[i].Dispose();
            }
            sGeneratedContents.Clear();

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
