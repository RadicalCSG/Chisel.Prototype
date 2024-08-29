using System;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public static class SelectionUtility
    {
        public static SelectionType GetCurrentSelectionType(Event current)
        {
            var selectionType = SelectionType.Replace;
            if (current.shift) { selectionType = SelectionType.Additive; } else
            if (UnityEditor.EditorGUI.actionKey) { selectionType = SelectionType.Subtractive; }
            return selectionType;
        }
    }
}
