using System;
using System.Collections.Generic;

using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    // TODO: rename
    public sealed class ChiselComponentFactory
    {
        public static T AddComponent<T>(GameObject gameObject) where T : ChiselNode
        {
            // TODO: ensure we're creating this in the active scene
            // TODO: handle scene being locked by version control

            if (!gameObject)
                return null;

            bool prevActive = gameObject.activeSelf;
            if (prevActive)
                gameObject.SetActive(false);
            try
            {
                var brushTransform = gameObject.transform;
#if UNITY_EDITOR
                return UnityEditor.Undo.AddComponent<T>(gameObject);
#else
                return gameObject.AddComponent<T>();
#endif
            }
            finally
            {
                if (prevActive)
                    gameObject.SetActive(prevActive);
            }
        }

        public static void SetTransform<T>(T component, Transform parent, Matrix4x4 trsMatrix) where T : ChiselNode
        {
            if (!component)
                return;
            SetTransform(component.transform, parent, trsMatrix);
        }

        public static void SetTransform<T>(T component, Matrix4x4 trsMatrix) where T : ChiselNode
        {
            if (!component)
                return;
            SetTransform(component.transform, trsMatrix);
        }

        public static void SetTransform(Transform transform, Matrix4x4 trsMatrix)
        {
            if (!transform)
                return;
            SetTransform(transform, (transform == null) ? null : transform.parent, trsMatrix);
        }

        public static void SetTransform(Transform transform, Transform parent, Matrix4x4 trsMatrix)
        {
            if (!transform)
                return;

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(transform, "Move child node to given position");
#endif
            if (parent)
                transform.SetLocal(parent.worldToLocalMatrix * trsMatrix);
            else
                transform.SetLocal(trsMatrix);
        }

        static Dictionary<string, string> cleanedupTypenames = new();

        public static T Create<T>(string name, Transform parent, Matrix4x4 trsMatrix) where T : ChiselNode
        {
            // TODO: ensure we're creating this in the active scene
            // TODO: handle scene being locked by version control

            if (string.IsNullOrEmpty(name))
			{
				string orgTypename = typeof(T).Name;
                if (!cleanedupTypenames.TryGetValue(orgTypename, out var typename))
                {
                    typename = orgTypename;
                    if (typename.StartsWith("Chisel"))
                        typename = typename.Substring("Chisel".Length);
                    if (typename.EndsWith("Component"))
                        typename = typename.Substring(0, typename.Length - "Component".Length);
                    cleanedupTypenames[orgTypename] = typename;
                }
#if UNITY_EDITOR
				name = UnityEditor.GameObjectUtility.GetUniqueNameForSibling(parent, typename);
#else
                name = typename;
#endif
			}

			var newGameObject = new GameObject(name);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(newGameObject, "Created " + name);
#endif
            newGameObject.SetActive(false);
            try
            {
                var brushTransform = newGameObject.transform;
#if UNITY_EDITOR
                if (parent)
                    UnityEditor.Undo.SetTransformParent(brushTransform, parent, "Move child node underneath parent composite");
                UnityEditor.Undo.RecordObject(brushTransform, "Move child node to given position");
#else
                if (parent)
                    brushTransform.SetParent(parent, false);
#endif
                if (parent)
                    brushTransform.SetLocal(parent.worldToLocalMatrix * trsMatrix);
                else
                    brushTransform.SetLocal(trsMatrix);

#if UNITY_EDITOR
                return UnityEditor.Undo.AddComponent<T>(newGameObject);
#else
                return newGameObject.AddComponent<T>();
#endif
            }
            finally
            {
                newGameObject.SetActive(true);
            }
        }
         
        
        public static Component Create(Type type, string name, Transform parent, Matrix4x4 trsMatrix)
        {
            // TODO: ensure we're creating this in the active scene
            // TODO: handle scene being locked by version control

            if (string.IsNullOrEmpty(name))
            {
#if UNITY_EDITOR
                name = UnityEditor.GameObjectUtility.GetUniqueNameForSibling(parent, type.Name);
#else
                name = type.Name;
#endif
            }

            var newGameObject = new GameObject(name);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(newGameObject, "Created " + name);
#endif
            newGameObject.SetActive(false);
            try
            {
                var brushTransform = newGameObject.transform;
#if UNITY_EDITOR
                if (parent)
                    UnityEditor.Undo.SetTransformParent(brushTransform, parent, "Move child node underneath parent composite");
                UnityEditor.Undo.RecordObject(brushTransform, "Move child node to given position");
#else
                if (parent)
                    brushTransform.SetParent(parent, false);
#endif
                if (parent)
                    brushTransform.SetLocal(parent.worldToLocalMatrix * trsMatrix);
                else
                    brushTransform.SetLocal(trsMatrix);

#if UNITY_EDITOR
                return UnityEditor.Undo.AddComponent(newGameObject, type);
#else
                return newGameObject.AddComponent(type);
#endif
            }
            finally
            {
                newGameObject.SetActive(true);
            }
        }
         
        public static T Create<T>(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale) where T : ChiselNode
        {
            return Create<T>(name, parent, Matrix4x4.TRS(position, rotation, scale));
        }

        public static T Create<T>(string name, ChiselModelComponent model) where T : ChiselNode { return Create<T>(name, model ? model.transform : null, Vector3.zero, Quaternion.identity, Vector3.one); }
        public static T Create<T>(string name, Transform parent = null) where T : ChiselNode { return Create<T>(name, parent, Vector3.zero, Quaternion.identity, Vector3.one); }
        public static T Create<T>(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale) where T : ChiselNode { return Create<T>(null, parent, position, rotation, scale); }
        public static T Create<T>(Transform parent, Matrix4x4 trsMatrix) where T : ChiselNode { return Create<T>(null, parent, trsMatrix); }
        public static T Create<T>(Transform parent = null) where T : ChiselNode { return Create<T>(null, parent, Vector3.zero, Quaternion.identity, Vector3.one); }
        public static T Create<T>(Vector3 position, Quaternion rotation, Vector3 scale) where T : ChiselNode { return Create<T>(null, (Transform)null, position, rotation, scale); }
        public static T Create<T>(Matrix4x4 trsMatrix) where T : ChiselNode { return Create<T>(null, (Transform)null, trsMatrix); }
        public static T Create<T>(ChiselModelComponent model, Vector3 position, Quaternion rotation, Vector3 scale) where T : ChiselNode { return Create<T>(null, model ? model.transform : null, position, rotation, scale); }
        public static T Create<T>(ChiselModelComponent model, Matrix4x4 trsMatrix) where T : ChiselNode { return Create<T>(null, model ? model.transform : null, trsMatrix); }
        public static T Create<T>(ChiselModelComponent model) where T : ChiselNode { return Create<T>(null, model ? model.transform : null, Vector3.zero, Quaternion.identity, Vector3.one); }
        public static T Create<T>(string name, ChiselModelComponent model, Matrix4x4 trsMatrix) where T : ChiselNode { return Create<T>(name, model ? model.transform : null, trsMatrix); }
        public static T Create<T>(string name, ChiselModelComponent model, Vector3 position, Quaternion rotation, Vector3 scale) where T : ChiselNode { return Create<T>(name, model ? model.transform : null, position, rotation, scale); }
    }
}
