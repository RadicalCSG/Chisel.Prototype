using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCapsule))]
    public sealed class ChiselCapsuleEditor : ChiselGeneratorDefinitionEditor<ChiselCapsule, ChiselCapsuleDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselCapsule.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCapsule.kNodeTypeName); }
    }
}