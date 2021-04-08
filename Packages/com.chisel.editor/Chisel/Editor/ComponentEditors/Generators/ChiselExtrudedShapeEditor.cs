using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselExtrudedShape))]
    public sealed class ChiselExtrudedShapeEditor : ChiselBrushGeneratorDefinitionEditor<ChiselExtrudedShape, ChiselExtrudedShapeDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselExtrudedShape.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselExtrudedShape.kNodeTypeName); }
    }
}
