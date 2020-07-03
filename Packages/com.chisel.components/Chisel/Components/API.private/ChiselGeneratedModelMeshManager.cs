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
            Profiler.BeginSample("OnVisibilityChanged");
            ChiselGeneratedComponentManager.OnVisibilityChanged();
            Profiler.EndSample();
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

            Profiler.BeginSample("GetMeshDescriptions");
            var meshTypes			= ChiselMeshQueryManager.GetMeshQuery(model);
            CSGManager.GetMeshDescriptions(tree.NodeID, meshTypes, model.VertexChannelMask, out var meshDescriptions, out var meshContents);
            Profiler.EndSample();

            Profiler.BeginSample("Update");
            model.generated.Update(model, meshDescriptions, meshContents);
            Profiler.EndSample();
        }
    }
}
