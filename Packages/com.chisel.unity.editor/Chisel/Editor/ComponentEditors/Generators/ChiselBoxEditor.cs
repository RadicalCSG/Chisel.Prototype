using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselBoxComponent))]
    public sealed class ChiselBoxEditor : ChiselGeneratorDefinitionEditor<ChiselBoxComponent, ChiselBoxDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselBoxComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselBoxComponent.kNodeTypeName); }
    }
}