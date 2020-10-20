/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserLayoutTest.Styles.cs

License:
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

#define CHISEL_MWIN_DEBUG_UI

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindowTest
    {
        private GUIStyle  m_TileButtonStyle;
        private GUIStyle  m_AssetLabelStyle;
        private Texture2D m_TileButtonBGTex;

        private void RebuildStyles()
        {
            //if( m_TileButtonBGTex == null )
            {
                m_TileButtonBGTex = new Texture2D( 32, 32, TextureFormat.RGBA32, false, PlayerSettings.colorSpace == ColorSpace.Linear );

                for( int i = 0; i < 32; i++ )
                {
                    for( int j = 0; j < 32; j++ ) { m_TileButtonBGTex.SetPixel( i, j, new Color32( 50, 80, 65, 255 ) ); }
                }

                m_TileButtonBGTex.Apply();
            }

#if CHISEL_MWIN_DEBUG_UI
            m_TileButtonStyle = new GUIStyle( "button" )
#else
            m_TileButtonStyle ??= new GUIStyle( "button" )
#endif
            {
                    margin        = new RectOffset( 1, 1, 3, 1 ),
                    padding       = new RectOffset( 1, 1, 3, 1 ),
                    normal        = { background = Texture2D.grayTexture },
                    hover         = { background = m_TileButtonBGTex },
                    active        = { background = Texture2D.redTexture }
            };
            m_AssetLabelStyle ??= new GUIStyle( "assetlabel" ) { alignment = TextAnchor.UpperCenter };

            if( mouseOverWindow == this )
                Repaint();
        }
    }
}
