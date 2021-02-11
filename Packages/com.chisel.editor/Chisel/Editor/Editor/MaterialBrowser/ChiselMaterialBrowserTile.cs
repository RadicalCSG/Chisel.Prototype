/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserTile.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal class ChiselMaterialBrowserTile : IDisposable
    {
        public readonly string   path;
        public readonly string   guid;
        public readonly string   shaderName;
        public readonly string   materialName;
        public readonly string[] labels;
        public readonly int      id;

        public  Texture2D Preview => m_Preview;
        private Texture2D m_Preview;

        /// <inheritdoc />
        public void Dispose()
        {
            m_Preview = null;
        }

        public bool CheckVisible( float yOffset, float thumbnailSize, Vector2 scrollPos, float scrollViewHeight )
        {
            if( scrollPos.y + scrollViewHeight < ( yOffset - thumbnailSize ) ) return false;
            if( yOffset     + thumbnailSize    < scrollPos.y ) return false;
            return true;
        }

        public void RenderPreview()
        {
            // dont include specific materials
            if( ( m_Preview && m_Preview != AssetPreview.GetMiniTypeThumbnail( typeof( Material ) ) )
                || AssetPreview.IsLoadingAssetPreview( id )
                || materialName.Contains( "Font Material" )
                || !ChiselMaterialBrowserUtilities.IsValidEntry( this ) ) { return; }

            m_Preview = ChiselMaterialBrowserUtilities.GetAssetPreviewFromGUID( guid );
        }

        public ChiselMaterialBrowserTile( string instID )
        {
            path = AssetDatabase.GUIDToAssetPath( instID );

            Material m = AssetDatabase.LoadAssetAtPath<Material>( path );

            id           = m.GetInstanceID();
            guid         = instID;
            labels       = AssetDatabase.GetLabels( m );
            shaderName   = m.shader.name;
            materialName = m.name;

            RenderPreview();
        }
    }
}
