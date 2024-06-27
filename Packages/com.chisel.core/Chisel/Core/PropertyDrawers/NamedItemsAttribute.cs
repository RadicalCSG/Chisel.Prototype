using UnityEngine;

namespace Chisel.Core
{
    public class NamedItemsAttribute : PropertyAttribute
    {
        public string   overflow        = "Item {0}";
        public string[] surfaceNames    = null;
        public int      fixedSize       = 0;

        public NamedItemsAttribute() { }
        public NamedItemsAttribute(params string[] items)
        {
            surfaceNames = items; 
        }
    }
}