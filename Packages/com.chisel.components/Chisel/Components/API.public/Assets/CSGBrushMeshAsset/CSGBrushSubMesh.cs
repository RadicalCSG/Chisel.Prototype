using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Assets
{
    [Serializable]
    public sealed class CSGBrushSubMesh
    {
        public CSGBrushSubMesh() { }

        public CSGBrushSubMesh(CSGBrushSubMesh other)
        {
            this.brushMesh = new BrushMesh(other.brushMesh);
            this.operation = other.operation;
        }

        [SerializeField] public BrushMesh			brushMesh = new BrushMesh() { version = BrushMesh.CurrentVersion };
        [SerializeField] public CSGOperationType    operation = CSGOperationType.Additive;
    }
}
