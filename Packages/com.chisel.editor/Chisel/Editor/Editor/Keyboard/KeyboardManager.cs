using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
	public static class KeyboardManager
	{
		// TODO: create proper delegate for this, with named parameters for clarity
		public static event Action<KeyEventType> KeyboardEventCalled;

		public static void Call(KeyEventType evt)
		{
			if (KeyboardEventCalled == null)
				return;

			// TODO: make this so that there can be more than 1 subscriber, and that only the 'active' subscriber can take the key

			KeyboardEventCalled(evt);
		}
		

		public class KeyDescription
		{
			public KeyEventType Value;
			public KeyEvent		KeyEvent;
		}

		static KeyDescription[]			keyArray	= null; 
		public static KeyDescription[]	KeyDescriptions
		{
			get
			{
				if (keyArray == null)
					keyArray = FindKeys();

				return keyArray;
			}
		}

		static KeyDescription[]		FindKeys()
		{
			var foundKeys	= new List<KeyDescription>();
			var type		= typeof(KeyEventType);
			var fields		= type.GetFields(BindingFlags.Static | BindingFlags.Public);
			foreach (var field in fields)
			{
				var pref = new KeyDescription();
				var attribute = field.GetCustomAttributes(typeof(KeyDescriptionAttribute), false).FirstOrDefault() as KeyDescriptionAttribute;

				if (attribute == null)
					continue;
				
				pref.Value		= (KeyEventType)field.GetValue(null);
				pref.KeyEvent	= attribute.KeyEvent;
				foundKeys.Add(pref);
			}
			return foundKeys.ToArray();
		}

		public static void ResetKeysToDefault()
		{
			var keyDescriptions = KeyDescriptions;
			for (int i = 0; i < keyDescriptions.Length; i++)
			{
				keyDescriptions[i].KeyEvent.Keys = keyDescriptions[i].KeyEvent.defaultKeys.ToList().ToArray();
			}
		}


		public static KeyDescription[] ReadKeysFromStorage()
		{
			var keyDescriptions = KeyDescriptions;
			for (int i = 0; i < keyDescriptions.Length; i++)
			{
				keyDescriptions[i].KeyEvent.Keys = keyDescriptions[i].KeyEvent.defaultKeys.ToList().ToArray();
			}
			 
			for (int i = 0; i < keyDescriptions.Length; i++)
			{
				var key_name	= "KEY:" + keyDescriptions[i].KeyEvent.Name;
				var key_value	= EditorPrefs.GetString(key_name, null);

				if (string.IsNullOrEmpty(key_value))
				{
					keyDescriptions[i].KeyEvent.Keys = keyDescriptions[i].KeyEvent.defaultKeys.ToList().ToArray();
					continue;
				}
				try
				{
					var key_strings = key_value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
					keyDescriptions[i].KeyEvent.Keys = new KeyCodeWithModifier[key_strings.Length];
					for (int j = 0; j < key_strings.Length; j++)
					{
						var key_string	= key_strings[j].Split(':');
						if (key_string.Length == 0)
							continue;
						var keyCode		= (KeyCode) Enum.Parse(typeof(KeyCode), key_string[0]);
						int intModifier;
						var modifiers = EventModifiers.None;
						if (key_string.Length > 0 && Int32.TryParse(key_string[1], out intModifier))
							modifiers	= (EventModifiers)intModifier;
						keyDescriptions[i].KeyEvent.Keys[j].KeyCode = keyCode;
						keyDescriptions[i].KeyEvent.Keys[j].Modifiers = modifiers;
					}
				}
				catch
				{
					keyDescriptions[i].KeyEvent.Keys = keyDescriptions[i].KeyEvent.defaultKeys.ToList().ToArray();
				}
			}
			return keyDescriptions;
		}

		public static void StoreKeys()
		{
			var keyDescriptions = KeyDescriptions;
			for (int i = 0; i < keyDescriptions.Length; i++)
			{
				var key_name	= "KEY:" + keyDescriptions[i].KeyEvent.Name; 
				var key_builder = new StringBuilder();
				var subKeys = keyDescriptions[i].KeyEvent.Keys;
				for (int j = 0; j < subKeys.Length; j++)
				{
					var key = subKeys[j];
					key_builder.Append(key.KeyCode.ToString());
					key_builder.Append(':');
					key_builder.Append((int)key.Modifiers);
					key_builder.Append('|');
				}
				EditorPrefs.SetString(key_name, key_builder.ToString()); 
			}
		}

		// TODO: properly handle pressing down and releasing keys
		public static void HandleKeyboardEvents(Event evt)
		{
			switch (evt.type)
			{
				case EventType.ValidateCommand:
				{
					var keyDescription = FindPressedKey(evt);
					if (keyDescription == null)
						break;
					evt.Use();
					break;
				}
				case EventType.KeyDown:
				{
					var keyDescription = FindPressedKey(evt);
					if (keyDescription == null)
						break;
					evt.Use();
					break;
				}
				case EventType.KeyUp:
				{
					var keyDescription = FindPressedKey(evt);
					if (keyDescription == null)
						break;
					Call(keyDescription.Value);
					evt.Use();
					break;
				}
			}
		}

		internal static KeyDescription FindPressedKey(Event evt)
		{
			// TODO: use hashtables to speed things up
			var keyDescriptions = KeyDescriptions;			
			for (int i = 0; i < keyDescriptions.Length; i++)
			{
				if (keyDescriptions[i].KeyEvent.IsKeyPressed(evt))
					return keyDescriptions[i];
			}
			return null;
		}

		internal static void OnSceneGUI(SceneView sceneView)
		{
			if (EditorGUIUtility.editingTextField)
				return;

			if (GUIUtility.hotControl != 0)
				return;

			var eventType = Event.current;
			HandleKeyboardEvents(eventType);
		}
	}
}
