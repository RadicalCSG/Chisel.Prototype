using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using LightProbeUsage = UnityEngine.Rendering.LightProbeUsage;
using ReflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage;
using UnityEngine.Rendering;

namespace Chisel.Components
{
    [Serializable]
    public class ChiselColliderObjects
    {
        public int              surfaceParameter;
        public Mesh             sharedMesh;
        public MeshCollider     meshCollider;
        public PhysicMaterial   physicsMaterial;

        private ChiselColliderObjects() { }
        public static ChiselColliderObjects Create(GameObject container, int surfaceParameter)
        {
            var physicsMaterial = ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(surfaceParameter);
            var sharedMesh      = new Mesh { name = ChiselModelGeneratedObjects.kGeneratedMeshColliderName };
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

        public void Update(ChiselModel model, GeneratedMeshDescription meshDescription)
        {
            // Retrieve the generatedMesh, and store it in the Unity Mesh
            var generatedMeshContents = model.Node.GetGeneratedMesh(meshDescription);
            if (generatedMeshContents == null || generatedMeshContents.indices.Length == 0)
            {
                if (sharedMesh.vertexCount > 0) sharedMesh.Clear(keepVertexLayout: true);
            } else
                sharedMesh.CopyFromPositionOnly(generatedMeshContents);
            if (meshCollider.sharedMesh != sharedMesh)
                meshCollider.sharedMesh = sharedMesh;
            meshCollider.enabled = sharedMesh.vertexCount > 0;
            generatedMeshContents?.Dispose();
        }

        public static void UpdateColliders(ChiselModel model, ChiselColliderObjects[] colliders)
        {
            if (colliders == null)
                return;
            var colliderSettings = model.ColliderSettings;
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;

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