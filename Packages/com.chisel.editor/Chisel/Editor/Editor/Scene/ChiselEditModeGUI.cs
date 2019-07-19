using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public interface IChiselToolMode
    {
        string          ToolName    { get; }

        bool EnableComponentEditors { get; }
        bool CanSelectSurfaces      { get; }
        bool ShowCompleteOutline    { get; }

        void OnEnable();
        void OnDisable();

        void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }

    public abstract class ChiselGeneratorToolMode : IChiselToolMode
    {
        public abstract string  ToolName                { get; }

        public virtual bool     EnableComponentEditors  { get { return false; } }
        public virtual bool     CanSelectSurfaces       { get { return false; } }
        public virtual bool     ShowCompleteOutline     { get { return true; } }

        public virtual void     OnEnable()
        {
            // TODO: shouldn't just always set this param
            Tools.hidden = true;
            Reset();
        }

        public virtual void     OnDisable()
        {
            Reset();
        }

        public virtual void     Reset() { }

        public void Commit(GameObject newGameObject)
        {
            if (!newGameObject)
            {
                Cancel();
                return;
            }
            UnityEditor.Selection.activeGameObject = newGameObject;
            Reset();
            ChiselEditModeManager.EditModeType = typeof(ChiselShapeEditMode);
        }

        public void Cancel()
        { 
            Reset();
            Undo.RevertAllInCurrentGroup();
            EditorGUIUtility.ExitGUI();
        }

        public virtual void     OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.KeyDown:
                case EventType.ValidateCommand:
                {
                    if (Tools.current == Tool.View ||
                        Tools.current == Tool.None ||
                        (evt.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != EventModifiers.None ||
                        GUIUtility.hotControl != 0)
                        break;

                    if (evt.keyCode == KeyCode.Escape)
                    {
                        evt.Use();
                        break;
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (Tools.current == Tool.View ||
                        Tools.current == Tool.None ||
                        (evt.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != EventModifiers.None ||
                        GUIUtility.hotControl != 0)
                        break;

                    if (evt.keyCode == KeyCode.Escape)
                    {
                        evt.Use();
                        ChiselEditModeGUI.RestoreEditModeState();
                        GUIUtility.ExitGUI();
                    }
                    break;
                }
            }
        }
    }

    // TODO: add ability to store position (per sceneview?)
    // TODO: add ability to become dockable window?
    // TODO: add scrollbar support
    // TODO: use icons, make this look better
    public class ChiselEditModeGUI : ScriptableObject
    {
        #region Instance
        static ChiselEditModeGUI _instance;
        public static ChiselEditModeGUI Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = ScriptableObject.CreateInstance<ChiselEditModeGUI>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;
            }
        }
        #endregion

        const float kSingleLineHeight   = 20f;
        const float kSingleSpacing      = 0.0f;
        const float kGeneratorSeparator = 5.0f;
        const float kExtraBottomSpacing = 2.0f;


        static GUIResizableWindow editModeWindow;

        static void EditModeButton(ChiselEditModeManager.CSGEditModeItem editMode, Rect togglePosition)
        {
            EditorGUI.BeginChangeCheck();
            var value = GUI.Toggle(togglePosition, ChiselEditModeManager.EditMode == editMode.instance, editMode.content, GUI.skin.button);
            
            if (EditorGUI.EndChangeCheck() && value)
            {
                // If we're changing edit mode from a generator, we restore our previous selection.
                if (Instance.HaveStoredEditModeState())
                    RestoreEditModeState(skipEditMode: true);
                ChiselEditModeManager.EditMode = editMode.instance;
                ChiselEditorSettings.Save();
            }
        }

        static void OnWindowGUI(Rect position)
        {
            var editModes       = ChiselEditModeManager.editModes;
            var generatorModes  = ChiselEditModeManager.generatorModes;

            var togglePosition = position;
            togglePosition.height = kSingleLineHeight * 2;

            togglePosition.width *= 0.5f;
            EditModeButton(editModes[0], togglePosition);
            togglePosition.x += togglePosition.width;
            EditModeButton(editModes[1], togglePosition);
            togglePosition.y += kSingleLineHeight * 2;
            EditModeButton(editModes[3], togglePosition);
            togglePosition.x -= togglePosition.width;
            EditModeButton(editModes[2], togglePosition);

            // Spacing
            togglePosition.y += kSingleLineHeight * 2;
            togglePosition.y += kGeneratorSeparator;

            // Reset button size
            // togglePosition.x -= togglePosition.width;
            togglePosition.width *= 2f;
            togglePosition.height = kSingleLineHeight;

            for (int i = 0; i < generatorModes.Length; i++)
            {
                var editMode = generatorModes[i];
                EditorGUI.BeginChangeCheck();
                var value = GUI.Toggle(togglePosition, ChiselEditModeManager.EditMode == editMode.instance, editMode.content, GUI.skin.button);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    // When we select a generator, we don't want anything else selected since the combination makes no sense.
                    // We store the selection, however, if our previous edit mode was not a generator.
                    if (!(ChiselEditModeManager.EditMode is ChiselGeneratorToolMode))
                        Instance.StoreEditModeState();
                    ChiselEditModeManager.EditMode = editMode.instance;
                    ChiselEditorSettings.Save();
                }
                togglePosition.y += kSingleLineHeight + kSingleSpacing;
            }
        }

        public void OnSelectionChanged()
        {
            // Make sure we're currently in a non-generator, otherwise this makes no sense
            // We might actually be currently restoring a selection
            if (!(ChiselEditModeManager.EditMode is ChiselGeneratorToolMode))
                return;

            var activeObject = Selection.activeObject;
            // This event is fired when we select or deselect something.
            // We only care if we select something
            if (activeObject == null)
                return;

            // We just selected something in the editor, so we want to get rid of our 
            // stored selection to avoid restoring an old selection for no reason later on.
            ClearStoredEditModeState();
            
            var is_generator = activeObject is Components.ChiselGeneratorComponent;
            if (!is_generator)
            {
                var gameObject = activeObject as GameObject;
                if (gameObject != null)
                    is_generator = gameObject.GetComponent<Components.ChiselGeneratorComponent>() != null;
            }

            if (is_generator)
                ChiselEditModeManager.EditModeType = typeof(ChiselShapeEditMode);
            else
                ChiselEditModeManager.EditModeType = typeof(ChiselObjectEditMode);
        }

        #region Edit Mode State
        [SerializeField] UnityEngine.Object[]   storedSelection = null;
        [SerializeField] IChiselToolMode        storedEditMode  = null;
        
        internal bool HaveStoredEditModeState()
        {
            return (storedSelection != null);
        }

        void StoreEditModeState()
        {
            storedSelection = Selection.objects;
            storedEditMode = ChiselEditModeManager.EditMode;
            Selection.activeObject = null;
        }

        internal static void RestoreEditModeState(bool skipEditMode = false)
        {
            var instance = Instance;
            if (instance.storedSelection != null)
                Selection.objects = instance.storedSelection;
            else
                // Selection.objects doesn't like being set to null, 
                // it'll generate an error, Selection.activeObject has 
                // no problem with it however.
                Selection.activeObject = null;
            if (!skipEditMode)
                ChiselEditModeManager.EditMode = instance.storedEditMode;
            instance.ClearStoredEditModeState();
        }

        void ClearStoredEditModeState()
        {
            storedSelection = null;
            storedEditMode = null;
        }
        #endregion


        static IChiselToolMode prevToolMode = null;
        public static void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var editModes       = ChiselEditModeManager.editModes;
            var generatorModes  = ChiselEditModeManager.generatorModes;

            if (editModeWindow == null)
            {
                var minWidth	= 80;
                var minHeight	= 40;
                var rect		= new Rect(0, 0, 92, 24 + ((editModes.Length + generatorModes.Length) * (kSingleLineHeight + kSingleSpacing)) + kGeneratorSeparator + kExtraBottomSpacing );
                editModeWindow = new GUIResizableWindow("Tools", rect, minWidth, minHeight, OnWindowGUI);
            }

            editModeWindow.Show(dragArea);
            

            IChiselToolMode currentToolMode = ChiselEditModeManager.EditMode;

            if (currentToolMode != prevToolMode)
            {
                if (prevToolMode != null) prevToolMode.OnDisable();
                
                // Set defaults
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
                Tools.hidden = false; 

                if (currentToolMode != null) currentToolMode.OnEnable();
            }
            prevToolMode = currentToolMode;


            if (currentToolMode != null)
            {
                dragArea.x = 0;
                dragArea.y = 0;
                currentToolMode.OnSceneGUI(sceneView, dragArea);
            }
        }
    }
}
