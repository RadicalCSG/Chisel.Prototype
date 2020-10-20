/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserTile.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chisel.Editors
{
    internal class ChiselMaterialBrowserTile
    {
        public readonly string   path;
        public readonly string   guid;
        public readonly string   shaderName;
        public readonly string   materialName;
        public readonly Vector2  mainTexSize;
        public readonly Vector2  uvOffset;
        public readonly Vector2  uvScale;
        public readonly string   albedoName;
        public readonly string[] labels;

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

        public ChiselMaterialBrowserTile( string instID, ref ChiselMaterialBrowserCache cache )
        {
            path = AssetDatabase.GUIDToAssetPath( instID );

            Material m = AssetDatabase.LoadAssetAtPath<Material>( path );

            guid         = instID;
            m_InstanceID = m.GetInstanceID();
            labels       = AssetDatabase.GetLabels( m );
            shaderName   = m.shader.name;
            materialName = m.name;
            uvOffset     = m.mainTextureOffset;
            uvScale      = m.mainTextureScale;

            for( int i = 0; i < ShaderUtil.GetPropertyCount( m.shader ); i++ )
            {
                if( ShaderUtil.GetPropertyType( m.shader, i ) == ShaderUtil.ShaderPropertyType.TexEnv )
                {
                    string propName = ShaderUtil.GetPropertyName( m.shader, i );

                    if( propName == "_MainTex" )
                    {
                        Texture t = m.GetTexture( propName );

                        if( t != null )
                        {
                            albedoName  = t.name;
                            mainTexSize = new Vector2( t.width, t.height );
                        }
                    }
                }
            }

            if( m_Preview == null )
            {
                m_Preview = cache.GetThumbnail( m_InstanceID );

                if( !materialName.Contains( "Font Material" ) ) // dont even consider font materials
                    ChiselMaterialThumbnailRenderer.Add( materialName, () => !AssetPreview.IsLoadingAssetPreviews(), () => { m_Preview = AssetPreview.GetAssetPreview( m ); } );

                if( ChiselMaterialBrowserUtilities.IsValidEntry( this ) )
                {
                    ChiselMaterialBrowserCache.CachedThumbnail thumbnail = new ChiselMaterialBrowserCache.CachedThumbnail()
                    {
                            name = materialName,
                            data = Convert.ToBase64String( m_Preview.EncodeToPNG() )
                    };
                    thumbnail.hashCode = thumbnail.GetHashCode();
                    cache.AddEntry( thumbnail );
                }
            }
        }

        // $TODO: Use GUI instead of GUILayout
        public void Draw( Rect offset )
        {
            //if(m_Preview == null) Debug.LogError( $"Preview thumbnail [{materialName}] null" );

            if( Preview != null )
            {
                GUIContent previewContent = new GUIContent( Preview, $"{materialName}\nIn: [{path}]" );

                GUILayout.Box( previewContent, GUILayout.Height( offset.height ), GUILayout.Width( offset.width ) );
            }
        }
    }
}
