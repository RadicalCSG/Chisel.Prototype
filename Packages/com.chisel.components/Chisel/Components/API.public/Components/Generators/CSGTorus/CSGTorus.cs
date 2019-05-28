using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    public sealed class CSGTorus : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Torus"; } }

        // TODO: make this private
        [SerializeField] public CSGTorusDefinition definition = new CSGTorusDefinition();

        // TODO: implement properties

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            BrushMeshAssetFactory.GenerateTorus(brushMeshAsset, definition);
        }
    }
}
