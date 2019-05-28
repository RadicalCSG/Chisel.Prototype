using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public interface ICSGToolMode
    {
        void OnEnable();
        void OnDisable();
        void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }

    // TODO: add ability to store position (per sceneview?)
    // TODO: add ability to become dockable window?
    // TODO: add scrollbar support
    // TODO: use icons, make this look better
    public class CSGEditModeGUI : ScriptableObject
    {
        #region Instance
        static CSGEditModeGUI _instance;
        public static CSGEditModeGUI Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = ScriptableObject.CreateInstance<CSGEditModeGUI>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;
            }
        }
        #endregion

        const float kSingleLineHeight = 20f;
        const float kSingleSpacing = 0.0f;
        const float kGeneratorSeparator = 5.0f;
        const float kExtraBottomSpacing = 2.0f;

        sealed class CSGEditModeItem
        {
            public CSGEditModeItem(CSGEditMode value, GUIContent content) { this.value = value; this.content = content; }
            public CSGEditMode	value;
            public GUIContent   content;
        }

        static readonly CSGEditModeItem[] editModes = new[]
        {
            new CSGEditModeItem(CSGEditMode.Object,         new GUIContent("Object")),
            new CSGEditModeItem(CSGEditMode.Pivot,          new GUIContent("Pivot")),
            new CSGEditModeItem(CSGEditMode.ShapeEdit,      new GUIContent("Shape Edit")),
            new CSGEditModeItem(CSGEditMode.SurfaceEdit,    new GUIContent("Surface Edit")),
        };

        static readonly CSGEditModeItem[] generatorModes = new[]
        {
            new CSGEditModeItem(CSGEditMode.FreeDraw,		new GUIContent("FreeDraw")),

            new CSGEditModeItem(CSGEditMode.Box,			new GUIContent("Box")),
            new CSGEditModeItem(CSGEditMode.Cylinder,		new GUIContent("Cylinder")),

            new CSGEditModeItem(CSGEditMode.Capsule,        new GUIContent("Capsule")),
            new CSGEditModeItem(CSGEditMode.Hemisphere,     new GUIContent("Hemisphere")),
            new CSGEditModeItem(CSGEditMode.Sphere,         new GUIContent("Sphere")),

//          new CSGEditModeItem(CSGEditMode.Torus,          new GUIContent("Torus")),
//          new CSGEditModeItem(CSGEditMode.Stadium,        new GUIContent("Stadium")),

//          new CSGEditModeItem(CSGEditMode.RevolvedShape,   new GUIContent("Revolved Shape")),

 //         new CSGEditModeItem(CSGEditMode.PathedStairs,   new GUIContent("Pathed Stairs")),

            new CSGEditModeItem(CSGEditMode.LinearStairs,   new GUIContent("Linear Stairs")),
            new CSGEditModeItem(CSGEditMode.SpiralStairs,   new GUIContent("Spiral Stairs"))
        };


        static GUIResizableWindow editModeWindow;

        static void OnWindowGUI(Rect position)
        {
            var togglePosition = position;
            togglePosition.height = kSingleLineHeight;
            for (int i = 0; i < editModes.Length; i++)
            {
                var editMode = editModes[i];
                EditorGUI.BeginChangeCheck();
                var value = GUI.Toggle(togglePosition, CSGEditModeManager.EditMode == editMode.value, editMode.content, GUI.skin.button);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    // If we're changing edit mode from a generator, we restore our previous selection.
                    if (Instance.HaveSelection())
                        Instance.RestoreSelection(skipEditMode: true);
                    CSGEditModeManager.EditMode = editMode.value;
                    CSGEditorSettings.Save();
                }
                togglePosition.y += kSingleLineHeight + kSingleSpacing;
            }

            togglePosition.y += kGeneratorSeparator;

            for (int i = 0; i < generatorModes.Length; i++)
            {
                var editMode = generatorModes[i];
                EditorGUI.BeginChangeCheck();
                var value = GUI.Toggle(togglePosition, CSGEditModeManager.EditMode == editMode.value, editMode.content, GUI.skin.button);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    // When we select a generator, we don't want anything else selected since the combination makes no sense.
                    // We store the selection, however, if our previous edit mode was not a generator.
                    if (CSGEditModeManager.EditMode < CSGEditMode.FirstGenerator)
                        Instance.RememberSelection();
                    CSGEditModeManager.EditMode = editMode.value;
                    CSGEditorSettings.Save();
                }
                togglePosition.y += kSingleLineHeight + kSingleSpacing;
            }
        }

        public void OnSelectionChanged()
        {
            // Make sure we're currently in a non-generator, otherwise this makes no sense
            // We might actually be currently restoring a selection
            if (CSGEditModeManager.EditMode < CSGEditMode.FirstGenerator)
                return;

            var activeObject = Selection.activeObject;
            // This event is fired when we select or deselect something.
            // We only care if we select something
            if (activeObject == null)
                return;

            // We just selected something in the editor, so we want to get rid of our 
            // stored selection to avoid restoring an old selection for no reason later on.
            ClearStoredSelection();
            
            var is_generator = activeObject is Components.ChiselGeneratorComponent;
            if (!is_generator)
            {
                var gameObject = activeObject as GameObject;
                if (gameObject != null)
                    is_generator = gameObject.GetComponent<Components.ChiselGeneratorComponent>() != null;
            }

            if (is_generator)
                CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
            else
                CSGEditModeManager.EditMode = CSGEditMode.Object;
        }


        [SerializeField] UnityEngine.Object[] prevSelection = null;
        [SerializeField] CSGEditMode prevEditMode = CSGEditMode.Object;
        
        internal bool HaveSelection()
        {
            return (prevSelection != null);
        }

        void RememberSelection()
        {
            prevSelection = Selection.objects;
            prevEditMode = CSGEditModeManager.EditMode;
            Selection.activeObject = null;
        }

        void RestoreSelection(bool skipEditMode = false)
        {
            if (prevSelection != null)
                Selection.objects = prevSelection;
            else
                // Selection.objects doesn't like being set to null, 
                // it'll generate an error, Selection.activeObject has 
                // no problem with it however.
                Selection.activeObject = null;
            if (!skipEditMode)
                CSGEditModeManager.EditMode = prevEditMode;
            ClearStoredSelection();
        }

        void ClearStoredSelection()
        {
            prevSelection = null;
            prevEditMode = CSGEditMode.Object;
        }



        static CSGObjectEditMode		ObjectEditMode			= new CSGObjectEditMode();
        static CSGPivotEditMode			PivotEditMode			= new CSGPivotEditMode();
        static CSGSurfaceEditMode		SurfaceEditMode			= new CSGSurfaceEditMode();
        static CSGShapeEditMode			ShapeEditMode			= new CSGShapeEditMode();
        
        // TODO: automatically find generators
        static CSGExtrudedShapeGeneratorMode	ExtrudedShapeGeneratorMode	= new CSGExtrudedShapeGeneratorMode();
        static CSGRevolvedShapeGeneratorMode	RevolvedShapeGeneratorMode	= new CSGRevolvedShapeGeneratorMode();

        static CSGBoxGeneratorMode				BoxGeneratorMode			= new CSGBoxGeneratorMode();
        static CSGCylinderGeneratorMode			CylinderGeneratorMode		= new CSGCylinderGeneratorMode();
        static CSGHemisphereGeneratorMode       HemisphereGeneratorMode     = new CSGHemisphereGeneratorMode();
        static CSGSphereGeneratorMode           SphereGeneratorMode         = new CSGSphereGeneratorMode();
        static CSGCapsuleGeneratorMode          CapsuleGeneratorMode        = new CSGCapsuleGeneratorMode();
        static CSGTorusGeneratorMode			TorusGeneratorMode			= new CSGTorusGeneratorMode();
        static CSGStadiumGeneratorMode          StadiumGeneratorMode        = new CSGStadiumGeneratorMode();

        static CSGPathedStairsGeneratorMode     PathedStairsGeneratorMode   = new CSGPathedStairsGeneratorMode();
        static CSGLinearStairsGeneratorMode     LinearStairsGeneratorMode   = new CSGLinearStairsGeneratorMode();
        static CSGSpiralStairsGeneratorMode		SpiralStairsGeneratorMode	= new CSGSpiralStairsGeneratorMode();

        static ICSGToolMode         prevToolMode    = null;

        public static void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            if (editModeWindow == null)
            {
                var minWidth	= 80;
                var minHeight	= 40;
                var rect		= new Rect(0, 0, 92, 24 + ((editModes.Length + generatorModes.Length) * (kSingleLineHeight + kSingleSpacing)) + kGeneratorSeparator + kExtraBottomSpacing );
                editModeWindow = new GUIResizableWindow("Tools", rect, minWidth, minHeight, OnWindowGUI);
            }

            editModeWindow.Show(dragArea);
            

            ICSGToolMode currentToolMode = null;
            switch (CSGEditModeManager.EditMode)
            {
                // Edit modes
                case CSGEditMode.Object:		currentToolMode = ObjectEditMode;	break;
                case CSGEditMode.Pivot:			currentToolMode = PivotEditMode;	break;
                case CSGEditMode.SurfaceEdit:	currentToolMode = SurfaceEditMode;	break;
                case CSGEditMode.ShapeEdit:		currentToolMode = ShapeEditMode;	break;
                
                // Generators
                case CSGEditMode.FreeDraw:		currentToolMode = ExtrudedShapeGeneratorMode; break;
                case CSGEditMode.RevolvedShape:	currentToolMode = RevolvedShapeGeneratorMode; break;
                
                case CSGEditMode.Box:			currentToolMode = BoxGeneratorMode; break;
                case CSGEditMode.Stadium:		currentToolMode = StadiumGeneratorMode; break;
                case CSGEditMode.Cylinder:		currentToolMode = CylinderGeneratorMode; break;
                case CSGEditMode.Hemisphere:	currentToolMode = HemisphereGeneratorMode; break;
                case CSGEditMode.Sphere:		currentToolMode = SphereGeneratorMode; break;
                case CSGEditMode.Capsule:		currentToolMode = CapsuleGeneratorMode; break;
                case CSGEditMode.Torus:			currentToolMode = TorusGeneratorMode; break;
                
                case CSGEditMode.PathedStairs:	currentToolMode = PathedStairsGeneratorMode; break;
                case CSGEditMode.LinearStairs:	currentToolMode = LinearStairsGeneratorMode; break;
                case CSGEditMode.SpiralStairs:	currentToolMode = SpiralStairsGeneratorMode; break;
            }


            if (currentToolMode != prevToolMode)
            {
                if (prevToolMode != null) prevToolMode.OnDisable();
                
                // Set defaults
                CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
                Tools.hidden = false; 

                if (currentToolMode != null) currentToolMode.OnEnable();
            }
            prevToolMode = currentToolMode;


            if (currentToolMode != null)
            {
                if (CSGEditModeManager.EditMode >= CSGEditMode.FirstGenerator)
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
                                Instance.RestoreSelection();
                                GUIUtility.ExitGUI();
                            }
                            break;
                        }
                    }
                }
                dragArea.x = 0;
                dragArea.y = 0;
                currentToolMode.OnSceneGUI(sceneView, dragArea);
            }
        }
    }
}
