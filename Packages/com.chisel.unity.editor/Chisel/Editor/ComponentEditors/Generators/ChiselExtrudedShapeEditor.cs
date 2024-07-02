using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselExtrudedShapeComponent))]
    public sealed class ChiselExtrudedShapeEditor : ChiselGeneratorDefinitionEditor<ChiselExtrudedShapeComponent, ChiselExtrudedShapeDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselExtrudedShapeComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselExtrudedShapeComponent.kNodeTypeName); }
    }
}
