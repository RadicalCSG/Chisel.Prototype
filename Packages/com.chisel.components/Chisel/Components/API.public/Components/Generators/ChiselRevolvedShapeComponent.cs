using UnityEngine;
using Chisel.Core;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselRevolvedShapeComponent : ChiselBranchGeneratorComponent<Core.ChiselRevolvedShape, ChiselRevolvedShapeDefinition>
    {
        public const string kNodeTypeName = Core.ChiselRevolvedShapeDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Curve2D Shape
        {
            get { return definition.shape; }
            set { if (value == definition.shape) return; definition.shape = value; OnValidateState(); }
        }
        #endregion
    }
}
