using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    public sealed class CSGSphere : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Sphere"; } }

        // TODO: make this private
        [SerializeField] public CSGSphereDefinition definition = new CSGSphereDefinition();

        #region Properties
        public Vector3 DiameterXYZ
        {
            get { return definition.diameterXYZ; }
            set { if (definition.diameterXYZ == value) return; definition.diameterXYZ = value; OnValidateInternal(); }
        }
        public float Height
        {
            get { return definition.diameterXYZ.y; }
            set { if (definition.diameterXYZ.y == value) return; definition.diameterXYZ.y = value; OnValidateInternal(); }
        }

        public float DiameterX
        {
            get { return definition.diameterXYZ.x; }
            set { if (definition.diameterXYZ.x == value) return; definition.diameterXYZ.x = value; OnValidateInternal(); }
        }

        public float DiameterZ
        {
            get { return definition.diameterXYZ.z; }
            set { if (definition.diameterXYZ.z == value) return; definition.diameterXYZ.z = value; OnValidateInternal(); }
        }

        public int HorizontalSegments
        {
            get { return definition.horizontalSegments; }
            set { if (value == definition.horizontalSegments) return; definition.horizontalSegments = value; OnValidateInternal(); }
        }

        public int VerticalSegments
        {
            get { return definition.verticalSegments; }
            set { if (value == definition.verticalSegments) return; definition.verticalSegments = value; OnValidateInternal(); }
        }

        public bool GenerateFromCenter
        {
            get { return definition.generateFromCenter; }
            set { if (value == definition.generateFromCenter) return; definition.generateFromCenter = value; OnValidateInternal(); }
        }
        #endregion

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            BrushMeshAssetFactory.GenerateSphereAsset(brushMeshAsset, definition);
        }
    }
}
