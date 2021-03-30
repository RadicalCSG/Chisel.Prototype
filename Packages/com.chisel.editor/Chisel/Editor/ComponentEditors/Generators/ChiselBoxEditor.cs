using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselBox))]
    public sealed class ChiselBoxEditor : ChiselBrushGeneratorDefinitionEditor<ChiselBox, ChiselBoxDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselBox.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselBox.kNodeTypeName); }
    }
}