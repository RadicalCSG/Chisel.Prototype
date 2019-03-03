using System;
using UnityEngine;

namespace UnitySceneExtensions
{
    [Serializable]
	public enum SelectionType { Normal, Additive, Subtractive };

	public static class SelectionUtility
	{
        public static SelectionType GetCurrentSelectionType(Event current)
        {
            var selectionType = SelectionType.Normal;
            if (current.shift) { selectionType = SelectionType.Additive; } else
            if (UnityEditor.EditorGUI.actionKey) { selectionType = SelectionType.Subtractive; }
            return selectionType;
        }
	}
}
