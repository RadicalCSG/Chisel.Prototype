using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace Chisel.Editors
{
    [Serializable]
    public enum SelectionType { Replace, Additive, Subtractive };

    // TODO: rewrite
    internal static class ChiselRectSelection
    {
        public static bool		Valid			{ get { return reflectionSucceeded; } }
        public static int		RectSelectionID { get; private set; }
        
        static object rectSelection;
        static SceneView sceneView;
        public static SceneView SceneView
        {
            get
            {
                return sceneView;
            }
            set
            {
                if (sceneView == value)
                    return;
                sceneView = value;
                rectSelection = rectSelectionField.GetValue(sceneView);
            }
        }
        
        public static bool		RectSelecting		{ get { return (bool)rectSelectingField.GetValue(rectSelection); } }
        public static Vector2	SelectStartPoint	{ get { return (Vector2)selectStartPointField.GetValue(rectSelection); } }
        public static Vector2	SelectMousePoint	{ get { return (Vector2)selectMousePointField.GetValue(rectSelection); } }
        public static UnityEngine.Object[]			SelectionStart		{ get { return (UnityEngine.Object[])selectionStartField.GetValue(rectSelection); } set { selectionStartField.SetValue(rectSelection, value); } }
        public static UnityEngine.Object[]			CurrentSelection	{ get { return (UnityEngine.Object[])currentSelectionField.GetValue(rectSelection); } set { currentSelectionField.SetValue(rectSelection, value); } }
        public static Dictionary<GameObject, bool>	LastSelection		{ get { return (Dictionary<GameObject, bool>)lastSelectionField.GetValue(rectSelection); } }

        public static void UpdateSelection(Object[] existingSelection, Object[] newObjects, SelectionType type)
        {
            object selectionType;
            switch (type)
            {
                default:						selectionType = selectionTypeNormal; break;
                case SelectionType.Additive:	selectionType = selectionTypeAdditive; break;
                case SelectionType.Subtractive:	selectionType = selectionTypeSubtractive; break;
            }

            updateSelectionMethod.Invoke(null,
                new object[] 
                {
                    existingSelection,
                    newObjects,
                    selectionType,
                    RectSelecting
                });
        }

        static Type			unityRectSelectionType;
        static Type			unityEnumSelectionType;

        static object		selectionTypeAdditive;
        static object		selectionTypeSubtractive;
        static object		selectionTypeNormal;
            
        static FieldInfo	rectSelectionField;
        static FieldInfo	rectSelectingField;
        static FieldInfo	selectStartPointField;
        static FieldInfo	selectMousePointField;
        static FieldInfo	selectionStartField;
        static FieldInfo	lastSelectionField;
        static FieldInfo	currentSelectionField;
        
        static FieldInfo	rectSelectionIDField;

        static MethodInfo	updateSelectionMethod;
        
        static bool			reflectionSucceeded = false;

        static ChiselRectSelection()
        {
            reflectionSucceeded	= false;

            unityRectSelectionType		= ReflectionExtensions.GetTypeByName("UnityEditor.RectSelection");
            if (unityRectSelectionType == null)
                return; 

            unityEnumSelectionType 		= ReflectionExtensions.GetTypeByName("UnityEditor.RectSelection+SelectionType");
            if (unityEnumSelectionType == null)
                return;
            
            rectSelectionField			= typeof(SceneView).GetField("m_RectSelection",			BindingFlags.NonPublic | BindingFlags.Instance);
            if (rectSelectionField == null) return;
            
            rectSelectionIDField		= unityRectSelectionType.GetField("s_RectSelectionID",	BindingFlags.NonPublic | BindingFlags.Static);
            if (rectSelectionIDField == null) return;

            RectSelectionID				= (int)rectSelectionIDField.GetValue(null);
            rectSelectingField			= unityRectSelectionType.GetField("m_RectSelecting",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectStartPointField		= unityRectSelectionType.GetField("m_SelectStartPoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectionStartField			= unityRectSelectionType.GetField("m_SelectionStart",	BindingFlags.NonPublic | BindingFlags.Instance);
            lastSelectionField			= unityRectSelectionType.GetField("m_LastSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            currentSelectionField		= unityRectSelectionType.GetField("m_CurrentSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectMousePointField		= unityRectSelectionType.GetField("m_SelectMousePoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            
            updateSelectionMethod		= unityRectSelectionType.GetMethod("UpdateSelection", BindingFlags.NonPublic | BindingFlags.Static,
                                                                            null,
                                                                            new Type[] {
                                                                                typeof(UnityEngine.Object[]),
                                                                                typeof(UnityEngine.Object[]),
                                                                                unityEnumSelectionType,
                                                                                typeof(bool)
                                                                            },
                                                                            null);
            selectionTypeAdditive		= Enum.Parse(unityEnumSelectionType, "Additive");
            selectionTypeSubtractive	= Enum.Parse(unityEnumSelectionType, "Subtractive");
            selectionTypeNormal			= Enum.Parse(unityEnumSelectionType, "Normal");
            
            reflectionSucceeded =	rectSelectingField			!= null &&
                                    selectStartPointField		!= null &&
                                    selectionStartField			!= null &&
                                    lastSelectionField			!= null &&
                                    currentSelectionField		!= null &&
                                    selectMousePointField		!= null &&
                                    updateSelectionMethod		!= null &&

                                    selectionTypeAdditive		!= null &&
                                    selectionTypeSubtractive	!= null &&
                                    selectionTypeNormal			!= null;
        }
    }

    // TODO: clean up, rename
    internal static class ChiselRectSelectionManager
    {
        static HashSet<CSGTreeNode> rectFoundTreeNodes	= new HashSet<CSGTreeNode>();
        static HashSet<GameObject> rectFoundGameObjects = new HashSet<GameObject>();
        static Vector2  prevStartGUIPoint;
        static Vector2  prevMouseGUIPoint;
        static Vector2  prevStartScreenPoint;
        static Vector2  prevMouseScreenPoint;


        static bool     rectClickDown       = false;
        static bool     mouseDragged        = false;
        static Vector2  clickMousePosition  = Vector2.zero;

        // TODO: put somewhere else
        public static SelectionType GetCurrentSelectionType()
        {
            var selectionType = SelectionType.Replace;
            // shift only
            if ( Event.current.shift && !EditorGUI.actionKey && !Event.current.alt) { selectionType = SelectionType.Additive; } else
            // action key only (Command on macOS, Control on Windows)
            if (!Event.current.shift &&  EditorGUI.actionKey && !Event.current.alt) { selectionType = SelectionType.Subtractive; } 
            return selectionType;
        }

        static void RemoveGeneratedMeshesFromSelection()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects != null)
            {
                var foundObjects = selectedObjects;

                RemoveGeneratedMeshesFromArray(ref foundObjects);
                
                if (foundObjects.Length != selectedObjects.Length)
                    Selection.objects = foundObjects;
            }
        }

        static bool RemoveGeneratedMeshesFromArray(ref UnityEngine.Object[] selection)
        {
            var found = new List<UnityEngine.Object>();
            for (int i = selection.Length - 1; i >= 0; i--)
            {
                var obj = selection[i];
                if (ChiselGeneratedComponentManager.IsObjectGenerated(obj))
                    continue;
                found.Add(obj);
            }
            if (selection.Length != found.Count)
            {
                selection = found.ToArray();
                return true;
            }
            return false;
        }
        
        internal static void Update(SceneView sceneView)
        {
            if (!ChiselRectSelection.Valid)
            {
                prevStartGUIPoint = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                prevMouseGUIPoint = prevStartGUIPoint;
                prevStartScreenPoint = Vector2.zero;
                prevMouseScreenPoint = Vector2.zero;
                rectFoundGameObjects.Clear();
                rectFoundTreeNodes.Clear();
                return;
            }

            ChiselRectSelection.SceneView = sceneView;

            var rectSelectionID		= ChiselRectSelection.RectSelectionID;
            var hotControl			= GUIUtility.hotControl;
            var areRectSelecting	= hotControl == rectSelectionID;
            var typeForControl		= Event.current.GetTypeForControl(rectSelectionID);
            
            // check if we're rect-selecting
            if (areRectSelecting)
            {
                if ((typeForControl == EventType.Used || Event.current.commandName == "ModifierKeysChanged") && 
                    ChiselRectSelection.RectSelecting)
                {
                    var selectStartPoint = ChiselRectSelection.SelectStartPoint;
                    var selectMousePoint = ChiselRectSelection.SelectMousePoint;

                    // determine if our frustum changed since the last time
                    bool modified	= false;
                    bool needUpdate = false;
                    if (prevStartGUIPoint != selectStartPoint)
                    {
                        prevStartGUIPoint		= selectStartPoint;
                        prevStartScreenPoint	= Event.current.mousePosition;
                        needUpdate				= true;
                    }
                    if (prevMouseGUIPoint != selectMousePoint)
                    {
                        prevMouseGUIPoint		= selectMousePoint;
                        prevMouseScreenPoint	= Event.current.mousePosition;
                        needUpdate				= true;
                    }
                    if (needUpdate)
                    {
                        var rect = ChiselCameraUtility.PointsToRect(prevStartScreenPoint, prevMouseScreenPoint);
                        if (rect.width > 3 && 
                            rect.height > 3)
                        { 
                            var frustum         = ChiselCameraUtility.GetCameraSubFrustum(Camera.current, rect);
                            var selectionType   = GetCurrentSelectionType();

                            if (selectionType == SelectionType.Replace)
                            {
                                rectFoundTreeNodes.Clear();
                                rectFoundGameObjects.Clear();
                            }


                            // Find all the brushes (and it's gameObjects) that are inside the frustum
                            if (!ChiselSceneQuery.GetNodesInFrustum(frustum, UnityEditor.Tools.visibleLayers, ref rectFoundTreeNodes))
                            {
                                if (rectFoundGameObjects != null &&
                                    rectFoundGameObjects.Count > 0)
                                {
                                    rectFoundTreeNodes.Clear();
                                    rectFoundGameObjects.Clear();
                                    modified = true;
                                }
                            } else
                                modified = true;
            
                            foreach(var treeNode in rectFoundTreeNodes)
                            {
                                var brush = (CSGTreeBrush)treeNode;
                                if (brush.Valid)
                                {
                                    switch (selectionType)
                                    {
                                        case SelectionType.Additive:
                                        {
                                            ChiselSyncSelection.SelectBrushVariant(brush, uniqueSelection: false);
                                            break;
                                        }
                                        case SelectionType.Subtractive:
                                        {
                                            ChiselSyncSelection.DeselectBrushVariant(brush);
                                            break;
                                        }
                                        default:
                                        {
                                            ChiselSyncSelection.SelectBrushVariant(brush, uniqueSelection: true);
                                            break;
                                        }
                                    }
                                }
                                var nodeComponent	= ChiselNodeHierarchyManager.FindChiselNodeByTreeNode(treeNode);
                                if (!nodeComponent)
                                    continue;
                                var gameObject = nodeComponent.gameObject;
                                rectFoundGameObjects.Add(gameObject);
                            }
                        }
                    }

                    UnityEngine.Object[] currentSelection = null;
                    var originalLastSelection	= ChiselRectSelection.LastSelection;
                    var originalSelectionStart	= ChiselRectSelection.SelectionStart;

                    if (modified &&
                        rectFoundGameObjects != null &&
                        rectFoundGameObjects.Count > 0)
                    {
                        foreach(var obj in rectFoundGameObjects)
                        {
                            // if it hasn't already been added, add the obj
                            if (!originalLastSelection.ContainsKey(obj))
                            {
                                originalLastSelection.Add(obj, false);
                            }
                        }
                            
                        currentSelection = originalLastSelection.Keys.ToArray();
                        ChiselRectSelection.CurrentSelection = currentSelection;
                    } else
                    {
                        if (currentSelection == null || modified) { currentSelection = originalLastSelection.Keys.ToArray(); }
                    }
                    
                    if (RemoveGeneratedMeshesFromArray(ref originalSelectionStart))
                        modified = true;
                    
                    if (currentSelection != null && RemoveGeneratedMeshesFromArray(ref currentSelection))
                        modified = true;

                    if ((Event.current.commandName == "ModifierKeysChanged" || modified))
                    {
                        var foundObjects = currentSelection;

                        RemoveGeneratedMeshesFromArray(ref foundObjects);
                            
                        // calling static method UpdateSelection of RectSelection 
                        ChiselRectSelection.UpdateSelection(originalSelectionStart, foundObjects, GetCurrentSelectionType());
                    }
                }
                hotControl = GUIUtility.hotControl;
            }

            if (hotControl != rectSelectionID)
            {
                prevStartGUIPoint = Vector2.zero;
                prevMouseGUIPoint = Vector2.zero;
                rectFoundGameObjects.Clear();
                rectFoundTreeNodes.Clear();
            } /*else
            if (ignoreRect)
            {
                hotControl = 0;
                GUIUtility.hotControl = 0;
            }
            */

            bool click = false;
            var evt = Event.current;
            switch (typeForControl)
            {
                case EventType.MouseDown:
                {
                    rectClickDown = (Event.current.button == 0 && areRectSelecting);
                    clickMousePosition = Event.current.mousePosition;
                    mouseDragged = false;
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!mouseDragged)
                    {
                        if ((UnityEditor.HandleUtility.nearestControl != 0 || evt.button != 0) &&
                            (GUIUtility.keyboardControl != 0 || evt.button != 2))
                            break;
                        click = true;
                        Event.current.Use();
                    }
                    rectClickDown = false;
                    break;
                }
                case EventType.MouseMove:
                {
                    rectClickDown = false;
                    break;
                }
                case EventType.MouseDrag:
                {
                    mouseDragged = true;
                    break;
                }
                case EventType.Used:
                {
                    if (!mouseDragged)
                    {
                        var delta = Event.current.mousePosition - clickMousePosition;
                        if (Mathf.Abs(delta.x) > 4 || Mathf.Abs(delta.y) > 4) { mouseDragged = true; }
                    }
                    if (mouseDragged || !rectClickDown || Event.current.button != 0 || ChiselRectSelection.RectSelecting) { rectClickDown = false; break; }

                    click = true;
                    Event.current.Use();
                    break;
                }
                case EventType.KeyUp:
                {
                    if (hotControl == 0 &&
                        Event.current.keyCode == UnityEngine.KeyCode.Escape)
                    {
                        if (GUIUtility.hotControl == 0 && // make sure we're not actively doing anything
                            Tools.current != Tool.Custom)
                        {
                            // This deselects everything and disables all tool modes
                            Selection.activeTransform = null;
                            Event.current.Use();
                        }
                    }
                    break;
                }
                case EventType.ValidateCommand:
                {
                    if (Event.current.commandName != "SelectAll")
                        break;
                    
                    Event.current.Use();
                    break;
                }
                case EventType.ExecuteCommand:
                {
                    if (Event.current.commandName != "SelectAll")
                        break;
                    
                    var transforms = new List<UnityEngine.Object>();
                    for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                    {
                        var scene = SceneManager.GetSceneAt(sceneIndex);
                        if (!scene.isLoaded)
                            continue;
                        foreach (var gameObject in scene.GetRootGameObjects())
                        {
                            foreach (var transform in gameObject.GetComponentsInChildren<Transform>())
                            {
                                if ((transform.hideFlags & (HideFlags.NotEditable | HideFlags.HideInHierarchy)) == (HideFlags.NotEditable | HideFlags.HideInHierarchy))
                                    continue;
                                transforms.Add(transform.gameObject);
                            }
                        }
                    }

                    var foundObjects = transforms.ToArray();
                        
                    RemoveGeneratedMeshesFromArray(ref foundObjects);

                    Selection.objects = foundObjects;

                    Event.current.Use();
                    break;
                }

                /*
                case EventType.ValidateCommand:
                {
                    if (Event.current.commandName == "SelectAll")
                    {
                        Event.current.Use();
                        break;
                    }
                    if (Keys.HandleSceneValidate(EditModeManager.CurrentTool, true))
                    {
                        Event.current.Use();
                        HandleUtility.Repaint();
                    }
                    break; 
                }
                case EventType.ExecuteCommand:
                {
                    if (Event.current.commandName == "SelectAll")
                    {
                        var transforms = new List<UnityEngine.Object>();
                        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                        {
                            var scene = SceneManager.GetSceneAt(sceneIndex);
                            foreach (var gameObject in scene.GetRootGameObjects())
                            {
                                foreach (var transform in gameObject.GetComponentsInChildren<Transform>())
                                {
                                    if ((transform.hideFlags & (HideFlags.NotEditable | HideFlags.HideInHierarchy)) == (HideFlags.NotEditable | HideFlags.HideInHierarchy))
                                        continue;
                                    transforms.Add(transform.gameObject);
                                }
                            }
                        }
                        Selection.objects = transforms.ToArray();

                        Event.current.Use();
                        break;
                    }
                    break;
                }

                case EventType.KeyDown:
                {
                    if (Keys.HandleSceneKeyDown(EditModeManager.CurrentTool, true))
                    {
                        Event.current.Use();
                        HandleUtility.Repaint();
                    }
                    break;
                }

                case EventType.KeyUp:
                {
                    if (Keys.HandleSceneKeyUp(EditModeManager.CurrentTool, true))
                    {
                        Event.current.Use();
                        HandleUtility.Repaint();
                    }
                    break;
                }
                */
            }		
            
            if (click)
            {
                // make sure GeneratedMeshes are not part of our selection
                RemoveGeneratedMeshesFromSelection();

                DoSelectionClick(sceneView, Event.current.mousePosition);
            }
        }
        
        // TODO: make selecting variants work when selecting in hierarchy/rect-select too
        public static void DoSelectionClick(SceneView sceneView, Vector2 mousePosition)
        {
            ChiselIntersection intersection;
            var gameobject = ChiselClickSelectionManager.PickClosestGameObject(mousePosition, out intersection);

            // If we're a child of an composite that has a "handle as one" flag set, return that instead
            gameobject = ChiselSceneQuery.FindSelectionBase(gameobject); 
            
            var selectionType = GetCurrentSelectionType();

            var selectedObjectsOnClick = new List<int>(Selection.instanceIDs);
            switch (selectionType)
            {
                case SelectionType.Additive:
                {
                    if (!gameobject)
                        break;
                    
                    ChiselSyncSelection.SelectBrushVariant(intersection.brushIntersection.brush, uniqueSelection: false);
                    var instanceID = gameobject.GetInstanceID();
                    selectedObjectsOnClick.Add(instanceID);
                    ChiselClickSelectionManager.ignoreSelectionChanged = true;
                    Selection.instanceIDs = selectedObjectsOnClick.ToArray();
                    break;
                }
                case SelectionType.Subtractive:
                {
                    if (!gameobject)
                        break;
                    
                    Undo.RecordObject(ChiselSyncSelection.Instance, "Deselected brush variant");
                    ChiselSyncSelection.DeselectBrushVariant(intersection.brushIntersection.brush);
                    // Can only deselect brush if all it's synchronized brushes have also been deselected
                    if (!ChiselSyncSelection.IsAnyBrushVariantSelected(intersection.brushIntersection.brush))
                    {
                        var instanceID = gameobject.GetInstanceID();
                        selectedObjectsOnClick.Remove(instanceID);
                    }
                    ChiselClickSelectionManager.ignoreSelectionChanged = true;
                    Selection.instanceIDs = selectedObjectsOnClick.ToArray();
                    return;
                }
                default:
                { 
                    Undo.RecordObject(ChiselSyncSelection.Instance, "Selected brush variant");
                    ChiselSyncSelection.SelectBrushVariant(intersection.brushIntersection.brush, uniqueSelection: true);
                    ChiselClickSelectionManager.ignoreSelectionChanged = true;
                    Selection.activeGameObject = gameobject;
                    break;
                }
            }
        }
    }
}