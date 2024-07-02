using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselSphereComponent))]
    public sealed class ChiselSphereEditor : ChiselGeneratorDefinitionEditor<Components.ChiselSphereComponent, ChiselSphereDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselSphereComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselSphereComponent.kNodeTypeName); }
    }
}