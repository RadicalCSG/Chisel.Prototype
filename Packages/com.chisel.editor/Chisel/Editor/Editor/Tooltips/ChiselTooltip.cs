/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselTooltip.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public sealed class ChiselTooltip : EditorWindow
    {
        private static ChiselTooltip m_Instance;

        private ChiselTooltipContent m_Content;

        private Color32 IndieColor   = new Color32( 222, 222, 222, 255 );
        private Rect    m_WindowRect = Rect.zero;

        public static ChiselTooltip Instance
        {
            get
            {
                if( m_Instance == null )
                {
                    ChiselTooltip[] tooltips = Resources.FindObjectsOfTypeAll<ChiselTooltip>();
                    if( tooltips.Length > 0 ) return m_Instance = tooltips[0]; //

                    m_Instance           = CreateInstance<ChiselTooltip>();
                    m_Instance.minSize   = Vector2.zero;
                    m_Instance.maxSize   = Vector2.zero;
                    m_Instance.hideFlags = HideFlags.HideAndDontSave;

                    if( showPopupWithMode != null && showModeEnum != null )
                        showPopupWithMode.Invoke( m_Instance, new[] { Enum.ToObject( showModeEnum, 1 ), false } );
                    else m_Instance.ShowPopup();

                    object parent = ChiselReflectionUtility.GetValue( m_Instance, m_Instance.GetType(), "m_Parent" );
                    object pHost  = ChiselReflectionUtility.GetValue( parent,     parent.GetType(),     "window" );
                    ChiselReflectionUtility.SetValue( parent, "mouseRayVisible",    true );
                    ChiselReflectionUtility.SetValue( pHost,  "m_DontSaveToLayout", true );
                }

                return m_Instance;
            }
        }

        private static readonly Type       showModeEnum;
        private static readonly MethodInfo showPopupWithMode;

        static ChiselTooltip()
        {
            showModeEnum      = ChiselReflectionUtility.GetType( "UnityEditor.ShowMode" );
            showPopupWithMode = ChiselReflectionUtility.GetMethod( typeof( EditorWindow ), "ShowPopupWithMode", BindingFlags.NonPublic | BindingFlags.Instance );
        }

        public static void Hide() => m_Instance?.Close();

        public static void Show( Rect pos, ChiselTooltipContent content )
        {
            Instance.ShowInternal( pos, content );
        }

        private void ShowInternal( Rect pos, ChiselTooltipContent content )
        {
            m_Content = content;
            Vector2 size = m_Content.CalcSize();

            Vector2 p = new Vector2( pos.x + pos.width + 4, pos.y );

            if( ( ( p.x % Screen.currentResolution.width ) + size.x ) > Screen.currentResolution.width ) { p.x = pos.x - 4 - size.x; }

            minSize = size;
            maxSize = size;

            m_WindowRect = new Rect( pos.x, pos.y, size.x, size.y );
            position     = m_WindowRect;
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            {
                if( !EditorGUIUtility.isProSkin )
                {
                    GUI.backgroundColor = IndieColor;
                    GUI.Box( m_WindowRect, "" );
                    GUI.backgroundColor = Color.white;
                }

                m_Content?.Draw();
            }
            GUILayout.EndVertical();
        }
    }
}
