using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselSpiralStairsComponent))]
    public sealed class ChiselSpiralStairsEditor : ChiselGeneratorDefinitionEditor<ChiselSpiralStairsComponent, ChiselSpiralStairsDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselSpiralStairsComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselSpiralStairsComponent.kNodeTypeName); }
    }
}
