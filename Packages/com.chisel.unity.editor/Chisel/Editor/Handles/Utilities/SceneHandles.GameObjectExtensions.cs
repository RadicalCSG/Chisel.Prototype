using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public sealed partial class GameObjectExtensions
    {
        public static bool ContainsStatic(GameObject[] objects)
        {
            if (objects == null || objects.Length == 0)
                return false;
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].isStatic)
                    return true;
            }
            return false;
        }
    }
}