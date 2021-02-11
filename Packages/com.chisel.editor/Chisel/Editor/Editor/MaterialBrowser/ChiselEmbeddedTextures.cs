using System;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal static class ChiselEmbeddedTextures
    {
        private const string m_TempTexB64 =
                @"iVBORw0KGgoAAAANSUhEUgAAAI
                AAAACACAYAAADDPmHLAAACRElEQVR4Ae3bwW3FMAxEQRfhAt
                R/kz/QwcAiLbw5BOGVsHdEKs7zvu+v/HPO+ZV/nvLDv72XH/
                7t3QtAAEdAWQECEIAABAhvAuWHbwi0BdgCCBDm3z2AewAXQW
                4CXQX7W0D4LsBFUPjhWwOtgdZAa6A1ML0JmAHMAP4YVD4GCE
                AAAhAgPAiWH/7t3RHgCHAElBUgAAEIQABDYPY20BHgCHAEOA
                IcAY6A6qdh5fTf3s0AZgAzQFkBAhCAAASwBdgCbAHNfxAxA5
                gBzABmADOAGcAMYAZI/pNomf/buyHQEGgILCtAAAIQgADWQG
                ugNdAaaA0MzgOGwOBD35nHC+AFsAVsImo1AQhAgFrqt18CEI
                AAm4haTQACEKCW+u2XAAQgwCaiVhOAAASopX77JQABCLCJqN
                VPreH//VY/hPn69gKEP4e7L4EXwAvQ/BbuOwo+Cqu/CUAAAl
                TTbwY4J/kl9L7wjgBHgCNgE1GrCUAAAtRSv/0SgAAE2ETUag
                IQgAC11G+/BCAAATYRtZoABCBALfXbLwEIQIBNRK0mAAEIUE
                v99ksAAhBgE1GrCUAAAtRSv/0SgAAE2ETUagIQgAC11G+/BC
                AAATYRtZoABCBALfXbLwEIQIBNRK0mAAEIUEv99ksAAhBgE1
                GrCUAAAtRSv/0SgAAE2ETUagIQgAC11G+/BCAAATYRtZoABC
                BALfXbLwEIQIBNRK0mAAEIUEv99ksAAhBgE1GrCUAAAtRSv/
                0SgAAE2ETUagIQgAC11G+/BIgL8AfYVETeKhvzFQAAAABJRU
                5ErkJggg==";

        private static Texture2D m_TemporaryTexture = null;
        private static Texture2D m_BlackTexture     = null;
        private static Texture2D m_UnityDarkBGTex   = null;

        private static bool IsGamma => PlayerSettings.colorSpace == ColorSpace.Gamma;

        public static Texture2D TemporaryTexture
        {
            get
            {
                if( m_TemporaryTexture == null )
                {
                    m_TemporaryTexture = new Texture2D( 128, 128, TextureFormat.RGBA32, false, IsGamma );

                    m_TemporaryTexture.LoadImage( Convert.FromBase64String( m_TempTexB64 ) );

                    m_TemporaryTexture.Apply();
                }

                return m_TemporaryTexture;
            }
        }

        public static Texture2D BlackTexture
        {
            get
            {
                if( m_BlackTexture == null )
                {
                    m_BlackTexture = new Texture2D( 1, 1, TextureFormat.RGBA32, false, IsGamma );
                    m_BlackTexture.SetPixel( 0, 0, Color.black );
                    m_BlackTexture.Apply();
                }

                return m_BlackTexture;
            }
        }

        public static Texture2D DarkBGTex
        {
            get
            {
                if( m_UnityDarkBGTex == null )
                {
                    m_UnityDarkBGTex = new Texture2D( 1, 1, TextureFormat.RGBA32, false, IsGamma );
                    m_UnityDarkBGTex.SetPixel( 0, 0, new Color32( 40, 40, 40, 220 ) );
                    m_UnityDarkBGTex.Apply();
                }

                return m_UnityDarkBGTex;
            }
        }

        public static Texture2D GetColoredTexture( Color color )
        {
            Texture2D tex = new Texture2D( 1, 1, TextureFormat.RGBA32, false, IsGamma );
            tex.SetPixel( 0, 0, color );
            tex.Apply();

            return tex;
        }
    }
}
