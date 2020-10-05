using System;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal static class ChiselEmbeddedTextures
    {
        private static string m_TempTexB64 =
                @"iVBORw0KGgoAAAANSUhE
                UgAAAIAAAACACAYAAADDPmHLAAACQ0lEQVR4Ae3bwW
                3FMAxEQRetLlz0D3QwsEgLbw5BeCXsHZGK87zv+yv/
                nHN+5Z+n/PBv7+WHf3v3AhDAEVBWgAAEIAABwptA+e
                EbAm0BtgAChPl3D+AewEWQm0BXwf4WEL4LcBEUfvjW
                QGugNdAaaA1MbwJmADOAPwaVjwECEIAABAgPguWHf3
                t3BDgCHAFlBQhAAAIQwBCYvQ10BDgCHAGOAEeAI6D6
                aVg5/bd3M4AZwAxQVoAABCAAAWwBtgBbQPMfRMwAZg
                AzgBnADGAGMAOYAZL/JFrm//ZuCDQEGgLLChCAAAQg
                gDXQGmgNtAZaA4PzgCEw+NB35vECeAFsAZuIWk0AAh
                CglvrtlwAEIMAmolYTgAAEqKV++yUAAQiwiajVBCAA
                AWqp334JQAACbCJq9VNr+H+/1Q9hvr69AOHP4e5L4A
                XwAjS/hfuOgo/C6m8CEIAA1fSbAc5Jfgm9L7wjwBHg
                CNhE1GoCEIAAtdRvvwQgAAE2EbWaAAQgQC312y8BCE
                CATUStJgABCFBL/fZLAAIQYBNRqwlAAALUUr/9EoAA
                BNhE1GoCEIAAtdRvvwQgAAE2EbWaAAQgQC312y8BCE
                CATUStJgABCFBL/fZLAAIQYBNRqwlAAALUUr/9EoAA
                BNhE1GoCEIAAtdRvvwQgAAE2EbWaAAQgQC312y8BCE
                CATUStJgABCFBL/fZLAAIQYBNRqwlAAALUUr/9EoAA
                BNhE1GoCEIAAtdRvvwQgAAE2EbWaAAQgQC312y8B4g
                L8ARmZaVKQSMMPAAAAAElFTkSuQmCC";

        private static Texture2D m_TemporaryTexture = null;

        public static Texture2D TemporaryTexture
        {
            get
            {
                if( m_TemporaryTexture == null )
                {
                    m_TemporaryTexture = new Texture2D( 128, 128, TextureFormat.RGBA32, false, PlayerSettings.colorSpace == ColorSpace.Linear );

                    m_TemporaryTexture.LoadImage( Convert.FromBase64String( m_TempTexB64 ) );

                    m_TemporaryTexture.Apply();
                }

                return m_TemporaryTexture;
            }
        }
    }
}
