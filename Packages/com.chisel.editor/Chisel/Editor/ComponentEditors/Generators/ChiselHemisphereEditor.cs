using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselHemisphereComponent))]
    public sealed class ChiselHemisphereEditor : ChiselGeneratorDefinitionEditor<Components.ChiselHemisphereComponent, ChiselHemisphereDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselHemisphereComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselHemisphereComponent.kNodeTypeName); }
    }
}