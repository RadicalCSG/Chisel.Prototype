/* * * * * * * * * * * * * * * * * * * * * *
URL:     https://github.com/RadicalCSG/Chisel.Prototype
License: MIT (https://tldrlegal.com/license/mit-license)
Author:  Daniel Cornelius

Core class of the material browser component for chisel.
Various comments are left throughout the file noting
anything important or needing change.

$TODO: add context menus for each tile to eliminate the need for the side bar. This will simplify a lot of the code and make things more consistent.
$TODO: automatically find any browser tabs in the project. this should find any subclasses of BrowserTab
* * * * * * * * * * * * * * * * * * * * * */


using System;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;


namespace Chisel.Editors.MaterialBrowser
{
    internal sealed partial class ChiselMaterialBrowserWindow : EditorWindow
    {
        private const string PREVIEW_SIZE_PREF_KEY = "chisel_matbrowser_pviewSize";
        private const string TILE_LABEL_PREF_KEY   = "chisel_matbrowser_pviewShowLabel";
        private const string TILE_LAST_TAB_OPT_KEY = "chisel_matbrowser_currentTileTab";

        private static List<PreviewTile<Material>> m_Tiles     = new();
        private static List<PreviewTile<Material>> m_UsedTiles = new();
        private static List<string>                           m_Labels    = new();
        private static List<ChiselModel>                      m_Models    = new();

        private static ChiselMaterialBrowserTab m_CurrentTilesTab = 0;

        private static int     m_CurrentPropsTab         = 1;
        private static int     lastSelectedMaterialIndex = 0;
        private static int     tileSize                  = 64;
        private static Vector2 tileScrollPos             = Vector2.zero;
        private static bool    showNameLabels            = false;
        private static string  searchText                = string.Empty;

        private readonly GUIContent m_PropsAreaToggleContent = new( "", "" );

        private readonly GUIContent[] m_PropsTabLabels = new GUIContent[]
        {
            new( "Labels\t    " ),
            new( "Current Selection    " ),
        };

        // event that gets triggered on project change.
        private void Reload()
        {
            OnDisable();
            OnEnable();
        }

        [MenuItem( "Window/Chisel/Material Browser" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindow window = EditorWindow.GetWindow<ChiselMaterialBrowserWindow>( false, "Material Browser" );
            window.maxSize = new Vector2( 3840, 2160 );
            window.minSize = new Vector2( 420, 342 );
        }

        // set any prefs and then clean up memory when the window closes.
        private void OnDisable()
        {
            EditorPrefs.SetInt( PREVIEW_SIZE_PREF_KEY, tileSize );
            EditorPrefs.SetBool( TILE_LABEL_PREF_KEY, showNameLabels );
            EditorPrefs.SetInt( TILE_LAST_TAB_OPT_KEY, (int) m_CurrentTilesTab );

            m_Tiles.ForEach
            (
                e =>
                {
                    e.Dispose();
                    e = null;
                }
            );

            m_UsedTiles.ForEach
            (
                e =>
                {
                    e.Dispose();
                    e = null;
                }
            );

            m_Tiles.Clear();
            m_UsedTiles.Clear();
            m_Labels.Clear();

            // $BUG: always make sure styles are refreshed... prevents bug where the background texture for preview labels dont persist through domain reload
            ResetStyles();

            // we do the following 3 method calls below to release any memory that was used for thumbnails that no longer are used.
            AssetDatabase.ReleaseCachedFileHandles();
            EditorUtility.UnloadUnusedAssetsImmediate( true );

            GC.Collect( GC.GetGeneration( this ), GCCollectionMode.Forced, false, true );
        }

        // ensure the window loads any content when the project changes, and then set up any prefs and necessary data.
        private void OnEnable()
        {
            EditorApplication.projectChanged -= Reload;
            EditorApplication.projectChanged += Reload;

            tileSize          = EditorPrefs.GetInt( PREVIEW_SIZE_PREF_KEY, 64 );
            showNameLabels    = EditorPrefs.GetBool( TILE_LABEL_PREF_KEY, false );
            m_CurrentTilesTab = (ChiselMaterialBrowserTab) EditorPrefs.GetInt( TILE_LAST_TAB_OPT_KEY, 0 );

            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false );
        }

