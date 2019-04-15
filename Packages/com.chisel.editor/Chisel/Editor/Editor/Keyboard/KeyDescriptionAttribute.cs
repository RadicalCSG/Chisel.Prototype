using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Editors
{
    public enum KeyOptions
    {
        Hold,
        IgnoreModifiers,
        CreateMenu
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    internal class KeyDescriptionAttribute : Attribute
    {
        public KeyDescriptionAttribute(string name, KeyCode code) { var keys = new KeyCodeWithModifier[1] { new KeyCodeWithModifier(code, EventModifiers.None, KeyOptions.CreateMenu) }; KeyEvent = new KeyEvent(name, keys); }
        public KeyDescriptionAttribute(string name, KeyCode code, KeyOptions options = KeyOptions.CreateMenu) { var keys = new KeyCodeWithModifier[1] { new KeyCodeWithModifier(code, EventModifiers.None, options) }; KeyEvent = new KeyEvent(name, keys); }
        public KeyDescriptionAttribute(string name, KeyCode code, EventModifiers modifier) { var keys = new KeyCodeWithModifier[1] { new KeyCodeWithModifier(code, modifier, KeyOptions.CreateMenu) }; KeyEvent = new KeyEvent(name, keys); }
        public KeyDescriptionAttribute(string name, KeyCode code, EventModifiers modifier, KeyOptions options = KeyOptions.CreateMenu) { var keys = new KeyCodeWithModifier[1] { new KeyCodeWithModifier(code, modifier, options) }; KeyEvent = new KeyEvent(name, keys); }

        public KeyDescriptionAttribute(string name, params KeyCode[] keyCodes)
        {
            var keys = new KeyCodeWithModifier[keyCodes.Length];
            for (int i = 0; i < keyCodes.Length; i++) keys[i] = new KeyCodeWithModifier(keyCodes[i], EventModifiers.None);			
            KeyEvent = new KeyEvent(name, keys);
        }

        public KeyEvent         KeyEvent;
    }
    
    [Serializable]
    public struct KeyCodeWithModifier
    {
        public KeyCodeWithModifier(KeyCode code, EventModifiers modifier = EventModifiers.None, KeyOptions options = KeyOptions.CreateMenu) { KeyCode = code; Modifiers = modifier; Options = options; }
        public KeyCode			KeyCode;
        public EventModifiers	Modifiers;
        internal KeyOptions		Options;
        public bool				Hold				{ get { return (Options & KeyOptions.Hold) != 0; } }
        public bool				CreateMenu			{ get { return (Options & KeyOptions.CreateMenu) != 0; } }
        public bool				IgnoreModifiers		{ get { return (Options & KeyOptions.IgnoreModifiers) != 0; } set { Options = (value) ? Options | KeyOptions.IgnoreModifiers : Options & ~KeyOptions.IgnoreModifiers; } }

        public bool IsKeyPressed(Event evt)
        {
            if (IgnoreModifiers)
                return evt.keyCode == KeyCode;

            return (//EditorGUIUtility.editingTextField && 
                    evt.keyCode == KeyCode && (evt.modifiers & ~EventModifiers.FunctionKey) == Modifiers);
        }
        
        public override string ToString()
        {
            var builder = new StringBuilder();
            if (!IgnoreModifiers)
            { 
                if ((Modifiers & EventModifiers.Command) > 0) { if (builder.Length > 0) builder.Append('+'); builder.Append("Command"); }
                if ((Modifiers & EventModifiers.Control) > 0) { if (builder.Length > 0) builder.Append('+'); builder.Append("Control"); }
                if ((Modifiers & EventModifiers.Alt    ) > 0) { if (builder.Length > 0) builder.Append('+'); builder.Append("Alt"); }
                if ((Modifiers & EventModifiers.Shift  ) > 0) { if (builder.Length > 0) builder.Append('+'); builder.Append("Shift"); }
            }
            if (builder.Length > 0) builder.Append('+');
            builder.Append(KeyEvent.CodeToString(KeyCode));
            if (Hold)
                builder.Append(" (hold)");
            return builder.ToString();
        }
    }
    
    public class KeyEvent
    {
        public KeyEvent(string name, params KeyCodeWithModifier[] keys) { Name = name; Keys = keys; defaultKeys = keys.ToList().ToArray(); }
        public string Name;
        public readonly KeyCodeWithModifier[] defaultKeys;
        public KeyCodeWithModifier[] Keys;

        public bool IsEmpty()
        {
            return Keys == null || Keys.Length == 0;
        }

        public bool IsKeyPressed(Event evt)
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i].IsKeyPressed(evt))
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (Keys.Length == 0)
                return string.Empty;
            if (Keys.Length == 1)
                return Keys[0].ToString();
            var builder = new StringBuilder();
            for (int i = 0; i < Keys.Length; i++)
            {
                if (i > 0)
                    builder.Append(" or ");
                builder.Append(Keys[i].ToString());
            }
            return builder.ToString();
        }

        public static string CodeToString(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Exclaim: return "!";
                case KeyCode.DoubleQuote: return "\"";
                case KeyCode.Hash: return "#";
                case KeyCode.Dollar: return "$";
                case KeyCode.Ampersand: return "&";
                case KeyCode.Quote: return "\'";
                case KeyCode.LeftParen: return "(";
                case KeyCode.RightParen: return ")";
                case KeyCode.Asterisk: return "*";
                case KeyCode.Plus: return "+";
                case KeyCode.Comma: return ",";
                case KeyCode.Minus: return "-";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                case KeyCode.Colon: return ":";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Less: return "<";
                case KeyCode.Equals: return "=";
                case KeyCode.Greater: return ">";
                case KeyCode.Question: return "?";
                case KeyCode.At: return "@";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.Backslash: return "\\";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Caret: return "^";
                case KeyCode.Underscore: return "_";
                case KeyCode.BackQuote: return "`";
                case KeyCode.KeypadPeriod: return ("Keypad '.'");
                case KeyCode.KeypadDivide: return ("Keypad '/'");
                case KeyCode.KeypadMultiply: return ("Keypad '*'");
                case KeyCode.KeypadMinus: return ("Keypad '-'");
                case KeyCode.KeypadPlus: return ("Keypad '+'");
                case KeyCode.KeypadEquals: return ("Keypad '='");
#if UNITY_EDITOR
                default: return UnityEditor.ObjectNames.NicifyVariableName(code.ToString());
#else
                default: return code.ToString();
#endif
            }
        }
    }
}