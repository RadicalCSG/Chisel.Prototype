using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCylinder))]
    public sealed class ChiselCylinderEditor : ChiselBrushGeneratorDefinitionEditor<ChiselCylinder, ChiselCylinderDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselCylinder.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCylinder.kNodeTypeName); }
    }
}