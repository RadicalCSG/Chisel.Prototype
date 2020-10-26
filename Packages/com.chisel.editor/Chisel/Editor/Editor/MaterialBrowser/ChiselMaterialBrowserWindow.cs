/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindow.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using System.Text;
using Chisel.Core;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    internal sealed partial class ChiselMaterialBrowserWindow : EditorWindow
    {
        private const string PREVIEW_SIZE_PREF_KEY = "chisel_matbrowser_pviewSize";

        private List<ChiselMaterialBrowserTile> m_Tiles  = new List<ChiselMaterialBrowserTile>();
        private List<string>                    m_Labels = new List<string>();

        private int m_LastSelectedMaterialIndex = 0;
        private int m_CurrentPropsTab           = 1;

        private int     m_TileSize  = 64;
        private Vector2 m_ScrollPos = Vector2.zero;

        private bool   m_ShowTooltip         = false;
        private float  m_TooltipTimerRefresh = 0;
        private string m_HoveringTooltipName = "";

        private (string label, double timer) tooltipTimer = ( "", 0.0d );

        private readonly GUIContent[] m_PropsTabLabels = new GUIContent[]
        {
                new GUIContent( "Labels\t    " ), new GUIContent( "Properties    " ),
        };

        [MenuItem( "Window/Chisel/Material Browser" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindow window = EditorWindow.GetWindow<ChiselMaterialBrowserWindow>( false, "Material Browser" );
            window.maxSize = new Vector2( 1254, 2000 );
            window.minSize = new Vector2( 420,  342 );
        }

        private void Update()
        {
            if( !tooltipTimer.label.Equals( m_HoveringTooltipName ) )
            {
                tooltipTimer.label = m_HoveringTooltipName;
                tooltipTimer.timer = EditorApplication.timeSinceStartup;
            }

            if( !string.IsNullOrEmpty( tooltipTimer.label ) )
            {
                if( EditorApplication.timeSinceStartup - tooltipTimer.timer > m_TooltipTimerRefresh )
                {
                    if( !m_ShowTooltip )
                    {
                        m_ShowTooltip = true;
                        if( mouseOverWindow == this ) Repaint();
                    }
                }
                else m_ShowTooltip = false;
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt( PREVIEW_SIZE_PREF_KEY, m_TileSize );

            m_Tiles.ForEach( e =>
            {
                e.Dispose();
                e = null;
            } );

            m_Tiles  = null;
            m_Labels = null;

            AssetDatabase.ReleaseCachedFileHandles();
            EditorUtility.UnloadUnusedAssetsImmediate( true );

            GC.Collect( GC.GetGeneration( this ), GCCollectionMode.Forced, false, true );
        }

        private void OnEnable()
        {
            m_TileSize = EditorPrefs.GetInt( PREVIEW_SIZE_PREF_KEY, 64 );

            ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels /*, ref m_Cache*/, false );
        }

        private Material m_PreviewMaterial;
        private Editor   m_PreviewEditor;
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

            if( m_PreviewMaterial == null )
                m_PreviewMaterial = GetPreviewMaterial( m_LastSelectedMaterialIndex );

            m_NotifRect.x      = ( position.width  * 0.5f ) - 200;
            m_NotifRect.y      = ( position.height * 0.5f ) - 60;
            m_NotifRect.width  = 400;
            m_NotifRect.height = 120;

            if( EditorApplication.isCompiling ) { GUI.Box( m_NotifRect, "Please wait...", "NotificationBackground" ); }

            if( !EditorApplication.isCompiling && !EditorApplication.isPlaying )
            {
                m_ToolbarRect.x      = 0;
                m_ToolbarRect.y      = 2;
                m_ToolbarRect.width  = position.width;
                m_ToolbarRect.height = 24;

                GUI.Label( m_ToolbarRect, "", m_ToolbarStyle );

                m_ToolbarRect.y += 25;
                GUI.Label( m_ToolbarRect, "", m_ToolbarStyle );

                m_ToolbarRect.y     = 2;
                m_ToolbarRect.width = 60;
                if( GUI.Button( m_ToolbarRect, "Refresh", EditorStyles.toolbarButton ) )
                {
                    ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels, false );
                    m_PreviewEditor = Editor.CreateEditor( m_PreviewMaterial = GetPreviewMaterial( 0 ) );
                }

                m_ToolbarRect.width  = 22;
                m_ToolbarRect.height = 24;
                m_ToolbarRect.x      = position.width - 24;
                m_ToolbarRect.y      = 2;

                m_ShowPropsArea = GUI.Toggle( m_ToolbarRect, m_ShowPropsArea, ( m_ShowPropsArea ) ? ">>" : "<<", EditorStyles.toolbarButton );

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
                                        ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels, true, m_Labels[i], "" );
                                        m_LastSelectedMaterialIndex = 0;

                                        m_PreviewEditor = Editor.CreateEditor( m_PreviewMaterial = GetPreviewMaterial( 0 ) );
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

                            GUI.BeginGroup( m_PropsAreaRect, m_PropsSectionBG );
                            {
                                m_PropsAreaRect.x      = 0;
                                m_PropsAreaRect.y      = 0;
                                m_PropsAreaRect.height = 22;
                                GUI.Label( m_PropsAreaRect, $"{m_Tiles[m_LastSelectedMaterialIndex].materialName}", EditorStyles.toolbarButton );

                                m_PropsAreaRect.x      = 2;
                                m_PropsAreaRect.y      = 22;
                                m_PropsAreaRect.width  = 256;
                                m_PropsAreaRect.height = 256;

                                if( m_Tiles[m_LastSelectedMaterialIndex] != null && m_PreviewMaterial != null )
                                {
                                    if( m_PreviewEditor == null ) m_PreviewEditor = Editor.CreateEditor( m_PreviewMaterial, typeof( MaterialEditor ) );
                                    m_PreviewEditor.OnInteractivePreviewGUI( m_PropsAreaRect, EditorStyles.whiteLabel );
                                    m_PreviewEditor.Repaint();
                                }
                                //GUI.DrawTexture( contentPos, m_Tiles[m_LastSelectedMaterialIndex].Preview );

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
                                        /*m_PropsAreaRect.x += 2;
                                        m_PropsAreaRect.y += 22;
                                        GUI.Label( m_PropsAreaRect, $"Shader:\t{m_Tiles[m_LastSelectedMaterialIndex].shaderName}", EditorStyles.miniLabel );
                                        m_PropsAreaRect.y += 22;
                                        GUI.Label( m_PropsAreaRect, $"Albedo:\t{m_Tiles[m_LastSelectedMaterialIndex].albedoName}", EditorStyles.miniLabel );
                                        //GUI.Label( m_PropsAreaRect, $"{m_Tiles[m_LastSelectedMaterialIndex].materialName}", EditorStyles.toolbarButton );
                                        m_PropsAreaRect.y += 22;
                                        GUI.Label( m_PropsAreaRect, $"Size:\t{m_Tiles[m_LastSelectedMaterialIndex].mainTexSize:###0}", EditorStyles.miniLabel );
                                        m_PropsAreaRect.y += 22;
                                        GUI.Label( m_PropsAreaRect, $"Offset:\t{m_Tiles[m_LastSelectedMaterialIndex].uvOffset:####0}", EditorStyles.miniLabel );
                                        m_PropsAreaRect.y += 22;
                                        GUI.Label( m_PropsAreaRect, $"Scale:\t{m_Tiles[m_LastSelectedMaterialIndex].uvScale:####0}", EditorStyles.miniLabel );*/

                                        m_PropsAreaRect.y += 24;
                                        if( GUI.Button( m_PropsAreaRect, "Select In Project", "largebutton" ) )
                                        {
                                            EditorGUIUtility.PingObject( Selection.activeObject = GetPreviewMaterial( m_LastSelectedMaterialIndex ) );
                                        }

                                        m_PropsAreaRect.y += 24;
                                        if( GUI.Button( m_PropsAreaRect, "Apply to Selected Face", "largebutton" ) ) {}
                                    }
                                    GUI.EndGroup();

                                    if( position.height >= 400 )
                                    {
                                        m_PropsAreaRect.x      = 2;
                                        m_PropsAreaRect.y      = 369;
                                        m_PropsAreaRect.height = 200;
                                        GUI.BeginGroup( m_PropsAreaRect );
                                        {
                                            m_PropsAreaRect.x      = 0;
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
                                                        ChiselMaterialBrowserUtilities.GetMaterials( ref m_Tiles, ref m_Labels /*, ref m_Cache*/, true, l, "" );
                                                        m_LastSelectedMaterialIndex = 0;

                                                        m_PreviewEditor = Editor.CreateEditor( m_PreviewMaterial = GetPreviewMaterial( 0 ) );
                                                        //m_SearchFilterLabel.SetText( $"Search Filter: {l}" );
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

                DrawTileArea();
            }

            if( !m_ShowTooltip ) ChiselTooltip.Hide();
        }

        private float      m_ScrollViewHeight;
        private Rect       m_TileContentRect = Rect.zero;
        private Rect       m_ScrollViewRect  = Rect.zero;
        private GUIContent m_TileContentText = new GUIContent();

        private void DrawTileArea()
        {
            //GUI.Box( new Rect( 0, 48, rect.width - 24, rect.height - ( 48 * 2 ) ), "", "GameViewBackground" );

            int idx        = 0;
            int numColumns = (int) ( ( position.width - ( ( m_ShowPropsArea ) ? ( 264 + 10 ) : 0 ) ) / m_TileSize );

            if( m_ShowPropsArea )
            {
                m_ScrollViewRect.x      = 0;
                m_ScrollViewRect.y      = 52;
                m_ScrollViewRect.width  = position.width  - 260;
                m_ScrollViewRect.height = position.height - 72;

                m_TileContentRect.x      = 0;
                m_TileContentRect.y      = 0;
                m_TileContentRect.width  = position.width - 280;
                m_TileContentRect.height = m_ScrollViewHeight;
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
                m_TileContentRect.height = m_ScrollViewHeight;
            }

            m_ScrollPos = GUI.BeginScrollView( m_ScrollViewRect, m_ScrollPos, m_TileContentRect, false, true );
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
                        m_TileContentText.tooltip = "";

                        StringBuilder summary = new StringBuilder();
                        summary.AppendLine( $"Shader:\t{m_Tiles[idx].shaderName}" );
                        //.AppendLine( $"GUID:\t{m_Tiles[idx].guid}" );

                        if( m_Tiles[idx].labels.Length > 0 )
                        {
                            summary.Append( $"Labels:\t" );

                            for( int i = 0; i < m_Tiles[idx].labels.Length; i++ )
                            {
                                summary.Append( $"{m_Tiles[idx].labels[i]}" );
                                if( i > 1 ) summary.Append( ", " );
                            }

                            summary.AppendLine( string.Empty );
                        }

                        summary.AppendLine( string.Empty );
                        summary.AppendLine( "Click to apply to the selected face. Farther application of the selected material can be done using the shortcut below." );

                        ChiselTooltipContent m_TooltipContent = new ChiselTooltipContent( $"{m_Tiles[idx].materialName}", summary.ToString() );

                        if( m_Tiles[idx].CheckVisible( yOffset, m_TileSize, m_ScrollPos, m_ScrollViewHeight ) )
                        {
                            SetButtonStyleBG( in idx );

                            //Debug.Log( $"IsInView for element {idx} is true, show thumbnail for material {m_Tiles[idx].materialName}" );
                            if( GUI.Button( m_TileContentRect, m_TileContentText, m_TileButtonStyle ) )
                            {
                                m_PreviewMaterial = GetPreviewMaterial( idx );
                                m_PreviewEditor   = null;

                                m_LastSelectedMaterialIndex = idx;
                            }

                            if( m_TileContentRect.Contains( Event.current.mousePosition ) )
                            {
                                m_HoveringTooltipName = m_Tiles[idx].materialName;
                                m_TooltipTimerRefresh = 0.5f;

                                m_TileContentRect.x = ( position.x + xOffset ) - m_ScrollPos.x;
                                m_TileContentRect.y = ( position.y + yOffset ) - m_ScrollPos.y - ( ( m_TileSize + 20 ) + m_TooltipContent.Height );

                                if( m_ShowTooltip )
                                {
                                    m_TooltipContent.Shortcut =
                                            $"{ShortcutManager.instance.GetShortcutBinding( "Chisel/Material Browser/Apply Last Selected Material" )}";
                                    ChiselTooltip.Show( m_TileContentRect, m_TooltipContent );
                                }
                            }

                            //else ChiselTooltip.Hide();
                        }
                        //else Debug.Log( $"IsInView for element {idx} is false, not drawing." );

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

            // if the properties area is shown:
            // check for the window width, if its 500px or narrower, then move the slider all the way to the right
            // otherwise, align it with the tile view.
            m_TileContentRect.x      = ( m_ShowPropsArea ) ? ( ( position.width < 500 ) ? position.width - 90 : position.width - 360 ) : position.width - 90;
            m_TileContentRect.y      = position.height - 20;
            m_TileContentRect.width  = 80;
            m_TileContentRect.height = 24;

            EditorGUI.BeginChangeCheck();
            m_TileSize = (int) GUI.HorizontalSlider( m_TileContentRect, m_TileSize, 64, 122 );
            if( EditorGUI.EndChangeCheck() ) EditorPrefs.SetInt( PREVIEW_SIZE_PREF_KEY, m_TileSize );
        }

        private Material GetPreviewMaterial( int index )
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.GUIDToAssetPath( m_Tiles[index].guid ) );

            if( m == null ) return ChiselMaterialManager.DefaultMaterial;

            return m;
        }

        [Shortcut( "Chisel/Material Browser/Apply Last Selected Material", ChiselKeyboardDefaults.ApplyLastSelectedMaterialKey, ChiselKeyboardDefaults.ApplyLastSelectedMaterialModifier )]
        private static void ApplySelectedMaterial()
        {
        }
    }
}
