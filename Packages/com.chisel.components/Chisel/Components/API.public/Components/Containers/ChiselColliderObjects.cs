using UnityEngine;
using Chisel.Core;
using System;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;

namespace Chisel.Components
{
    public struct ChiselColliderObjectUpdate
    {
        public int                  meshIndex;
        public Mesh.MeshDataArray   meshDataArray;
    }

    [Serializable]
    public class ChiselColliderObjects
    {
        public int              surfaceParameter;
        public Mesh             sharedMesh;
        public MeshCollider     meshCollider;
        public PhysicMaterial   physicsMaterial;

        public ulong            geometryHashValue;

        private ChiselColliderObjects() { }
        public static ChiselColliderObjects Create(GameObject container, int surfaceParameter)
        {
            var physicsMaterial = ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(surfaceParameter);
            var sharedMesh      = new Mesh { name = ChiselGeneratedObjects.kGeneratedMeshColliderName };
            var meshCollider    = container.AddComponent<MeshCollider>();
            var colliderObjects = new ChiselColliderObjects
            {
                surfaceParameter    = surfaceParameter,
                meshCollider        = meshCollider,
                physicsMaterial     = physicsMaterial,
                sharedMesh          = sharedMesh
            };
            colliderObjects.Initialize();
            return colliderObjects;
        }

        public void Destroy()
        {
            ChiselObjectUtility.SafeDestroy(meshCollider);
            ChiselObjectUtility.SafeDestroy(sharedMesh);
            sharedMesh = null;
            meshCollider = null;
            physicsMaterial = null;
        }

        public void DestroyWithUndo()
        {
            ChiselObjectUtility.SafeDestroyWithUndo(meshCollider);
            ChiselObjectUtility.SafeDestroyWithUndo(sharedMesh);
        }

        public void RemoveContainerFlags()
        {
            ChiselObjectUtility.RemoveContainerFlags(meshCollider);
        }

        public static bool IsValid(ChiselColliderObjects colliderObjects)
        {
            if (colliderObjects == null)
                return false;

            if (!colliderObjects.sharedMesh ||
                !colliderObjects.meshCollider)
                return false;

            return true;
        }

        void Initialize()
        {
            meshCollider.sharedMesh = sharedMesh;
            meshCollider.sharedMaterial = physicsMaterial;
        }

        public static void UpdateProperties(ChiselModel model, ChiselColliderObjects[] colliders)
        {
            if (colliders == null)
                return;
            var colliderSettings = model.ColliderSettings;
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

                var sharedMesh = colliders[i].sharedMesh;
                if (meshCollider.sharedMesh != sharedMesh)
                    meshCollider.sharedMesh = sharedMesh;

                var expectedEnabled = sharedMesh.vertexCount > 0;
                if (meshCollider.enabled != expectedEnabled)
                    meshCollider.enabled = expectedEnabled;

                if (meshCollider.cookingOptions != colliderSettings.cookingOptions)
                    meshCollider.cookingOptions	=  colliderSettings.cookingOptions;
                if (meshCollider.convex         != colliderSettings.convex)
                    meshCollider.convex			=  colliderSettings.convex;
                if (meshCollider.isTrigger      != colliderSettings.isTrigger)
                    meshCollider.isTrigger		=  colliderSettings.isTrigger;
            }
        }
    }
}