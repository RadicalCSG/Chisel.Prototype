using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselBox : ChiselDefinedBrushGeneratorComponent<ChiselBoxDefinition>
    {
        public const string kNodeTypeName = ChiselBoxDefinition.kNodeTypeName;
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

        #region HasValidState
        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (!base.HasValidState())
                return false;

            if (Size.x == 0 ||
                Size.y == 0 ||
                Size.z == 0)
                return false;

            return true;
        }
        #endregion
    }
}