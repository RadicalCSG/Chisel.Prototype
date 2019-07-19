using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselCapsule : ChiselDefinedGeneratorComponent<ChiselCapsuleDefinition>
    {
        public const string kNodeTypeName = "Capsule";
        public override string NodeTypeName { get { return kNodeTypeName; } }
        
        #region Properties
        public float Height
        {
            get { return definition.height; }
            set { if (definition.height == value) return; definition.height = value; OnValidateInternal(); }
        }
        public float TopHeight
        {
            get { return definition.topHeight; }
            set { if (definition.topHeight == value) return; definition.topHeight = value; OnValidateInternal(); }
        }
        public float BottomHeight
        {
            get { return definition.bottomHeight; }
            set { if (definition.bottomHeight == value) return; definition.bottomHeight = value; OnValidateInternal(); }
        }
        public float DiameterX
        {
            get { return definition.diameterX; }
            set { if (definition.diameterX == value) return; definition.diameterX = value; OnValidateInternal(); }
        }
        public float DiameterZ
        {
            get { return definition.diameterZ; }
            set { if (definition.diameterZ == value) return; definition.diameterZ = value; OnValidateInternal(); }
        }
        public int Sides
        {
            get { return definition.sides; }
            set { if (definition.sides == value) return; definition.sides = value; OnValidateInternal(); }
        }
        public int TopSegments
        {
            get { return definition.topSegments; }
            set { if (definition.topSegments == value) return; definition.topSegments = value; OnValidateInternal(); }
        }
        public int BottomSegments
        {
            get { return definition.bottomSegments; }
            set { if (definition.bottomSegments == value) return; definition.bottomSegments = value; OnValidateInternal(); }
        }
        public bool HaveRoundedTop
        {
            get { return definition.haveRoundedTop; }
        }
        public bool HaveRoundedBottom
        {
            get { return definition.haveRoundedBottom; }
        }
        #endregion
    }
}
