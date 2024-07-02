using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselCylinderComponent))]
    public sealed class ChiselCylinderEditor : ChiselGeneratorDefinitionEditor<Components.ChiselCylinderComponent, ChiselCylinderDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselCylinderComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselCylinderComponent.kNodeTypeName); }
    }
}