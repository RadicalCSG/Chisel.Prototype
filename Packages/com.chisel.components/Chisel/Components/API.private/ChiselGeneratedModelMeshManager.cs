using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    public static class ChiselGeneratedModelMeshManager
    {
        public static event Action              PreReset;
        public static event Action              PostReset;
        public static event Action<ChiselModel> PostUpdateModel;
        public static event Action              PostUpdateModels;
        
        internal static HashSet<ChiselNode>     registeredNodeLookup    = new HashSet<ChiselNode>();
        internal static List<ChiselModel>       registeredModels        = new List<ChiselModel>();

        static ChiselGeneratedComponentManager  componentGenerator      = new ChiselGeneratedComponentManager();
        
        static List<ChiselModel> updateList = new List<ChiselModel>();

        internal static void Reset()
        {
            PreReset?.Invoke();

            registeredNodeLookup.Clear();
            registeredModels.Clear();
            updateList.Clear();


            PostReset?.Invoke();
        }

        internal static void Unregister(ChiselNode node)
        {
            if (!registeredNodeLookup.Remove(node))
                return;

            var model = node as ChiselModel;
            if (!ReferenceEquals(model, null))
            {
                componentGenerator.Unregister(model);
                registeredModels.Remove(model);
            }
        }

        internal static void Register(ChiselNode node)
        {
            if (!registeredNodeLookup.Add(node))
                return;

            var model = node as ChiselModel;
            if (!ReferenceEquals(model, null))
            {
                registeredModels.Add(model);
                componentGenerator.Register(model);
            }
        }

        static void BeginMeshUpdates()
        {
            s_UpdatedModels.Clear();
            ChiselGeneratedObjects.BeginMeshUpdates();
        }

        static readonly HashSet<ChiselModel> s_UpdatedModels = new HashSet<ChiselModel>();

        static JobHandle PerformMeshUpdate(CSGTree tree, List<Mesh.MeshDataArray> meshDataArrays, ref VertexBufferContents vertexBufferContents, JobHandle dependencies)
        {
            ChiselModel model = null;
            for (int m = 0; m < registeredModels.Count; m++)
            {
                if (!registeredModels[m])
                    continue;

                if (registeredModels[m].Node == tree)
                    model = registeredModels[m];
            }
            if (model == null)
                return (JobHandle)default;

            if (!ChiselGeneratedObjects.IsValid(model.generated))
            {
                if (model.generated != null)
                    model.generated.Destroy();
                model.generated = ChiselGeneratedObjects.Create(model.gameObject);
            }

            s_UpdatedModels.Add(model);
            return model.generated.UpdateMeshes(model, model.gameObject, meshDataArrays, ref vertexBufferContents, dependencies);
        }

        static int FinishMeshUpdates(CSGTree tree)
        {
            ChiselModel model = null;
            for (int m = 0; m < registeredModels.Count; m++)
            {
                if (!registeredModels[m])
                    continue;

                if (registeredModels[m].Node == tree)
                    model = registeredModels[m];
            }
            if (model == null)
                return 0;

            var count = ChiselGeneratedObjects.FinishMeshUpdates(model);
            componentGenerator.Rebuild(model);
            PostUpdateModel?.Invoke(model);
            return count;
        }
        
        static readonly Action              s_BeginMeshUpdates      = (Action)BeginMeshUpdates;
        static readonly PerformMeshUpdate   s_PerformMeshUpdate     = (PerformMeshUpdate)PerformMeshUpdate;
        static readonly FinishMeshUpdate    s_FinishMeshUpdates     = (FinishMeshUpdate)FinishMeshUpdates;

        public static void UpdateModels()
        {

            // Update the tree meshes
            Profiler.BeginSample("Flush");
            try
            {
                if (!CSGManager.Flush(s_BeginMeshUpdates, s_PerformMeshUpdate, s_FinishMeshUpdates))
                {
                    ChiselGeneratedComponentManager.DelayedUVGeneration();
                    return; // Nothing to update ..
                }
            }
            finally
            {
                Profiler.EndSample();
            }

            {
                Profiler.BeginSample("PostUpdateModels");
                PostUpdateModels?.Invoke();
                Profiler.EndSample();
            }
        }
    }
}
