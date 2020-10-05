/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindow.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

$TODO: Optimize away all the GUILayout logic
$TODO: Implement culling for tiles not visible (hide them if they arent within the viewable window area)
$TODO: Do we want to filter by label, too? it would allow user-ignored materials.
$TODO: Optimize optimize optimize

* * * * * * * * * * * * * * * * * * * * * */

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal class ChiselMaterialBrowserWindow : EditorWindow
    {
        public static ChiselMaterialBrowserCache CachedTiles => m_CachedTiles;

        private Vector2 m_PreviewsScrollPosition = Vector2.zero;
        private Vector2 m_LabelsScrollPosition   = Vector2.zero;

        private static int    m_PreviewSize     = 128;
        private static string m_SearchFieldText = string.Empty;
        private static string m_LabelSearchText = string.Empty;

        private static List<ChiselMaterialBrowserTile> m_Materials = new List<ChiselMaterialBrowserTile>();
        private static List<string>              m_Labels    = new List<string>();

        private static ChiselMaterialBrowserCache m_CachedTiles = null;

        private const string PREVIEW_SIZE_PREF_NAME = "chisel_matbrowser_pviewSize";

        [MenuItem( "Window/Chisel/Material Browser" )]
        private static void Init()
        {
            InitCache();
            GetMaterials();

            ChiselMaterialBrowserWindow window = EditorWindow.GetWindow<ChiselMaterialBrowserWindow>( false, "Material Browser" );
            window.maxSize = new Vector2( 1920, 2000 );
            window.minSize = new Vector2( 200,  100 );

            m_PreviewSize = EditorPrefs.GetInt( PREVIEW_SIZE_PREF_NAME, 128 );

            //Debug.Log( $"Thumbnail cache: [{CachedTiles.name}], Number of entries: [{CachedTiles.NumEntries}]" );
        }

        private static void InitCache()
        {
            m_CachedTiles ??= ChiselMaterialBrowserCache.Load();

            Debug.Log( $"Thumbnail cache: [{CachedTiles.Name}], Number of entries: [{CachedTiles.NumEntries}]" );
        }

        private void OnEnable()
        {
            m_PreviewSize = EditorPrefs.GetInt( PREVIEW_SIZE_PREF_NAME, 128 );

            if( m_Materials == null || m_Materials.Count < 1 )
                GetMaterials();
        }

        private void OnGUI()
        {
            Rect rect = this.position;

            // toolbar
            using( GUILayout.HorizontalScope hScope = new GUILayout.HorizontalScope( EditorStyles.toolbar, GUILayout.ExpandWidth( true ) ) )
            {
                if( GUILayout.Button( "Refresh", EditorStyles.toolbarButton ) )
                {
                    m_LabelSearchText = string.Empty;
                    GetMaterials();
                }

                GUILayout.FlexibleSpace();
                if( m_LabelSearchText.Length > 0 )
                {
                    if( GUILayout.Button( "x", EditorStyles.toolbarButton, GUILayout.Width( 24 ) ) )
                    {
                        m_LabelSearchText = string.Empty;
                        GetMaterials( false );
                    }

                    GUILayout.Label( $"Label Search: {m_LabelSearchText}", EditorStyles.toolbarButton, GUILayout.Width( 160 ) );
                }

                string lastText = "";
                m_SearchFieldText = EditorGUILayout.DelayedTextField( lastText = m_SearchFieldText, EditorStyles.toolbarSearchField );

                if( m_SearchFieldText != lastText )
                    GetMaterials();
            }

            // header bar
            GUILayout.BeginHorizontal();
            GUILayout.Label( "Asset Labels (used)", EditorStyles.toolbarButton, GUILayout.Width( 120 ) );
            GUILayout.Label( "",                    EditorStyles.toolbarButton, GUILayout.ExpandWidth( true ) );
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                // tag bar
                using( GUILayout.ScrollViewScope lScope = new GUILayout.ScrollViewScope( m_LabelsScrollPosition, false, false, GUILayout.ExpandHeight( true ), GUILayout.Width( 120 ) ) )
                {
                    m_LabelsScrollPosition = lScope.scrollPosition;
                    GUILayout.BeginVertical( GUILayout.ExpandHeight( true ), GUILayout.ExpandWidth( true ) );
                    {
                        for( int i = 0; i < m_Labels.Count; i++ )
                        {
                            if( m_Labels != null )
                                if( GUILayout.Button( m_Labels[i] ) )
                                {
                                    m_LabelSearchText = m_Labels[i];
                                    GetMaterials( true );
                                }
                        }
                    }
                    GUILayout.EndVertical();
                }

                // previews area
                GUILayout.BeginVertical( "GameViewBackground" );
                {
                    // view window
                    using( GUILayout.ScrollViewScope svScope = new GUILayout.ScrollViewScope( m_PreviewsScrollPosition, false, true, GUILayout.ExpandHeight( true ), GUILayout.ExpandWidth( true ) ) )
                    {
                        m_PreviewsScrollPosition = svScope.scrollPosition;

                        int idx        = 0;
                        int numColumns = (int) ( ( rect.width - 130 ) / m_PreviewSize );

                        foreach( var entry in m_Materials )
                        {
                            if( idx == m_Materials.Count ) break;

                            GUILayout.BeginHorizontal();
                            for( int x = 0; x < numColumns; x++ )
                            {
                                if( idx == m_Materials.Count ) break;

                                m_Materials[idx].Draw( new RectOffset( 0, 0, m_PreviewSize, m_PreviewSize ) );

                                idx++;
                            }

                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.EndVertical(); // previews area
            }
            GUILayout.EndHorizontal(); // tag & previews area

            // bottom toolbar
            using( GUILayout.HorizontalScope toolbarScope = new GUILayout.HorizontalScope( EditorStyles.toolbar, GUILayout.ExpandWidth( true ) ) )
            {
                int count = ( m_Labels.Count > 0 ) ? m_Labels.Count : 0;
                GUILayout.Label( $"Materials: {m_Materials.Count}" );
                GUILayout.Label( $"Labels: {count}" );

                GUILayout.FlexibleSpace();

                int lastSize;
                m_PreviewSize = EditorGUILayout.IntSlider( new GUIContent( "", "Preview Size" ), lastSize = m_PreviewSize, 64, 128, GUILayout.Width( 200 ) );
                //m_PreviewSize = GetPow2( m_PreviewSize );

                if( m_PreviewSize != lastSize )
                    EditorPrefs.SetInt( PREVIEW_SIZE_PREF_NAME, m_PreviewSize );
            }

            if( focusedWindow == this )
                Repaint();
        }

        // gets all materials and the labels on them in the project, compares them against a filter,
        // and then adds them to the list of materials to be used in this window
        private static void GetMaterials( bool usingLabel = false )
        {
            if( m_CachedTiles == null )
                InitCache();

            ChiselMaterialThumbnailRenderer.CancelAll();
            AssetPreview.SetPreviewTextureCacheSize( 2000 );

            m_Materials.Clear();
            //m_Labels.Clear();

            // exclude the label search tag if we arent searching for a specific label right now
            string search = usingLabel ? $"l:{m_LabelSearchText} {m_SearchFieldText}" : $"{m_SearchFieldText}";

            string[] guids = AssetDatabase.FindAssets( $"t:Material {search}" );

            // assemble preview tiles
            foreach( var id in guids )
            {
                ChiselMaterialBrowserTile browserTile = new ChiselMaterialBrowserTile( id );

                // add any used labels we arent currently storing
                foreach( string label in browserTile.labels )
                {
                    if( !m_Labels.Contains( label ) )
                        m_Labels.Add( label );
                }

                // check each entry against a filter to exclude certain entries
                if( ChiselMaterialBrowserUtilities.IsValidEntry( browserTile ) )
                {
                    // if we have the material already, skip, else add it
                    m_Materials.Add( browserTile );
                }
            }

            CachedTiles.Save();
        }
    }
}
