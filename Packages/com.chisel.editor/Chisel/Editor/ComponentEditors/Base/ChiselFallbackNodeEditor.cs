using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselNode), isFallback = true)]
    [CanEditMultipleObjects]
    public sealed class ChiselFallbackNodeEditor : ChiselNodeEditor<ChiselNode>
    {
        protected override void OnEditSettingsGUI(SceneView sceneView) { }
    }
}
