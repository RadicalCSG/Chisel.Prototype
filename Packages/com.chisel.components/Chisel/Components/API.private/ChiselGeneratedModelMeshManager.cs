using System;
using System.Collections.Generic;
using Chisel.Core;
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
            if (ReferenceEquals(model, null))
                return;
            
            registeredModels.Add(model);
            componentGenerator.Register(model);
        }

        static int FinishMeshUpdates(CSGTree                        tree, 
                                     ref VertexBufferContents       vertexBufferContents, 
                                     ref Mesh.MeshDataArray         meshDataArray,
                                     NativeList<ChiselMeshUpdate>   colliderMeshUpdates,
                                     NativeList<ChiselMeshUpdate>   debugHelperMeshes,
                                     NativeList<ChiselMeshUpdate>   renderMeshes,
                                     JobHandle                      dependencies)
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
            {
                if (meshDataArray.Length > 0) meshDataArray.Dispose();
                meshDataArray = default;
                return 0;
            }

            if (!ChiselGeneratedObjects.IsValid(model.generated))
            {
                if (model.generated != null)
                    model.generated.Destroy();
                model.generated = ChiselGeneratedObjects.Create(model.gameObject);
            }

            var count = model.generated.FinishMeshUpdates(model, model.gameObject, ref meshDataArray, ref vertexBufferContents,
                                                          colliderMeshUpdates, debugHelperMeshes, renderMeshes,
                                                          dependencies);
            componentGenerator.Rebuild(model);
            PostUpdateModel?.Invoke(model);
            return count;
        }
        
        static readonly FinishMeshUpdate    s_FinishMeshUpdates     = (FinishMeshUpdate)FinishMeshUpdates;

        public static void UpdateModels()
        {

            // Update the tree meshes
            Profiler.BeginSample("Flush");
            try
            {
                if (!CompactHierarchyManager.Flush(s_FinishMeshUpdates))
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
