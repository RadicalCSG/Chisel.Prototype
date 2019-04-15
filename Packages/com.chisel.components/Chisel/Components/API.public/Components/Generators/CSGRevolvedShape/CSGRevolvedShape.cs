﻿using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    public sealed class CSGRevolvedShape : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Revolved Shape"; } }

        // TODO: make this private
        [SerializeField] public CSGRevolvedShapeDefinition definition = new CSGRevolvedShapeDefinition();

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
            BrushMeshAssetFactory.GenerateRevolvedShapeAsset(brushMeshAsset, definition);
        }
    }
}
