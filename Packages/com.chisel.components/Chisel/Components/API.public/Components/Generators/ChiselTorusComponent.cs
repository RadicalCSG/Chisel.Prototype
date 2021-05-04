using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [ExecuteInEditMode, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselTorusComponent : ChiselBranchGeneratorComponent<Core.ChiselTorus, ChiselTorusDefinition>
    {
        public const string kNodeTypeName = Core.ChiselTorusDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        // TODO: add all properties of ChiselTorusDefinition
    }
}
