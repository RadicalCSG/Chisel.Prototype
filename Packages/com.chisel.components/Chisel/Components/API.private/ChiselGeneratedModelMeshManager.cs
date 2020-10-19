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

        static void BeginMeshEvent()
        {
            s_UpdatedModels.Clear();
            ChiselGeneratedObjects.BeginMeshEvent();
        }

        static readonly HashSet<ChiselModel> s_UpdatedModels = new HashSet<ChiselModel>();

        static bool PreUpdateMeshEvent(CSGTree tree, int index, ref VertexBufferContents vertexBufferContents)
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
                return false;

            if (!ChiselGeneratedObjects.IsValid(model.generated))
            {
                if (model.generated != null)
                    model.generated.Destroy();
                model.generated = ChiselGeneratedObjects.Create(model.gameObject);
            }

            s_UpdatedModels.Add(model);
            model.generated.PreUpdate(model, model.gameObject, ref vertexBufferContents);
            return true;
        }

        static JobHandle UpdateMeshEvent(JobHandle dependencies)
        {
            return ChiselGeneratedObjects.UpdateMeshes(dependencies);
        }

        static void PostUpdateMeshEvent()
        {
            ChiselGeneratedObjects.PostUpdate();
            foreach (var model in s_UpdatedModels)
            {
                componentGenerator.Rebuild(model);
                PostUpdateModel?.Invoke(model);
            }
        }
        
        static Action s_BeginMeshEvent = (Action)BeginMeshEvent;
        static PreUpdateMeshEvent s_PreUpdateMeshEvent = (PreUpdateMeshEvent)PreUpdateMeshEvent;
        static ScheduleMeshUploads s_UpdateMeshEvent = (ScheduleMeshUploads)UpdateMeshEvent;
        static Action s_PostUpdateMeshEvent = (Action)PostUpdateMeshEvent;

        public static void UpdateModels()
        {

            // Update the tree meshes
            Profiler.BeginSample("Flush");
            try
            {
                if (!CSGManager.Flush(s_BeginMeshEvent, s_PreUpdateMeshEvent, s_UpdateMeshEvent, s_PostUpdateMeshEvent))
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
