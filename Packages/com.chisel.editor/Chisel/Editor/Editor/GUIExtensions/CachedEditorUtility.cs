using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;

namespace Chisel.Editors
{
	public static class CachedEditorUtility
	{
		public static void ShowEditor<T>(UnityEngine.Object target, ref T cachedEditor) where T : Editor
		{
			if (Equals(null,target))
				return;
			//var cachedEditor = Editor.CreateEditor(target);
			var editor = (Editor)cachedEditor;
			Editor.CreateCachedEditor(target, typeof(T), ref editor);
			cachedEditor = editor as T;
			if (!cachedEditor || !cachedEditor.target)
				return;
			var prevGUIChanged = GUI.changed;
			GUI.changed = false;
			var so = cachedEditor.serializedObject;
			so.Update();
			try { cachedEditor.OnInspectorGUI(); } catch (Exception ex) { Debug.LogException(ex); }
			if (GUI.changed)
			{
				so.ApplyModifiedProperties();
				prevGUIChanged = true;
			}
			GUI.changed = prevGUIChanged;
		}

		public static void ShowEditor<T>(UnityEngine.Object[] targets, ref T cachedEditor) where T : Editor
		{
			if (targets == null ||
				targets.Length == 0)
				return;
			//var cachedEditor = Editor.CreateEditor(target);
			var editor = (Editor)cachedEditor;
			Editor.CreateCachedEditor(targets, typeof(T), ref editor);
			cachedEditor = editor as T;
			if (!cachedEditor || !cachedEditor.target)
				return;
			var prevGUIChanged = GUI.changed;
			GUI.changed = false;
			var so = cachedEditor.serializedObject;
			so.Update();
			try
			{ cachedEditor.OnInspectorGUI(); }
			catch (Exception ex) { Debug.LogException(ex); }
			if (GUI.changed)
			{
				so.ApplyModifiedProperties();
				prevGUIChanged = true;
			}
			GUI.changed = prevGUIChanged;
		}

		public static Type FindEditorTypeForObject(UnityEngine.Object target)
		{
			if (Equals(null,target))
				return null;
			var editor = Editor.CreateEditor(target);
			if (!editor)
				return null;
			return editor.GetType();
		}
	}
}
