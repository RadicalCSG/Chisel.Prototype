using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselLinearStairsComponent))]
    public sealed class ChiselLinearStairsEditor : ChiselGeneratorDefinitionEditor<Components.ChiselLinearStairsComponent, ChiselLinearStairsDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselLinearStairsComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselLinearStairsComponent.kNodeTypeName); }
    }
}
