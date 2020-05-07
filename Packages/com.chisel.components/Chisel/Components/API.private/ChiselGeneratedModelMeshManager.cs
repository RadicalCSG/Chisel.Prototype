using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            foreach (var model in registeredModels) 
            {
                componentGenerator.RemoveAllGeneratedComponents(model);
            }

            registeredNodeLookup.Clear();
            registeredModels.Clear();
            updateList.Clear();
            
            
            if (PostReset != null) PostReset();
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

        public static void UpdateModels()
        {
            // Update the tree meshes
            Profiler.BeginSample("Flush");
            try
            {
                if (!CSGManager.Flush())
                {
                    ChiselGeneratedComponentManager.DelayedUVGeneration();
                    return; // Nothing to update ..
                }
            }
            finally
            {
                Profiler.EndSample();
            }

#if UNITY_EDITOR
            ChiselGeneratedComponentManager.OnVisibilityChanged();
#endif


            for (int m = 0; m < registeredModels.Count; m++)
            {
                var model = registeredModels[m];
                if (!model)
                    continue;

                var tree = model.Node;

                // See if the tree has been modified
                if (!tree.Dirty)
                    continue;

                Profiler.BeginSample("UpdateModelMeshDescriptions");
                UpdateModelMeshDescriptions(model);
                Profiler.EndSample();

                updateList.Add(model);
            }
            
            bool modifications = false;
            try
            {
                for (int m = 0; m < updateList.Count; m++)
                {
                    var model = updateList[m];

                    // Generate (or re-use) components and set them up properly
                    Profiler.BeginSample("componentGenerator.Rebuild");
                    componentGenerator.Rebuild(model);
                    Profiler.EndSample();
                }
            }
            finally
            {
                for (int m = 0; m < updateList.Count; m++)
                {
                    var model = updateList[m];
                    try
                    {
                        modifications = true;
                        Profiler.BeginSample("PostUpdateModel");
                        PostUpdateModel?.Invoke(model);
                        Profiler.EndSample();
                    }
                    catch (Exception ex) // if there's a bug in user-code we don't want to end up in a bad state
                    {
                        Debug.LogException(ex);
                    }
                }
                updateList.Clear();
            }

            if (modifications)
            {
                Profiler.BeginSample("PostUpdateModels");
                PostUpdateModels?.Invoke();
                Profiler.EndSample();
            }
        }

        static int MeshDescriptionSorter(GeneratedMeshDescription x, GeneratedMeshDescription y)
        {
            if (x.meshQuery.LayerParameterIndex != y.meshQuery.LayerParameterIndex) return ((int)x.meshQuery.LayerParameterIndex) - ((int)y.meshQuery.LayerParameterIndex);
            if (x.meshQuery.LayerQuery          != y.meshQuery.LayerQuery) return ((int)x.meshQuery.LayerQuery) - ((int)y.meshQuery.LayerQuery);
            if (x.surfaceParameter  != y.surfaceParameter) return ((int)x.surfaceParameter) - ((int)y.surfaceParameter);
            if (x.geometryHashValue != y.geometryHashValue) return ((int)x.geometryHashValue) - ((int)y.geometryHashValue);
            return 0;
        }

        static Comparison<GeneratedMeshDescription> kMeshDescriptionSorterDelegate = MeshDescriptionSorter;

        internal static void UpdateModelMeshDescriptions(ChiselModel model)
        {
            if (!ChiselModelGeneratedObjects.IsValid(model.generated))
            {
                if (model.generated != null)
                    model.generated.Destroy();
                model.generated = ChiselModelGeneratedObjects.Create(model);
            }

            var tree				= model.Node;
            if (!tree.Valid)
                return;
            
            var meshTypes			= ChiselMeshQueryManager.GetMeshQuery(model);
            var meshDescriptions	= tree.GetMeshDescriptions(meshTypes, model.VertexChannelMask);

            // Check if the tree creates *any* meshes
            if (meshDescriptions == null || meshDescriptions.Length == 0)
            {
                componentGenerator.RemoveAllGeneratedComponents(model);
                PostUpdateModel?.Invoke(model);
                return;
            }

            // Sort all meshDescriptions so that meshes that can be merged are next to each other
            Array.Sort(meshDescriptions, kMeshDescriptionSorterDelegate);

            model.generated.Update(model, meshDescriptions);
        }
    }
}
