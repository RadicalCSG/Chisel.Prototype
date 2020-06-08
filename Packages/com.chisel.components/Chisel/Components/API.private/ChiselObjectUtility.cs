using System;
using UnityEngine;

namespace Chisel.Components
{
    public struct GameObjectState
    {
        public int				                layer;
#if UNITY_EDITOR
        public UnityEditor.StaticEditorFlags	staticFlags;
#endif
        public static GameObjectState Create(ChiselModel model)
        {
            var modelGameObject = model.gameObject;
            return new GameObjectState
            {
                layer           = modelGameObject.layer,
#if UNITY_EDITOR
                staticFlags     = UnityEditor.GameObjectUtility.GetStaticEditorFlags(modelGameObject)
#endif
            };
        }
    }

    //TODO: Move this somewhere else
    public static class ChiselObjectUtility
    {
        public static void SafeDestroy(UnityEngine.Object obj)
        {
            if (!obj)
                return;
            obj.hideFlags = UnityEngine.HideFlags.None;
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                UnityEngine.Object.DestroyImmediate(obj);
            else
#endif
                UnityEngine.Object.Destroy(obj);
        }

        public static void SafeDestroyWithUndo(UnityEngine.Object obj)
        {
            if (!obj)
                return;
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.Undo.RecordObject(obj, "Destroying object");
                obj.hideFlags = UnityEngine.HideFlags.None;
                UnityEditor.Undo.DestroyObjectImmediate(obj);
            } else
#endif
            {
                Debug.Log("Undo not possible");
                obj.hideFlags = UnityEngine.HideFlags.None;
                UnityEngine.Object.Destroy(obj);
            }
        }

        public static void SafeDestroy(UnityEngine.GameObject obj, bool ignoreHierarchyEvents = false)
        {
            if (!obj)
                return;
            if (ignoreHierarchyEvents)
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
            try
            {
                obj.hideFlags = UnityEngine.HideFlags.None;
                obj.transform.hideFlags = HideFlags.None;
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                    UnityEngine.Object.DestroyImmediate(obj);
                else
#endif
                    UnityEngine.Object.Destroy(obj);
            }
            finally
            {
                if (ignoreHierarchyEvents)
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
            }
        }

        public static void SafeDestroyWithUndo(UnityEngine.GameObject obj, bool ignoreHierarchyEvents = false)
        {
            if (!obj)
                return;
            if (ignoreHierarchyEvents)
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
            try
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                {
                    UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { obj, obj.transform }, "Destroying object");
                    obj.hideFlags = UnityEngine.HideFlags.None;
                    obj.transform.hideFlags = HideFlags.None;
                    UnityEditor.Undo.DestroyObjectImmediate(obj);
                } else
#endif
                {
                    Debug.Log("Undo not possible");
                    obj.hideFlags = UnityEngine.HideFlags.None;
                    obj.transform.hideFlags = HideFlags.None;
                    UnityEngine.Object.Destroy(obj);
                }
            }
            finally
            {
                if (ignoreHierarchyEvents)
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
            }
        }

        public static void ResetTransform(Transform transform)
        {
            var prevLocalPosition   = transform.localPosition;
            var prevLocalRotation   = transform.localRotation;
            var prevLocalScale      = transform.localScale;
                
            if (prevLocalPosition.x != 0 ||
                prevLocalPosition.y != 0 ||
                prevLocalPosition.z != 0)
                transform.localPosition = Vector3.zero;
                
            if (prevLocalRotation != Quaternion.identity)
                transform.localRotation = Quaternion.identity;

            if (prevLocalScale.x != 1 ||
                prevLocalScale.y != 1 ||
                prevLocalScale.z != 1)
                transform.localScale = Vector3.one;
        }

        public static void ResetTransform(Transform transform, Transform requiredParent)
        {
            if (transform.parent != requiredParent)
                transform.SetParent(requiredParent, false);
            ResetTransform(transform);
        }

        public static GameObject CreateGameObject(string name, Transform parent, params Type[] components)
        {
            var parentScene = parent.gameObject.scene;
            var oldActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (parentScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(parentScene);
            try
            {
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
                var newGameObject = new GameObject(name, components);
                newGameObject.SetActive(false);
                try
                {
                    var transform = newGameObject.GetComponent<Transform>();
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
                    transform.SetParent(parent, false);
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
                    ResetTransform(transform);
                }
                finally
                {
                    newGameObject.SetActive(true);
                }
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
                return newGameObject;
            }
            finally
            {
                if (parentScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        public static GameObject CreateGameObject(string name, Transform parent)
        {
            var parentScene = parent.gameObject.scene;
            var oldActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (parentScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(parentScene);
            try
            {
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
                var newGameObject = new GameObject(name);
                newGameObject.SetActive(false);
                try
                {
                    var transform = newGameObject.GetComponent<Transform>();
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
                    transform.SetParent(parent, false);
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
                    ResetTransform(transform);
                }
                finally
                {
                    newGameObject.SetActive(true);
                }
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
                return newGameObject;
            } 
            finally
            {
                if (parentScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        public static GameObject CreateGameObject(string name, Transform parent, GameObjectState state)
        {
            var parentScene = parent.gameObject.scene;
            var oldActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (parentScene != oldActiveScene)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(parentScene);
            try
            {
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
                var newGameObject = new GameObject(name);
                newGameObject.SetActive(false);
                try
                {
                    UpdateContainerFlags(newGameObject, state);
                    var transform = newGameObject.GetComponent<Transform>();
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = true;
                    transform.SetParent(parent, false);
                    ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
                    ResetTransform(transform);
                }
                finally
                {
                    newGameObject.SetActive(true);
                }
                ChiselNodeHierarchyManager.ignoreNextChildrenChanged = false;
                return newGameObject;
            } 
            finally
            {
                if (parentScene != oldActiveScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        const HideFlags kGameObjectHideFlags    = HideFlags.NotEditable;
        const HideFlags kTransformHideFlags     = HideFlags.NotEditable;// | HideFlags.HideInInspector;
        const HideFlags kComponentHideFlags     = HideFlags.HideInHierarchy | HideFlags.NotEditable; // Avoids MeshCollider showing wireframe

        internal static void UpdateContainerFlags(GameObject gameObject, GameObjectState state)
        {
            var transform = gameObject.transform;
            if (gameObject.layer     != state.layer         ) gameObject.layer     = state.layer;
            if (gameObject.hideFlags != kGameObjectHideFlags) gameObject.hideFlags = kGameObjectHideFlags;
            if (transform .hideFlags != kTransformHideFlags ) transform .hideFlags = kTransformHideFlags;

#if UNITY_EDITOR
            var prevStaticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(gameObject);
            if (prevStaticFlags != state.staticFlags)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(gameObject, state.staticFlags);
#endif
        }

        internal static void UpdateContainerFlags(Component component, GameObjectState modelState)
        { 
            var gameObject  = component.gameObject;
            UpdateContainerFlags(gameObject, modelState);

            if (component.hideFlags != kComponentHideFlags ) component.hideFlags = kComponentHideFlags;
        }

        public static void RemoveContainerFlags(GameObject gameObject)
        {
            var transform = (gameObject) ? gameObject.transform : null;
            if (gameObject) gameObject.hideFlags = HideFlags.None;
            if (transform) transform.hideFlags = HideFlags.None;
        }

        public static void RemoveContainerFlags(Component component)
        {
            if (component) component.hideFlags = HideFlags.None;
        }
    }
}
