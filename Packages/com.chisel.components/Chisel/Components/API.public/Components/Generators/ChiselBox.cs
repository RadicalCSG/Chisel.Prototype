using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselBox : ChiselDefinedGeneratorComponent<ChiselBoxDefinition>
    {
        public const string kNodeTypeName = "Box";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Bounds Bounds
        {
            get { return definition.bounds; }
            set { if (value == definition.bounds) return; definition.bounds = value; OnValidateInternal(); }
        }

        public Vector3 Min
        {
            get { return definition.min; }
            set { if (value == definition.min) return; definition.min = value; OnValidateInternal(); }
        }

        public Vector3 Max
        {
            get { return definition.max; }
            set { if (value == definition.max) return; definition.max = value; OnValidateInternal(); }
        }

        public Vector3 Center
        {
            get { return definition.center; }
            set { if (value == definition.center) return; definition.center = value; OnValidateInternal(); }
        }

        public Vector3 Size
        {
            get { return definition.size; }
            set { if (value == definition.size) return; definition.size = value; OnValidateInternal(); }
        }
        #endregion
    }
}