using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
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
#if UNITY_2022_3_OR_NEWER
                RectSelectionID = (int)rectSelectionIDField.GetValue(rectSelection);
#endif
            }
        }

#if UNITY_2022_3_OR_NEWER
        public static bool      RectSelecting       { get { return RectSelectionID != 0 && (GUIUtility.hotControl == RectSelectionID || HandleUtility.nearestControl == RectSelectionID); } }
        public static bool      IsNearestControl    { get { return (bool)isNearestControlField.GetValue(rectSelection); } }
#else
        public static bool		RectSelecting		{ get { return (bool)rectSelectingField.GetValue(rectSelection); } }
#endif
        public static Vector2	SelectStartPoint	{ get { return (Vector2)selectStartPointField.GetValue(rectSelection); } }
        public static Vector2	SelectMousePoint	{ get { return (Vector2)selectMousePointField.GetValue(rectSelection); } }
        public static Object[]	SelectionStart		{ get { return (Object[])selectionStartField.GetValue(rectSelection); } set { selectionStartField.SetValue(rectSelection, value); } }
        public static Object[]  CurrentSelection	{ get { return (Object[])currentSelectionField.GetValue(rectSelection); } set { currentSelectionField.SetValue(rectSelection, value); } }
        public static Dictionary<GameObject, bool>	LastSelection { get { return (Dictionary<GameObject, bool>)lastSelectionField.GetValue(rectSelection); } }

        public static void UpdateSelection(Object[] existingSelection, Object[] newObjects, SelectionType type)
        {
            object selectionType;
            switch (type)
            {
                default:						selectionType = selectionTypeNormal; break;
                case SelectionType.Additive:	selectionType = selectionTypeAdditive; break;
                case SelectionType.Subtractive:	selectionType = selectionTypeSubtractive; break;
            }

#if UNITY_2022_3_OR_NEWER
            updateSelectionMethod.Invoke(rectSelection,
                new object[]
                {
                    existingSelection,
                    newObjects,
                    selectionType,
                    RectSelecting
                });
#else
            updateSelectionMethod.Invoke(null,
                new object[] 
                {
                    existingSelection,
                    newObjects,
                    selectionType,
                    RectSelecting
                });
#endif
        }

        static Type			unityRectSelectionType;
        static Type			unityEnumSelectionType;

        static object		selectionTypeAdditive;
        static object		selectionTypeSubtractive;
        static object		selectionTypeNormal;
            
        static FieldInfo	rectSelectionField;
#if !UNITY_2022_3_OR_NEWER
        static FieldInfo	rectSelectingField;
#endif
        static FieldInfo	selectStartPointField;
        static FieldInfo    isNearestControlField;
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

