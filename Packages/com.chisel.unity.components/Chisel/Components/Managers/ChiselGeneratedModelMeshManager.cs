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
        public static event Action<ChiselModelComponent> PostUpdateModel;
        public static event Action              PostUpdateModels;
        
        internal static HashSet<ChiselNode>     s_RegisteredNodeLookup = new();
        internal static List<ChiselModelComponent>       s_RegisteredModels = new();

        static readonly ChiselGeneratedComponentManager componentGenerator = new();
        
        internal static void Reset()
        {
            PreReset?.Invoke();

            s_RegisteredNodeLookup.Clear();
            s_RegisteredModels.Clear();


            PostReset?.Invoke();
        }

        internal static void Unregister(ChiselNode node)
        {
            if (!s_RegisteredNodeLookup.Remove(node))
                return;

            var model = node as ChiselModelComponent;
            if (!ReferenceEquals(model, null))
            {
                componentGenerator.Unregister(model);
                s_RegisteredModels.Remove(model);
            }
        }

        internal static void Register(ChiselNode node)
        {
            if (!s_RegisteredNodeLookup.Add(node))
                return;

            var model = node as ChiselModelComponent;
            if (ReferenceEquals(model, null))
                return;
            
            s_RegisteredModels.Add(model);
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
            ChiselModelComponent model = null;
            for (int m = 0; m < s_RegisteredModels.Count; m++)
            {
                if (!s_RegisteredModels[m])
                    continue;

                if (s_RegisteredModels[m].Node == tree)
                    model = s_RegisteredModels[m];
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
