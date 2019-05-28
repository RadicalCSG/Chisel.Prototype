using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnitySceneExtensions;
using System.Linq;

namespace Chisel.Components
{
    // TODO: change name
    [ExecuteInEditMode]
    public sealed class CSGExtrudedShape : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Extruded Shape"; } }
        
        [SerializeField] public CSGExtrudedShapeDefinition definition = new CSGExtrudedShapeDefinition();

        #region Properties
        public Path Path
        {
            get { return definition.path; }
            set
            {
                if (value == definition.path)
                    return;

                definition.path = value;

                OnValidateInternal();
            }
        }
        
        public Curve2D Shape
        {
            get { return definition.shape; }
            set
            {
                if (value == definition.shape)
                    return;

                definition.shape = value;

                OnValidateInternal();
            }
        }
        #endregion

        protected override void OnResetInternal()    { definition.Reset(); base.OnResetInternal(); }
        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        

        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = generatedBrushes.BrushMeshes;
            if (!BrushMeshFactory.GenerateExtrudedShape(ref brushMeshes, ref definition))
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
