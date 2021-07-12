using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(Components.ChiselBoxComponent))]
    public sealed class ChiselBoxEditor : ChiselGeneratorDefinitionEditor<Components.ChiselBoxComponent, ChiselBoxDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + Components.ChiselBoxComponent.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, Components.ChiselBoxComponent.kNodeTypeName); }
    }
}