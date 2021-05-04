using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselExtrudedShapeComponent))]
    public sealed class ChiselExtrudedShapeEditor : ChiselGeneratorDefinitionEditor<Components.ChiselExtrudedShapeComponent, ChiselExtrudedShapeDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselExtrudedShapeComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselExtrudedShapeComponent.kNodeTypeName); }
    }
}
