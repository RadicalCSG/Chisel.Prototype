using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselRevolvedShape))]
    public sealed class ChiselRevolvedShapeEditor : ChiselGeneratorDefinitionEditor<ChiselRevolvedShape, ChiselRevolvedShapeDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselRevolvedShape.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselRevolvedShape.kNodeTypeName); }
    }
}