using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselHemisphereComponent))]
    public sealed class ChiselHemisphereEditor : ChiselGeneratorDefinitionEditor<ChiselHemisphereComponent, ChiselHemisphereDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselHemisphereComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselHemisphereComponent.kNodeTypeName); }
    }
}