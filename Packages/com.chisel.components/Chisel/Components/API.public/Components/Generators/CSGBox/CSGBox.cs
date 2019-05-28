using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    public sealed class CSGBox : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Box"; } }
        
        [SerializeField] public CSGBoxDefinition definition = new CSGBoxDefinition();

        #region Properties
        public Bounds Bounds
        {
            get { return definition.bounds; }
            set { if (value == definition.bounds) return; definition.bounds = value; OnValidateInternal(); }
        }

        public Vector3 Min
        {
            get { return definition.min; }
            set { if (value == definition.min) return; definition.min = value; OnValidateInternal(); }
        }

        public Vector3 Max
        {
            get { return definition.max; }
            set { if (value == definition.max) return; definition.max = value; OnValidateInternal(); }
        }

        public Vector3 Center
        {
            get { return definition.center; }
            set { if (value == definition.center) return; definition.center = value; OnValidateInternal(); }
        }

        public Vector3 Size
        {
            get { return definition.size; }
            set { if (value == definition.size) return; definition.size = value; OnValidateInternal(); }
        }
        #endregion

        protected override void OnResetInternal()    { definition.Reset(); base.OnResetInternal(); }
        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        
        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = new[] { new BrushMesh() };
            if (!BrushMeshFactory.GenerateBox(ref brushMeshes[0], ref definition))
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