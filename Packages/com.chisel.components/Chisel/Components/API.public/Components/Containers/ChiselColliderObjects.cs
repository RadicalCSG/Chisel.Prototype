using UnityEngine;
using Chisel.Core;
using System;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Burst;

namespace Chisel.Components
{
    public struct ChiselColliderObjectUpdate
    {
        public int              meshIndex;
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

        [BurstCompile(CompileSynchronously = true)]
        struct BakeColliderJob : IJobParallelFor
        {
            [NoAlias, ReadOnly] public NativeArray<BakeData>.ReadOnly bakingSettings;
            public void Execute(int index)
            {
                if (bakingSettings[index].instanceID != 0)
                    Physics.BakeMesh(bakingSettings[index].instanceID, bakingSettings[index].convex);
            }
        }

        struct BakeData
        {
            public bool convex;
            public int  instanceID;
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

                // Requires us to set the sharedMesh again, which would force a full slow rebake, 
                // even if we already did a Bake in a job
                //if (meshCollider.cookingOptions != colliderSettings.cookingOptions)
                //    meshCollider.cookingOptions = colliderSettings.cookingOptions;

                if (meshCollider.convex != colliderSettings.convex)
                    meshCollider.convex = colliderSettings.convex;
                if (meshCollider.isTrigger != colliderSettings.isTrigger)
                    meshCollider.isTrigger = colliderSettings.isTrigger;

                var sharedMesh = colliders[i].sharedMesh;
                var expectedEnabled = sharedMesh.vertexCount > 0;
                if (meshCollider.enabled != expectedEnabled)
                    meshCollider.enabled = expectedEnabled;
            }

            // TODO: find all the instanceIDs before we start doing CSG, then we can do the Bake's in the same job that sets the meshes
            //          hopefully that will make it easier for Unity to not fuck up the scheduling
            var bakingSettings = new NativeArray<BakeData>(colliders.Length,Allocator.TempJob);
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                {
                    bakingSettings[i] = new BakeData
                    {
                        instanceID = 0
                    };
                    continue;
                }

                var sharedMesh = colliders[i].sharedMesh;
                bakingSettings[i] = new BakeData
                {
                    convex      = colliderSettings.convex,
                    instanceID  = sharedMesh.GetInstanceID()
                };
            }
            var bakeColliderJob = new BakeColliderJob
            {
                bakingSettings = bakingSettings.AsReadOnly()
            };
            // WHY ARE THEY RUN SEQUENTIALLY ON THE SAME WORKER THREAD?
            var jobHandle = bakeColliderJob.Schedule(colliders.Length, 1);

            jobHandle.Complete();
            bakingSettings.Dispose(jobHandle);
            // TODO: is there a way to defer forcing the collider to update?
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

                meshCollider.sharedMesh = colliders[i].sharedMesh;
            }
        }
    }
}