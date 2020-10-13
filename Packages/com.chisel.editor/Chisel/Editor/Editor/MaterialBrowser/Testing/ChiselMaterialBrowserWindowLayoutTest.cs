/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindowLayoutTest.cs

License:
Author: Daniel Cornelius

$TODO: Port over the browser window to this system.
* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindowTest : EditorWindow
    {
        private const int NUM_TILES  = 8 * 200;
        private const int THUMB_SIZE = 64;

        private int m_TileSize = 64;

        private Vector2 m_ScrollPos = Vector2.zero;

        private static List<Texture2D> tiles = new List<Texture2D>();

        //private static bool isRenderingNoise = false;

        private VisualElement Root => rootVisualElement;

        private float m_ContentSize;

        private Toolbar m_TopToolbar;
        private Toolbar m_TopToolBar2;
        private Toolbar m_TabBar;
        private Toolbar m_StatusBar;

        private ToolbarSpacer m_TopToolBar2Spacer0;

        private ToolbarButton m_PropertiesLabel;

        private Box        m_PropertiesGroup;
        private Box        m_StatusLabelBox;
        private ScrollView m_TileView;
        private Label      m_TileSizeLabel;

        [MenuItem( "Window/Chisel/Material Browser Test" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindowTest window = EditorWindow.GetWindow<ChiselMaterialBrowserWindowTest>( false, "Material Browser" );
            window.maxSize = new Vector2( 800, 720 );
            window.minSize = new Vector2( 800, 720 );

            /* // legacy code
            for( int i = 0; i < NUM_TILES; i++ )
            {
                if( !isRenderingNoise )
                    tiles.Add( GetRandNoiseTex() );
            }*/
        }

        private VisualElement ConstructTilePreview( string baseElementName, int num )
        {
            VisualElement tileBase = new VisualElement();

            // button for tile
            Button bg = tileBase.AddButton( $"", $"{baseElementName}BG", () => { Debug.Log( $"Clicked material button 'Label [{num}]'" ); } );
            bg.SetSize( m_TileSize, m_TileSize );
            // tile thumbnail
            bg.AddImage( GetRandNoiseTex(), $"{baseElementName}Image" );
            // label bg
            Box box = bg.AddBox( $"{baseElementName}LabelBG" );
            box.SetPosition( 0, m_TileSize - 19 );
            box.SetSize( m_TileSize, 19 );
            box.style.backgroundColor = new StyleColor( new Color( 0, 0, 0, 0.3f ) );
            // label
            box.AddLabel( $"Label [{num}]", $"{baseElementName}Label" );

            return tileBase;
        }

        private void OnEnable()
        {
            Root.Clear();
            /*====================================================
             * Construct data
             */
            List<VisualElement> elements = new List<VisualElement>();

            void ConstructElements()
            {
                //Stopwatch watch = Stopwatch.StartNew();
                elements.Clear();
                m_ContentSize = 0;
                for( int i = 0; i < NUM_TILES; i++ )
                {
                    elements.Add( ConstructTilePreview( "tileImage", i + 1 ) );

                    if( i % (int) Mathf.Clamp( ( position.width - 270 ) / m_TileSize, 2, 8 ) == 0 ) { m_ContentSize += m_TileSize + 4; }
                }

                if( m_TileView != null )
                {
                    foreach( var e in elements ) { m_TileView.Add( e ); }

                    m_TileView.MarkDirtyRepaint();
                }

                //watch.Stop();
                //Debug.Log( $"Constructed {elements.Count} elements in {watch.ElapsedMilliseconds:###00}ms" );
            }

            ConstructElements();

            EditorApplication.update -= Update;
            EditorApplication.update += Update;


            /*====================================================
             * Begin UI layout
             */
            // load and assign the root VisualElement
            this.GetRootElement( "MaterialBrowserWindow", "MaterialBrowser" );

            // top toolbar
            m_TopToolbar = Root.AddToolbar( "toolBar" );
            m_TopToolbar.AddButton( "Refresh", "toolBarRefreshButton", () =>
            {
                if( m_TileView != null )
                {
                    m_TileView.Clear();
                    ConstructElements();
                    Root.MarkDirtyRepaint();
                }

                //Debug.Log( "Clicked refresh button" );
            } );

            // label bar
            m_TopToolBar2        = Root.AddToolbar( "toolBar2" );
            m_TopToolBar2Spacer0 = m_TopToolBar2.AddSpacer( "topToolBar2Spacer0" );
            m_TopToolBar2Spacer0.SetSize( position.width - 263, 24 );

            // properties area label on label bar
            m_PropertiesLabel = (ToolbarButton) m_TopToolBar2.AddButton( "Properties", "topToolBar2PropertiesLabel", () => {}, false );
            m_PropertiesLabel.SetSize( 264, 24 );

            // vertical tab bar
            m_TabBar = Root.AddToolbar( "tabBar" );
            m_TabBar.SetPosition( position.width, 0 );
            m_TabBar.SetRotation( 90 );

            // properties area controlled by tab bar
            m_PropertiesGroup = Root.AddBox( "propertiesBox" );
            m_PropertiesGroup.SetPosition( position.width   - 264, 48 );
            m_PropertiesGroup.SetSize( 240, position.height - 48 );

            // content scroll view, has all the material previews.
            m_TileView = Root.AddScrollView( in elements, ref m_ScrollPos, in m_TileSize, false, "tileView" );
            m_TileView.SetPosition( 0, 48 );
            m_TileView.SetSize( position.width - 252, position.height - 70 );

            // bottom status bar
            m_StatusBar = Root.AddToolbar( "statusBar" );
            m_StatusBar.AddLabel( "status text", "statusBarLabel" );

            // bottom status bar, preview tile size DOWN
            Button downBTN = m_StatusBar.AddButton( "<", "previewSizeButtonLeft", () =>
            {
                m_TileSize = Mathf.Clamp( ChiselMaterialBrowserUtilities.GetPow2( m_TileSize / 2 ), 64, 256 );

                m_ContentSize = 0;

                for( int i = 0; i < elements.Count; i++ )
                {
                    if( i % (int) Mathf.Clamp( ( position.width - 270 ) / m_TileSize, 2, 8 ) == 0 ) { m_ContentSize += m_TileSize + 4; }
                }

                foreach( var ve in m_TileView.contentContainer.Children() )
                {
                    // button + preview texture
                    ve.SetSize( m_TileSize, m_TileSize );
                    ve[0].SetSize( m_TileSize, m_TileSize );

                    // label BG
                    ve[0][1].SetSize( m_TileSize, 19 );
                    ve[0][1].SetPosition( 0, m_TileSize - 19 );
                }

                m_TileSizeLabel.text = $"{m_TileSize}";
            } );
            downBTN.SetPosition( position.width - 364, 0 );
            downBTN.SetSize( 20, 24 );

            // bottom status bar, preview tile size label
            m_StatusLabelBox = m_StatusBar.AddBox( "tileSizeLabelBG" );
            m_StatusLabelBox.SetPosition( position.width - 346, 0 );
            m_StatusLabelBox.SetSize( 60, 22 );
            m_TileSizeLabel = m_StatusLabelBox.AddLabel( $"{m_TileSize}", "tileSizeLabel" );

            // bottom status bar, preview tile size UP
            Button upBTN = m_StatusBar.AddButton( ">", "previewSizeButtonRight", () =>
            {
                m_TileSize = Mathf.Clamp( ChiselMaterialBrowserUtilities.GetPow2( m_TileSize * 2 ), 64, 256 );

                m_ContentSize = 0;

                for( int i = 0; i < elements.Count; i++ )
                {
                    if( i % (int) Mathf.Clamp( ( position.width - 270 ) / m_TileSize, 2, 8 ) == 0 ) { m_ContentSize += m_TileSize + 4; }
                }

                foreach( var ve in m_TileView.contentContainer.Children() )
                {
                    // button + preview texture
                    ve.SetSize( m_TileSize, m_TileSize );
                    ve[0].SetSize( m_TileSize, m_TileSize );

                    // label BG
                    ve[0][1].SetSize( m_TileSize, 19 );
                    ve[0][1].SetPosition( 0, m_TileSize - 19 );
                }

                m_TileSizeLabel.text = $"{m_TileSize}";
            } );
            upBTN.SetPosition( position.width - 284, 0 );
            upBTN.SetSize( 20, 24 );


            // handle setting tab labels and functionality
            for( int i = 0; i < 4; i++ )
            {
                int    num   = i;
                string label = $"Button {i}";

                // set tab labels
                switch( num )
                {
                    case 0:
                    {
                        label = "Labels";
                        break;
                    }
                    case 1:
                    {
                        label = "Properties";
                        break;
                    }
                    case 2:  { break; }
                    case 3:  { break; }
                    default: throw new IndexOutOfRangeException( $"Tab {num} is out of maximum range of 4, add a new tab before trying to access it" );
                }

                // for each button in the vertical tab well, figure out what it is, and set its action accordingly
                Button b = m_TabBar.AddButton( label, $"tabBarButton{num}", () =>
                {
                    switch( num )
                    {
                        case 0:
                        {
                            m_PropertiesLabel.SetText( "Labels" );
                            break;
                        }
                        case 1:
                        {
                            m_PropertiesLabel.SetText( "Properties" );
                            break;
                        }
                        case 2:  { break; }
                        case 3:  { break; }
                        default: throw new IndexOutOfRangeException( $"Tab {num} is out of maximum range of 4, add a new tab before trying to access it" );
                    }
                } );
                b.SetSize( 100, 24 );
            }

            /* // legacy code
            if( tiles.Count < 1 )
            {
                for( int i = 0; i < NUM_TILES; i++ )
                {
                    if( !isRenderingNoise )
                        tiles.Add( GetRandNoiseTex() );
                }
            }*/
        }

        private void Update()
        {
            m_TileView.contentContainer.SetSize( position.width - 270, m_ContentSize );
        }

        // $TODO: REMOVE ME, Replace with thumbnailing system.
        // generates a random perlin noise texture, used for preview purposes and to create load
        private static Texture2D GetRandNoiseTex()
        {
            //isRenderingNoise = true;
            Texture2D noiseTex = new Texture2D( THUMB_SIZE, THUMB_SIZE, TextureFormat.RGB24, false, PlayerSettings.colorSpace == ColorSpace.Linear );
            Color[]   pix      = new Color[THUMB_SIZE * THUMB_SIZE];

            for( int y = 0; y < THUMB_SIZE; y++ )
            {
                for( int x = 0; x < THUMB_SIZE; x++ )
                {
                    float xCoord = 0 + x / THUMB_SIZE;
                    float yCoord = 0 + y / THUMB_SIZE;
                    float sample = Mathf.Clamp01( 1 - Mathf.PerlinNoise( xCoord, yCoord ) * Random.value ) * 0.5f;
                    pix[y * THUMB_SIZE + x] = new Color( sample, sample, sample );
                }
            }

            noiseTex.SetPixels( pix );
            noiseTex.Apply();

            //isRenderingNoise = false;
            return noiseTex;
        }

        /* // legacy code
        private float scrollViewHeight;
        private float xOffset = 2;

        private void DrawLabelAndTileArea( Rect rect )
        {
            GUI.Box( new Rect( 0, TOOLBAR_HEIGHT, rect.width - TOOLBAR_WIDTH, rect.height - ( TOOLBAR_HEIGHT * 2 ) ), "", "GameViewBackground" );

            int idx        = 0;
            int numColumns = (int) ( ( rect.width - ( TOOLBAR_WIDTH + 10 ) ) / THUMB_SIZE );

            m_ScrollPos = GUI.BeginScrollView( new Rect( 0, 28, rect.width - ( TOOLBAR_WIDTH + 2 ),  rect.height - 52 ), m_ScrollPos,
                                               new Rect( 0, 28, rect.width - ( TOOLBAR_WIDTH + 16 ), scrollViewHeight ) );
            {
                float yOffset = 22;
                int   row     = 0;

                foreach( var entry in tiles )
                {
                    if( idx == tiles.Count ) break;

                    // begin horizontal
                    for( int x = 0; x < numColumns; x++ )
                    {
                        if( x > 0 )
                            xOffset = ( THUMB_SIZE + 3 ) * x;

                        if( idx == tiles.Count ) break;

                        GUI.Box( new Rect( xOffset, yOffset, THUMB_SIZE, THUMB_SIZE ), tiles[idx] ); // make a custom guistyle for these, or make a method to do a proper tile

                        idx++;
                    }

                    row     += 1;
                    xOffset =  2;
                    if( row > 0 )
                    {
                        yOffset = TOOLBAR_HEIGHT + ( ( THUMB_SIZE + 3 ) * row );

                        scrollViewHeight = yOffset;
                    }

                    // end horizontal
                }
            }
            GUI.EndScrollView();
        }*/
    }
}
