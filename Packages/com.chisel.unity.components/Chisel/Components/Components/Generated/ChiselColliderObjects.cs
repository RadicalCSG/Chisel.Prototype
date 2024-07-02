using System;
using Chisel.Core;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Components
{
    public struct ChiselColliderObjectUpdate
    {
        public int              meshIndex;
    }

    [Serializable, BurstCompile(CompileSynchronously = true)]
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
            var physicsMaterial = ChiselMaterialManager.Instance.GetPhysicMaterial(surfaceParameter);
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

        //*
        [BurstCompile(CompileSynchronously = true)]
        struct BakeColliderJobParallel : IJobParallelFor
        {
            [NoAlias, ReadOnly] public NativeArray<BakeData> bakingSettings;
            public void Execute(int index)
            {
                if (bakingSettings[index].instanceID != 0)
                    Physics.BakeMesh(bakingSettings[index].instanceID, bakingSettings[index].convex, bakingSettings[index].cookingOptions);
            }
        }
        /*/
        [BurstCompile(CompileSynchronously = true)]
        struct BakeColliderJob : IJob
        {
            [NoAlias, ReadOnly] public BakeData bakingSettings;
            public void Execute()
            {
                if (bakingSettings.instanceID != 0)
                    Physics.BakeMesh(bakingSettings.instanceID, bakingSettings.convex, bakingSettings.cookingOptions);
            }
        }
        //*/
        struct BakeData
        {
            public bool                         convex;
            public MeshColliderCookingOptions   cookingOptions;
            public int                          instanceID;
        }

        //*/
        public static void UpdateProperties(ChiselModelComponent model, ChiselColliderObjects[] colliders)
        {
            var colliderSettings = model.ColliderSettings;
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

                // If the cookingOptions are not the default values it would force a full slow rebake later, 
                // even if we already did a Bake in a job
                if (meshCollider.cookingOptions != colliderSettings.cookingOptions)
                    meshCollider.cookingOptions = colliderSettings.cookingOptions;

                if (meshCollider.convex != colliderSettings.convex)
                    meshCollider.convex = colliderSettings.convex;
                if (meshCollider.isTrigger != colliderSettings.isTrigger)
                    meshCollider.isTrigger = colliderSettings.isTrigger;

                var sharedMesh = colliders[i].sharedMesh;
                var expectedEnabled = sharedMesh.vertexCount > 0;
                if (meshCollider.enabled != expectedEnabled)
                    meshCollider.enabled = expectedEnabled;
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;

        public static void ScheduleColliderBake(ChiselModelComponent model, ChiselColliderObjects[] colliders)
        {
            var colliderSettings = model.ColliderSettings;
            //*
            // TODO: find all the instanceIDs before we start doing CSG, then we can do the Bake's in the same job that sets the meshes
            //          hopefully that will make it easier for Unity to not screw up the scheduling
            var bakingSettings = new NativeArray<BakeData>(colliders.Length, defaultAllocator);
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
                    convex          = colliderSettings.convex,
                    cookingOptions  = colliderSettings.cookingOptions,
                    instanceID      = sharedMesh.GetInstanceID()
                };
            }
            //*
            var bakeColliderJob = new BakeColliderJobParallel
            {
                bakingSettings = bakingSettings
            };
            // WHY ARE ALL OF THESE JOBS SEQUENTIAL ON A SINGLE WORKER THREAD?
            var allJobHandles = bakeColliderJob.Schedule(colliders.Length, 1);
            /*/
            var allJobHandles = default(JobHandle);
            for (int i = 0; i < bakingSettings.Length; i++)
            {
                var bakeColliderJob = new BakeColliderJob
                {
                    bakingSettings = bakingSettings[i]
                };
                var jobHandle = bakeColliderJob.Schedule();
                allJobHandles = JobHandle.CombineDependencies(allJobHandles, jobHandle);
            }
            //*/

            bakingSettings.Dispose(allJobHandles);
            bakingSettings = default;

            allJobHandles.Complete();

            //*/
            //*
            // TODO: is there a way to defer forcing the collider to update?
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

                meshCollider.sharedMesh = colliders[i].sharedMesh;
            }//*/
        }
    }
}