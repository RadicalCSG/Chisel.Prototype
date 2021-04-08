using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselTorus))]
    public sealed class ChiselTorusEditor : ChiselBrushGeneratorDefinitionEditor<ChiselTorus, ChiselTorusDefinition>
    {
        [MenuItem("GameObject/Chisel/Create/" + ChiselTorus.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselTorus.kNodeTypeName); }
    }
}