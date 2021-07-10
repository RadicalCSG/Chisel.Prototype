using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class ChiselMaterialManager
    {
        readonly Dictionary<int, Material>           idToMaterial       = new Dictionary<int, Material>();
        readonly Dictionary<int, int>                materialToID       = new Dictionary<int, int>();
        readonly Dictionary<int, PhysicMaterial>     idToPhysicMaterial = new Dictionary<int, PhysicMaterial>();
        readonly Dictionary<int, int>                physicMaterialToID = new Dictionary<int, int>();

#if UNITY_EDITOR
        int ToID(Material material)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(material);
            return objectID.GetHashCode();
        }

        int ToID(PhysicMaterial physicMaterial)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(physicMaterial);
            return objectID.GetHashCode();
        }
#else
        // TODO: Implement a runtime solution
        int ToID(Material material)
        {
            throw new NotImplementedException();
        }

        int ToID(PhysicMaterial physicMaterial)
        {
            throw new NotImplementedException();
        }
#endif

        internal int GetID(Material material)
        {
            if (!material)
                return 0;
            var instanceID = material.GetInstanceID();
            if (materialToID.TryGetValue(instanceID, out var id))
                return id;
            id = ToID(material);
            materialToID[instanceID] = id;
            idToMaterial[id] = material;
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

        internal int GetID(PhysicMaterial physicMaterial)
        {
            if (!physicMaterial)
                return 0;
            var instanceID = physicMaterial.GetInstanceID();
            if (physicMaterialToID.TryGetValue(instanceID, out var id))
                return id;
            id = ToID(physicMaterial);
            physicMaterialToID[instanceID] = id;
            idToPhysicMaterial[id] = physicMaterial;
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
