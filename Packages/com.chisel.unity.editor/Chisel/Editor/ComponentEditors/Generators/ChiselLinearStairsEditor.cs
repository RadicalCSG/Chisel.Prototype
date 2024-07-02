using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselLinearStairsComponent))]
    public sealed class ChiselLinearStairsEditor : ChiselGeneratorDefinitionEditor<ChiselLinearStairsComponent, ChiselLinearStairsDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselLinearStairsComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselLinearStairsComponent.kNodeTypeName); }
    }
}
