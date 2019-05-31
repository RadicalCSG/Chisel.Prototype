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
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselExtrudedShape : ChiselGeneratorComponent
    {
        public const string kNodeTypeName = "Extruded Shape";
        public override string NodeTypeName { get { return kNodeTypeName; } }
        
        [SerializeField] public ChiselExtrudedShapeDefinition definition = new ChiselExtrudedShapeDefinition();

        #region Properties
        public ChiselPath Path
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
            var brushMeshes = brushContainerAsset.BrushMeshes;
            if (!BrushMeshFactory.GenerateExtrudedShape(ref brushMeshes, ref definition))
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
