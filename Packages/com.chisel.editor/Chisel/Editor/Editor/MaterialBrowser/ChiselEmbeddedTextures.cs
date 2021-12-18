/* * * * * * * * * * * * * * * * * * * * * *
URL:     https://github.com/RadicalCSG/Chisel.Prototype
License: MIT (https://tldrlegal.com/license/mit-license)
Author:  Daniel Cornelius

Static class for textures commonly used within Chisel
* * * * * * * * * * * * * * * * * * * * * */


using System;
using UnityEditor;
using UnityEngine;


namespace Chisel.Editors.MaterialBrowser
{
    // color space adjusted texture utilities
    // $TODO: move this to a class with other embedded resources, e.g. "ChiselEmbeddedResources"
    internal static class ChiselEmbeddedTextures
    {
        private static Texture2D m_BlackTexture   = null;
        private static Texture2D m_UnityDarkBGTex = null;

        private static bool IsGamma => PlayerSettings.colorSpace == ColorSpace.Gamma;

        public static Texture2D BlackTexture => m_BlackTexture ??= GetColoredTexture( Color.black );
        public static Texture2D DarkBGTex    => m_UnityDarkBGTex ??= GetColoredTexture( new Color32( 40, 40, 40, 220 ) );

        public static Texture2D GetColoredTexture( Color color )
        {
            Texture2D tex = new Texture2D( 1, 1, TextureFormat.RGBA32, false, IsGamma );
            tex.SetPixel( 0, 0, color );
            tex.Apply();

            return tex;
        }
    }
}
