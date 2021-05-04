using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [ExecuteInEditMode, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselCylinderComponent : ChiselBrushGeneratorComponent<ChiselCylinderDefinition, Core.ChiselCylinder>
    {
        public const string kNodeTypeName = Core.ChiselCylinderDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public CylinderShapeType Type
        {
            get { return definition.settings.type; }
            set { if (value == definition.settings.type) return; definition.settings.type = value; OnValidateState(); }
        }

        public float Height
        {
            get { return definition.settings.height; }
            set { if (value == definition.settings.height) return; definition.settings.height = value; OnValidateState(); }
        }

        public float TopHeight
        {
            get { return definition.settings.height + definition.settings.bottomOffset; }
            set { if (value == definition.settings.height - definition.settings.bottomOffset) return; definition.settings.height = value - definition.settings.bottomOffset; OnValidateState(); }
        }

        public float BottomHeight
        {
            get { return definition.settings.bottomOffset; }
            set { if (value == definition.settings.bottomOffset) return; definition.settings.bottomOffset = value; OnValidateState(); }
        }

        public float TopDiameterX
        {
            get { return definition.settings.TopDiameterX; }
            set { if (value == definition.settings.TopDiameterX) return; definition.settings.TopDiameterX = value; OnValidateState(); }
        }

        public float TopDiameterZ
        {
            get { return definition.settings.TopDiameterZ; }
            set { if (value == definition.settings.TopDiameterZ) return; definition.settings.TopDiameterZ = value; OnValidateState(); }
        }
        
        public float Diameter
        {
            get { return definition.settings.Diameter; }
            set { if (value == definition.settings.Diameter) return; definition.settings.Diameter = value; OnValidateState(); }
        }
        
        public float DiameterX
        {
            get { return definition.settings.DiameterX; }
            set { if (value == definition.settings.DiameterX) return; definition.settings.DiameterX = value; OnValidateState(); }
        }
        
        public float DiameterZ
        {
            get { return definition.settings.DiameterZ; }
            set { if (value == definition.settings.DiameterZ) return; definition.settings.DiameterZ = value; OnValidateState(); }
        }

        public float BottomDiameterX
        {
            get { return definition.settings.BottomDiameterX; }
            set { if (value == definition.settings.BottomDiameterX) return; definition.settings.BottomDiameterX = value; OnValidateState(); }
        }

        public float BottomDiameterZ
        {
            get { return definition.settings.BottomDiameterZ; }
            set { if (value == definition.settings.BottomDiameterZ) return; definition.settings.BottomDiameterZ = value; OnValidateState(); }
        }

        public float Rotation
        {
            get { return definition.settings.rotation; }
            set { if (value == definition.settings.rotation) return; definition.settings.rotation = value; OnValidateState(); }
        }
        
        public bool IsEllipsoid
        {
            get { return definition.settings.isEllipsoid; }
            set { if (value == definition.settings.isEllipsoid) return; definition.settings.isEllipsoid = value; OnValidateState(); }
        }
        
        public uint SmoothingGroup
        {
            get { return definition.settings.smoothingGroup; }
            set { if (value == definition.settings.smoothingGroup) return; definition.settings.smoothingGroup = value; OnValidateState(); }
        }

        public int Sides
        {
            get { return definition.settings.sides; }
            set { if (value == definition.settings.sides) return; definition.settings.sides = value; OnValidateState(); }
        }
        #endregion
    }
}