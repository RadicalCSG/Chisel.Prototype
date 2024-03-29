﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class ChiselMaterialManager
    {
        readonly Dictionary<int, Material>          idToMaterial       = new Dictionary<int, Material>();
        readonly Dictionary<Material, int>          materialToID       = new Dictionary<Material, int>();
        readonly Dictionary<int, PhysicMaterial>    idToPhysicMaterial = new Dictionary<int, PhysicMaterial>();
        readonly Dictionary<PhysicMaterial, int>    physicMaterialToID = new Dictionary<PhysicMaterial, int>();

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(Material material)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(material);
            return objectID.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(PhysicMaterial physicMaterial)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(physicMaterial);
            return objectID.GetHashCode();
        }
#else
        // TODO: Implement a runtime solution
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(Material material)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToID(PhysicMaterial physicMaterial)
        {
            throw new NotImplementedException();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SurfaceLayers GetLayerDefinition(ChiselBrushMaterial brushMaterial)
        {
            return new SurfaceLayers
            {
                layerUsage      = brushMaterial.layerUsage,
                layerParameter1 = brushMaterial.renderMaterial  == null ? 0 : GetID(brushMaterial.renderMaterial),
                layerParameter2 = brushMaterial.physicsMaterial == null ? 0 : GetID(brushMaterial.physicsMaterial)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetID(Material material)
        {
            if (!material)
                return 0;
            if (!materialToID.TryGetValue(material, out var id))
            {
                id = ToID(material);
                materialToID[material] = id;
                idToMaterial[id] = material;
            }
            return id;
        }

        public Material GetMaterial(int id)
        {
            if (idToMaterial.TryGetValue(id, out var material))
            {
                if (material)
                    return material;
                idToMaterial.Remove(id);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetID(PhysicMaterial physicMaterial)
        {
            if (!physicMaterial)
                return 0;
            if (!physicMaterialToID.TryGetValue(physicMaterial, out var id))
            {
                id = ToID(physicMaterial);
                physicMaterialToID[physicMaterial] = id;
                idToPhysicMaterial[id] = physicMaterial;
            }
            return id;
        }

        public PhysicMaterial GetPhysicMaterial(int id)
        {
            if (idToPhysicMaterial.TryGetValue(id, out var physicMaterial))
            {
                if (physicMaterial)
                    return physicMaterial;
                idToPhysicMaterial.Remove(id);
            }
            return null;
        }
    }
}
