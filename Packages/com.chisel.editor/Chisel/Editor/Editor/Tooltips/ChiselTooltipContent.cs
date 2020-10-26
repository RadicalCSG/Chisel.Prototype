/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselTooltipContent.cs

License:
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    public sealed class ChiselTooltipContent : IEquatable<ChiselTooltipContent>
    {
        public string Title    { get; set; }
        public string Summary  { get; set; }
        public string Shortcut { get; set; }

        private static GUIStyle m_TitleStyle;
        private static GUIStyle m_ShortcutStyle;
        private static GUIStyle m_ImageStyle;
        private static GUIStyle m_SummaryLabelStyle;

        private static readonly Color32 m_SeparatorColor = new Color32( 200, 200, 200, 255 );

        private const float MIN_WIDTH  = 128;
        private const float MAX_WIDTH  = 330;
        private const float MIN_HEIGHT = 0;

        internal static ChiselTooltipContent tempContent = new ChiselTooltipContent( "", "" );

        private static GUIStyle TitleStyle
        {
            get
            {
                if( m_TitleStyle == null )
                {
                    m_TitleStyle = new GUIStyle()
                    {
                            margin    = new RectOffset( 4, 4, 4, 4 ),
                            padding   = new RectOffset( 4, 4, 4, 4 ),
                            fontSize  = 14,
                            fontStyle = FontStyle.Bold,
                            normal    = { textColor = EditorGUIUtility.isProSkin ? new Color32( 200, 200, 200, 255 ) : new Color32( 0, 0, 0, 255 ) },
                            richText  = true
                    };
                }

                return m_TitleStyle;
            }
        }

        private static GUIStyle ShortcutStyle
        {
            get
            {
                if( m_ShortcutStyle == null )
                {
                    m_ShortcutStyle = new GUIStyle()
                    {
                            fontSize  = 14,
                            fontStyle = FontStyle.Normal,
                            normal    = { textColor = EditorGUIUtility.isProSkin ? new Color32( 140, 140, 140, 255 ) : new Color32( 60, 60, 60, 255 ) }
                    };
                }

                return m_ShortcutStyle;
            }
        }

        private static GUIStyle SummaryLabelStyle
        {
            get
            {
                if( m_SummaryLabelStyle == null )
                {
                    m_SummaryLabelStyle = new GUIStyle( "WordWrapLabel" )
                    {
                            richText = true
                    };
                }

                return m_SummaryLabelStyle;
            }
        }

        private static GUIStyle ImageStyle
        {
            get
            {
                if( m_ImageStyle == null )
                {
                    m_ImageStyle = new GUIStyle()
                    {
                            border        = new RectOffset( 0, 0, 0, 0 ),
                            margin        = new RectOffset( 0, 0, 0, 0 ),
                            contentOffset = Vector2.zero,
                            imagePosition = ImagePosition.ImageOnly,
                            fixedHeight   = 128,
                            fixedWidth    = 128
                    };
                }

                return m_ImageStyle;
            }
        }

        private float m_Height;
        public  float Height => m_Height;

        public ChiselTooltipContent( string title, string summary, string shortcut = "" )
        {
            Title    = title;
            Summary  = summary;
            Shortcut = shortcut;
        }

        internal Vector2 CalcSize()
        {
            const float pad         = 8;
            Vector2     total       = new Vector2( MIN_WIDTH, MIN_HEIGHT );
            bool        hasTitle    = !string.IsNullOrEmpty( Title );
            bool        hasSummary  = !string.IsNullOrEmpty( Summary );
            bool        hasShortcut = !string.IsNullOrEmpty( Shortcut );

            if( hasTitle )
            {
                Vector2 ns = TitleStyle.CalcSize( TempContent( Title ) );

                if( hasShortcut )
                {
                    ns.x += EditorStyles.boldLabel.CalcSize( TempContent( Shortcut ) ).x;
                    ns.y += 28;
                }

                total.x += Mathf.Max( ns.x, 256 );
                total.y += ns.y;
            }

            if( hasSummary )
            {
                if( !hasTitle )
                {
                    Vector2 sumSize = EditorStyles.wordWrappedLabel.CalcSize( TempContent( Summary ) );
                    total.x = Mathf.Min( sumSize.x, MAX_WIDTH );
                }

                float summaryHeight = EditorStyles.wordWrappedLabel.CalcHeight( TempContent( Summary ), total.x );
                total.y += summaryHeight;
            }

            if( hasTitle && hasSummary )
                total.y += 16;

            total.x += pad;
            total.y += pad;

            m_Height = total.y;
            return total;
        }

        private GUIContent m_TempContent;

        private GUIContent TempContent( string text, string tooltip = null, Texture2D icon = null )
        {
            if( m_TempContent == null ) m_TempContent = new GUIContent();

            m_TempContent.text    = text;
            m_TempContent.tooltip = tooltip;
            m_TempContent.image   = icon;

            return m_TempContent;
        }

        internal void Draw()
        {
            if( !string.IsNullOrEmpty( Title ) )
            {
                GUILayout.BeginHorizontal( "box" );
                {
                    GUILayout.Label( Title, TitleStyle );
                }
                GUILayout.EndHorizontal();
            }

            if( !string.IsNullOrEmpty( Summary ) )
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Space( 12 );
                    GUILayout.Label( Summary, SummaryLabelStyle );
                }
                GUILayout.EndHorizontal();
                GUILayout.Space( 4 );

                if( !string.IsNullOrEmpty( Shortcut ) )
                {
                    GUILayout.BeginHorizontal( "box" );
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label( Shortcut, ShortcutStyle );
                        GUILayout.Space( 8 );
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        /// <inheritdoc />
        public bool Equals( ChiselTooltipContent other )
        {
            return other != null && other.Title != null && other.Title.Equals( Title );
        }

        /// <inheritdoc />
        public override bool Equals( object obj )
        {
            return obj is ChiselTooltipContent && ( (ChiselTooltipContent) obj ).Title.Equals( Title );
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Title.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Title;
        }
    }
}
