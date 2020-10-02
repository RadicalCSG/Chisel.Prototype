/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.MaterialBrowserCache.cs

License:
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    [CreateAssetMenu( fileName = "MaterialBrowserCache", menuName = "Material Browser Cache", order = int.MaxValue )]
    internal class MaterialBrowserCache : ScriptableObject
    {
        [Serializable]
        public struct CachedThumbnail
        {
            public int    instanceID;
            public string name;
            public string data;

            public CachedThumbnail( string name, int instanceID, Texture2D data )
            {
                this.name       = name;
                this.instanceID = instanceID;
                this.data       = Convert.ToBase64String( data.EncodeToPNG() );
            }

            public Texture2D GetThumbnail()
            {
                Texture2D temp = EmbeddedTextures.TemporaryTexture;

                if( data.Length > 0 )
                {
                    temp.LoadImage( Convert.FromBase64String( data ) );
                    return temp;
                }

                return temp;
            }
        }

        private List<CachedThumbnail> storedPreviewTextures = new List<CachedThumbnail>();


        public void AddEntry( CachedThumbnail entry )
        {
            if( !TryGetEntry( entry.name, out CachedThumbnail e ) ) storedPreviewTextures.Add( entry );
        }

        public bool TryGetEntry( string materialName, out CachedThumbnail entry )
        {
            entry = storedPreviewTextures.Find( e => e.name == materialName );

            return !entry.Equals( null );
        }

        public Texture2D GetThumbnail( string materialName )
        {
            return TryGetEntry( materialName, out CachedThumbnail entry ) ? entry.GetThumbnail() : EmbeddedTextures.TemporaryTexture;
        }
    }
}
