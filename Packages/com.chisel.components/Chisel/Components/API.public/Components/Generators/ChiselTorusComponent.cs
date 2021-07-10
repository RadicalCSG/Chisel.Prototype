using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselTorusComponent : ChiselBranchGeneratorComponent<Core.ChiselTorus, ChiselTorusDefinition>
    {
        public const string kNodeTypeName = Core.ChiselTorusDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        // TODO: add all properties of ChiselTorusDefinition
    }
}
