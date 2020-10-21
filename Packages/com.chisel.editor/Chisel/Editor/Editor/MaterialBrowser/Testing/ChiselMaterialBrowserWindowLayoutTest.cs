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

        private int m_LastSelectedMaterialIndex = 0;
        private int m_CurrentPropsTab           = 1;

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
            m_TopToolBar2       = Root.AddToolbar( "toolBar" );
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
        private Rect     m_PropsAreaRect = Rect.zero;
        private Rect     m_NotifRect     = Rect.zero;

        private void OnGUI()
        {
            int lastSelected = m_LastSelectedMaterialIndex;

            m_NotifRect.x      = ( position.width  * 0.5f ) - 200;
            m_NotifRect.y      = ( position.height * 0.5f ) - 60;
            m_NotifRect.width  = 400;
            m_NotifRect.height = 120;

            // rebuild IMGUI styling
            RebuildStyles();

            if( EditorApplication.isPlaying && !EditorApplication.isPaused )
                Root.visible  = false;
            else Root.visible = true;

            if( EditorApplication.isCompiling )
            {
                Root.visible = false;
                GUI.Box( m_NotifRect, "Please wait...", "NotificationBackground" );
            }
            else Root.visible = true;

            if( !EditorApplication.isCompiling && !EditorApplication.isPlaying )
            {
                //GUI.Box( new Rect( position.width - 264, 48, 240, position.height - 48 ), "" );

                switch( m_CurrentPropsTab )
                {
                    case 0:
                    {
                        m_PropsAreaRect.x      = position.width - 260;
                        m_PropsAreaRect.y      = 50;
                        m_PropsAreaRect.width  = 224;
                        m_PropsAreaRect.height = 24;

                        for( int i = 0; i < m_Labels.Count; i++ )
                        {
                            if( i > 0 )
                                m_PropsAreaRect.y += 26;
                            if( GUI.Button( m_PropsAreaRect, m_Labels[i] ) ) {}
                        }

                        break;
                    }
                    case 1:
                    {
                        m_PropsAreaRect.x      = position.width - 264;
                        m_PropsAreaRect.y      = 50;
                        m_PropsAreaRect.width  = 240;
                        m_PropsAreaRect.height = position.height - 50;

                        GUI.BeginGroup( m_PropsAreaRect, (GUIStyle) "gameviewbackground" );
                        {
                            m_PropsAreaRect.x      = 2;
                            m_PropsAreaRect.y      = 2;
                            m_PropsAreaRect.width  = 234;
                            m_PropsAreaRect.height = 234;

                            if( m_Tiles[m_LastSelectedMaterialIndex] != null && m_PreviewMaterial != null )
                            {
                                if( m_PreviewEditor == null ) m_PreviewEditor = Editor.CreateEditor( m_PreviewMaterial, typeof( MaterialEditor ) );
                                m_PreviewEditor.OnInteractivePreviewGUI( m_PropsAreaRect, EditorStyles.whiteLabel );
                                m_PreviewEditor.Repaint();
                            }
                            //GUI.DrawTexture( contentPos, m_Tiles[m_LastSelectedMaterialIndex].Preview );

                            m_PropsAreaRect.y      = 236;
                            m_PropsAreaRect.x      = 1;
                            m_PropsAreaRect.height = 90;
                            m_PropsAreaRect.width  = 237;

                            GUI.BeginGroup( m_PropsAreaRect, (GUIStyle) "helpbox" );
                            {
                                m_PropsAreaRect.x      += 1;
                                m_PropsAreaRect.y      =  0;
                                m_PropsAreaRect.height =  22;
                                GUI.Label( m_PropsAreaRect, $"{m_Tiles[m_LastSelectedMaterialIndex].materialName}", EditorStyles.toolbarButton );
                                m_PropsAreaRect.y += 22;
                                GUI.Label( m_PropsAreaRect, $"Size:\t{m_Tiles[m_LastSelectedMaterialIndex].mainTexSize:###0}", EditorStyles.miniLabel );
                                m_PropsAreaRect.y += 22;
                                GUI.Label( m_PropsAreaRect, $"Offset:\t{m_Tiles[m_LastSelectedMaterialIndex].uvOffset:####0}", EditorStyles.miniLabel );
                                m_PropsAreaRect.y += 22;
                                GUI.Label( m_PropsAreaRect, $"Scale:\t{m_Tiles[m_LastSelectedMaterialIndex].uvScale:####0}", EditorStyles.miniLabel );
                            }
                            GUI.EndGroup();

                            m_PropsAreaRect.y      += 262;
                            m_PropsAreaRect.height =  140;
                            GUI.BeginGroup( m_PropsAreaRect, (GUIStyle) "helpbox" );
                            {
                                m_PropsAreaRect.y      = 0;
                                m_PropsAreaRect.x      = 0;
                                m_PropsAreaRect.height = 22;
                                GUI.Label( m_PropsAreaRect, $"Material", EditorStyles.toolbarButton );
                                m_PropsAreaRect.x += 1;
                                m_PropsAreaRect.y += 22;
                                GUI.Label( m_PropsAreaRect, $"Shader:\t{m_Tiles[m_LastSelectedMaterialIndex].shaderName}", EditorStyles.miniLabel );
                                m_PropsAreaRect.y += 22;
                                GUI.Label( m_PropsAreaRect, $"Albedo:\t{m_Tiles[m_LastSelectedMaterialIndex].albedoName}", EditorStyles.miniLabel );

                                m_PropsAreaRect.x     += 1;
                                m_PropsAreaRect.y     =  88;
                                m_PropsAreaRect.width -= 4;
                                if( GUI.Button( m_PropsAreaRect, "Select In Project", "largebutton" ) )
                                {
                                    Material m = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_Tiles[m_LastSelectedMaterialIndex].guid ) );
                                    Selection.activeObject = m;
                                    EditorGUIUtility.PingObject( m );
                                }

                                m_PropsAreaRect.y += 24;
                                if( GUI.Button( m_PropsAreaRect, "Apply to Selected Face", "largebutton" ) ) {}
                            }
                            GUI.EndGroup();

                            m_PropsAreaRect.y      = 469;
                            m_PropsAreaRect.height = 186;
                            GUI.BeginGroup( m_PropsAreaRect, (GUIStyle) "helpbox" );
                            {
                                m_PropsAreaRect.y      = 0;
                                m_PropsAreaRect.height = 22;
                                GUI.Label( m_PropsAreaRect, "Labels", EditorStyles.toolbarButton );

                                float btnWidth = 60;
                                int   idx      = 0;
                                foreach( var l in m_Tiles[m_LastSelectedMaterialIndex].labels )
                                {
                                    float xOffset = 2;
                                    float yOffset = 22;
                                    int   row     = 0;
                                    for( int i = 0; i < ( m_PropsAreaRect.width / btnWidth ); i++ )
                                    {
                                        if( idx == m_Tiles[m_LastSelectedMaterialIndex].labels.Length ) break;

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

        private float      m_ScrollViewHeight;
        private Rect       m_TileContentRect = Rect.zero;
        private Rect       m_ScrollViewRect  = Rect.zero;
        private GUIContent m_TileContentText = new GUIContent();

        private void DrawLabelAndTileArea()
        {
            //GUI.Box( new Rect( 0, 48, rect.width - 24, rect.height - ( 48 * 2 ) ), "", "GameViewBackground" );

            int idx        = 0;
            int numColumns = (int) ( ( position.width - ( 264 + 10 ) ) / m_TileSize );

            m_ScrollViewRect.x      = 0;
            m_ScrollViewRect.y      = 52;
            m_ScrollViewRect.width  = position.width  - 262;
            m_ScrollViewRect.height = position.height - 72;

            m_TileContentRect.x      = 0;
            m_TileContentRect.y      = 0;
            m_TileContentRect.width  = position.width - 282;
            m_TileContentRect.height = m_ScrollViewHeight;

            m_ScrollPos = GUI.BeginScrollView( m_ScrollViewRect, m_ScrollPos, m_TileContentRect );
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

                        m_TileContentRect.x      = xOffset;
                        m_TileContentRect.y      = yOffset;
                        m_TileContentRect.width  = m_TileSize - 2;
                        m_TileContentRect.height = m_TileSize - 4;

                        m_TileContentText.image   = m_Tiles[idx].Preview;
                        m_TileContentText.tooltip = $"Name: {m_Tiles[idx].materialName}\nShader: {m_Tiles[idx].shaderName}";

                        if( GUI.Button( m_TileContentRect, m_TileContentText, m_TileButtonStyle ) )
                        {
                            m_PreviewMaterial = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_Tiles[idx].guid ) );
                            m_PreviewEditor   = null;

                            m_LastSelectedMaterialIndex = idx;
                        }

                        idx++;
                    }

                    row                += 1;
                    yOffset            =  ( m_TileSize * row );
                    m_ScrollViewHeight =  yOffset;

                    // end horizontal
                }
            }
            GUI.EndScrollView();

            m_TileContentRect.x      = 0;
            m_TileContentRect.y      = position.height - 20;
            m_TileContentRect.width  = position.width;
            m_TileContentRect.height = 20;

            GUI.Box( m_TileContentRect, "", "toolbar" );

            m_TileContentRect.width = 400;
            GUI.Label( m_TileContentRect, $"Materials: {m_Tiles.Count} | Labels: {m_Labels.Count}" );

            m_TileContentRect.x      = position.width  - 360;
            m_TileContentRect.y      = position.height - 20;
            m_TileContentRect.width  = 80;
            m_TileContentRect.height = 24;

            m_TileSize = (int) GUI.HorizontalSlider( m_TileContentRect, m_TileSize, 64, 128 );
        }
    }
}
