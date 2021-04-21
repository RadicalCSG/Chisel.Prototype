using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselPathedStairs))]
    public sealed class ChiselPathedStairsEditor : ChiselGeneratorDefinitionEditor<ChiselPathedStairs, ChiselPathedStairsDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselPathedStairs.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselPathedStairs.kNodeTypeName); }
    }
}