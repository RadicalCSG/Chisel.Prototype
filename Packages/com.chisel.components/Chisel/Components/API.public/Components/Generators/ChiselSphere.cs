using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselSphere : ChiselDefinedGeneratorComponent<ChiselSphereDefinition>
    {
        public const string kNodeTypeName = ChiselSphereDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Vector3 DiameterXYZ
        {
            get { return definition.diameterXYZ; }
            set { if (definition.diameterXYZ == value) return; definition.diameterXYZ = value; OnValidateState(); }
        }
        public float Height
        {
            get { return definition.diameterXYZ.y; }
            set { if (definition.diameterXYZ.y == value) return; definition.diameterXYZ.y = value; OnValidateState(); }
        }

        public float DiameterX
        {
            get { return definition.diameterXYZ.x; }
            set { if (definition.diameterXYZ.x == value) return; definition.diameterXYZ.x = value; OnValidateState(); }
        }

        public float DiameterZ
        {
            get { return definition.diameterXYZ.z; }
            set { if (definition.diameterXYZ.z == value) return; definition.diameterXYZ.z = value; OnValidateState(); }
        }

        public int HorizontalSegments
        {
            get { return definition.horizontalSegments; }
            set { if (value == definition.horizontalSegments) return; definition.horizontalSegments = value; OnValidateState(); }
        }

        public int VerticalSegments
        {
            get { return definition.verticalSegments; }
            set { if (value == definition.verticalSegments) return; definition.verticalSegments = value; OnValidateState(); }
        }

        public bool GenerateFromCenter
        {
            get { return definition.generateFromCenter; }
            set { if (value == definition.generateFromCenter) return; definition.generateFromCenter = value; OnValidateState(); }
        }
        #endregion 
    }
}
