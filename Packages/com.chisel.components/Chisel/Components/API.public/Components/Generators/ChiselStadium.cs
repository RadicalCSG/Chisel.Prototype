using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselStadium : ChiselDefinedGeneratorComponent<ChiselStadiumDefinition>
    {
        public const string kNodeTypeName = ChiselStadiumDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }

        // TODO: add all properties of ChiselStadiumDefinition
    }
}
