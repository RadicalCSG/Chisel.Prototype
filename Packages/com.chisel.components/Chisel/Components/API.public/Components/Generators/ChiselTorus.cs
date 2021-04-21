using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselTorus : ChiselDefinedGeneratorComponent<ChiselTorusDefinition>
    {
        public const string kNodeTypeName = ChiselTorusDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }

        // TODO: add all properties of ChiselTorusDefinition
    }
}
