/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselSurfacePresetBrowserTile.cs

License:    MIT (https://tldrlegal.com/license/mit-license)
Author:     Daniel Cornelius
Date:       2/12/2021 @ 12:16 AM

* * * * * * * * * * * * * * * * * * * * * */

using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public class ChiselSurfacePresetBrowserTile
    {
        public readonly string   assetName;
        public readonly string   path;
        public readonly string[] labels;

        public ChiselSurfacePresetAsset Asset { get; set; }

        public bool CheckVisible( float yOffset, float thumbnailSize, Vector2 scrollPos, float scrollViewHeight )
        {
            if( scrollPos.y + scrollViewHeight < ( yOffset - thumbnailSize ) ) return false;
            if( yOffset     + thumbnailSize    < scrollPos.y ) return false;
            return true;
        }

        public ChiselSurfacePresetBrowserTile( string instID )
        {
            path = AssetDatabase.GUIDToAssetPath( instID );

            Asset = AssetDatabase.LoadAssetAtPath<ChiselSurfacePresetAsset>( path );

            labels    = AssetDatabase.GetLabels( Asset );
            assetName = Asset.name;
        }

        public void Draw( Rect offset, Rect parentRect )
        {
            // offset = tile rect, parent = scrollview

            
        }
    }
}
/*
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
 */
