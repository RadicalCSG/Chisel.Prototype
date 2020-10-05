/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserTile.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal class ChiselMaterialBrowserTile
    {
        public string   path;
        public string   shaderName;
        public string   materialName;
        public string[] labels;

        public Texture2D Preview
        {
            get
            {
                if( m_Preview == null ) { return ChiselEmbeddedTextures.TemporaryTexture; }

                return m_Preview;
            }
        }

        public int InstanceID => m_InstanceID;

        private int       m_InstanceID = 0;
        private Texture2D m_Preview    = null;

        public ChiselMaterialBrowserTile( string instID )
        {
            path = AssetDatabase.GUIDToAssetPath( instID );

            Material m = AssetDatabase.LoadAssetAtPath<Material>( path );

            m_InstanceID = m.GetInstanceID();
            labels       = AssetDatabase.GetLabels( m );
            shaderName   = m.shader.name;
            materialName = m.name;

            if( m_Preview == null )
            {
                m_Preview = ChiselMaterialBrowserWindow.CachedTiles.GetThumbnail( m_InstanceID );

                if( !materialName.Contains( "Font Material" ) ) // dont even consider font materials
                    ChiselMaterialThumbnailRenderer.Add( materialName, () => !AssetPreview.IsLoadingAssetPreviews(), () => { m_Preview = AssetPreview.GetAssetPreview( m ); } );

                if( ChiselMaterialBrowserUtilities.IsValidEntry( this ) )
                    ChiselMaterialBrowserWindow.CachedTiles.AddEntry( new ChiselMaterialBrowserCache.CachedThumbnail( materialName, m_InstanceID, m_Preview ) );
            }
        }

        // $TODO: Use GUI instead of GUILayout
        public void Draw( RectOffset offset )
        {
            //if(m_Preview == null) Debug.LogError( $"Preview thumbnail [{materialName}] null" );

            if( Preview != null )
            {
                GUIContent previewContent = new GUIContent( Preview, $"{materialName}\nIn: [{path}]" );

                GUILayout.Box( previewContent, GUILayout.Height( offset.top ), GUILayout.Width( offset.bottom ) );
            }
        }
    }
}
