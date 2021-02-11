/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindow.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindow : EditorWindow
    {
        private const string PREVIEW_SIZE_PREF_KEY = "chisel_matbrowser_pviewSize";
        private const string TILE_LABEL_PREF_KEY   = "chisel_matbrowser_pviewShowLabel";
        private const string TILE_LAST_TAB_OPT_KEY = "chisel_matbrowser_currentTileTab";

        private static List<ChiselMaterialBrowserTile> m_Tiles     = new List<ChiselMaterialBrowserTile>();
        private static List<ChiselMaterialBrowserTile> m_UsedTiles = new List<ChiselMaterialBrowserTile>();
        private static List<string>                    m_Labels    = new List<string>();
        private static List<ChiselModel>               m_Models    = new List<ChiselModel>();

        private static int                      m_CurrentPropsTab         = 1;
        private static ChiselMaterialBrowserTab m_CurrentTilesTab         = 0;
        private static int                      lastSelectedMaterialIndex = 0;
        private static int                      tileSize                  = 64;
        private static Vector2                  tileScrollPos             = Vector2.zero;
        private static bool                     showNameLabels            = false;
        private static string                   searchText                = string.Empty;

        private readonly GUIContent m_PropsAreaToggleContent = new GUIContent( "", "" );

        private readonly GUIContent[] m_PropsTabLabels = new GUIContent[]
        {
                new GUIContent( "Labels\t    " ), new GUIContent( "Current Selection    " ),
        };

        [MenuItem( "Window/Chisel/Material Browser" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindow window = EditorWindow.GetWindow<ChiselMaterialBrowserWindow>( false, "Material Browser" );
            window.maxSize = new Vector2( 3840, 2160 );
            window.minSize = new Vector2( 420,  342 );
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt( PREVIEW_SIZE_PREF_KEY, tileSize );
            EditorPrefs.SetBool( TILE_LABEL_PREF_KEY, showNameLabels );
            EditorPrefs.SetInt( TILE_LAST_TAB_OPT_KEY, (int) m_CurrentTilesTab );

            m_Tiles.ForEach( e =>
            {
                e.Dispose();
                e = null;
            } );

            m_UsedTiles.ForEach( e =>
            {
                e.Dispose();
                e = null;
            } );

            m_Tiles.Clear();
            m_UsedTiles.Clear();
            m_Labels.Clear();

            AssetDatabase.ReleaseCachedFileHandles();
            EditorUtility.UnloadUnusedAssetsImmediate( true );

            GC.Collect( GC.GetGeneration( this ), GCCollectionMode.Forced, false, true );
        }

        private void OnEnable()
        {
            tileSize          = EditorPrefs.GetInt( PREVIEW_SIZE_PREF_KEY, 64 );
            showNameLabels    = EditorPrefs.GetBool( TILE_LABEL_PREF_KEY, false );
            m_CurrentTilesTab = (ChiselMaterialBrowserTab) EditorPrefs.GetInt( TILE_LAST_TAB_OPT_KEY, 0 );

            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false );
        }

        public  Material previewMaterial;
        public  Editor   previewEditor;
        private Rect     m_PropsAreaRect       = Rect.zero;
        private Rect     m_NotifRect           = Rect.zero;
        private Rect     m_ToolbarRect         = Rect.zero;
        private Rect     m_LabelScrollViewArea = Rect.zero;
        private Vector2  m_LabelScrollPositon  = Vector2.zero;
        private bool     m_ShowPropsArea       = true;

        private void OnGUI()
        {
            // rebuild IMGUI styling
            RebuildStyles();

            if( previewMaterial == null )
                previewMaterial = GetPreviewMaterial( lastSelectedMaterialIndex );

            m_NotifRect.x      = ( position.width  * 0.5f ) - 200;
            m_NotifRect.y      = ( position.height * 0.5f ) - 60;
            m_NotifRect.width  = 400;
            m_NotifRect.height = 150;

            if( EditorApplication.isCompiling ) { GUI.Box( m_NotifRect, "Please wait...", "NotificationBackground" ); }

            if( EditorApplication.isPlaying || EditorApplication.isPaused ) GUI.Box( m_NotifRect, "Exit Playmode to Edit Materials", "NotificationBackground" );

            if( !EditorApplication.isCompiling && !EditorApplication.isPlaying )
            {
                m_ToolbarRect.x      = 0;
                m_ToolbarRect.y      = 2;
                m_ToolbarRect.width  = position.width;
                m_ToolbarRect.height = 24;

                GUI.Label( m_ToolbarRect, "", toolbarStyle );

                m_ToolbarRect.y += 25;
                GUI.Label( m_ToolbarRect, "", toolbarStyle );

                m_ToolbarRect.y     = 2;
                m_ToolbarRect.width = 60;
                if( GUI.Button( m_ToolbarRect, "Refresh", EditorStyles.toolbarButton ) )
                {
                    ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false );
                    previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( lastSelectedMaterialIndex = 0 ) );
                }

                m_ToolbarRect.x      =  6;
                m_ToolbarRect.y      += 28;
                m_ToolbarRect.width  =  200;
                m_ToolbarRect.height =  22;

                EditorGUI.BeginChangeCheck();
                searchText = EditorGUI.DelayedTextField( m_ToolbarRect, string.Empty, searchText, EditorStyles.toolbarSearchField );
                if( EditorGUI.EndChangeCheck() ) { ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false, searchText: searchText ); }


                m_ToolbarRect.x      = position.width - ( m_ShowPropsArea ? 372 : 0 );
                m_ToolbarRect.width  = 100;
                m_ToolbarRect.height = 22;

                EditorGUI.BeginChangeCheck();
                m_CurrentTilesTab = (ChiselMaterialBrowserTab) EditorGUI.EnumPopup( m_ToolbarRect, m_CurrentTilesTab, EditorStyles.toolbarDropDown );
                if( EditorGUI.EndChangeCheck() )
                {
                    EditorPrefs.SetInt( TILE_LAST_TAB_OPT_KEY, (int) m_CurrentTilesTab );
                    previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( lastSelectedMaterialIndex = 0 ) );
                    ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false, searchText: searchText );
                }

                m_ToolbarRect.width = 40;
                m_ToolbarRect.x     = position.width - ( m_ShowPropsArea ? 412 : 0 );
                GUI.Label( m_ToolbarRect, "Show:", EditorStyles.toolbarButton );

                if( m_Tiles.Count > 0 )
                {
                    if( m_ShowPropsArea )
                    {
                        m_ToolbarRect.x      = position.width - 262;
                        m_ToolbarRect.y      = 26;
                        m_ToolbarRect.width  = 262;
                        m_ToolbarRect.height = 24;

                        GUI.Box( m_ToolbarRect, "", "dockHeader" );
                        m_ToolbarRect.y   += 2;
                        m_ToolbarRect.x   += 4;
                        m_CurrentPropsTab =  GUI.Toolbar( m_ToolbarRect, m_CurrentPropsTab, m_PropsTabLabels, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents );

                        switch( m_CurrentPropsTab )
                        {
                            case 0:
                            {
                                m_PropsAreaRect.x      = position.width - 256;
                                m_PropsAreaRect.y      = 50;
                                m_PropsAreaRect.width  = 240;
                                m_PropsAreaRect.height = position.height;


                                m_LabelScrollViewArea.x      = position.width - 256;
                                m_LabelScrollViewArea.y      = 50;
                                m_LabelScrollViewArea.width  = 240;
                                m_LabelScrollViewArea.height = 0;

                                m_LabelScrollPositon = GUI.BeginScrollView( m_PropsAreaRect, m_LabelScrollPositon, m_LabelScrollViewArea );
                                {
                                    m_PropsAreaRect.height = 24;

                                    for( int i = 0; i < m_Labels.Count; i++ )
                                    {
                                        m_LabelScrollViewArea.height += 24;

                                        if( i > 0 )
                                            m_PropsAreaRect.y += 26;
                                        if( GUI.Button( m_PropsAreaRect, m_Labels[i] ) )
                                        {
                                            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, true, m_Labels[i], searchText );
                                            lastSelectedMaterialIndex = 0;

                                            previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( 0 ) );
                                        }
                                    }
                                }
                                GUI.EndScrollView();

                                break;
                            }
                            case 1:
                            {
                                m_PropsAreaRect.x      = position.width - 260;
                                m_PropsAreaRect.y      = 50;
                                m_PropsAreaRect.width  = 260;
                                m_PropsAreaRect.height = position.height - 50;

                                GUI.BeginGroup( m_PropsAreaRect, propsSectionBG );
                                {
                                    m_PropsAreaRect.x      = 0;
                                    m_PropsAreaRect.y      = 0;
                                    m_PropsAreaRect.height = 22;
                                    GUI.Label( m_PropsAreaRect, $"{m_Tiles[lastSelectedMaterialIndex].materialName}", EditorStyles.toolbarButton );

                                    m_PropsAreaRect.x      = 2;
                                    m_PropsAreaRect.y      = 22;
                                    m_PropsAreaRect.width  = 256;
                                    m_PropsAreaRect.height = 256;

                                    if( m_Tiles[lastSelectedMaterialIndex] != null && previewMaterial != null )
                                    {
                                        if( previewEditor == null ) previewEditor = Editor.CreateEditor( previewMaterial, typeof( MaterialEditor ) );
                                        previewEditor.OnInteractivePreviewGUI( m_PropsAreaRect, EditorStyles.whiteLabel );
                                        previewEditor.Repaint();
                                    }

                                    if( position.height > 520 )
                                    {
                                        m_PropsAreaRect.y      = 278;
                                        m_PropsAreaRect.x      = 2;
                                        m_PropsAreaRect.height = 188;
                                        m_PropsAreaRect.width  = 256;

                                        GUI.BeginGroup( m_PropsAreaRect );
                                        {
                                            m_PropsAreaRect.x      = 0;
                                            m_PropsAreaRect.y      = 0;
                                            m_PropsAreaRect.height = 22;
                                            GUI.Label( m_PropsAreaRect, $"Material", EditorStyles.toolbarButton );

                                            m_PropsAreaRect.y += 24;
                                            if( GUI.Button( m_PropsAreaRect, "Select In Project", "largebutton" ) )
                                            {
                                                EditorGUIUtility.PingObject( Selection.activeObject = GetPreviewMaterial( lastSelectedMaterialIndex ) );
                                            }

                                            if( m_CurrentTilesTab > 0 )
                                            {
                                                m_PropsAreaRect.y += 24;
                                                if( GUI.Button( m_PropsAreaRect, "Select In Scene", "largebutton" ) )
                                                {
                                                    ChiselMaterialBrowserUtilities.SelectMaterialInScene( GetPreviewMaterial( lastSelectedMaterialIndex )?.name );
                                                }
                                            }

                                            m_PropsAreaRect.y += 24;
                                            if( GUI.Button( m_PropsAreaRect, applyToSelectedFaceLabelContent, "largebutton" ) ) { ApplySelectedMaterial(); }
                                        }
                                        GUI.EndGroup();

                                        if( position.height >= 400 )
                                        {
                                            m_PropsAreaRect.x      = 2;
                                            m_PropsAreaRect.y      = 379;
                                            m_PropsAreaRect.height = 200;
                                            GUI.BeginGroup( m_PropsAreaRect );
                                            {
                                                m_PropsAreaRect.x      = 0;
                                                m_PropsAreaRect.y      = 0;
                                                m_PropsAreaRect.height = 22;
                                                GUI.Label( m_PropsAreaRect, "Labels", EditorStyles.toolbarButton );

                                                const float btnWidth = 60;
                                                int         idx      = 0;

                                                foreach( string l in m_Tiles[lastSelectedMaterialIndex].labels )
                                                {
                                                    float xOffset = 2;
                                                    float yOffset = 22;
                                                    int   row     = 0;
                                                    for( int i = 0; i < ( m_PropsAreaRect.width / btnWidth ); i++ )
                                                    {
                                                        if( idx == m_Tiles[lastSelectedMaterialIndex].labels.Length ) break;

                                                        xOffset = ( 40 * i ) + 2;
                                                        if( GUI.Button( new Rect( xOffset, yOffset, btnWidth, 22 ), new GUIContent( l, $"Click to filter for label \"{l}\"" ), assetLabelStyle ) )
                                                        {
                                                            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, true, l, searchText );
                                                            lastSelectedMaterialIndex = 0;

                                                            previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( 0 ) );
                                                        }

                                                        idx++;
                                                    }

                                                    row     += 1;
                                                    yOffset =  22 + ( 22 * row );
                                                }
                                            }
                                            GUI.EndGroup();
                                        }
                                    }
                                }
                                GUI.EndGroup();

                                break;
                            }
                            default: break;
                        }
                    }

                    m_ToolbarRect.width  = 22;
                    m_ToolbarRect.height = 24;
                    m_ToolbarRect.x      = position.width - 24;
                    m_ToolbarRect.y      = 28;

                    m_PropsAreaToggleContent.text    = ( m_ShowPropsArea ) ? ">>" : "<<";
                    m_PropsAreaToggleContent.tooltip = ( m_ShowPropsArea ) ? "Hide utility bar" : "Show utility bar";
                    m_ShowPropsArea                  = GUI.Toggle( m_ToolbarRect, m_ShowPropsArea, m_PropsAreaToggleContent, EditorStyles.toolbarButton );

                    m_ToolbarRect.x      = position.width - 104;
                    m_ToolbarRect.y      = 2;
                    m_ToolbarRect.width  = 100;
                    m_ToolbarRect.height = 24;

                    // $TODO: change styling on this... its fugly. Also needs tooltips.
                    EditorGUI.BeginChangeCheck();
                    showNameLabels = GUI.Toggle( m_ToolbarRect, showNameLabels, ( showNameLabels ) ? "Hide Labels" : "Show Labels", "ToolbarButton" );
                    if( EditorGUI.EndChangeCheck() ) EditorPrefs.SetBool( TILE_LABEL_PREF_KEY, showNameLabels );

                    DrawTileArea( (ChiselMaterialBrowserTab) m_CurrentTilesTab );
                }
                else
                {
                    m_NotifRect.x      = ( position.width  * 0.5f ) - 200;
                    m_NotifRect.y      = ( position.height * 0.5f ) - 80;
                    m_NotifRect.width  = 400;
                    m_NotifRect.height = 130;

                    GUI.Box( m_NotifRect, "No Results", "NotificationBackground" );
                }
            }
        }

        public  float tileScrollViewHeight;
        private Rect  m_TileContentRect = Rect.zero;
        private Rect  m_ScrollViewRect  = Rect.zero;

        private readonly GUIContent m_TileContentText = new GUIContent();

        private void DrawTileArea( ChiselMaterialBrowserTab selectedTab )
        {
            int idx        = 0;
            int numColumns = (int) ( ( position.width - ( ( m_ShowPropsArea ) ? ( 264 + 10 ) : 0 ) ) / tileSize );

            if( m_ShowPropsArea )
            {
                m_ScrollViewRect.x      = 0;
                m_ScrollViewRect.y      = 52;
                m_ScrollViewRect.width  = position.width  - 260;
                m_ScrollViewRect.height = position.height - 72;

                m_TileContentRect.x      = 0;
                m_TileContentRect.y      = 0;
                m_TileContentRect.width  = position.width - 280;
                m_TileContentRect.height = tileScrollViewHeight;
            }
            else
            {
                m_ScrollViewRect.x      = 0;
                m_ScrollViewRect.y      = 52;
                m_ScrollViewRect.width  = position.width;
                m_ScrollViewRect.height = position.height - 72;

                m_TileContentRect.x      = 0;
                m_TileContentRect.y      = 0;
                m_TileContentRect.width  = position.width - 20;
                m_TileContentRect.height = tileScrollViewHeight;
            }

            tileScrollPos = GUI.BeginScrollView( m_ScrollViewRect, tileScrollPos, m_TileContentRect, false, true );
            {
                float xOffset = 0;
                float yOffset = 0;
                int   row     = 0;

                List<ChiselMaterialBrowserTile> current = selectedTab > 0 ? m_UsedTiles : m_Tiles;

                foreach( ChiselMaterialBrowserTile entry in current )
                {
                    if( idx == current.Count ) break;

                    // begin horizontal
                    for( int x = 0; x < numColumns; x++ )
                    {
                        xOffset = ( tileSize * x ) + 4;

                        if( idx == current.Count ) break;

                        m_TileContentRect.x      = xOffset;
                        m_TileContentRect.y      = yOffset;
                        m_TileContentRect.width  = tileSize - 2;
                        m_TileContentRect.height = tileSize - 4;

                        if( current[idx].CheckVisible( yOffset, tileSize, tileScrollPos, m_ScrollViewRect.height ) )
                        {
                            current[idx].RenderPreview();
                            m_TileContentText.image   = current[idx].Preview;
                            m_TileContentText.tooltip = $"{current[idx].materialName}\n\nClick to apply to the currently selected surface.";

                            SetButtonStyleBG( in idx );

                            if( GUI.Button( m_TileContentRect, m_TileContentText, tileButtonStyle ) )
                            {
                                previewMaterial = GetPreviewMaterial( idx );
                                previewEditor   = null;

                                lastSelectedMaterialIndex = idx;

                                ApplySelectedMaterial();
                            }

                            if( !m_TileContentRect.Contains( Event.current.mousePosition ) && tileSize > 100 && showNameLabels && lastSelectedMaterialIndex != idx )
                            {
                                m_TileContentRect.height = 22;
                                m_TileContentRect.y      = ( m_TileContentRect.y ) + tileSize - 22;

                                GUI.Box( m_TileContentRect, "", tileLabelBGStyle );

                                m_TileContentRect.x     += 4;
                                m_TileContentRect.width -= 8;

                                GUI.Label( m_TileContentRect, current[idx].materialName, tileLabelStyle );
                            }
                        }

                        idx++;
                    }

                    row                  += 1;
                    yOffset              =  ( tileSize * row );
                    tileScrollViewHeight =  yOffset;

                    // end horizontal
                }
            }
            GUI.EndScrollView();

            m_TileContentRect.x      = 0;
            m_TileContentRect.y      = position.height - 20;
            m_TileContentRect.width  = position.width;
            m_TileContentRect.height = 20;

            GUI.Box( m_TileContentRect, "", "toolbar" );

            m_TileContentRect.width =  400;
            m_TileContentRect.x     += 4;
            GUI.Label( m_TileContentRect, $"Materials: (Used: {m_UsedTiles.Count} / Total: {m_Tiles.Count}) | Labels: {m_Labels.Count}", EditorStyles.miniLabel );

            // if the properties area is shown:
            // check for the window width, if its 500px or narrower, then move the slider all the way to the right
            // otherwise, align it with the tile view.
            m_TileContentRect.x      = ( m_ShowPropsArea ) ? ( ( position.width < 500 ) ? position.width - 90 : position.width - 360 ) : position.width - 90;
            m_TileContentRect.y      = position.height - 20;
            m_TileContentRect.width  = 80;
            m_TileContentRect.height = 24;

            EditorGUI.BeginChangeCheck();
            tileSize = (int) GUI.HorizontalSlider( m_TileContentRect, tileSize, 64, 122 );
            if( EditorGUI.EndChangeCheck() ) EditorPrefs.SetInt( PREVIEW_SIZE_PREF_KEY, tileSize );
        }

        private static Material GetPreviewMaterial( int index )
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_CurrentTilesTab > 0 ? m_UsedTiles[index].guid : m_Tiles[index].guid ) );

            return m == null ? ChiselMaterialManager.DefaultMaterial : m;
        }

        [Shortcut( "Chisel/Material Browser/Apply Last Selected Material", ChiselKeyboardDefaults.ApplyLastSelectedMaterialKey, ChiselKeyboardDefaults.ApplyLastSelectedMaterialModifier )]
        public static void ApplySelectedMaterial()
        {
            if( lastSelectedMaterialIndex > m_Tiles.Count )
                return;

            if( m_Tiles[lastSelectedMaterialIndex] != null ) { ApplyMaterial( GetPreviewMaterial( lastSelectedMaterialIndex ) ); }
        }

        private static void ApplyMaterial( Material material )
        {
            if( ChiselSurfaceSelectionManager.HaveSelection )
            {
                HashSet<SurfaceReference> selected = ChiselSurfaceSelectionManager.Selection;

                foreach( SurfaceReference surf in selected ) { surf.BrushMaterial.RenderMaterial = material; }
            }
        }
    }
}
