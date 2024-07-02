using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselPathedStairsComponent))]
    public sealed class ChiselPathedStairsEditor : ChiselGeneratorDefinitionEditor<ChiselPathedStairsComponent, ChiselPathedStairsDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselPathedStairsComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselPathedStairsComponent.kNodeTypeName); }
    }
}