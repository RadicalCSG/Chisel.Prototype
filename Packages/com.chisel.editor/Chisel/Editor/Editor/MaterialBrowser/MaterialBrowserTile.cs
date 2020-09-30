/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.MaterialBrowserTile.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    internal static class EmbeddedTextures
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

    internal class MaterialBrowserTile
    {
        public string   path;
        public string   shaderName;
        public string   materialName;
        public string[] labels;

        public Texture2D Preview
        {
            get
            {
                if( m_Preview == null ) { return EmbeddedTextures.TemporaryTexture; }

                return m_Preview;
            }
        }

        public int InstanceID => m_InstanceID;

        private int       m_InstanceID = 0;
        private Texture2D m_Preview    = null;

        public MaterialBrowserTile( string instID )
        {
            path = AssetDatabase.GUIDToAssetPath( instID );

            Material m = AssetDatabase.LoadAssetAtPath<Material>( path );

            m_InstanceID = m.GetInstanceID();
            labels       = AssetDatabase.GetLabels( m );
            shaderName   = m.shader.name;
            materialName = m.name;

            if( !materialName.Contains( "Font Material" ) ) // dont even consider font materials
                DelayedThumbnailRenderHandler.Add( materialName, () => !AssetPreview.IsLoadingAssetPreview( m_InstanceID ), () => { m_Preview = AssetPreview.GetAssetPreview( m ); } );
        }

        public void Draw( RectOffset offset )
        {
        }
    }

    // slightly modified from http://answers.unity.com/answers/243291/view.html
    internal static class DelayedThumbnailRenderHandler
    {
        private class RenderJob
        {
            // used for debug
            public string taskName = "";

            public Func<bool> Completed    { get; }
            public Action     ContinueWith { get; }

            public RenderJob( string name, Func<bool> completed, Action continueWith )
            {
                Completed    = completed;
                ContinueWith = continueWith;
                taskName     = name;
            }
        }

        private static readonly List<RenderJob> jobs = new List<RenderJob>();

        public static void Add( string name, Func<bool> completed, Action continueWith )
        {
            if( !jobs.Any() ) EditorApplication.update += Update;
            jobs.Add( new RenderJob( name, completed, continueWith ) );
        }

        private static void Update()
        {
            int i = 0;
            for( i = 0; i >= 0; i-- )
            {
                if( jobs[i].Completed() )
                {
                    //Debug.Log( $"Completed thumbnail render task for [{jobs[i].taskName}]" );

                    jobs[i].ContinueWith();
                    jobs.RemoveAt( i );
                }
            }

            if( !jobs.Any() ) EditorApplication.update -= Update;
        }
    }
}
