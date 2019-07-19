using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    public sealed class ChiselRevolvedShapeGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselRevolvedShape.kNodeTypeName;
        public override string ToolName => kToolName;

        // Commented out, for the time being, because this generator has not been implemented yet
        /*
        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.RevolvedShapeBuilderModeKey, ChiselKeyboardDefaults.RevolvedShapeBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselRevolvedShapeGeneratorMode); }
        #endregion
        */
    }
}
