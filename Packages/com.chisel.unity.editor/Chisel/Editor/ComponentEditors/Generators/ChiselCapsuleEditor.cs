using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCapsuleComponent))]
    public sealed class ChiselCapsuleEditor : ChiselGeneratorDefinitionEditor<ChiselCapsuleComponent, ChiselCapsuleDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselCapsuleComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCapsuleComponent.kNodeTypeName); }
    }
}