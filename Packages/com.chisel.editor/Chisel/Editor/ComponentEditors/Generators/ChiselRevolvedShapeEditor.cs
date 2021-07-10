using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselRevolvedShapeComponent))]
    public sealed class ChiselRevolvedShapeEditor : ChiselGeneratorDefinitionEditor<Components.ChiselRevolvedShapeComponent, ChiselRevolvedShapeDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselRevolvedShapeComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselRevolvedShapeComponent.kNodeTypeName); }
    }
}