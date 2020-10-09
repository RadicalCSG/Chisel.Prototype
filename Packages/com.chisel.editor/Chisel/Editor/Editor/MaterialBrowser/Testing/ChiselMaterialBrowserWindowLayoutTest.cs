/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserWindowLayoutTest.cs

License:
Author: Daniel Cornelius

$TODO: DELETE ME WHEN DONE

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    internal sealed class ChiselMaterialBrowserWindowTest : EditorWindow
    {
        private const int NUM_TILES      = 8 * 200; // 8 collumns, 50 rows
        private const int TOOLBAR_WIDTH  = 140;
        private const int THUMB_SIZE     = 64;
        private const int TOOLBAR_HEIGHT = 22;

        private Vector2 m_ScrollPos = Vector2.zero;

        private static List<Texture2D> tiles = new List<Texture2D>();

        private static bool isRenderingNoise = false;

        private VisualElement Root => rootVisualElement;

        private Box     m_TopToolbar;
        private Toolbar m_TabBar;

        [MenuItem( "Window/Chisel/Material Browser Test" )]
        private static void Init()
        {
            ChiselMaterialBrowserWindowTest window = EditorWindow.GetWindow<ChiselMaterialBrowserWindowTest>( false, "Material Browser" );
            window.maxSize = new Vector2( 690, 2000 );
            window.minSize = new Vector2( 420, 310 );


            for( int i = 0; i < NUM_TILES; i++ )
            {
                if( !isRenderingNoise )
                    tiles.Add( GetRandNoiseTex() );
            }
        }

        private void OnEnable()
        {
            // $TODO: Convert to UI Toolkit
            this.GetRootElement();

            m_TopToolbar = Root.AddBox( new Rect( position.width, 0, TOOLBAR_HEIGHT, position.height ), new Color( 0.15f, 0.15f, 0.15f, 1f ), -1 );

            m_TabBar = m_TopToolbar.AddToolbar( new Rect( 0, 0, position.width, TOOLBAR_HEIGHT ) );
            m_TopToolbar.SetRotation( 90 );

            for( int i = 0; i < 4; i++ ) { m_TabBar.AddButton( $"Tab {i}", new Vector2Int( 100, 22 ), 0 ); }

            if( tiles.Count < 1 )
            {
                for( int i = 0; i < NUM_TILES; i++ )
                {
                    if( !isRenderingNoise )
                        tiles.Add( GetRandNoiseTex() );
                }
            }
        }

        private static Texture2D GetRandNoiseTex()
        {
            isRenderingNoise = true;
            Texture2D noiseTex = new Texture2D( THUMB_SIZE, THUMB_SIZE, TextureFormat.RGB24, false, PlayerSettings.colorSpace == ColorSpace.Linear );
            Color[]   pix      = new Color[THUMB_SIZE * THUMB_SIZE];

            for( int y = 0; y < THUMB_SIZE; y++ )
            {
                for( int x = 0; x < THUMB_SIZE; x++ )
                {
                    float xCoord = 0 + x / THUMB_SIZE;
                    float yCoord = 0 + y / THUMB_SIZE;
                    float sample = Mathf.Clamp01( 1 - Mathf.PerlinNoise( xCoord, yCoord ) * Random.value );
                    pix[y * THUMB_SIZE + x] = new Color( sample, sample, sample );
                }
            }

            noiseTex.SetPixels( pix );
            noiseTex.Apply();

            isRenderingNoise = false;
            return noiseTex;
        }

        private void OnGUI()
        {
            m_TopToolbar?.SetPosition( new Vector2( position.width,                                       0 ) );
            m_TopToolbar?.SetSize( new Vector2( position.height,                                          TOOLBAR_HEIGHT ) );
            m_TabBar.SetSize( new Vector2( ( position.height > ( 100 * 4 ) ) ? 100 * 4 : position.height, TOOLBAR_HEIGHT) );

            Rect rect = this.position;

            //DrawTabBar( rect );
            //DrawLabelAndTileArea( rect );
            //DrawToolbar( rect );
            //DrawFooter( rect );
        }

        private float scrollViewHeight;
        private float xOffset = 2;

        private void DrawLabelAndTileArea( Rect rect )
        {
            GUI.Box( new Rect( 0, TOOLBAR_HEIGHT, rect.width - TOOLBAR_WIDTH, rect.height - ( TOOLBAR_HEIGHT * 2 ) ), "", "GameViewBackground" );

            int idx        = 0;
            int numColumns = (int) ( ( rect.width - ( TOOLBAR_WIDTH + 10 ) ) / THUMB_SIZE );

            m_ScrollPos = GUI.BeginScrollView( new Rect( 0, 28, rect.width - ( TOOLBAR_WIDTH + 2 ),  rect.height - 52 ), m_ScrollPos,
                                               new Rect( 0, 28, rect.width - ( TOOLBAR_WIDTH + 16 ), scrollViewHeight ) );
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
                            xOffset = ( THUMB_SIZE + 3 ) * x;

                        if( idx == tiles.Count ) break;

                        GUI.Box( new Rect( xOffset, yOffset, THUMB_SIZE, THUMB_SIZE ), tiles[idx] ); // make a custom guistyle for these, or make a method to do a proper tile

                        idx++;
                    }

                    row     += 1;
                    xOffset =  2;
                    if( row > 0 )
                    {
                        yOffset = TOOLBAR_HEIGHT + ( ( THUMB_SIZE + 3 ) * row );

                        scrollViewHeight = yOffset;
                    }

                    // end horizontal
                }
            }
            GUI.EndScrollView();
        }


        private int tabSel = 0;

        private void DrawTabBar( Rect rect )
        {
            Matrix4x4 guiMatrix = GUI.matrix;

            GUI.Box( new Rect( rect.width - TOOLBAR_HEIGHT, 0, TOOLBAR_HEIGHT, rect.height ), "", "DockHeader" );
            //GUI.EndClip();

            RotateAroundPivot( 90, rect.center, rect );
            //GUIUtility.RotateAroundPivot( 90, new Vector2( rect.width, rect.height -TOOLBAR_HEIGHT ) );
            //GUI.matrix = Matrix4x4.identity;
            //GUI.matrix = new Matrix4x4( new Vector4( 0, 1, 0, 0 ), new Vector4( -1, 0, 0, 0 ), new Vector4( 0, 0, 1, 0 ), new Vector4( 0, 0, 0, 1 ) );

            int offset = TOOLBAR_HEIGHT;
            for( int i = 0; i < 4; i++ ) { GUI.Button( new Rect( TOOLBAR_HEIGHT, 0, TOOLBAR_HEIGHT, rect.height ), $"BUTTON {i}" ); }

            GUI.matrix = guiMatrix;
            //GUI.BeginClip( rect );
        }

        private void DrawFooter( Rect rect )
        {
            GUI.Label( new Rect( 0, rect.height - TOOLBAR_HEIGHT, rect.width, TOOLBAR_HEIGHT ), $"xMax: {rect.xMax} | yMax: {rect.yMax} | xMin: {rect.xMin} | yMin: {rect.yMin}" );
        }

        private void DrawToolbar( Rect rect )
        {
            GUI.Label( new Rect( 0, 0, rect.width, TOOLBAR_HEIGHT ), $"WindowSize: {position}" );
        }

        private void RotateAroundPivot( float angle, Vector2 pivot, Rect windowRect )
        {
            Matrix4x4 matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Vector2 vector = (Vector2) GUI.matrix.MultiplyPoint3x4( pivot ) + windowRect.position;

            GUI.matrix = ( Matrix4x4.TRS( vector, Quaternion.Euler( 0, 0, angle ), Vector3.one ) * Matrix4x4.TRS( -vector, Quaternion.identity, Vector3.one ) ) * matrix;
        }
    }
}
