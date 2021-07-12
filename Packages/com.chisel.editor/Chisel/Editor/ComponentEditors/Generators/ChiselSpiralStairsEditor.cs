using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselSpiralStairsComponent))]
    public sealed class ChiselSpiralStairsEditor : ChiselGeneratorDefinitionEditor<Components.ChiselSpiralStairsComponent, ChiselSpiralStairsDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselSpiralStairsComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselSpiralStairsComponent.kNodeTypeName); }
    }
}
