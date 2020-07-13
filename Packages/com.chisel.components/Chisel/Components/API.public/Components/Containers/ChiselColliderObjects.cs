using UnityEngine;
using Chisel.Core;
using System;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;

namespace Chisel.Components
{
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

        public void Update(ChiselModel model, ref VertexBufferContents contents, int contentsIndex)
        {
            var meshIsModified = false;

            // Retrieve the generatedMesh, and store it in the Unity Mesh
            var modelTree = model.Node;
            Profiler.BeginSample("CopyFromPositionOnly");
            meshIsModified = contents.CopyPositionOnlyToMesh(contentsIndex, sharedMesh);
            Profiler.EndSample();

            if (meshCollider.sharedMesh != sharedMesh)
                meshIsModified = true;

            var expectedEnabled = sharedMesh.vertexCount > 0;
            if (meshCollider.enabled != expectedEnabled)
                meshCollider.enabled = expectedEnabled;

#if UNITY_EDITOR
            if (meshIsModified)
            {
                // MeshCollider doesn't rebuild it's internal collider mesh unless you change it's mesh
                meshCollider.sharedMesh = sharedMesh;
                UnityEditor.EditorUtility.SetDirty(meshCollider);
                UnityEditor.EditorUtility.SetDirty(model);
            }
#endif
        }

        public static void UpdateColliders(ChiselModel model, ChiselColliderObjects[] colliders)
        {
            if (colliders == null)
                return;
            var colliderSettings = model.ColliderSettings;
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

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