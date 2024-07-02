using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselSphereComponent))]
    public sealed class ChiselSphereEditor : ChiselGeneratorDefinitionEditor<ChiselSphereComponent, ChiselSphereDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselSphereComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselSphereComponent.kNodeTypeName); }
    }
}