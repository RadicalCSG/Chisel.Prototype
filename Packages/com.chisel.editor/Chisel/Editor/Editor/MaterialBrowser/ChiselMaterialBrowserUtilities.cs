/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselMaterialBrowserCache.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

$TODO: Do we want to filter by label, too? it would allow user-ignored materials.
* * * * * * * * * * * * * * * * * * * * * */

namespace Chisel.Editors
{
    internal static class ChiselMaterialBrowserUtilities
    {
        // checks a path and returns true/false if a material is ignored or not
        public static bool IsValidEntry( ChiselMaterialBrowserTile tile )
        {
            // these are here to clean things up a little bit and make it easier to read

            bool PathContains( string path )
            {
                return tile.path.ToLower().Contains( path );
            }

            // checks for any shaders we want to exclude
            bool HasInvalidShader()
            {
                string shader = tile.shaderName.ToLower();

                string[] excludedShaders = new string[]
                {
                        "skybox/"
                };

                return shader.Contains( excludedShaders[0] );
            }

            string chiselPath = "packages/com.chisel.components/package resources/";

            string[] ignoredEntries = new string[]
            {
                    "packages/com.unity.searcher/",    // 0, we ignore this to get rid of the built-in font materials
                    "packages/com.unity.entities/",    // 1, we ignore this to get rid of the entities materials
                    $"{chiselPath}preview materials/", // 2, these are tool textures, so we are ignoring them
            };

            // if the path contains any of the ignored paths, then this will return false
            bool valid = !PathContains( ignoredEntries[0] )
                         && !PathContains( ignoredEntries[1] )
                         && !PathContains( ignoredEntries[2] )
                         && !HasInvalidShader(); // also check the shader

            return valid;
        }

        // step val by powers of two
        private static int GetPow2( int val )
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
    }
}
