using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselCapsule : ChiselDefinedGeneratorComponent<ChiselCapsuleDefinition>
    {
        public const string kNodeTypeName = ChiselCapsuleDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }
        
        #region Properties
        public float Height
        {
            get { return definition.settings.height; }
            set { if (definition.settings.height == value) return; definition.settings.height = value; OnValidateInternal(); }
        }
        public float TopHeight
        {
            get { return definition.settings.topHeight; }
            set { if (definition.settings.topHeight == value) return; definition.settings.topHeight = value; OnValidateInternal(); }
        }
        public float BottomHeight
        {
            get { return definition.settings.bottomHeight; }
            set { if (definition.settings.bottomHeight == value) return; definition.settings.bottomHeight = value; OnValidateInternal(); }
        }
        public float DiameterX
        {
            get { return definition.settings.diameterX; }
            set { if (definition.settings.diameterX == value) return; definition.settings.diameterX = value; OnValidateInternal(); }
        }
        public float DiameterZ
        {
            get { return definition.settings.diameterZ; }
            set { if (definition.settings.diameterZ == value) return; definition.settings.diameterZ = value; OnValidateInternal(); }
        }
        public int Sides
        {
            get { return definition.settings.sides; }
            set { if (definition.settings.sides == value) return; definition.settings.sides = value; OnValidateInternal(); }
        }
        public int TopSegments
        {
            get { return definition.settings.topSegments; }
            set { if (definition.settings.topSegments == value) return; definition.settings.topSegments = value; OnValidateInternal(); }
        }
        public int BottomSegments
        {
            get { return definition.settings.bottomSegments; }
            set { if (definition.settings.bottomSegments == value) return; definition.settings.bottomSegments = value; OnValidateInternal(); }
        }
        public bool HaveRoundedTop
        {
            get { return definition.settings.HaveRoundedTop; }
        }
        public bool HaveRoundedBottom
        {
            get { return definition.settings.HaveRoundedBottom; }
        }
        #endregion
    }
}
