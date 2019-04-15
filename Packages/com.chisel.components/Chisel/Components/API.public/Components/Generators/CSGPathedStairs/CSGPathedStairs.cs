using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    public sealed class CSGPathedStairs : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Pathed Stairs"; } }


        // TODO: make this private
        [SerializeField] public CSGPathedStairsDefinition definition = new CSGPathedStairsDefinition();

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
            BrushMeshAssetFactory.GeneratePathedStairsAsset(brushMeshAsset, definition);
        }
    }
}
