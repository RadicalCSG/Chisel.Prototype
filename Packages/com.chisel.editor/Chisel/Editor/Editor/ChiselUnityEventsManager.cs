using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            UnityEditor.EditorApplication.update						-= OnEditorApplicationUpdate;
            UnityEditor.EditorApplication.update						+= OnEditorApplicationUpdate;

            // Called after prefab instances in the scene have been updated.
            UnityEditor.PrefabUtility.prefabInstanceUpdated				-= OnPrefabInstanceUpdated;
            UnityEditor.PrefabUtility.prefabInstanceUpdated				+= OnPrefabInstanceUpdated;

            // OnGUI events for every visible list item in the HierarchyWindow.
            UnityEditor.EditorApplication.hierarchyWindowItemOnGUI		-= OnHierarchyWindowItemOnGUI;
            UnityEditor.EditorApplication.hierarchyWindowItemOnGUI		+= OnHierarchyWindowItemOnGUI;

            // Triggered when currently active/selected item has changed.
            UnityEditor.Selection.selectionChanged						-= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged						+= OnSelectionChanged;
            
            // Triggered when currently active/selected item has changed.
            ChiselSurfaceSelectionManager.selectionChanged				-= OnSurfaceSelectionChanged;
            ChiselSurfaceSelectionManager.selectionChanged				+= OnSurfaceSelectionChanged;
            ChiselSurfaceSelectionManager.hoverChanged					-= OnSurfaceHoverChanged;
            ChiselSurfaceSelectionManager.hoverChanged					+= OnSurfaceHoverChanged;

            UnityEditor.EditorApplication.playModeStateChanged			-= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged			+= OnPlayModeStateChanged;

            // Callback that is triggered after an undo or redo was executed.
            UnityEditor.Undo.undoRedoPerformed							-= OnUndoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed							+= OnUndoRedoPerformed;

            UnityEditor.Undo.postprocessModifications					-= OnPostprocessModifications;
            UnityEditor.Undo.postprocessModifications					+= OnPostprocessModifications;
            
            UnityEditor.Undo.willFlushUndoRecord                        -= OnWillFlushUndoRecord;
            UnityEditor.Undo.willFlushUndoRecord                        += OnWillFlushUndoRecord;

            UnityEditor.SceneView.duringSceneGui                        -= OnDuringSceneGUI;
            UnityEditor.SceneView.duringSceneGui                        += OnDuringSceneGUI;

            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

            ChiselNodeHierarchyManager.NodeHierarchyReset -= OnHierarchyReset;
            ChiselNodeHierarchyManager.NodeHierarchyReset += OnHierarchyReset;

            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarcyModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarcyModified;

            ChiselNodeHierarchyManager.TransformationChanged -= OnTransformationChanged;
            ChiselNodeHierarchyManager.TransformationChanged += OnTransformationChanged;

            ChiselGeneratedModelMeshManager.PostUpdateModels -= OnPostUpdateModels;
            ChiselGeneratedModelMeshManager.PostUpdateModels += OnPostUpdateModels;
            
            ChiselGeneratedModelMeshManager.PostReset -= OnPostResetModels;
            ChiselGeneratedModelMeshManager.PostReset += OnPostResetModels;

            EditorTools.activeToolChanged -= OnEditModeChanged;
            EditorTools.activeToolChanged += OnEditModeChanged;

            ChiselClickSelectionManager.Instance.OnReset();
            ChiselOutlineRenderer.Instance.OnReset();

            // TODO: clean this up
            ChiselGeneratorComponent.GetSelectedVariantsOfBrushOrSelf = ChiselSyncSelection.GetSelectedVariantsOfBrushOrSelf;
        }

        private static void OnActiveSceneChanged(Scene prevScene, Scene newScene)
        {
            ChiselModelManager.OnActiveSceneChanged(prevScene, newScene);
        }

        static void OnTransformationChanged()
        {
            ChiselOutlineRenderer.Instance.OnTransformationChanged();
        }
        
        
       

        static void OnDuringSceneGUI(SceneView sceneView)
        {
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

                if (Tools.current != Tool.Custom)
                    ChiselEditToolBase.ShowDefaultOverlay();
            }
            finally
            {
                GUI.skin = prevSkin;
            }
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
            ChiselEditToolBase.NotifyOnSelectionChanged();
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
    
        private static void OnNodeHierarcyModified()
        {
            ChiselOutlineRenderer.Instance.OnReset();
            Editors.ChiselManagedHierarchyView.RepaintAll();
            Editors.ChiselInternalHierarchyView.RepaintAll();
            //SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void OnHierarchyReset()
        {			
            Editors.ChiselManagedHierarchyView.RepaintAll();
            Editors.ChiselInternalHierarchyView.RepaintAll(); 
        }

        private static void OnPrefabInstanceUpdated(GameObject instance)
        {
            ChiselNodeHierarchyManager.OnPrefabInstanceUpdated(instance);
        }

        private static void OnEditorApplicationUpdate()
        {
            ChiselNodeHierarchyManager.Update();
            ChiselGeneratedModelMeshManager.UpdateModels();
            ChiselNodeEditorBase.HandleCancelEvent();
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            var obj =  UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
            if (!obj)
                return;
            var gameObject = (GameObject)obj;

            // TODO: implement material drag & drop support for meshes

            var component = gameObject.GetComponent<ChiselNode>();
            if (!component)
                return;
            Editors.ChiselHierarchyWindowManager.OnHierarchyWindowItemGUI(instanceID, component, selectionRect);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ChiselNodeHierarchyManager.firstStart = false;
        }

        private static void OnUndoRedoPerformed()
        {
            ChiselNodeHierarchyManager.UpdateAllTransformations();
            ChiselOutlineRenderer.Instance.OnTransformationChanged();
            ChiselOutlineRenderer.OnUndoRedoPerformed();
        }

        static readonly HashSet<ChiselNode>	modifiedNodes		= new HashSet<ChiselNode>();
        static readonly HashSet<Transform>	processedTransforms = new HashSet<Transform>();

        private static void OnWillFlushUndoRecord()
        {
            ChiselModelManager.OnWillFlushUndoRecord();
        }

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

                var nodes = transform.GetComponentsInChildren<ChiselNode>();
                if (nodes.Length == 0)
                    continue;
                if (nodes[0] is ChiselModel)
                    continue;
                for (int n = 0; n < nodes.Length; n++)
                    modifiedNodes.Add(nodes[n]);
            }
            if (modifiedNodes.Count > 0)
            {
                ChiselNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
            }
            return modifications;
        }
    }

}