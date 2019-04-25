using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chisel.Editors
{ 

    public sealed class CSGUnityEventsManager
    {
        [UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
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
            CSGSurfaceSelectionManager.selectionChanged					-= OnSurfaceSelectionChanged;
            CSGSurfaceSelectionManager.selectionChanged					+= OnSurfaceSelectionChanged;
            CSGSurfaceSelectionManager.hoverChanged						-= OnSurfaceHoverChanged;
            CSGSurfaceSelectionManager.hoverChanged						+= OnSurfaceHoverChanged;

            // A callback to be raised when an object in the hierarchy changes.
            // Each time an object is (or a group of objects are) created, 
            // renamed, parented, unparented or destroyed this callback is raised.
//			UnityEditor.EditorApplication.hierarchyWindowChanged		-= OnHierarchyWindowChanged;
//			UnityEditor.EditorApplication.hierarchyWindowChanged		+= OnHierarchyWindowChanged;

            UnityEditor.EditorApplication.playModeStateChanged			-= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged			+= OnPlayModeStateChanged;

            // Callback that is triggered after an undo or redo was executed.
            UnityEditor.Undo.undoRedoPerformed							-= OnUndoRedoPerformed;                     
            UnityEditor.Undo.undoRedoPerformed							+= OnUndoRedoPerformed;

            UnityEditor.Undo.postprocessModifications					-= OnPostprocessModifications;
            UnityEditor.Undo.postprocessModifications					+= OnPostprocessModifications;

#if UNITY_2019_1_OR_NEWER
            UnityEditor.SceneView.duringSceneGui					    -= OnSceneGUI;
            UnityEditor.SceneView.duringSceneGui                        += OnSceneGUI;
#else
            UnityEditor.SceneView.onSceneGUIDelegate					-= OnSceneGUI;
            UnityEditor.SceneView.onSceneGUIDelegate					+= OnSceneGUI;
#endif            
                
            CSGNodeHierarchyManager.NodeHierarchyReset -= OnHierarchyReset;
            CSGNodeHierarchyManager.NodeHierarchyReset += OnHierarchyReset;

            CSGNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarcyModified;
            CSGNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarcyModified;

            CSGNodeHierarchyManager.TransformationChanged -= OnTransformationChanged;
            CSGNodeHierarchyManager.TransformationChanged += OnTransformationChanged;

            CSGGeneratedModelMeshManager.PostUpdateModels -= OnPostUpdateModels;
            CSGGeneratedModelMeshManager.PostUpdateModels += OnPostUpdateModels;
            
            CSGGeneratedModelMeshManager.PostReset -= OnPostResetModels;
            CSGGeneratedModelMeshManager.PostReset += OnPostResetModels;

            CSGEditModeManager.EditModeChanged -= OnEditModeChanged;
            CSGEditModeManager.EditModeChanged += OnEditModeChanged;
            CSGEditModeManager.Init();

            SnappingKeyboard.Init();

            CSGClickSelectionManager.Instance.OnReset();
            CSGOutlineRenderer.Instance.OnReset();

            // TODO: clean this up
            CSGGeneratorComponent.GetSelectedVariantsOfBrushOrSelf = CSGSyncSelection.GetSelectedVariantsOfBrushOrSelf;
        }

        static void OnTransformationChanged()
        {
            CSGOutlineRenderer.Instance.OnTransformationChanged();
        }
        
        
        private static Type			m_annotationUtility;
        private static PropertyInfo m_showGridPropContainer;

        private static PropertyInfo m_showUnityGrid
        {
            get
            {
                if (m_showGridPropContainer == null)
                {
                    m_annotationUtility = Type.GetType("UnityEditor.AnnotationUtility,UnityEditor.dll");
                    m_showGridPropContainer = m_annotationUtility.GetProperty("showGrid", BindingFlags.Static | BindingFlags.NonPublic);
                }
                return m_showGridPropContainer;
            }
        }

        public static bool ShowUnityGrid
        {
            get
            {
                return (bool)m_showUnityGrid.GetValue(null, null);
            }
            set
            {
                m_showUnityGrid.SetValue(null, value, null);
            }
        }
     
        private static void GridOnSceneGUI(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (ShowUnityGrid)
            {
                ShowUnityGrid = false;
                CSGEditorSettings.Load();
                CSGEditorSettings.ShowGrid = false;
                CSGEditorSettings.Save();
            }

            if (Tools.pivotRotation == PivotRotation.Local) 
            {
                var activeTransform = Selection.activeTransform;

                var rotation	= Tools.handleRotation;
                var center		= (activeTransform && activeTransform.parent) ? activeTransform.parent.position : Vector3.zero;

                UnitySceneExtensions.Grid.defaultGrid.GridToWorldSpace = Matrix4x4.TRS(center, rotation, Vector3.one);
            } else
            {
                UnitySceneExtensions.Grid.defaultGrid.GridToWorldSpace = Matrix4x4.identity;
            }

            if (CSGEditorSettings.ShowGrid)
            {
                var grid = UnitySceneExtensions.Grid.HoverGrid;
                if (grid == null)
                    grid = UnitySceneExtensions.Grid.ActiveGrid;
                grid.Render(sceneView);
            }
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            GridOnSceneGUI(sceneView);
            CSGOutlineRenderer.Instance.OnSceneGUI(sceneView);
            var dragArea = CSGSceneBottomGUI.OnSceneGUI(sceneView);
            CSGEditModeGUI.OnSceneGUI(sceneView, dragArea);

            CSGDragAndDropManager.Instance.OnSceneGUI(sceneView);
            CSGClickSelectionManager.Instance.OnSceneGUI(sceneView);
            KeyboardManager.OnSceneGUI(sceneView);
        }

        private static void OnEditModeChanged(CSGEditMode prevEditMode, CSGEditMode newEditMode)
        {
            CSGOutlineRenderer.Instance.OnEditModeChanged(prevEditMode, newEditMode);
        }

        private static void OnSelectionChanged()
        {
            CSGClickSelectionManager.Instance.OnSelectionChanged();
            CSGOutlineRenderer.Instance.OnSelectionChanged();
            CSGEditModeGUI.Instance.OnSelectionChanged();
            //Editors.CSGManagedHierarchyView.RepaintAll();
            //Editors.CSGNativeHierarchyView.RepaintAll();
        }

        private static void OnSurfaceSelectionChanged()
        {
            CSGOutlineRenderer.Instance.OnSurfaceSelectionChanged(); 
        }
        
        private static void OnSurfaceHoverChanged()
        {
            CSGOutlineRenderer.Instance.OnSurfaceHoverChanged(); 
        }
        

        private static void OnPostUpdateModels()
        {
            CSGOutlineRenderer.Instance.OnGeneratedMeshesChanged();
        }

        private static void OnPostResetModels()
        {
            CSGOutlineRenderer.Instance.OnReset();
        }
    
        private static void OnNodeHierarcyModified()
        {
            CSGOutlineRenderer.Instance.OnReset();
            Editors.CSGManagedHierarchyView.RepaintAll();
            Editors.CSGInternalHierarchyView.RepaintAll();
            SceneView.RepaintAll(); 
        }

        private static void OnHierarchyReset()
        {			
            Editors.CSGManagedHierarchyView.RepaintAll();
            Editors.CSGInternalHierarchyView.RepaintAll(); 
        }

        /*
        private static void OnHierarchyWindowChanged()
        {
            if (CSGNodeHierarchyManager.CheckHierarchyModifications())
            {
                Editors.CSGManagedHierarchyView.RepaintAll();
                Editors.CSGNativeHierarchyView.RepaintAll(); 
            }
        }
        */

        private static void OnPrefabInstanceUpdated(GameObject instance)
        {
            CSGNodeHierarchyManager.OnPrefabInstanceUpdated(instance);
        }


        static bool loggingMethodsRegistered = false;

        private static void OnEditorApplicationUpdate()
        {
            // TODO: remove this once we've moved to managed implementation of CSG algorithm
            if (!loggingMethodsRegistered)
            {
                Editors.NativeLogging.RegisterUnityMethods();
                loggingMethodsRegistered = true;
            }

            //Grid.HoverGrid = null;
            CSGNodeHierarchyManager.Update();
            CSGGeneratedModelMeshManager.UpdateModels();
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            var obj =  UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
            if (!obj)
                return;
            var gameObject = (GameObject)obj;

            // TODO: implement material drag & drop support for meshes

            var component = gameObject.GetComponent<CSGNode>();
            if (!component)
                return;
            Editors.CSGNodeEditor.HierarchyWindowItemOnGUI(component, selectionRect);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            CSGNodeHierarchyManager.firstStart = false;
        }

        private static void OnUndoRedoPerformed()
        {
            CSGNodeHierarchyManager.UpdateAllTransformations();
            CSGOutlineRenderer.Instance.OnTransformationChanged();
        }

        static HashSet<CSGNode>		modifiedNodes		= new HashSet<CSGNode>();
        static HashSet<Transform>	processedTransforms = new HashSet<Transform>();
        
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

                var nodes = transform.GetComponentsInChildren<CSGNode>();
                if (nodes.Length == 0)
                    continue;
                if (nodes[0] is CSGModel)
                    continue;
                for (int n = 0; n < nodes.Length; n++)
                    modifiedNodes.Add(nodes[n]);
            }
            if (modifiedNodes.Count > 0)
            {
                CSGNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
            }
            return modifications;
        }
    }

}