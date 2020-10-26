/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindow.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

#define CHISEL_MWIN_DEBUG_UI

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindow
    {
        private GUIStyle m_TileButtonStyle;
        private GUIStyle m_AssetLabelStyle;
        private GUIStyle m_ToolbarStyle;
        private GUIStyle m_PropsSectionBG;

        private Texture2D m_TileButtonBGTexHover;
        private Texture2D m_TileButtonBGTex;

        private void RebuildStyles()
        {
#if !CHISEL_MWIN_DEBUG_UI // update every frame while debugging, helps when changing color without the need to re-init the window
            if( m_TileButtonBGTexHover == null )
#endif
            {
                m_TileButtonBGTexHover = new Texture2D( 32, 32, TextureFormat.RGBA32, false, PlayerSettings.colorSpace == ColorSpace.Linear );

                for( int i = 0; i < 32; i++ )
                {
                    for( int j = 0; j < 32; j++ ) { m_TileButtonBGTexHover.SetPixel( i, j, new Color32( 50, 80, 65, 255 ) ); }
                }

                m_TileButtonBGTexHover.Apply();
            }

#if !CHISEL_MWIN_DEBUG_UI
            if( m_TileButtonBGTex == null )
#endif
            {
                m_TileButtonBGTex = new Texture2D( 32, 32, TextureFormat.RGBA32, false, PlayerSettings.colorSpace == ColorSpace.Linear );

                for( int i = 0; i < 32; i++ )
                {
                    for( int j = 0; j < 32; j++ ) { m_TileButtonBGTex.SetPixel( i, j, new Color32( 32, 32, 32, 255 ) ); }
                }

                m_TileButtonBGTex.Apply();
            }

#if CHISEL_MWIN_DEBUG_UI // update every frame while debugging, helps when changing color without the need to re-init the window
            m_TileButtonStyle = new GUIStyle()
#else
            m_TileButtonStyle ??= new GUIStyle()
#endif
            {
                    margin  = new RectOffset( 2, 2, 2, 2 ),
                    padding = new RectOffset( 2, 2, 2, 2 ),
                    contentOffset = new Vector2( 1, 0 ),
                    //border  = new RectOffset( 1, 0, 1, 1 ),
                    normal = { background = m_TileButtonBGTex },
                    hover  = { background = m_TileButtonBGTexHover },
                    active = { background = Texture2D.redTexture },
                    //onNormal = { background = Texture2D.grayTexture }
            };

            m_AssetLabelStyle ??= new GUIStyle( "assetlabel" ) { alignment = TextAnchor.UpperCenter };
            m_ToolbarStyle ??= new GUIStyle( "dragtab" )
            {
                    fixedHeight = 0, fixedWidth = 0
            };
            m_PropsSectionBG ??= new GUIStyle( "flow background" );

            if( mouseOverWindow == this )
                Repaint();
        }

        private void SetButtonStyleBG( in int index )
        {
            if( m_TileButtonStyle != null )
            {
                if( index == m_LastSelectedMaterialIndex ) { m_TileButtonStyle.normal.background = m_TileButtonBGTexHover; }
                else
                {
                    if( m_TileButtonStyle.normal.background != m_TileButtonBGTex ) m_TileButtonStyle.normal.background = m_TileButtonBGTex;
                }
            }
        }
    }
}
