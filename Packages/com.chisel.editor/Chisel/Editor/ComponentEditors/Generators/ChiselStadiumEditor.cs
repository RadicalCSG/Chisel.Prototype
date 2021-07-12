using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselStadiumComponent))]
    public sealed class ChiselStadiumEditor : ChiselGeneratorDefinitionEditor<Components.ChiselStadiumComponent, ChiselStadiumDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselStadiumComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselStadiumComponent.kNodeTypeName); }
    }
}