        // $TODO: Clean up rect code... i can probably use more rects at the cost of a slight bit of memory in order to make code easier to follow.
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

            // notification layout
            m_NotifRect.x      = ( position.width * 0.5f ) - 200;
            m_NotifRect.y      = ( position.height * 0.5f ) - 60;
            m_NotifRect.width  = 400;
            m_NotifRect.height = 150;

            if( EditorApplication.isCompiling )
            {
                GUI.Box( m_NotifRect, "Please wait...", "NotificationBackground" );
            }

            if( EditorApplication.isPlaying || EditorApplication.isPaused ) GUI.Box( m_NotifRect, "Exit Playmode to Edit Materials", "NotificationBackground" );

            // toolbar and side bar
            if( !EditorApplication.isCompiling && !EditorApplication.isPlaying )
            {
                // toolbars
                m_ToolbarRect.x      = 0;
                m_ToolbarRect.y      = 2;
                m_ToolbarRect.width  = position.width;
                m_ToolbarRect.height = 24;

                // top
                GUI.Label( m_ToolbarRect, "", toolbarStyle );

                // 2nd top
                m_ToolbarRect.y += 25;
                GUI.Label( m_ToolbarRect, "", toolbarStyle );

                // refresh button
                m_ToolbarRect.y     = 2;
                m_ToolbarRect.width = 60;

                if( GUI.Button( m_ToolbarRect, "Refresh", EditorStyles.toolbarButton ) )
                {
                    // $BUG: when in used materials mode, refresh just appends new tiles for some reason. maybe isnt context aware...
                    ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false );
                    previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( lastSelectedMaterialIndex = 0 ) );
                    searchText    = string.Empty;

                    ResetStyles();
                }

                /* $TODO: merge search field with current display type.
                 * |> it needs to be clear it isnt used specifically for search, but that it is being considered for such.
                 * |> something like https://user-images.githubusercontent.com/157976/108815534-eca91880-75b4-11eb-9ba5-1a8a7ef9dbd3.png
                 */
                // search field
                m_ToolbarRect.x      =  position.width < 600 ? 66 : 6;
                m_ToolbarRect.y      += position.width < 600 ? 0 : 28;
                m_ToolbarRect.width  =  200;
                m_ToolbarRect.height =  22;

                EditorGUI.BeginChangeCheck();
                searchText = EditorGUI.DelayedTextField( m_ToolbarRect, string.Empty, searchText, EditorStyles.toolbarSearchField );

