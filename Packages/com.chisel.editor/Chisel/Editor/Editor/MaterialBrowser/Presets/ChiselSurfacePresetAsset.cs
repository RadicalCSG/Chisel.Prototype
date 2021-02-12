/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselSurfacePresetAsset.cs

License:    MIT (https://tldrlegal.com/license/mit-license)
Author:     Daniel Cornelius
Date:       2/12/2021 @ 1:08 AM

* * * * * * * * * * * * * * * * * * * * * */

using System.Collections.Generic;
using System.IO;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    [CreateAssetMenu( fileName = "New Chisel Surface Preset", menuName = "Chisel/Create New Surface Preset", order = 0 )]
    public class ChiselSurfacePresetAsset : ScriptableObject
    {
        public List<ChiselSurface> surfaces = new List<ChiselSurface>();

        public void CreateNew( string assetName, ChiselSurfacePresetAsset asset )
        {
            if( asset == null )
                asset = CreateInstance<ChiselSurfacePresetAsset>();

            string path = AssetDatabase.GetAssetPath( Selection.activeObject );

            if( path == string.Empty )
                path = "Assets";
            else if( Path.GetExtension( path ) != string.Empty )
                path = path.Replace( Path.GetFileName( AssetDatabase.GetAssetPath( Selection.activeObject ) ), string.Empty );

            string finalPath = AssetDatabase.GenerateUniqueAssetPath( $"{path}/{assetName}.asset" );

            AssetDatabase.CreateAsset( asset, finalPath );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}
