using System;
using System.Collections.Generic;
using Chisel.Components;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{

    public sealed class ChiselUnityEventsManager
    {
        [UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            // Note that it's always safer to first unregister an event before 
            // assigning it, since this will avoid double assigning / leaking events 
            // whenever this code is, for whatever reason, run more than once.

            // Update loop
            UnityEditor.EditorApplication.update -= OnEditorApplicationUpdate;
            UnityEditor.EditorApplication.update += OnEditorApplicationUpdate;

            // Called after prefab instances in the scene have been updated.
            UnityEditor.PrefabUtility.prefabInstanceUpdated -= OnPrefabInstanceUpdated;
            UnityEditor.PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;

            // OnGUI events for every visible list item in the HierarchyWindow.
            UnityEditor.EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemOnGUI;
            UnityEditor.EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;

            // Triggered when the hierarchy changes
            UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            UnityEditor.EditorApplication.hierarchyChanged += OnHierarchyChanged;

            // Triggered when currently active/selected item has changed.
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;

            // Triggered when currently active/selected item has changed.
            ChiselSurfaceSelectionManager.selectionChanged -= OnSurfaceSelectionChanged;
            ChiselSurfaceSelectionManager.selectionChanged += OnSurfaceSelectionChanged;
            ChiselSurfaceSelectionManager.hoverChanged -= OnSurfaceHoverChanged;
            ChiselSurfaceSelectionManager.hoverChanged += OnSurfaceHoverChanged;

            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;


            // Triggered when changing visibility/picking in hierarchy
            UnityEditor.SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;
            UnityEditor.SceneVisibilityManager.pickingChanged += OnPickingChanged;


            // Callback that is triggered after an undo or redo was executed.
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;

            UnityEditor.Undo.postprocessModifications -= OnPostprocessModifications;
            UnityEditor.Undo.postprocessModifications += OnPostprocessModifications;

            UnityEditor.Undo.willFlushUndoRecord -= OnWillFlushUndoRecord;
            UnityEditor.Undo.willFlushUndoRecord += OnWillFlushUndoRecord;

            UnityEditor.SceneView.beforeSceneGui -= OnBeforeSceneGUI;
            UnityEditor.SceneView.beforeSceneGui += OnBeforeSceneGUI;

            UnityEditor.SceneView.duringSceneGui -= OnDuringSceneGUI;
            UnityEditor.SceneView.duringSceneGui += OnDuringSceneGUI;

            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

            ChiselNodeHierarchyManager.NodeHierarchyReset -= OnHierarchyReset;
            ChiselNodeHierarchyManager.NodeHierarchyReset += OnHierarchyReset;

            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarchyModified;

            ChiselNodeHierarchyManager.TransformationChanged -= OnTransformationChanged;
            ChiselNodeHierarchyManager.TransformationChanged += OnTransformationChanged;

            ChiselGeneratedModelMeshManager.PostUpdateModels -= OnPostUpdateModels;
            ChiselGeneratedModelMeshManager.PostUpdateModels += OnPostUpdateModels;

            ChiselGeneratedModelMeshManager.PostReset -= OnPostResetModels;
            ChiselGeneratedModelMeshManager.PostReset += OnPostResetModels;

            ToolManager.activeToolChanged -= OnEditModeChanged;
            ToolManager.activeToolChanged += OnEditModeChanged;

            ChiselClickSelectionManager.Instance.OnReset();
            ChiselOutlineRenderer.Instance.OnReset();
        }

        private static void OnHierarchyChanged()
        {
            ChiselNodeHierarchyManager.CheckOrderOfChildNodesModifiedOfNonNodeGameObject();
        }

        private static void OnPickingChanged()
        {
            ChiselGeneratedComponentManager.OnVisibilityChanged();
        }

        private static void OnVisibilityChanged()
        {
            ChiselGeneratedComponentManager.OnVisibilityChanged();
        }

        private static void OnActiveSceneChanged(Scene prevScene, Scene newScene)
        {
            ChiselModelManager.OnActiveSceneChanged(prevScene, newScene);
        }

        static void OnTransformationChanged()
        {
            ChiselOutlineRenderer.Instance.OnTransformationChanged();
        }


        static void OnBeforeSceneGUI(SceneView sceneView)
        {
            Profiler.BeginSample("OnBeforeSceneGUI");
            ChiselDrawModes.HandleDrawMode(sceneView);
            Profiler.EndSample();
        }

        static void OnDuringSceneGUI(SceneView sceneView)
        {
            Profiler.BeginSample("OnDuringSceneGUI");
            // Workaround where Unity stops redrawing sceneview after a second, which makes hovering over edge visualization stop working
            if (Event.current.type == EventType.MouseMove)
                sceneView.Repaint();

            var prevSkin = GUI.skin;
            GUI.skin = ChiselSceneGUIStyle.GetSceneSkin();
            try
            {
                ChiselSceneGUIStyle.Update();
                ChiselGridSettings.GridOnSceneGUI(sceneView);
                ChiselOutlineRenderer.Instance.OnSceneGUI(sceneView);

                if (EditorWindow.mouseOverWindow == sceneView || // This helps prevent weird issues with overlapping sceneviews + avoid some performance issues with multiple sceneviews open
                    (Event.current.type != EventType.MouseMove && Event.current.type != EventType.Layout))
                {
                    ChiselDragAndDropManager.Instance.OnSceneGUI(sceneView);
                    ChiselClickSelectionManager.Instance.OnSceneGUI(sceneView);
                }
            }
            finally
            {
                GUI.skin = prevSkin;
            }
            Profiler.EndSample();
        }

        private static void OnEditModeChanged()//IChiselToolMode prevEditMode, IChiselToolMode newEditMode)
        {
            ChiselOutlineRenderer.Instance.OnEditModeChanged();
            if (Tools.current != Tool.Custom)
            {
                ChiselGeneratorManager.ActivateTool(null);
            }
            ChiselGeneratorManager.ActivateTool(ChiselGeneratorManager.GeneratorMode);
        }

        private static void OnSelectionChanged()
        {
            ChiselClickSelectionManager.Instance.OnSelectionChanged();
            ChiselOutlineRenderer.Instance.OnSelectionChanged();
        }

        private static void OnSurfaceSelectionChanged()
        {
            ChiselOutlineRenderer.Instance.OnSurfaceSelectionChanged();
        }

        private static void OnSurfaceHoverChanged()
        {
            ChiselOutlineRenderer.Instance.OnSurfaceHoverChanged();
        }


        private static void OnPostUpdateModels()
        {
            ChiselOutlineRenderer.Instance.OnGeneratedMeshesChanged();
        }

        private static void OnPostResetModels()
        {
            ChiselOutlineRenderer.Instance.OnReset();
        }

        private static void OnNodeHierarchyModified()
        {
            ChiselOutlineRenderer.Instance.OnReset();

            // Prevent infinite loops
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
                return;

            Editors.ChiselManagedHierarchyView.RepaintAll();

            // THIS IS SLOW! DON'T DO THIS
            //UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void OnHierarchyReset()
        {
            // Prevent infinite loops
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
                return;
            Editors.ChiselManagedHierarchyView.RepaintAll();
            //Editors.ChiselInternalHierarchyView.RepaintAll(); 
        }

        private static void OnPrefabInstanceUpdated(GameObject instance)
        {
            ChiselNodeHierarchyManager.OnPrefabInstanceUpdated(instance);
        }

        private static void OnEditorApplicationUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            try
            {
                ChiselNodeHierarchyManager.Update();
                ChiselGeneratedModelMeshManager.UpdateModels();
                ChiselNodeEditorBase.HandleCancelEvent();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            Profiler.BeginSample("OnHierarchyWindowItemOnGUI");
            try
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
                if (!obj)
                    return;
                var gameObject = (GameObject)obj;

                // TODO: implement material drag & drop support for meshes

                var component = gameObject.GetComponent<ChiselNode>();
                if (!component)
                    return;
                Editors.ChiselHierarchyWindowManager.OnHierarchyWindowItemGUI(instanceID, component, selectionRect);
            }
            finally { Profiler.EndSample(); }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ChiselNodeHierarchyManager.firstStart = false;
        }



        private static void OnUndoRedoPerformed()
        {
            //ProfileFrame("myLog2");

            //ChiselNodeHierarchyManager.firstStart = false;
            ChiselNodeHierarchyManager.UpdateAllTransformations();
            ChiselOutlineRenderer.Instance.OnTransformationChanged();
            ChiselOutlineRenderer.OnUndoRedoPerformed();

        }
        /*
        static bool profilerStarted = false;
        public static void ProfileFrame(string filename)
        {
            if (profilerStarted)
                return;
            profilerStarted = true;
            Profiler.logFile = filename; //Also supports passing "myLog.raw"
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            // Optional, if more memory is needed for the buffer
            Profiler.maxUsedMemory = 1024 * 1024 * 1024;
            
            EditorApplication.update -= EndProfiling;
            EditorApplication.update += EndProfiling;
        }

        static void EndProfiling()
        {
            profilerStarted = false;
            EditorApplication.update -= EndProfiling;
            // Optional, to close the file when done
            Profiler.enabled = false;
            Profiler.logFile = "";
        }
        */

        static readonly HashSet<ChiselNode>	modifiedNodes		= new HashSet<ChiselNode>();
        static readonly HashSet<Transform>	processedTransforms = new HashSet<Transform>();

        private static void OnWillFlushUndoRecord()
        {
            ChiselModelManager.OnWillFlushUndoRecord();
        }

        static readonly List<ChiselNode> s_ChildNodes = new List<ChiselNode>();

        private static UnityEditor.UndoPropertyModification[] OnPostprocessModifications(UnityEditor.UndoPropertyModification[] modifications)
        {
            // Note: this is not always properly called 
            //			- when? can't remember? maybe prefab related?
            modifiedNodes.Clear();
            processedTransforms.Clear();
            for (int i = 0; i < modifications.Length; i++)
            {
                var currentValue = modifications[i].currentValue;
                var transform	 = currentValue.target as Transform;
                if (object.Equals(null, transform))
                    continue;

                if (processedTransforms.Contains(transform))
                    continue;

                var propertyPath = currentValue.propertyPath;
                if (!propertyPath.StartsWith("m_Local"))
                    continue;

                processedTransforms.Add(transform);

                s_ChildNodes.Clear();
                transform.GetComponentsInChildren<ChiselNode>(false, s_ChildNodes);
                if (s_ChildNodes.Count == 0)
                    continue;
                if (s_ChildNodes[0] is ChiselModel)
                    continue;
                for (int n = 0; n < s_ChildNodes.Count; n++)
                    modifiedNodes.Add(s_ChildNodes[n]);
            }
            if (modifiedNodes.Count > 0)
            {
                ChiselNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
            }
            return modifications;
        }
    }

}