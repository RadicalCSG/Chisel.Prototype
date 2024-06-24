/* * * * * * * * * * * * * * * * * * * * * *
URL:     https://github.com/RadicalCSG/Chisel.Prototype
License: MIT (https://tldrlegal.com/license/mit-license)
Author:  Daniel Cornelius

Various utility methods used throughout ChiselMaterialBrowser
and its various classes
* * * * * * * * * * * * * * * * * * * * * */


using System;
using System.Collections.Generic;
using System.Reflection;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace Chisel.Editors.MaterialBrowser
{
    internal static class ChiselMaterialBrowserUtilities
    {
        private static readonly string[] ignored = new[]
        {
            "packages/com.unity.searcher/",                                        // 0, we ignore this to get rid of the built-in font materials
            "packages/com.unity.entities/",                                        // 1, we ignore this to get rid of the entities materials
            "packages/com.chisel.components/package resources/preview materials/", // 2, these are tool textures, so we are ignoring them
            "font material",                                                       // 3, ignore font materials
            "skybox/",                                                             // 4, ignore skybox shader
        };

        // checks a path and returns true/false if a material is ignored or not
        public static bool IsValidEntry<T>( PreviewTile<T> tile ) where T : UnityEngine.Object
        {
            // method to add functionality that exists in .net but not in unity (string.Contains(string, StringComparison)).
            bool StringContains( string searchTerm, string source, StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase )
            {
                return source.IndexOf( searchTerm, stringComparison ) >= 0;
            }

            foreach( string s in ignored )
            {
                if( StringContains( s, tile.shaderName ) ) return false;
                if( StringContains( s, tile.path ) ) return false;
            }

            return true;
        }

        // $TODO: Is this needed anymore?
        // step val by powers of two
        public static int GetPow2( int val )
        {
            val--;
            val |= val >> 1;
            val |= val >> 2;
            val |= val >> 4;
            val |= val >> 8;
            val |= val >> 16;
            val++;

            return val;
        }

        // gets all materials and the labels on them in the project, compares them against a filter,
        // and then adds them to the list of materials to be used in this window
        internal static void GetMaterials
        (
            ref List<PreviewTile<Material>> materials,
            ref List<PreviewTile<Material>> usedMaterials,
            ref List<string>                           labels,
            ref List<ChiselModel>                      models,
            bool                                       usingLabel,
            string                                     searchLabel = "",
            string                                     searchText  = ""
        )
        {
            if( materials == null || usedMaterials == null || labels == null || models == null )
                return;

            if( usingLabel && searchLabel == string.Empty )
                Debug.LogError( $"usingLabel set to true, but no search term was given. This may give undesired results." );

            materials.ForEach
            (
                e =>
                {
                    e.Dispose();
                }
            );

            materials.Clear();

            // exclude the label search tag if we arent searching for a specific label right now
            string search = usingLabel ? $"l:{searchLabel} {searchText}" : $"{searchText}";

            string[] guids = AssetDatabase.FindAssets( $"t:Material {search}" );

            // assemble preview tiles
            foreach( string id in guids )
            {
                PreviewTile<Material> browserTile = new( id );

                if( labels != null )
                {
                    // add any used labels we arent currently storing
                    foreach( string label in browserTile.labels )
                    {
                        if( !labels.Contains( label ) )
                            labels.Add( label );
                    }
                }

                // check each entry against a filter to exclude certain entries
                if( IsValidEntry( browserTile ) )
                {
                    materials.Add( browserTile );
                }
            }

            AssetPreview.SetPreviewTextureCacheSize( materials.Count + 1 );

            models.AddRange( Object.FindObjectsOfType<ChiselModel>() );

            PopulateUsedMaterials( ref usedMaterials, ref models, searchLabel, searchText );
        }

        private static void PopulateUsedMaterials( ref List<PreviewTile<Material>> tiles, ref List<ChiselModel> models, string searchLabel, string searchText )
        {
            tiles.ForEach
            (
                e =>
                {
                    e.Dispose();
                }
            );

            tiles.Clear();

            foreach( ChiselModel m in models )
            {
                MeshRenderer[] renderMeshes = m.generated.meshRenderers;

                if( renderMeshes.Length > 0 )
                    foreach( MeshRenderer mesh in renderMeshes )
                    {
                        foreach( Material mat in mesh.sharedMaterials )
                        {
                            if( mat != null )
                                if( mat.name.Contains( searchText ) || MaterialContainsLabel( searchLabel, mat ) )
                                {
                                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier( mat, out string guid, out long id );

                                    tiles.Add( new PreviewTile<Material>( guid ) );
                                }
                        }
                    }
            }
        }

        private static bool MaterialContainsLabel( string label, Material material )
        {
            string[] labels = AssetDatabase.GetLabels( material );

            if( labels.Length > 0 )
                foreach( string l in labels )
                {
                    if( l.Contains( label ) ) return true;
                }

            return false;
        }

        public static void SelectMaterialInScene( string name )
        {
            MeshRenderer[] objects = Object.FindObjectsOfType<MeshRenderer>();

            if( objects.Length > 0 )
                foreach( MeshRenderer r in objects )
                {
                    if( r.sharedMaterials.Length > 0 )
                        foreach( Material m in r.sharedMaterials )
                        {
                            if( m != null )
                                if( m.name.Contains( name ) )
                                {
                                    EditorGUIUtility.PingObject( r.gameObject );
                                    Selection.activeObject = r.gameObject;
                                }
                        }
                }
        }

        private static MethodInfo m_GetAssetPreviewMethod;

        // $TODO: can this be moved to the reflection utilities class?
        public static Texture2D GetAssetPreviewFromGUID( string guid )
        {
            m_GetAssetPreviewMethod ??= typeof(AssetPreview).GetMethod
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

            return m_GetAssetPreviewMethod.Invoke
            (
                null,
                new object[]
                {
                    guid
                }
            ) as Texture2D;
        }
    }
}
