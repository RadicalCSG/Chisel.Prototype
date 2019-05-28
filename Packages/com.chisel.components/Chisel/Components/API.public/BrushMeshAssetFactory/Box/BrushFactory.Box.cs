using System;
using System.Linq;
using Chisel.Core;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateBox(ChiselGeneratedBrushes generatedBrushes, ref CSGBoxDefinition definition)
        {
            var brushMeshes = new[] { new BrushMesh() };
            if (!BrushMeshFactory.GenerateBox(ref brushMeshes[0], ref definition))
            {
                generatedBrushes.Clear();
                return false;
            }
            generatedBrushes.SetSubMeshes(brushMeshes);
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
            return true;
        }
    }
}