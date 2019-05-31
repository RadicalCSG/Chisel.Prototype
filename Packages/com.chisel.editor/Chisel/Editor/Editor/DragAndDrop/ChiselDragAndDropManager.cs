using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public sealed class ChiselDragAndDropManager : ScriptableObject
    {
        static ChiselDragAndDropManager _instance;
        public static ChiselDragAndDropManager Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                var foundInstances = UnityEngine.Object.FindObjectsOfType<ChiselDragAndDropManager>();
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<ChiselDragAndDropManager>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    return _instance;
                }

                _instance = foundInstances[0];
                return _instance;
            }
        }

        static IChiselDragAndDropOperation dragAndDropOperation;

        static readonly int CSGDragAndDropManagerHash = "CSGDragAndDropManager".GetHashCode();

        public void OnSceneGUI(SceneView sceneView)
        {
            int id = GUIUtility.GetControlID(CSGDragAndDropManagerHash, FocusType.Keyboard);
            switch (Event.current.type)
            {
                case EventType.ValidateCommand:
                {
                    // TODO:
                    // "Copy", "Cut", "Paste", "Delete", "SoftDelete", "Duplicate", "FrameSelected", "FrameSelectedWithLock", "SelectAll", "Find" and "FocusProjectWindow".
                    //Debug.Log(Event.current.commandName);
                    break;
                }
                case EventType.DragUpdated:
                {
                    if (dragAndDropOperation == null &&
                        DragAndDrop.activeControlID == 0)
                    {
                        dragAndDropOperation = ChiselDragAndDropMaterial.AcceptDrag();
                        if (dragAndDropOperation != null)
                            DragAndDrop.activeControlID = id;
                    }

                    if (dragAndDropOperation != null)
                    {
                        dragAndDropOperation.UpdateDrag();
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        DragAndDrop.activeControlID = id;
                        Event.current.Use();
                    }
                    break;
                }
                case EventType.DragPerform:
                {
                    if (dragAndDropOperation != null)
                    {
                        dragAndDropOperation.PerformDrag();
                        dragAndDropOperation = null;
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.activeControlID = 0;
                        Event.current.Use();
                    }
                    break;
                }
                case EventType.DragExited:
                {
                    if (dragAndDropOperation != null)
                    {
                        dragAndDropOperation.CancelDrag();
                        dragAndDropOperation = null;
                        DragAndDrop.activeControlID = 0;
                        HandleUtility.Repaint();
                        Event.current.Use();
                    }
                    break;
                }
            }
        }
    }
}
