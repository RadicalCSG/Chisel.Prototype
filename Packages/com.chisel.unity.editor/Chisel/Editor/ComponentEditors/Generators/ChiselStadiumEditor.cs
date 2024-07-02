using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselStadiumComponent))]
    public sealed class ChiselStadiumEditor : ChiselGeneratorDefinitionEditor<ChiselStadiumComponent, ChiselStadiumDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselStadiumComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselStadiumComponent.kNodeTypeName); }
    }
}