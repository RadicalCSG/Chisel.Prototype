﻿using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    // TODO: add properties for SurfaceDescription/BrushMaterials
    // TODO: beveled edges (for top and/or bottom)
    //			-> makes this a capsule
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselCylinder : ChiselDefinedGeneratorComponent<ChiselCylinderDefinition>
    {
        public const string kNodeTypeName = ChiselCylinderDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public CylinderShapeType Type
        {
            get { return definition.type; }
            set { if (value == definition.type) return; definition.type = value; OnValidateInternal(); }
        }

        public float Height
        {
            get { return definition.height; }
            set { if (value == definition.height) return; definition.height = value; OnValidateInternal(); }
        }

        public float TopHeight
        {
            get { return definition.height + definition.bottomOffset; }
            set { if (value == definition.height - definition.bottomOffset) return; definition.height = value - definition.bottomOffset; OnValidateInternal(); }
        }

        public float BottomHeight
        {
            get { return definition.bottomOffset; }
            set { if (value == definition.bottomOffset) return; definition.bottomOffset = value; OnValidateInternal(); }
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
    }
}