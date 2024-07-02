using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselTorusComponent))]
    public sealed class ChiselTorusEditor : ChiselGeneratorDefinitionEditor<ChiselTorusComponent, ChiselTorusDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselTorusComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselTorusComponent.kNodeTypeName); }
    }
}