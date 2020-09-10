using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselHemisphere))]
    public sealed class ChiselHemisphereEditor : ChiselGeneratorDefinitionEditor<ChiselHemisphere, ChiselHemisphereDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselHemisphere.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselHemisphere.kNodeTypeName); }
    }
}