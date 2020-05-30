using Chisel.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Components
{
    public static class TransformExtensions
    {
        public static GameObject FindChildByName(this Transform transform, string name)
        {
            if (!transform)
                return null;
            for (int i = 0, childCount = transform.childCount; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == name)
                    return child.gameObject;
            }
            return null;
        }

        public static GameObject FindChildByName(this GameObject gameObject, string name)
        {
            return (gameObject == null) ? null : gameObject.transform.FindChildByName(name);
        }

        public static GameObject FindChildByName(this Component component, string name)
        {
            return (component == null) ? null : component.transform.FindChildByName(name);
        }
    }
}
