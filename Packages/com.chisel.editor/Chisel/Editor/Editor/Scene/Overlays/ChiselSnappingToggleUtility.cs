using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;
using System.Reflection;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    static class ChiselSnappingToggleUtility
    {
        public static SnapSettings CurrentSnapSettings()
        {
            switch (Tools.current)
            {
                case Tool.Move: return SnapSettings.AllGeometry;
                case Tool.Transform: return SnapSettings.AllGeometry;
                case Tool.Rotate: return SnapSettings.None;
                case Tool.Rect: return SnapSettings.None;
                case Tool.Scale: return SnapSettings.None;
                case Tool.Custom: return Snapping.SnapMask;
            }
            return SnapSettings.None;
        }

        public static bool IsSnappingModeEnabled(SnapSettings flag)
        {
            return (CurrentSnapSettings() & flag) == flag;
        }
    }
}
