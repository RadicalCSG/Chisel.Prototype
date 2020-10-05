/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindowLayoutTest.cs

License:
Author: Daniel Cornelius

$TODO: DELETE ME WHEN DONE

* * * * * * * * * * * * * * * * * * * * * */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal sealed class ChiselMaterialBrowserWindowTest : EditorWindow
    {
        private Vector2 m_ScrollPos = Vector2.zero;

        private List<string> tiles = new List<string>() // 8x8
        {
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box",
                "box", "box", "box", "box", "box", "box", "box", "box"
        };

        [MenuItem( "Window/Chisel/Material Browser Test" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindowTest window = EditorWindow.GetWindow<ChiselMaterialBrowserWindowTest>( false, "Material Browser" );
            window.maxSize = new Vector2( 1920, 2000 );
            window.minSize = new Vector2( 200,  100 );
        }

        private void OnGUI()
        {
            Rect rect = this.position;

            DrawToolbar( rect );
            DrawLabelAndTileArea( rect );
            DrawFooter( rect );
        }

        private void DrawFooter( Rect rect )
        {
        }

        private float scrollViewHeight;
        private float xOffset = 2;

        private void DrawLabelAndTileArea( Rect rect )
        {
            GUI.Box( new Rect( 0, 20, rect.width - 130, rect.height - 20 ), "", "GameViewBackground" );

            int idx        = 0;
            int numColumns = (int) ( ( rect.width - 150 ) / 64 );

            m_ScrollPos = GUI.BeginScrollView( new Rect( 0, 20, rect.width - 130, rect.height - 20 ), m_ScrollPos, new Rect( 0, 20, rect.width - 150, scrollViewHeight ) );
            {
                float yOffset = 22;
                int   row     = 0;

                foreach( var entry in tiles )
                {
                    if( idx == tiles.Count ) break;

                    // begin horizontal
                    for( int x = 0; x < numColumns; x++ )
                    {
                        if( x > 0 )
                            xOffset = 68 * x;

                        if( idx == tiles.Count ) break;

                        GUI.Button( new Rect( xOffset, yOffset, 64, 64 ), $"{tiles[idx]}, #{idx + 1}" );

                        idx++;
                    }

                    row     += 1;
                    xOffset =  2;
                    if( row > 0 )
                    {
                        yOffset = 22 + ( 68 * row );

                        scrollViewHeight = yOffset;
                    }

                    // end horizontal
                }
            }
            GUI.EndScrollView();

            GUI.Label( new Rect( 0, 0, rect.width, 22 ),
                       $"ScrollPos: {m_ScrollPos.ToString()} | ScrollHeight: {scrollViewHeight:#000} | idx: {idx:#00} | NumCollums: {numColumns:#00} | NumElements: {tiles.Count:#000}" );
        }


        private void DrawToolbar( Rect rect )
        {
        }
    }
}
