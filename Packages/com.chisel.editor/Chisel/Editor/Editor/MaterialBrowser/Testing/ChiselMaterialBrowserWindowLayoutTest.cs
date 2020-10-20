/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindowLayoutTest.cs

License:
Author: Daniel Cornelius

$TODO: Port over the browser window to this system.
* * * * * * * * * * * * * * * * * * * * * */

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindowTest : EditorWindow
    {
        public static ChiselMaterialBrowserCache Cache => m_Cache;

        private static ChiselMaterialBrowserCache      m_Cache;
        private        List<ChiselMaterialBrowserTile> m_Tiles  = new List<ChiselMaterialBrowserTile>();
        private        List<string>                    m_Labels = new List<string>();

        private int m_LastSelectedMaterial = 0;
        private int m_CurrentPropsTab      = 1;

        private int     m_TileSize  = 64;
        private Vector2 m_ScrollPos = Vector2.zero;

        private VisualElement Root => rootVisualElement;

        private Toolbar       m_TopToolbar;
        private Toolbar       m_TopToolBar2;
        private Toolbar       m_TabBar;
        private Label         m_SearchFilterLabel;
        private ToolbarSpacer m_TopToolBar2Spacer0;
        private ToolbarButton m_PropertiesLabel;

        private string[] m_PropsTabLabels = new string[]
        {
                "Labels", "Properties"
        };

        [MenuItem( "Window/Chisel/Material Browser Test" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindowTest window = EditorWindow.GetWindow<ChiselMaterialBrowserWindowTest>( false, "Material Browser" );
            window.maxSize = new Vector2( 800, 720 );
            window.minSize = new Vector2( 800, 720 );

            // bugfix for UI layout not being correct on unity start. this is very hack-ish, and i'd really like to find a way to do this correctly.
            CompilationPipeline.RequestScriptCompilation();
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt( "chisel_matbrowser_pviewSize", m_TileSize );
        }

        private void OnEnable()
        {
            //EditorApplication.LockReloadAssemblies(); // ensure window is loaded before unity tries to reload anything

            m_TileSize = EditorPrefs.GetInt( "chisel_matbrowser_pviewSize", 64 );

            // load cached thumbs
            m_Cache ??= ChiselMaterialBrowserCache.Load();
            Debug.Log( $"Thumbnail cache: [{Cache.Name}], Number of entries: [{Cache.NumEntries}]" );

            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels, ref m_Cache, false );

            Root.Clear();
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
                m_Tiles.Clear();
                ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels, ref m_Cache, false );
            } );

            // label bar
            m_TopToolBar2        = Root.AddToolbar( "toolBar" );
            m_SearchFilterLabel = m_TopToolBar2.AddLabel( "", "statusBarLabel" );
            m_SearchFilterLabel.SetSize( 200, 24 );
            m_TopToolBar2Spacer0 = m_TopToolBar2.AddSpacer( "topToolBar2Spacer0" );
            m_TopToolBar2Spacer0.SetSize( position.width - 463, 24 );

            // properties area label on label bar
            m_PropertiesLabel = (ToolbarButton) m_TopToolBar2.AddButton( m_PropsTabLabels[m_CurrentPropsTab], "topToolBar2PropertiesLabel", () => {}, false );
            m_PropertiesLabel.SetSize( 264, 24 );

            // vertical tab bar
            m_TabBar = Root.AddToolbar( "tabBar" );
            m_TabBar.SetPosition( position.width, 0 );
            m_TabBar.SetRotation( 90 );

            m_TabBar.AddButton( "Labels", "tabBarButton", () => { m_CurrentPropsTab = 0; } ).SetSize( 100, 24 );

            m_TabBar.AddButton( "Properties", "tabBarButton", () => { m_CurrentPropsTab = 1; } ).SetSize( 100, 24 );

            //EditorApplication.UnlockReloadAssemblies(); // allow unity to reload assemblies
        }

        private void Update()
        {
            m_PropertiesLabel.SetText( m_PropsTabLabels[m_CurrentPropsTab] );
        }

        private Material m_PreviewMaterial;
        private Editor   m_PreviewEditor;

        private void OnGUI()
        {
            int lastSelected = m_LastSelectedMaterial;
            // rebuild IMGUI styling
            RebuildStyles();

            if( EditorApplication.isPlaying && !EditorApplication.isPaused )
                Root.visible  = false;
            else Root.visible = true;

            if( EditorApplication.isCompiling )
            {
                Root.visible = false;
                GUI.Box( new Rect( ( position.width * 0.5f ) - 200, ( position.height * 0.5f ) - 60, 400, 120 ), "Please wait...", "NotificationBackground" );
            }
            else Root.visible = true;

            if( !EditorApplication.isCompiling && !EditorApplication.isPlaying )
            {
                GUI.Box( new Rect( position.width - 264, 48, 240, position.height - 48 ), "" );

                switch( m_CurrentPropsTab )
                {
                    case 0:
                    {
                        Rect labelButtonRect = new Rect( position.width - 260, 50, 224, 24 );

                        for( int i = 0; i < m_Labels.Count; i++ )
                        {
                            if( i > 0 )
                                labelButtonRect.y += 26;
                            if( GUI.Button( labelButtonRect, m_Labels[i] ) ) {}
                        }

                        break;
                    }
                    case 1:
                    {
                        Rect propsAreaRect = new Rect( position.width - 264, 50, 240, position.height - 50 );

                        GUI.BeginGroup( propsAreaRect, (GUIStyle) "gameviewbackground" );
                        {
                            Rect contentPos = new Rect( 2, 2, 234, 234 );

                            if( m_Tiles[m_LastSelectedMaterial] != null )
                            {
                                if( m_PreviewEditor == null ) m_PreviewEditor = Editor.CreateEditor( m_PreviewMaterial, typeof( MaterialEditor ) );
                                m_PreviewEditor.OnInteractivePreviewGUI( contentPos, EditorStyles.whiteLabel );
                                m_PreviewEditor.Repaint();
                            }
                            //GUI.DrawTexture( contentPos, m_Tiles[m_LastSelectedMaterial].Preview );

                            propsAreaRect.y      = 236;
                            propsAreaRect.x      = 1;
                            propsAreaRect.height = 90;
                            propsAreaRect.width  = 237;
                            GUI.BeginGroup( propsAreaRect, (GUIStyle) "helpbox" );
                            {
                                propsAreaRect.x      += 1;
                                propsAreaRect.y      =  0;
                                propsAreaRect.height =  22;
                                GUI.Label( propsAreaRect, $"{m_Tiles[m_LastSelectedMaterial].materialName}", EditorStyles.toolbarButton );
                                propsAreaRect.y += 22;
                                GUI.Label( propsAreaRect, $"Size:\t{m_Tiles[m_LastSelectedMaterial].mainTexSize:###0}", EditorStyles.miniLabel );
                                propsAreaRect.y += 22;
                                GUI.Label( propsAreaRect, $"Offset:\t{m_Tiles[m_LastSelectedMaterial].uvOffset:####0}", EditorStyles.miniLabel );
                                propsAreaRect.y += 22;
                                GUI.Label( propsAreaRect, $"Scale:\t{m_Tiles[m_LastSelectedMaterial].uvScale:####0}", EditorStyles.miniLabel );
                            }
                            GUI.EndGroup();

                            propsAreaRect.y      += 262;
                            propsAreaRect.height =  140;
                            GUI.BeginGroup( propsAreaRect, (GUIStyle) "helpbox" );
                            {
                                propsAreaRect.y      = 0;
                                propsAreaRect.x      = 0;
                                propsAreaRect.height = 22;
                                GUI.Label( propsAreaRect, $"Material", EditorStyles.toolbarButton );
                                propsAreaRect.x += 1;
                                propsAreaRect.y += 22;
                                GUI.Label( propsAreaRect, $"Shader:\t{m_Tiles[m_LastSelectedMaterial].shaderName}", EditorStyles.miniLabel );
                                propsAreaRect.y += 22;
                                GUI.Label( propsAreaRect, $"Albedo:\t{m_Tiles[m_LastSelectedMaterial].albedoName}", EditorStyles.miniLabel );

                                propsAreaRect.x     += 1;
                                propsAreaRect.y     =  88;
                                propsAreaRect.width -= 4;
                                if( GUI.Button( propsAreaRect, "Select In Project", "largebutton" ) )
                                {
                                    Material m = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_Tiles[m_LastSelectedMaterial].guid ) );
                                    Selection.activeObject = m;
                                    EditorGUIUtility.PingObject( m );
                                }

                                propsAreaRect.y += 24;
                                if( GUI.Button( propsAreaRect, "Apply to Selected Face", "largebutton" ) ) {}
                            }
                            GUI.EndGroup();

                            propsAreaRect.y      = 469;
                            propsAreaRect.height = 186;
                            GUI.BeginGroup( propsAreaRect, (GUIStyle) "helpbox" );
                            {
                                propsAreaRect.y      = 0;
                                propsAreaRect.height = 22;
                                GUI.Label( propsAreaRect, "Labels", EditorStyles.toolbarButton );

                                float btnWidth = 60;
                                int   idx      = 0;
                                foreach( var l in m_Tiles[m_LastSelectedMaterial].labels )
                                {
                                    float xOffset = 2;
                                    float yOffset = 22;
                                    int   row     = 0;
                                    for( int i = 0; i < ( propsAreaRect.width / btnWidth ); i++ )
                                    {
                                        if( idx == m_Tiles[m_LastSelectedMaterial].labels.Length ) break;

                                        xOffset = ( 40 * i ) + 2;
                                        if( GUI.Button( new Rect( xOffset, yOffset, btnWidth, 22 ), new GUIContent( l, $"Click to filter for label \"{l}\"" ), m_AssetLabelStyle ) )
                                        {
                                            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels, ref m_Cache, true, l, "" );
                                            m_SearchFilterLabel.SetText( $"Search Filter: {l}" );
                                        }

                                        idx++;
                                    }

                                    row     += 1;
                                    yOffset =  22 + ( 22 * row );
                                }
                            }
                            GUI.EndGroup();
                        }
                        GUI.EndGroup();

                        break;
                    }
                    default: break;
                }

                DrawLabelAndTileArea();
            }
        }

        // legacy code
        private float scrollViewHeight;

        private void DrawLabelAndTileArea()
        {
            Rect rect = position;
            //GUI.Box( new Rect( 0, 48, rect.width - 24, rect.height - ( 48 * 2 ) ), "", "GameViewBackground" );

            int idx        = 0;
            int numColumns = (int) ( ( rect.width - ( 264 + 10 ) ) / m_TileSize );

            m_ScrollPos = GUI.BeginScrollView( new Rect( 0, 52, rect.width - 262, rect.height - 72 ), m_ScrollPos,
                                               new Rect( 0, 0,  rect.width - 282, scrollViewHeight ) );
            {
                float xOffset = 0;
                float yOffset = 0;
                int   row     = 0;

                foreach( var entry in m_Tiles )
                {
                    if( idx == m_Tiles.Count ) break;

                    // begin horizontal
                    for( int x = 0; x < numColumns; x++ )
                    {
                        xOffset = ( m_TileSize * x ) + 4;

                        if( idx == m_Tiles.Count ) break;

                        if( GUI.Button( new Rect( xOffset, yOffset, m_TileSize - 2, m_TileSize - 4 ),
                                        new GUIContent( m_Tiles[idx].Preview, $"Name: {m_Tiles[idx].materialName}\nShader: {m_Tiles[idx].shaderName}" ),
                                        m_TileButtonStyle ) ) // make a custom guistyle for these, or make a method to do a proper tile
                        {
                            m_PreviewMaterial = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_Tiles[idx].guid ) );
                            m_PreviewEditor   = null;

                            m_LastSelectedMaterial = idx;
                        }

                        idx++;
                    }

                    row              += 1;
                    yOffset          =  ( m_TileSize * row );
                    scrollViewHeight =  yOffset;

                    // end horizontal
                }
            }
            GUI.EndScrollView();

            GUI.Box( new Rect( 0,   rect.height - 20, rect.width, 20 ), "", "toolbar" );
            GUI.Label( new Rect( 0, rect.height - 20, 400,        20 ), $"Materials: {m_Tiles.Count} | Labels: {m_Labels.Count}" );
            m_TileSize = (int) GUI.HorizontalSlider( new Rect( rect.width - 360, rect.height - 20, 80, 24 ), m_TileSize, 64, 128 );
        }
    }
}
