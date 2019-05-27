using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Assets
{
    // TODO: create real Asset that contains this so we can drag & drop a complete "surface" onto a brush
    [Serializable]
    public sealed class CSGSurfaceAsset : IDisposable
    {
        public CSGSurfaceAsset()    { CSGSurfaceAssetManager.Register(this); }
        public void Dispose()       { CSGSurfaceAssetManager.Unregister(this); }
        ~CSGSurfaceAsset()          { Dispose(); }
        

        [SerializeField] private LayerUsageFlags  layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable;
        [SerializeField] private Material         renderMaterial;
        [SerializeField] private PhysicMaterial   physicsMaterial;

        public LayerUsageFlags	LayerUsage		            { get { return layerUsage;      } set { if (layerUsage      == value) return; var prevValue = layerUsage;      layerUsage      = value; CSGSurfaceAssetManager.OnLayerUsageFlagsChanged(this, prevValue, value); } }
        public Material			RenderMaterial	            { get { return renderMaterial;  } set { if (renderMaterial  == value) return; var prevValue = renderMaterial;  renderMaterial  = value; CSGSurfaceAssetManager.OnRenderMaterialChanged(this, prevValue, value); } }
        public PhysicMaterial	PhysicsMaterial             { get { return physicsMaterial; } set { if (physicsMaterial == value) return; var prevValue = physicsMaterial; physicsMaterial = value; CSGSurfaceAssetManager.OnPhysicsMaterialChanged(this, prevValue, value); } }
        public int				RenderMaterialInstanceID	{ get { return renderMaterial  ? renderMaterial .GetInstanceID() : 0; } }
        public int				PhysicsMaterialInstanceID	{ get { return physicsMaterial ? physicsMaterial.GetInstanceID() : 0; } }
        

        public static CSGSurfaceAsset CreateInstance(CSGSurfaceAsset other)
        {
            return new CSGSurfaceAsset
            {
                LayerUsage      = other.layerUsage,
                RenderMaterial  = other.renderMaterial,
                PhysicsMaterial = other.physicsMaterial
            };
        }

        public static CSGSurfaceAsset CreateInstance(LayerUsageFlags layerUsage = LayerUsageFlags.None)
        {
            return new CSGSurfaceAsset { LayerUsage = layerUsage };
        }

        public static CSGSurfaceAsset CreateInstance(Material renderMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows)
        {
            return new CSGSurfaceAsset
            {
                LayerUsage      = layerUsage,
                RenderMaterial  = renderMaterial
            };
        }

        public static CSGSurfaceAsset CreateInstance(PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.Collidable)
        {
            return new CSGSurfaceAsset
            {
                LayerUsage      = layerUsage,
                PhysicsMaterial = physicsMaterial
            };
        }

        public static CSGSurfaceAsset CreateInstance(Material renderMaterial, PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable)
        {
            return new CSGSurfaceAsset
            {
                LayerUsage      = layerUsage,
                RenderMaterial  = renderMaterial,
                PhysicsMaterial = physicsMaterial
            };
        } 
    }
}
