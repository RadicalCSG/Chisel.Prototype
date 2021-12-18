// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
// Author:             Daniel Cornelius (NukeAndBeans)
// Contact:            Twitter @nukeandbeans, Discord Nuke#3681
// License:
// Date/Time:          12-17-2021 @ 7:35 PM
// 
// Description:
// $TODO: optimize _previewGUIMethod - see https://stackoverflow.com/a/7999698/13224034
// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *


using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace Chisel.Editors.MaterialBrowser
{
    public class BrowserItem<T> : Object, IDisposable where T : Object
    {
        public readonly string   path;
        public readonly string   guid;
        public readonly string   shaderName;
        public readonly string   assetName;
        public readonly string[] labels;
        public readonly int      instID;

        public Vector2 PreviewSize { get; set; }

        private Texture _Preview;

        /// <summary>
        /// Retrieves the asset preview for this item.
        /// <para>Will return <see cref="AssetPreview.GetMiniTypeThumbnail"/> until the preview is loaded.</para>
        /// </summary>
        public Texture Preview
        {
            get
            {
                if( AssetPreview.IsLoadingAssetPreview( instID ) )
                    return AssetPreview.GetMiniTypeThumbnail( typeof(T) );

                if( _Preview == null )
                    _Preview = GetAssetPreviewFromGUID( guid );

                return _Preview;
            }
        }

        public BrowserItem( string guid )
        {
            path = AssetDatabase.GUIDToAssetPath( guid );
            T asset = AssetDatabase.LoadAssetAtPath<T>( path );

            this.guid = guid;
            instID    = asset.GetInstanceID();
            labels    = AssetDatabase.GetLabels( asset );

            if( asset is Material m )
            {
                shaderName = m.shader.name;
            }
            else
                shaderName = string.Empty;

            assetName = asset.name;

            _Preview    = null;
            PreviewSize = new Vector2( 128, 128 );

            _previewGUIDMethod = typeof(AssetPreview).GetMethod
            (
                "GetAssetPreviewFromGUID",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null,
                new[]
                {
                    typeof(string)
                },
                null
            );

            asset = null;
            EditorUtility.UnloadUnusedAssetsImmediate();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Preview = null;
        }

        private MethodInfo _previewGUIDMethod;

        private Texture2D GetAssetPreviewFromGUID( string assetGUID )
        {
            _previewGUIDMethod ??= typeof(AssetPreview).GetMethod
            (
                "GetAssetPreviewFromGUID",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null,
                new[]
                {
                    typeof(string)
                },
                null
            );

            return (Texture2D) _previewGUIDMethod?.Invoke
            (
                null,
                new object[]
                {
                    assetGUID
                }
            );
        }
    }
}
