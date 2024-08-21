using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    // TODO: create real Asset that contains this so we can drag & drop a complete "material" onto a brush
    [Serializable]
    public sealed class ChiselBrushMaterial : IDisposable
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kLayerUsageFieldName      = nameof(layerUsage);
        public const string kRenderMaterialFieldName  = nameof(renderMaterial);
        public const string kPhysicsMaterialFieldName = nameof(physicsMaterial);

        public void Dispose() { }
        

        [SerializeField] internal LayerUsageFlags   layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable;
        [SerializeField] internal Material          renderMaterial;
        [SerializeField] internal PhysicMaterial    physicsMaterial;

        public LayerUsageFlags	LayerUsage		            { get { return layerUsage;      } set { layerUsage      = value; } }
        public Material			RenderMaterial	            { get { return renderMaterial;  } set { renderMaterial  = value; } }
        public PhysicMaterial	PhysicsMaterial             { get { return physicsMaterial; } set { physicsMaterial = value; } }
        public int				RenderMaterialInstanceID	{ [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return ChiselMaterialManager.Instance.GetID(renderMaterial); } }
        public int				PhysicsMaterialInstanceID	{ [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return ChiselMaterialManager.Instance.GetID(physicsMaterial); } }

        public SurfaceLayers    LayerDefinition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var materialManager = ChiselMaterialManager.Instance;
                return new SurfaceLayers
                {
                    layerUsage      = layerUsage,
                    layerParameter1 = materialManager.GetID(renderMaterial),
                    layerParameter2 = materialManager.GetID(physicsMaterial)
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 
                    math.hash(new uint3(
                        (uint)layerUsage,
                        (uint)PhysicsMaterialInstanceID,
                        (uint)RenderMaterialInstanceID
                    ));
                return (int)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static ChiselBrushMaterial CreateInstance(ChiselBrushMaterial other) { return CreateInstance(other.renderMaterial, other.physicsMaterial, other.layerUsage); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static ChiselBrushMaterial CreateInstance(LayerUsageFlags layerUsage = LayerUsageFlags.None) { return CreateInstance(null, null, layerUsage); } 
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static ChiselBrushMaterial CreateInstance(Material renderMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows) { return CreateInstance(renderMaterial, null, layerUsage); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static ChiselBrushMaterial CreateInstance(PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.Collidable) { return CreateInstance(null, physicsMaterial, layerUsage); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBrushMaterial CreateInstance(Material renderMaterial, PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable)
        {
            return new ChiselBrushMaterial
            {
                LayerUsage = layerUsage,
                RenderMaterial = renderMaterial,
                PhysicsMaterial = physicsMaterial
            };
        }
    }
}
