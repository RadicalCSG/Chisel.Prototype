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
        void OnEnable();
        void OnDisable();
        void OnSceneGUI(SceneView sceneView, Rect dragArea);
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

        const float kSingleLineHeight = 20f;
        const float kSingleSpacing = 0.0f;
        const float kGeneratorSeparator = 5.0f;
        const float kExtraBottomSpacing = 2.0f;

        sealed class CSGEditModeItem
        {
            public CSGEditModeItem(ChiselEditMode value, GUIContent content) { this.value = value; this.content = content; }
            public ChiselEditMode	value;
            public GUIContent   content;
        }

        static readonly CSGEditModeItem[] editModes = new[]
        {
            new CSGEditModeItem(ChiselEditMode.Object,         new GUIContent("Object")),
            new CSGEditModeItem(ChiselEditMode.Pivot,          new GUIContent("Pivot")),
            new CSGEditModeItem(ChiselEditMode.ShapeEdit,      new GUIContent("Shape Edit")),
            new CSGEditModeItem(ChiselEditMode.SurfaceEdit,    new GUIContent("Surface Edit")),
        };

        static readonly CSGEditModeItem[] generatorModes = new[]
        {
            new CSGEditModeItem(ChiselEditMode.FreeDraw,		new GUIContent("FreeDraw")),

            new CSGEditModeItem(ChiselEditMode.Box,			new GUIContent("Box")),
            new CSGEditModeItem(ChiselEditMode.Cylinder,		new GUIContent("Cylinder")),

            new CSGEditModeItem(ChiselEditMode.Capsule,        new GUIContent("Capsule")),
            new CSGEditModeItem(ChiselEditMode.Hemisphere,     new GUIContent("Hemisphere")),
            new CSGEditModeItem(ChiselEditMode.Sphere,         new GUIContent("Sphere")),

//          new CSGEditModeItem(CSGEditMode.Torus,          new GUIContent("Torus")),
//          new CSGEditModeItem(CSGEditMode.Stadium,        new GUIContent("Stadium")),

//          new CSGEditModeItem(CSGEditMode.RevolvedShape,  new GUIContent("Revolved Shape")),

 //         new CSGEditModeItem(CSGEditMode.PathedStairs,   new GUIContent("Pathed Stairs")),

            new CSGEditModeItem(ChiselEditMode.LinearStairs,   new GUIContent("Linear Stairs")),
            new CSGEditModeItem(ChiselEditMode.SpiralStairs,   new GUIContent("Spiral Stairs"))
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
                var value = GUI.Toggle(togglePosition, ChiselEditModeManager.EditMode == editMode.value, editMode.content, GUI.skin.button);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    // If we're changing edit mode from a generator, we restore our previous selection.
                    if (Instance.HaveSelection())
                        Instance.RestoreSelection(skipEditMode: true);
                    ChiselEditModeManager.EditMode = editMode.value;
                    ChiselEditorSettings.Save();
                }
                togglePosition.y += kSingleLineHeight + kSingleSpacing;
            }

            togglePosition.y += kGeneratorSeparator;

            for (int i = 0; i < generatorModes.Length; i++)
            {
                var editMode = generatorModes[i];
                EditorGUI.BeginChangeCheck();
                var value = GUI.Toggle(togglePosition, ChiselEditModeManager.EditMode == editMode.value, editMode.content, GUI.skin.button);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    // When we select a generator, we don't want anything else selected since the combination makes no sense.
                    // We store the selection, however, if our previous edit mode was not a generator.
                    if (ChiselEditModeManager.EditMode < ChiselEditMode.FirstGenerator)
                        Instance.RememberSelection();
                    ChiselEditModeManager.EditMode = editMode.value;
                    ChiselEditorSettings.Save();
                }
                togglePosition.y += kSingleLineHeight + kSingleSpacing;
            }
        }

        public void OnSelectionChanged()
        {
            // Make sure we're currently in a non-generator, otherwise this makes no sense
            // We might actually be currently restoring a selection
            if (ChiselEditModeManager.EditMode < ChiselEditMode.FirstGenerator)
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
                ChiselEditModeManager.EditMode = ChiselEditMode.ShapeEdit;
            else
                ChiselEditModeManager.EditMode = ChiselEditMode.Object;
        }


        [SerializeField] UnityEngine.Object[] prevSelection = null;
        [SerializeField] ChiselEditMode prevEditMode = ChiselEditMode.Object;
        
        internal bool HaveSelection()
        {
            return (prevSelection != null);
        }

        void RememberSelection()
        {
            prevSelection = Selection.objects;
            prevEditMode = ChiselEditModeManager.EditMode;
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
                ChiselEditModeManager.EditMode = prevEditMode;
            ClearStoredSelection();
        }

        void ClearStoredSelection()
        {
            prevSelection = null;
            prevEditMode = ChiselEditMode.Object;
        }



        static ChiselObjectEditMode		ObjectEditMode			= new ChiselObjectEditMode();
        static ChiselPivotEditMode			PivotEditMode			= new ChiselPivotEditMode();
        static ChiselSurfaceEditMode		SurfaceEditMode			= new ChiselSurfaceEditMode();
        static ChiselShapeEditMode			ShapeEditMode			= new ChiselShapeEditMode();
        
        // TODO: automatically find generators
        static ChiselExtrudedShapeGeneratorMode	    ExtrudedShapeGeneratorMode	= new ChiselExtrudedShapeGeneratorMode();
        static ChiselRevolvedShapeGeneratorMode	    RevolvedShapeGeneratorMode	= new ChiselRevolvedShapeGeneratorMode();

        static ChiselBoxGeneratorMode				BoxGeneratorMode			= new ChiselBoxGeneratorMode();
        static ChiselCylinderGeneratorMode			CylinderGeneratorMode		= new ChiselCylinderGeneratorMode();
        static ChiselHemisphereGeneratorMode        HemisphereGeneratorMode     = new ChiselHemisphereGeneratorMode();
        static ChiselSphereGeneratorMode            SphereGeneratorMode         = new ChiselSphereGeneratorMode();
        static ChiselCapsuleGeneratorMode           CapsuleGeneratorMode        = new ChiselCapsuleGeneratorMode();
        static ChiselTorusGeneratorMode			    TorusGeneratorMode			= new ChiselTorusGeneratorMode();
        static ChiselStadiumGeneratorMode           StadiumGeneratorMode        = new ChiselStadiumGeneratorMode();

        static ChiselPathedStairsGeneratorMode      PathedStairsGeneratorMode   = new ChiselPathedStairsGeneratorMode();
        static ChiselLinearStairsGeneratorMode      LinearStairsGeneratorMode   = new ChiselLinearStairsGeneratorMode();
        static ChiselSpiralStairsGeneratorMode		SpiralStairsGeneratorMode	= new ChiselSpiralStairsGeneratorMode();

        static IChiselToolMode         prevToolMode    = null;

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
            

            IChiselToolMode currentToolMode = null;
            switch (ChiselEditModeManager.EditMode)
            {
                // Edit modes
                case ChiselEditMode.Object:		currentToolMode = ObjectEditMode;	break;
                case ChiselEditMode.Pivot:			currentToolMode = PivotEditMode;	break;
                case ChiselEditMode.SurfaceEdit:	currentToolMode = SurfaceEditMode;	break;
                case ChiselEditMode.ShapeEdit:		currentToolMode = ShapeEditMode;	break;
                
                // Generators
                case ChiselEditMode.FreeDraw:		currentToolMode = ExtrudedShapeGeneratorMode; break;
                case ChiselEditMode.RevolvedShape:	currentToolMode = RevolvedShapeGeneratorMode; break;
                
                case ChiselEditMode.Box:			currentToolMode = BoxGeneratorMode; break;
                case ChiselEditMode.Stadium:		currentToolMode = StadiumGeneratorMode; break;
                case ChiselEditMode.Cylinder:		currentToolMode = CylinderGeneratorMode; break;
                case ChiselEditMode.Hemisphere:	currentToolMode = HemisphereGeneratorMode; break;
                case ChiselEditMode.Sphere:		currentToolMode = SphereGeneratorMode; break;
                case ChiselEditMode.Capsule:		currentToolMode = CapsuleGeneratorMode; break;
                case ChiselEditMode.Torus:			currentToolMode = TorusGeneratorMode; break;
                
                case ChiselEditMode.PathedStairs:	currentToolMode = PathedStairsGeneratorMode; break;
                case ChiselEditMode.LinearStairs:	currentToolMode = LinearStairsGeneratorMode; break;
                case ChiselEditMode.SpiralStairs:	currentToolMode = SpiralStairsGeneratorMode; break;
            }


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
                if (ChiselEditModeManager.EditMode >= ChiselEditMode.FirstGenerator)
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
