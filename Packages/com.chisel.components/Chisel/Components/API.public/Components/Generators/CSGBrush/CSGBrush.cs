using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    // TODO: is RemoveUnusedAssets safe? (relative to monobehaviour / managers?)
    // TODO: how to handle duplication (ctrl-D)? (brushContainerAsset reference no longer unique)
    // TODO: have some sort of bounds that we can use to "focus" on brush
    // TODO: add reset method, use default box
    [ExecuteInEditMode]
    [HelpURL(ChiselGeneratorComponent.kDocumentationBaseURL + nameof(CSGBrush) + ChiselGeneratorComponent.KDocumentationExtension)]
    [AddComponentMenu("Chisel/" + CSGBrush.kNodeTypeName)]
    public sealed class CSGBrush : ChiselGeneratorComponent
    {
        public const string kNodeTypeName = "Brush";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        public CSGBrush() : base() {  }

        public override void UpdateGenerator()
        {
            // This class doesn't generate anything, so we keep the original brush
        }

        protected override void UpdateGeneratorInternal() { }
    }
}