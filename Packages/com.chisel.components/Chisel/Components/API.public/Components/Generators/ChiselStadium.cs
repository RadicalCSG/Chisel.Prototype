using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselStadium : ChiselBrushGeneratorComponent<ChiselStadiumDefinition, ChiselStadiumGenerator, StadiumSettings>
    {
        public const string kNodeTypeName = ChiselStadiumDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        // TODO: add all properties of ChiselStadiumDefinition
    }
}
