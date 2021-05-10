using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselStadiumComponent : ChiselBrushGeneratorComponent<ChiselStadiumDefinition, Core.ChiselStadium>
    {
        public const string kNodeTypeName = Core.ChiselStadiumDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        // TODO: add all properties of ChiselStadiumDefinition
    }
}
