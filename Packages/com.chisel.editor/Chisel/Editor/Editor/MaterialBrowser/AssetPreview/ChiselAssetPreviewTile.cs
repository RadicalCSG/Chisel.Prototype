/* * * * * * * * * * * * * * * * * * * * * *
URL:     https://github.com/RadicalCSG/Chisel.Prototype
License: MIT (https://tldrlegal.com/license/mit-license)
Author:  Daniel Cornelius

Generic base class for getting thumbnail previews for use in IMGUI
* * * * * * * * * * * * * * * * * * * * * */

using System;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal class ChiselAssetPreviewTile<T> : IDisposable where T : UnityEngine.Object
    {
        public readonly string   path;
        public readonly string   guid;
        public readonly string   shaderName;
        public readonly string   assetName;
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
            // dont include specific assets
            if( ( m_Preview && m_Preview != AssetPreview.GetMiniTypeThumbnail( typeof( Material ) ) )
                || AssetPreview.IsLoadingAssetPreview( id )
                || !ChiselMaterialBrowserUtilities.IsValidEntry( this ) )
            {
                return;
            }

            m_Preview = ChiselMaterialBrowserUtilities.GetAssetPreviewFromGUID( guid );
        }

        public ChiselAssetPreviewTile( string instID )
        {
            path = AssetDatabase.GUIDToAssetPath( instID );

            T asset = AssetDatabase.LoadAssetAtPath<T>( path );

            id     = asset.GetInstanceID();
            guid   = instID;
            labels = AssetDatabase.GetLabels( asset );

            if( asset is Material material )
                shaderName = material.shader.name;

            assetName = asset.name;

            RenderPreview();
        }
    }
}
