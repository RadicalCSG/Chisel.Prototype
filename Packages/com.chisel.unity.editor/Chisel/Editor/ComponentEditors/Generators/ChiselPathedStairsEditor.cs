using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselPathedStairsComponent))]
    public sealed class ChiselPathedStairsEditor : ChiselGeneratorDefinitionEditor<Components.ChiselPathedStairsComponent, ChiselPathedStairsDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselPathedStairsComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselPathedStairsComponent.kNodeTypeName); }
    }
}