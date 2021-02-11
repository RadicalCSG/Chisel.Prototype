/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindow.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindow
    {
        public GUIStyle tileButtonStyle;
        public GUIStyle assetLabelStyle;
        public GUIStyle toolbarStyle;
        public GUIStyle propsSectionBG;
        public GUIStyle tileLabelStyle;
        public GUIStyle tileLabelBGStyle;

        private Texture2D m_TileButtonBGTexHover;
        private Texture2D m_TileButtonBGTex;

        private GUIContent applyToSelectedFaceLabelContent;

        private bool IsDarkSkin => EditorGUIUtility.isProSkin;

        private string ApplyLastMaterialShortcut => ShortcutManager.instance.GetShortcutBinding( "Chisel/Material Browser/Apply Last Selected Material" ).ToString();

        private void ResetStyles()
        {
            tileButtonStyle                 = null;
            assetLabelStyle                 = null;
            toolbarStyle                    = null;
            propsSectionBG                  = null;
            tileLabelStyle                  = null;
            tileLabelBGStyle                = null;
            m_TileButtonBGTexHover          = null;
            m_TileButtonBGTex               = null;
            applyToSelectedFaceLabelContent = null;

            RebuildStyles();
        }

        private void RebuildStyles()
        {
            // highlight colors for dark/light themes
            m_TileButtonBGTexHover ??= ChiselEmbeddedTextures.GetColoredTexture( IsDarkSkin ? new Color32( 90, 130, 115, 255 ) : new Color32( 20,  140, 180, 255 ) );
            m_TileButtonBGTex      ??= ChiselEmbeddedTextures.GetColoredTexture( IsDarkSkin ? new Color32( 32, 32,  32,  255 ) : new Color32( 180, 180, 180, 255 ) );

            applyToSelectedFaceLabelContent ??= new GUIContent
            (
                    "Apply to Selected Face",
                    $"Apply the currently selected material to the face selected in the scene view. Shortcut: {ApplyLastMaterialShortcut}"
            );

            assetLabelStyle ??= new GUIStyle( "assetlabel" ) { alignment = TextAnchor.UpperCenter };
            toolbarStyle    ??= new GUIStyle( "dragtab" ) { fixedHeight  = 0, fixedWidth = 0 };
            propsSectionBG  ??= new GUIStyle( "flow background" );

            // background styling for tiles, adjusted for dark/light themes
            tileLabelBGStyle ??= new GUIStyle( "box" )
            {
                    normal =
                    {
                            background = IsDarkSkin ? ChiselEmbeddedTextures.DarkBGTex : ChiselEmbeddedTextures.GetColoredTexture( new Color32( 100, 100, 100, 220 ) ),
                            scaledBackgrounds = new[]
                            {
                                    IsDarkSkin ? ChiselEmbeddedTextures.DarkBGTex : ChiselEmbeddedTextures.GetColoredTexture( new Color32( 180, 180, 180, 200 ) )
                            }
                    }
            };

            tileLabelStyle ??= new GUIStyle()
            {
                    fontSize  = 10,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft,
                    normal    = { textColor = ( IsDarkSkin ? new Color32( 200, 200, 200, 255 ) : new Color32( 0, 0, 0, 255 ) ) },
                    clipping  = TextClipping.Clip
            };

            tileButtonStyle ??= new GUIStyle()
            {
                    margin        = new RectOffset( 2, 2, 2, 2 ),
                    padding       = new RectOffset( 2, 2, 2, 2 ),
                    contentOffset = new Vector2( 1, 0 ),
                    normal        = { background = m_TileButtonBGTex },
                    hover         = { background = m_TileButtonBGTexHover },
                    active        = { background = Texture2D.redTexture },
                    imagePosition = ImagePosition.ImageOnly
            };

            // refresh UI when mouse is over window. this ensures that hover hilighting is updated properly, and the interactive preview is drawn correctly.
            if( mouseOverWindow == this )
                Repaint();
        }

        // used to switch between hover/unhovered backgrounds
        private void SetButtonStyleBG( in int index )
        {
            if( tileButtonStyle != null )
            {
                if( index == lastSelectedMaterialIndex ) { tileButtonStyle.normal.background = m_TileButtonBGTexHover; }
                else
                {
                    if( tileButtonStyle.normal.background != m_TileButtonBGTex ) tileButtonStyle.normal.background = m_TileButtonBGTex;
                }
            }
        }
    }
}
