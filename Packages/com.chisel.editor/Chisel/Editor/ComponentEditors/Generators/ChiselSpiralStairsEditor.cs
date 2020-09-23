using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselSpiralStairs))]
    public sealed class ChiselSpiralStairsEditor : ChiselGeneratorDefinitionEditor<ChiselSpiralStairs, ChiselSpiralStairsDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselSpiralStairs.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselSpiralStairs.kNodeTypeName); }
    }
}