#if UNITY_2022_3_OR_NEWER
            rectSelectionIDField        = unityRectSelectionType.GetField("k_RectSelectionID",  BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (rectSelectionIDField == null) return;

            RectSelectionID             = 0;
            selectStartPointField		= unityRectSelectionType.GetField("m_StartPoint",	    BindingFlags.NonPublic | BindingFlags.Instance);
            isNearestControlField       = unityRectSelectionType.GetField("m_IsNearestControl",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectionStartField			= unityRectSelectionType.GetField("m_SelectionStart",	BindingFlags.NonPublic | BindingFlags.Instance);
            lastSelectionField			= unityRectSelectionType.GetField("m_LastSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            currentSelectionField		= unityRectSelectionType.GetField("m_CurrentSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectMousePointField		= unityRectSelectionType.GetField("m_SelectMousePoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            updateSelectionMethod       = unityRectSelectionType.GetMethod("UpdateSelection",   BindingFlags.NonPublic | BindingFlags.Instance,
                                                                            null,
                                                                            new Type[] {
                                                                                typeof(Object[]),
                                                                                typeof(Object[]),
                                                                                unityEnumSelectionType,
                                                                                typeof(bool)
                                                                            },
                                                                            null);
#else
            rectSelectionIDField		= unityRectSelectionType.GetField("s_RectSelectionID",	BindingFlags.NonPublic | BindingFlags.Static);
            if (rectSelectionIDField == null) return;

            RectSelectionID				= (int)rectSelectionIDField.GetValue(null);
            rectSelectingField			= unityRectSelectionType.GetField("m_RectSelecting",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectStartPointField		= unityRectSelectionType.GetField("m_SelectStartPoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectionStartField			= unityRectSelectionType.GetField("m_SelectionStart",	BindingFlags.NonPublic | BindingFlags.Instance);
            lastSelectionField			= unityRectSelectionType.GetField("m_LastSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            currentSelectionField		= unityRectSelectionType.GetField("m_CurrentSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectMousePointField		= unityRectSelectionType.GetField("m_SelectMousePoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            updateSelectionMethod = unityRectSelectionType.GetMethod("UpdateSelection", BindingFlags.NonPublic | BindingFlags.Static,
                                                                            null,
                                                                            new Type[] {
                                                                                typeof(Object[]),
                                                                                typeof(Object[]),
                                                                                unityEnumSelectionType,
                                                                                typeof(bool)
                                                                            },
                                                                            null);
#endif

            selectionTypeAdditive       = Enum.Parse(unityEnumSelectionType, "Additive");
            selectionTypeSubtractive	= Enum.Parse(unityEnumSelectionType, "Subtractive");
            selectionTypeNormal			= Enum.Parse(unityEnumSelectionType, "Normal");
            
            reflectionSucceeded =
#if !UNITY_2022_3_OR_NEWER
                                    rectSelectingField          != null &&
#endif

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


        static int[]    previousSelection   = null;
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

        static bool RemoveGeneratedMeshesFromArray(ref Object[] selection)
        {
            var found = new List<Object>();
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

        internal static void Update(SceneView sceneview)
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

            ChiselRectSelection.SceneView = sceneview;


            var rectSelectionID		= ChiselRectSelection.RectSelectionID;
            var hotControl			= GUIUtility.hotControl;
            var areRectSelecting	= rectSelectionID != 0 && hotControl == rectSelectionID;
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
#if UNITY_2022_3_OR_NEWER
                        prevStartScreenPoint    = selectStartPoint;
#else
                        prevStartScreenPoint    = Event.current.mousePosition;
#endif
                        needUpdate = true;
                    }
                    if (prevMouseGUIPoint != selectMousePoint)
                    {
                        prevMouseGUIPoint	 = selectMousePoint;
#if UNITY_2022_3_OR_NEWER
                        prevMouseScreenPoint = selectMousePoint;
#else
                        prevMouseScreenPoint = Event.current.mousePosition;
#endif
                        needUpdate = true;
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

                            // TODO: modify this depending on debug rendermode
                            LayerUsageFlags visibleLayerFlags = LayerUsageFlags.Renderable;

                            // Find all the brushes (and it's gameObjects) that are inside the frustum
                            if (!ChiselSceneQuery.GetNodesInFrustum(frustum, UnityEditor.Tools.visibleLayers, visibleLayerFlags, ref rectFoundTreeNodes))
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

                    Object[] currentSelection = null;
                    var originalLastSelection	= ChiselRectSelection.LastSelection;
                    var originalSelectionStart	= ChiselRectSelection.SelectionStart;

                    if (originalLastSelection != null)
                    {
                        if (modified &&
                            rectFoundGameObjects != null &&
                            rectFoundGameObjects.Count > 0)
                        {
                            foreach (var obj in rectFoundGameObjects)
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
                    } else
                        currentSelection = null;
                    
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

#if !UNITY_2022_3_OR_NEWER
            int pickingControlId = 0;
#endif

            bool click = false;
            if (hotControl != rectSelectionID)
            {
                prevStartGUIPoint = Vector2.zero;
                prevMouseGUIPoint = Vector2.zero;
                rectFoundGameObjects.Clear();
                rectFoundTreeNodes.Clear();


#if !UNITY_2022_3_OR_NEWER
                // Register a control so that if no other handle is engaged, we can use the event.
                pickingControlId = GUIUtility.GetControlID(FocusType.Passive);
#endif
            }

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
                    mouseDragged = false;
                    break;
                }
                case EventType.MouseMove:
                {
                    rectClickDown = false;
                    mouseDragged = false;
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
                        Event.current.keyCode == ChiselKeyboardDefaults.kCancelKey)
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
                    
                    var transforms = new List<Object>();
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
            }

#if !UNITY_2022_3_OR_NEWER
            if (pickingControlId != 0)
            {
                HandleUtility.AddDefaultControl(pickingControlId);
                var pickingType = Event.current.GetTypeForControl(pickingControlId);
                switch (pickingType)
                {
                    case EventType.MouseDown:
                    {
                        GUIUtility.hotControl = pickingControlId;
                        rectClickDown = true;
                        break;
                    }
                    case EventType.MouseUp:
                    {
                        if (GUIUtility.hotControl != pickingControlId)
                            break;
                        GUIUtility.hotControl = 0;
                        if (!mouseDragged)
                        {
                            var delta = Event.current.mousePosition - clickMousePosition;
                            if (Mathf.Abs(delta.x) > 4 || Mathf.Abs(delta.y) > 4)
                            { mouseDragged = true; }
                        }
                        GUIUtility.hotControl = pickingControlId;
                        if (mouseDragged || !rectClickDown || Event.current.button != 0 || ChiselRectSelection.RectSelecting)
                        {
                            rectClickDown = false; 
                            break; 
                        }

                        click = true;
                        Event.current.Use();
                        break;
                    }
                }
            }
#else
            if (!(sceneview == null) && Event.current.commandName == "SceneViewPickingEventCommand" && ChiselRectSelection.IsNearestControl)
            {
                // We call unity's own click method after we detected we need to handle click, which sends the exact same message again *sigh*
                if (previousSelection == null)
                {
                    click = true;
                } else
                {
                    Selection.instanceIDs = previousSelection;
                }
                previousSelection = null;
            }
#endif

            if (click)
            {
                click = false;
                // make sure GeneratedMeshes are not part of our selection
                RemoveGeneratedMeshesFromSelection();

#if UNITY_2022_3_OR_NEWER
                Selection.instanceIDs = DoSelectionClick(ChiselRectSelection.SelectStartPoint);
                previousSelection = Selection.instanceIDs;
#else
                Selection.instanceIDs = DoSelectionClick(Event.current.mousePosition);
#endif
            }
        }
        
        // TODO: make selecting variants work when selecting in hierarchy/rect-select too
        public static int[] DoSelectionClick(Vector2 mousePosition)
        {
            ChiselIntersection intersection;
            var gameobject = ChiselClickSelectionManager.PickClosestGameObject(mousePosition, out intersection);

            // If we're a child of an composite that has a "handle as one" flag set, return that instead
            gameobject = ChiselSceneQuery.FindSelectionBase(gameobject);
            if (!gameobject)
            {
                return Array.Empty<int>();
            }

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
                    break;
                }
                default:
                {
                    if (!gameobject)
                        break;
                    
                    Undo.RecordObject(ChiselSyncSelection.Instance, "Selected brush variant");
                    ChiselSyncSelection.SelectBrushVariant(intersection.brushIntersection.brush, uniqueSelection: true);
                    ChiselClickSelectionManager.ignoreSelectionChanged = true;
                    var instanceID = gameobject.GetInstanceID();
                    selectedObjectsOnClick.Clear();
                    selectedObjectsOnClick.Add(instanceID);
                    break;
                }
            }
            return selectedObjectsOnClick.ToArray();
        }

    }
}