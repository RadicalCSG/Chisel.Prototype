using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [ExecuteInEditMode, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselSphereComponent : ChiselBrushGeneratorComponent<ChiselSphereDefinition, Core.ChiselSphere>
    {
        public const string kNodeTypeName = Core.ChiselSphereDefinition.kNodeTypeName;
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

        public bool GenerateFromCenter
        {
            get { return definition.settings.generateFromCenter; }
            set { if (definition.settings.generateFromCenter == value) return; definition.settings.generateFromCenter = value; OnValidateState(); }
        }
        #endregion 
    }
}
