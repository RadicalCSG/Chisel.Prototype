// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
// Author:             Daniel Cornelius (NukeAndBeans)
// Contact:            Twitter @nukeandbeans, Discord Nuke#3681
// License:
// Date/Time:          12-16-2021 @ 11:06 PM
// 
// Description:
// 
// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *


using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace Chisel.Editors.MaterialBrowser
{
    internal sealed partial class MaterialBrowserTab : BrowserTab<BrowserItem<Material>>
    {
        ///<summary>Filter used when the user is searching for something from the browser</summary>
        public string DisplayFilter { get; set; } = string.Empty;

        private GUIContent _NoContentNotification;

        /// <inheritdoc />
        public MaterialBrowserTab( string title ) : base( title, "t:Material" )
        {
            AppendExclusionFilter
            (
                new[]
                {
                    "font material",
                    "packages/com.chisel.components/package resources/preview materials/"
                }
            );
        }

        private bool CheckValid( BrowserItem<Material> tile )
        {
            bool StringContains( string term, string source, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase )
            {
                return source.IndexOf( term, comparison ) >= 0;
            }

            foreach( string s in ExclusionFilter )
            {
                if( StringContains( s, tile.shaderName ) ) return false;
                if( StringContains( s, tile.path ) ) return false;
            }

            return true;
        }

        /// <inheritdoc />
        protected override void GetAssets()
        {
            Content.ForEach( e => e.Dispose() );
            Content.Clear();

            List<string> searchResults = new();

            SearchFilter.ForEach
            (
                e =>
                {
                    // search for assets using defined filters + user search filter
                    searchResults.AddRange( AssetDatabase.FindAssets( $"{e} {DisplayFilter}" ) );
                }
            );

            searchResults.ForEach
            (
                e =>
                {
                    // process found assets

                    BrowserItem<Material> tile = new( e );

                    if( CheckValid( tile ) )
                        Content.Add( tile );
                }
            );

            AssetPreview.SetPreviewTextureCacheSize( Count + 1 );
        }

        private Rect _drawAreaRect = Rect.zero;
        private Rect _tileRect     = new( 0, 0, 64, 64 );

        private GUIContent _tileLabelContent;

        /// <inheritdoc />
        public override void DrawContent( Rect drawArea, Vector2 tileSize, Rect scrollContentRect, Vector2 scrollPosition )
        {
            if( Count < 1 ) return;

            _drawAreaRect.x      = 0;
            _drawAreaRect.y      = 0;
            _drawAreaRect.width  = drawArea.width;
            _drawAreaRect.height = drawArea.height;

            float yOffset = 0;

            int row        = 0;
            int idx        = 0;
            int numColumns = (int) ( drawArea.width / tileSize.x );

            using( new GUI.GroupScope( drawArea, "", GUIStyle.none ) )
            {
                foreach( BrowserItem<Material> item in Content )
                {
                    if( idx == Count ) break; // if we've reached our item count, exit loop

                    for( int x = 0; x < numColumns; x++ )
                    {
                        float xOffset = tileSize.x * x + 4;

                        _tileRect.x      = xOffset;
                        _tileRect.y      = yOffset;
                        _tileRect.width  = tileSize.x - 2;
                        _tileRect.height = tileSize.y - 4;

                        // if visible...
                        {
                            _tileLabelContent.image   = Content[idx].Preview;
                            _tileLabelContent.tooltip = $"{Content[idx].assetName}\n\n\nClick to apply the currently selected surface.";

                            // set bg style...

                            if( GUI.Button( _tileRect, _tileLabelContent /*, tileButtonStyle*/ ) )
                            {
                                // set last index...
                                // apply selected...
                            }
                        }

                        idx++;
                    }

                    row     += 1;
                    yOffset =  tileSize.y * row;
                }
            }
        }
    }
}
