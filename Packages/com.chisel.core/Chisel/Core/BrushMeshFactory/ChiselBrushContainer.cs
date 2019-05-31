using System;
using System.Collections.Generic;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselBrushContainer
    {
        public BrushMesh[]        brushMeshes;
        public CSGOperationType[] operations;

        public bool Empty	{ get { if (brushMeshes == null) return true; return brushMeshes.Length == 0; } }
        public int	Count	{ get { if (brushMeshes == null) return 0; return brushMeshes.Length; } }

        public void Clear()
        {
            brushMeshes = null;
            operations = null;
        }
    }
}