using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselHemisphere : ChiselBrushGeneratorComponent<ChiselHemisphereDefinition, ChiselHemisphereGenerator, HemisphereSettings>
    {
        public const string kNodeTypeName = ChiselHemisphereDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Vector3 DiameterXYZ
        {
            get { return definition.settings.diameterXYZ; }
            set { if ((Vector3)definition.settings.diameterXYZ == value) return; definition.settings.diameterXYZ = value; OnValidateState(); }
        }

        public float Height
        {
            get { return definition.settings.diameterXYZ.y; }
            set { if (definition.settings.diameterXYZ.y == value) return; definition.settings.diameterXYZ.y = value; OnValidateState(); }
        }

        public float DiameterX
        {
            get { return definition.settings.diameterXYZ.x; }
            set { if (definition.settings.diameterXYZ.x == value) return; definition.settings.diameterXYZ.x = value; OnValidateState(); }
        }

        public float DiameterZ
        {
            get { return definition.settings.diameterXYZ.z; }
            set { if (definition.settings.diameterXYZ.z == value) return; definition.settings.diameterXYZ.z = value; OnValidateState(); }
        }

        public int HorizontalSegments
        {
            get { return definition.settings.horizontalSegments; }
            set { if (value == definition.settings.horizontalSegments) return; definition.settings.horizontalSegments = value; OnValidateState(); }
        }

        public int VerticalSegments
        {
            get { return definition.settings.verticalSegments; }
            set { if (value == definition.settings.verticalSegments) return; definition.settings.verticalSegments = value; OnValidateState(); }
        }
        #endregion
    }
}
