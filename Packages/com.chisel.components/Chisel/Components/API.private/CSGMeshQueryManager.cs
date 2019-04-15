using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Components
{
    public static class CSGMeshQueryManager
    {
        static MeshQuery[] renderOnly =
        {
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.RenderReceiveCastShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.RenderCastShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.RenderReceiveShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.Renderable,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.CastShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            )
        };

        static MeshQuery[] collisionOnly =
        {
            new MeshQuery(
                parameterIndex: LayerParameterIndex.PhysicsMaterial,
                query:          LayerUsageFlags.Collidable,
                mask:           LayerUsageFlags.Collidable,
                vertexChannels: VertexChannelFlags.Position
            )
        };

        static MeshQuery[] defaultModelSettings =
        {
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.RenderReceiveCastShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.RenderCastShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.RenderReceiveShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.Renderable,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.RenderMaterial,
                query:          LayerUsageFlags.CastShadows,
                mask:           LayerUsageFlags.RenderReceiveCastShadows,
                vertexChannels: VertexChannelFlags.All
            ),
            new MeshQuery(
                parameterIndex: LayerParameterIndex.PhysicsMaterial,
                query:          LayerUsageFlags.Collidable,
                mask:           LayerUsageFlags.Collidable,
                vertexChannels: VertexChannelFlags.Position
            )
        };

        public static MeshQuery[] GetMeshQuery(CSGModel model)
        {
            // TODO: make this depended on the model settings / helper surface view settings
            if (model.CreateRenderComponents &&
                model.CreateColliderComponents)
                return defaultModelSettings;

            if (model.CreateRenderComponents)
                return renderOnly;
            else
                return collisionOnly;
        }

        public static bool IsVisible(MeshQuery[] query)
        {
            // TODO: implement this
            return true;
        }
    }
}