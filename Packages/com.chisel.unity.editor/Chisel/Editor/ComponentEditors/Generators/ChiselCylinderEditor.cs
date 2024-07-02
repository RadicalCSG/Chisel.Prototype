using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCylinderComponent))]
    public sealed class ChiselCylinderEditor : ChiselGeneratorDefinitionEditor<ChiselCylinderComponent, ChiselCylinderDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselCylinderComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCylinderComponent.kNodeTypeName); }
    }
}