using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Chisel.Core;
using Chisel.Assets;

namespace Chisel.Components
{
    // TODO: add properties for SurfaceDescription/BrushMaterials
    // TODO: beveled edges (for top and/or bottom)
    //			-> makes this a capsule
    [ExecuteInEditMode] 
    public sealed class CSGCylinder : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Cylinder"; } }

        [SerializeField] public CSGCylinderDefinition definition = new CSGCylinderDefinition();

        #region Properties
        public CSGCircleDefinition Top      { get { return definition.top; } }
        public CSGCircleDefinition Bottom   { get { return definition.bottom; } }
        
        public CylinderShapeType Type
        {
            get { return definition.type; }
            set { if (value == definition.type) return; definition.type = value; OnValidateInternal(); }
        }

        public float Height
        {
            get { return definition.top.height; }
            set { if (value == definition.top.height) return; definition.top.height = definition.bottom.height + value; OnValidateInternal(); }
        }

        public float TopHeight
        {
            get { return definition.top.height; }
            set { if (value == definition.top.height) return; definition.top.height = value; OnValidateInternal(); }
        }

        public float BottomHeight
        {
            get { return definition.bottom.height; }
            set { if (value == definition.bottom.height) return; definition.bottom.height = value; OnValidateInternal(); }
        }

        public float TopDiameterX
        {
            get { return definition.TopDiameterX; }
            set { if (value == definition.TopDiameterX) return; definition.TopDiameterX = value; OnValidateInternal(); }
        }

        public float TopDiameterZ
        {
            get { return definition.TopDiameterZ; }
            set { if (value == definition.TopDiameterZ) return; definition.TopDiameterZ = value; OnValidateInternal(); }
        }
        
        public float Diameter
        {
            get { return definition.Diameter; }
            set { if (value == definition.Diameter) return; definition.Diameter = value; OnValidateInternal(); }
        }
        
        public float DiameterX
        {
            get { return definition.DiameterX; }
            set { if (value == definition.DiameterX) return; definition.DiameterX = value; OnValidateInternal(); }
        }
        
        public float DiameterZ
        {
            get { return definition.DiameterZ; }
            set { if (value == definition.DiameterZ) return; definition.DiameterZ = value; OnValidateInternal(); }
        }

        public float BottomDiameterX
        {
            get { return definition.BottomDiameterX; }
            set { if (value == definition.BottomDiameterX) return; definition.BottomDiameterX = value; OnValidateInternal(); }
        }

        public float BottomDiameterZ
        {
            get { return definition.BottomDiameterZ; }
            set { if (value == definition.BottomDiameterZ) return; definition.BottomDiameterZ = value; OnValidateInternal(); }
        }

        public float Rotation
        {
            get { return definition.rotation; }
            set { if (value == definition.rotation) return; definition.rotation = value; OnValidateInternal(); }
        }
        
        public bool IsEllipsoid
        {
            get { return definition.isEllipsoid; }
            set { if (value == definition.isEllipsoid) return; definition.isEllipsoid = value; OnValidateInternal(); }
        }
        
        public uint SmoothingGroup
        {
            get { return definition.smoothingGroup; }
            set { if (value == definition.smoothingGroup) return; definition.smoothingGroup = value; OnValidateInternal(); }
        }

        public int Sides
        {
            get { return definition.sides; }
            set { if (value == definition.sides) return; definition.sides = value; OnValidateInternal(); }
        }
        #endregion

        protected override void OnResetInternal()    { definition.Reset(); base.OnResetInternal(); }
        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            BrushMeshAssetFactory.GenerateCylinderAsset(brushMeshAsset, definition);
        }
    }
}