using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselStadium))]
    public sealed class ChiselStadiumEditor : ChiselBrushGeneratorDefinitionEditor<ChiselStadium, ChiselStadiumDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselStadium.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselStadium.kNodeTypeName); }
    }
}