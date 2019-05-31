using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselPathedStairs : ChiselGeneratorComponent
    {
        public const string kNodeTypeName = "Pathed Stairs";
        public override string NodeTypeName { get { return kNodeTypeName; } }


        // TODO: make this private
        [SerializeField] public ChiselPathedStairsDefinition definition = new ChiselPathedStairsDefinition();

        // TODO: implement properties

        public Curve2D Shape
        {
            get { return definition.shape; }
            set { if (value == definition.shape) return; definition.shape = value; OnValidateInternal(); }
        }

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = brushContainerAsset.BrushMeshes;
            if (!BrushMeshFactory.GeneratePathedStairs(ref brushMeshes, ref definition))
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
