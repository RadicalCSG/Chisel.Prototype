using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselLinearStairs))]
    public sealed class ChiselLinearStairsEditor : ChiselBrushGeneratorDefinitionEditor<ChiselLinearStairs, ChiselLinearStairsDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselLinearStairs.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselLinearStairs.kNodeTypeName); }
    }
}
