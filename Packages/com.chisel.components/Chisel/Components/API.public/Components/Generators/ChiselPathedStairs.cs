using UnityEngine;
using Chisel.Core;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselPathedStairs : ChiselDefinedGeneratorComponent<ChiselPathedStairsDefinition>
    {
        public const string kNodeTypeName = ChiselPathedStairsDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Curve2D Shape
        {
            get { return definition.shape; }
            set { if (value == definition.shape) return; definition.shape = value; OnValidateState(); }
        }
        #endregion
    }
}
