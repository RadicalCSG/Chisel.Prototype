using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselTorusComponent))]
    public sealed class ChiselTorusEditor : ChiselGeneratorDefinitionEditor<Components.ChiselTorusComponent, ChiselTorusDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselTorusComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselTorusComponent.kNodeTypeName); }
    }
}