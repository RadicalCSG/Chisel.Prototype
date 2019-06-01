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

        public void Reset()
        {
            brushMeshes = null;
            operations = null;
        }

        public void CopyFrom(List<BrushMesh> brushMeshesList)
        {
            EnsureSize(brushMeshesList.Count);
            if (brushMeshesList.Count > 0)
                brushMeshesList.CopyTo(brushMeshes);
        }

        public bool EnsureSize(int expectedSize)
        {
            if ((brushMeshes != null && expectedSize == brushMeshes.Length) ||
                (brushMeshes == null && expectedSize == 0))
                return false;
            
            if (expectedSize == 0)
            {
                brushMeshes = null;
                return true;
            }

            var newBrushMeshes  = new BrushMesh[expectedSize];
            var prevLength = (brushMeshes == null) ? 0 : brushMeshes.Length;
            if (prevLength > 0)
            {
                Array.Copy(brushMeshes, newBrushMeshes, Mathf.Min(newBrushMeshes.Length, prevLength));
            }
            for (int i = prevLength; i < newBrushMeshes.Length; i++)
                newBrushMeshes[i] = new BrushMesh();
            brushMeshes = newBrushMeshes;
            operations  = new CSGOperationType[expectedSize];
            return true;
        }
    }
}