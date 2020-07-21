using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Components
{
    public static class ChiselMeshQueryManager
    {
        public static MeshQuery[] GetMeshQuery(ChiselModel model)
        {
            // TODO: make this depended on the model settings / helper surface view settings
            if (model.CreateRenderComponents &&
                model.CreateColliderComponents)
                return MeshQuery.DefaultQueries;
            if (model.CreateRenderComponents)
                return MeshQuery.RenderOnly;
            else
                return MeshQuery.CollisionOnly;
        }

        public static MeshQuery[] GetVisibleQueries(MeshQuery[] queryArray)
        {
            // TODO: make this depend on debug visualization
            var visibleLayerFlags = LayerUsageFlags.Renderable;

            var queryList = queryArray.ToList();
            for (int n = queryList.Count - 1; n >= 0; n--)
            {
                if ((visibleLayerFlags & queryList[n].LayerQuery) != LayerUsageFlags.None)
                    continue;
                queryList.RemoveAt(n);
            }
            return queryList.ToArray();
        }
    }
}