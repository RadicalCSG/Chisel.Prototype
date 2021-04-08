using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselSphere))]
    public sealed class ChiselSphereEditor : ChiselBrushGeneratorDefinitionEditor<ChiselSphere, ChiselSphereDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselSphere.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselSphere.kNodeTypeName); }
    }
}