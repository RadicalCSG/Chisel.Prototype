using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(ChiselGeneratorComponent.kDocumentationBaseURL + nameof(CSGTorus) + ChiselGeneratorComponent.KDocumentationExtension)]
    [AddComponentMenu("Chisel/" + CSGTorus.kNodeTypeName)]
    public sealed class CSGTorus : ChiselGeneratorComponent
    {
        public const string kNodeTypeName = "Torus";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        // TODO: make this private
        [SerializeField] public ChiselTorusDefinition definition = new ChiselTorusDefinition();

        // TODO: implement properties

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = brushContainerAsset.BrushMeshes;
            if (!BrushMeshFactory.GenerateTorus(ref brushMeshes, ref definition))
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
