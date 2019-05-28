using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    public sealed class CSGStadium : ChiselGeneratorComponent
    {
        public override string NodeTypeName { get { return "Stadium"; } }

        // TODO: make this private
        [SerializeField] public CSGStadiumDefinition definition = new CSGStadiumDefinition();

        // TODO: implement properties

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = new[] { new BrushMesh() };
            if (BrushMeshFactory.GenerateStadium(ref brushMeshes[0], ref definition))
            {
                generatedBrushes.Clear();
                return;
            }

            generatedBrushes.SetSubMeshes(brushMeshes);
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
        }
    }
}
