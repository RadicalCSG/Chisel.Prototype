using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselStadium : ChiselGeneratorComponent
    {
        public const string kNodeTypeName = "Stadium";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        // TODO: make this private
        [SerializeField] public ChiselStadiumDefinition definition = new ChiselStadiumDefinition();

        // TODO: implement properties

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = new[] { new BrushMesh() };
            if (BrushMeshFactory.GenerateStadium(ref brushMeshes[0], ref definition))
            {
                brushContainerAsset.Clear();
                return;
            }

            brushContainerAsset.SetSubMeshes(brushMeshes);
            brushContainerAsset.CalculatePlanes();
            brushContainerAsset.SetDirty();
        }
    }
}
