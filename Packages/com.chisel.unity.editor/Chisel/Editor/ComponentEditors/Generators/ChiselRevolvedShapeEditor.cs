using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselRevolvedShapeComponent))]
    public sealed class ChiselRevolvedShapeEditor : ChiselGeneratorDefinitionEditor<ChiselRevolvedShapeComponent, ChiselRevolvedShapeDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselRevolvedShapeComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselRevolvedShapeComponent.kNodeTypeName); }
    }
}