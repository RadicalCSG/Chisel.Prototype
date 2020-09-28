/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.MaterialBrowserTile.cs

License: MIT (https://tldrlegal.com/license/mit-license)
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using UnityEngine;

namespace Chisel.Editors
{
    internal struct MaterialBrowserTile
    {
        public string    path;
        public Material  material;
        public Texture2D preview;
        public string[]  labels;
    }
}
