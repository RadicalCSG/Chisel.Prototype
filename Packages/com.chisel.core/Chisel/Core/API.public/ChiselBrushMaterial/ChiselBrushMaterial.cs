using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Chisel.Core
{
    // TODO: create real Asset that contains this so we can drag & drop a complete "material" onto a brush
    [Serializable]
    public sealed class ChiselBrushMaterial : IDisposable
    {
        public ChiselBrushMaterial()    { ChiselBrushMaterialManager.Register(this); }
        public void Dispose()           { ChiselBrushMaterialManager.Unregister(this); }
        ~ChiselBrushMaterial()          { Dispose(); }
        

        [SerializeField] private LayerUsageFlags  layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable;
        [SerializeField] private Material         renderMaterial;
        [SerializeField] private PhysicMaterial   physicsMaterial;

        public LayerUsageFlags	LayerUsage		            { get { return layerUsage;      } set { if (layerUsage      == value) return; var prevValue = layerUsage;      layerUsage      = value; ChiselBrushMaterialManager.OnLayerUsageFlagsChanged(this, prevValue, value); } }
        public Material			RenderMaterial	            { get { return renderMaterial;  } set { if (renderMaterial  == value) return; var prevValue = renderMaterial;  renderMaterial  = value; ChiselBrushMaterialManager.OnRenderMaterialChanged(this, prevValue, value); } }
        public PhysicMaterial	PhysicsMaterial             { get { return physicsMaterial; } set { if (physicsMaterial == value) return; var prevValue = physicsMaterial; physicsMaterial = value; ChiselBrushMaterialManager.OnPhysicsMaterialChanged(this, prevValue, value); } }
        public int				RenderMaterialInstanceID	{ get { return renderMaterial  ? renderMaterial .GetInstanceID() : 0; } }
        public int				PhysicsMaterialInstanceID	{ get { return physicsMaterial ? physicsMaterial.GetInstanceID() : 0; } }
        

        public static ChiselBrushMaterial CreateInstance(ChiselBrushMaterial other)
        {
            return new ChiselBrushMaterial
            {
                LayerUsage      = other.layerUsage,
                RenderMaterial  = other.renderMaterial,
                PhysicsMaterial = other.physicsMaterial
            };
        }

        public static ChiselBrushMaterial CreateInstance(LayerUsageFlags layerUsage = LayerUsageFlags.None)
        {
            return new ChiselBrushMaterial { LayerUsage = layerUsage };
        }

        public static ChiselBrushMaterial CreateInstance(Material renderMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows)
        {
            return new ChiselBrushMaterial
            {
                LayerUsage      = layerUsage,
                RenderMaterial  = renderMaterial
            };
        }

        public static ChiselBrushMaterial CreateInstance(PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.Collidable)
        {
            return new ChiselBrushMaterial
            {
                LayerUsage      = layerUsage,
                PhysicsMaterial = physicsMaterial
            };
        }

        public static ChiselBrushMaterial CreateInstance(Material renderMaterial, PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable)
        {
            return new ChiselBrushMaterial
            {
                LayerUsage      = layerUsage,
                RenderMaterial  = renderMaterial,
                PhysicsMaterial = physicsMaterial
            };
        } 
    }
}