                if( EditorGUI.EndChangeCheck() )
                {
                    ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_UsedTiles, ref m_Labels, ref m_Models, false, searchText: searchText );
                }

                // all/used material filter
                m_ToolbarRect.x      = position.width - ( m_ShowPropsArea ? 372 : 135 );
                m_ToolbarRect.y      = m_ToolbarRect.y + ( position.width < 600 ? 28 : 0 );
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
                m_ToolbarRect.x     = position.width - ( m_ShowPropsArea ? 412 : 175 );
                GUI.Label( m_ToolbarRect, "Show", EditorStyles.toolbarButton );

                if( m_Tiles.Count > 0 )
                {
                    if( m_ShowPropsArea )
                    {
                        // tab group area
                        m_ToolbarRect.x      = position.width - 262;
                        m_ToolbarRect.y      = 26;
                        m_ToolbarRect.width  = 262;
                        m_ToolbarRect.height = 24;

                        GUI.Box( m_ToolbarRect, "", "dockHeader" );

                        // tabs
                        m_ToolbarRect.y   += 2;
                        m_ToolbarRect.x   += 4;
                        m_CurrentPropsTab =  GUI.Toolbar( m_ToolbarRect, m_CurrentPropsTab, m_PropsTabLabels, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents );

                        switch( m_CurrentPropsTab )
                        {
                            // label search property page
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

                                        // label button, filters search to the specified label when clicked
                                        if( GUI.Button( m_PropsAreaRect, m_Labels[i] ) )
                                        {
                                            ChiselMaterialBrowserUtilities.GetMaterials
                                            (
                                                ref m_Tiles,
                                                ref m_UsedTiles,
                                                ref m_Labels,
                                                ref m_Models,
                                                true,
                                                m_Labels[i],
                                                searchText
                                            );

                                            lastSelectedMaterialIndex = 0;

                                            previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( 0 ) );
                                        }
                                    }
                                }

                                GUI.EndScrollView();

                                break;
                            }

                            // material property page
                            case 1:
                            {
                                // material property page group
                                m_PropsAreaRect.x      = position.width - 260;
                                m_PropsAreaRect.y      = 50;
                                m_PropsAreaRect.width  = 260;
                                m_PropsAreaRect.height = position.height - 50;

                                GUI.BeginGroup( m_PropsAreaRect, propsSectionBG );

                                {
                                    // header label
                                    m_PropsAreaRect.x      = 0;
                                    m_PropsAreaRect.y      = 0;
                                    m_PropsAreaRect.height = 22;
                                    GUI.Label( m_PropsAreaRect, $"{m_Tiles[lastSelectedMaterialIndex].assetName}", EditorStyles.toolbarButton );

                                    // interactive preview
                                    m_PropsAreaRect.x      = 2;
                                    m_PropsAreaRect.y      = 22;
                                    m_PropsAreaRect.width  = 256;
                                    m_PropsAreaRect.height = 256;

                                    if( m_Tiles[lastSelectedMaterialIndex] != null && previewMaterial != null )
                                    {
                                        if( previewEditor == null ) previewEditor = Editor.CreateEditor( previewMaterial, typeof(MaterialEditor) );
                                        previewEditor.OnInteractivePreviewGUI( m_PropsAreaRect, EditorStyles.whiteLabel );
                                        previewEditor.Repaint();
                                    }

                                    // buttons, hidden when the window is too small
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
                                            GUI.Label( m_PropsAreaRect, "Material", EditorStyles.toolbarButton );

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

                                            if( GUI.Button( m_PropsAreaRect, applyToSelectedFaceLabelContent, "largebutton" ) )
                                            {
                                                ApplySelectedMaterial();
                                            }
                                        }

                                        GUI.EndGroup();

                                        // labels on the selected material
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
                                                            ChiselMaterialBrowserUtilities.GetMaterials
                                                            (
                                                                ref m_Tiles,
                                                                ref m_UsedTiles,
                                                                ref m_Labels,
                                                                ref m_Models,
                                                                true,
                                                                l,
                                                                searchText
                                                            );

                                                            lastSelectedMaterialIndex = 0;

                                                            previewEditor = Editor.CreateEditor( previewMaterial = GetPreviewMaterial( 0 ) );
                                                            searchText    = $"l:{l}";
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
                        }
                    }

                    // side bar toggle, hides/shows the entire side bar when toggled
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
                    showNameLabels = EditorGUI.ToggleLeft( m_ToolbarRect, "Show Labels", showNameLabels );
                    if( EditorGUI.EndChangeCheck() ) EditorPrefs.SetBool( TILE_LABEL_PREF_KEY, showNameLabels );

                    DrawTileArea( m_CurrentTilesTab );
                }

                // search has no results, show notification to convey this
                else
                {
                    m_NotifRect.x      = ( position.width * 0.5f ) - 200;
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

        private readonly GUIContent m_TileContentText = new();

        private void DrawTileArea( ChiselMaterialBrowserTab selectedTab )
        {
            int idx        = 0;
            int numColumns = (int) ( ( position.width - ( ( m_ShowPropsArea ) ? ( 264 + 10 ) : 0 ) ) / tileSize );

            // adjust scroll and content areas based on if the side bar is visible
            if( m_ShowPropsArea )
            {
                m_ScrollViewRect.x      = 0;
                m_ScrollViewRect.y      = 52;
                m_ScrollViewRect.width  = position.width - 260;
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


            // tile scroll area
            tileScrollPos = GUI.BeginScrollView( m_ScrollViewRect, tileScrollPos, m_TileContentRect, false, true );
            // $TODO: replace this section with the new MaterialBrowserTab API
            {
                float xOffset = 0;
                float yOffset = 0;
                int   row     = 0;

                // show content for used/all tiles
                List<PreviewTile<Material>> current = selectedTab switch
                {
                    ChiselMaterialBrowserTab.All  => m_Tiles,
                    ChiselMaterialBrowserTab.Used => m_UsedTiles,
                    _                             => m_Tiles
                };

                foreach( PreviewTile<Material> entry in current )
                {
                    if( current.Count == 0 ) break;
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
                            m_TileContentText.tooltip = $"{current[idx].assetName}\n\n\nClick to apply to the currently selected surface.";

                            SetButtonStyleBG( in idx );

                            // tile
                            if( GUI.Button( m_TileContentRect, m_TileContentText, tileButtonStyle ) )
                            {
                                previewMaterial = GetPreviewMaterial( idx );
                                previewEditor   = null;

                                lastSelectedMaterialIndex = idx;

                                ApplySelectedMaterial();
                            }

                            // tile label
                            if( !m_TileContentRect.Contains( Event.current.mousePosition ) && tileSize > 100 && showNameLabels && lastSelectedMaterialIndex != idx )
                            {
                                m_TileContentRect.height = 22;
                                m_TileContentRect.y      = ( m_TileContentRect.y ) + tileSize - 22;

                                GUI.Box( m_TileContentRect, "", tileLabelBGStyle );

                                m_TileContentRect.x     += 4;
                                m_TileContentRect.width -= 8;

                                GUI.Label( m_TileContentRect, current[idx].assetName, tileLabelStyle );
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

            // bottom info bar
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

            // $BUG: moving the slider handle too quickly causes it to lose focus. is this unity side, or a side effect of something else?
            tileSize = (int) GUI.HorizontalSlider( m_TileContentRect, tileSize, 64, 122 );

            if( EditorGUI.EndChangeCheck() )
                EditorPrefs.SetInt( PREVIEW_SIZE_PREF_KEY, tileSize );
        }

        private static Material GetPreviewMaterial( int index )
        {
            // $BUG: this is done to avoid a bug where if one or the other collection had zero items in it, it would not render, even if the list requested had valid items
            switch( m_CurrentTilesTab )
            {
                case ChiselMaterialBrowserTab.All:
                    if( m_Tiles.Count == 0 ) return null;

                    break;

                case ChiselMaterialBrowserTab.Used:
                    if( m_UsedTiles.Count == 0 ) return null;

                    break;
            }

            // note: i really dont like that i have to do this. is there *any* possible way to avoid loading the material into memory? -Daniel
            Material m = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_CurrentTilesTab > 0 ? m_UsedTiles[index].guid : m_Tiles[index].guid ) );

            return m == null ? ChiselMaterialManager.DefaultMaterial : m;
        }

        [Shortcut( "Chisel/Material Browser/Apply Last Selected Material", ChiselKeyboardDefaults.ApplyLastSelectedMaterialKey, ChiselKeyboardDefaults.ApplyLastSelectedMaterialModifier )]
        public static void ApplySelectedMaterial()
        {
            if( lastSelectedMaterialIndex > m_Tiles.Count )
                return;

            if( m_Tiles[lastSelectedMaterialIndex] != null )
            {
                ApplyMaterial( GetPreviewMaterial( lastSelectedMaterialIndex ) );
            }
        }

        // $TODO: This can be a built-in method so it can be used for others. I may include this in a separate PR as part of an API update.
        private static void ApplyMaterial( Material material )
        {
            if( ChiselSurfaceSelectionManager.HaveSelection )
            {
                HashSet<SurfaceReference> selected = ChiselSurfaceSelectionManager.Selection;

                foreach( SurfaceReference surf in selected )
                {
                    surf.BrushMaterial.RenderMaterial = material;
                }
            }
            
            // once we apply the material, refresh the geometry
            ChiselNodeHierarchyManager.Rebuild();
        }
    }
}
