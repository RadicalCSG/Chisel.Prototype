using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselCapsuleComponent))]
    public sealed class ChiselCapsuleEditor : ChiselGeneratorDefinitionEditor<Components.ChiselCapsuleComponent, ChiselCapsuleDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselCapsuleComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselCapsuleComponent.kNodeTypeName); }
    }
}