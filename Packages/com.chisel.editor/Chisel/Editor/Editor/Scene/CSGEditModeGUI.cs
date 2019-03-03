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
	public static class CSGEditModeGUI
	{
		const float kSingleLineHeight = 20f;
		const float kSingleSpacing = 0.0f;

		sealed class CSGEditModeItem
		{
			public CSGEditModeItem(CSGEditMode value, GUIContent content) { this.value = value; this.content = content; }
			public CSGEditMode	value;
			public GUIContent   content;
		}

		static readonly CSGEditModeItem[] editModes = new []
		{
			new CSGEditModeItem(CSGEditMode.Object,			new GUIContent("Object")),
			new CSGEditModeItem(CSGEditMode.Pivot,			new GUIContent("Pivot")),
			new CSGEditModeItem(CSGEditMode.ShapeEdit,		new GUIContent("Shape Edit")),
			new CSGEditModeItem(CSGEditMode.SurfaceEdit,	new GUIContent("Surface Edit")),
			
			new CSGEditModeItem(CSGEditMode.FreeDraw,		new GUIContent("FreeDraw")),
			new CSGEditModeItem(CSGEditMode.RevolvedShape,	new GUIContent("Revolved Shape")),

			new CSGEditModeItem(CSGEditMode.Box,			new GUIContent("Box")),
			new CSGEditModeItem(CSGEditMode.Cylinder,		new GUIContent("Cylinder")),
			new CSGEditModeItem(CSGEditMode.Torus,          new GUIContent("Torus")),
			new CSGEditModeItem(CSGEditMode.Hemisphere,		new GUIContent("Hemisphere")),
			new CSGEditModeItem(CSGEditMode.Sphere,			new GUIContent("Sphere")),
			new CSGEditModeItem(CSGEditMode.Capsule,        new GUIContent("Capsule")),
			new CSGEditModeItem(CSGEditMode.Stadium,        new GUIContent("Stadium")),

			new CSGEditModeItem(CSGEditMode.PathedStairs,   new GUIContent("Pathed Stairs")),
			new CSGEditModeItem(CSGEditMode.LinearStairs,	new GUIContent("Linear Stairs")),
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
					CSGEditModeManager.EditMode = editMode.value;
					CSGEditorSettings.Save();
				}
				togglePosition.y += kSingleLineHeight + kSingleSpacing;
			}
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
		static CSGTorusGeneratorMode			TorusGeneratorMode			= new CSGTorusGeneratorMode();
		static CSGHemisphereGeneratorMode       HemisphereGeneratorMode     = new CSGHemisphereGeneratorMode();
		static CSGSphereGeneratorMode           SphereGeneratorMode         = new CSGSphereGeneratorMode();
		static CSGCapsuleGeneratorMode          CapsuleGeneratorMode        = new CSGCapsuleGeneratorMode();
		static CSGStadiumGeneratorMode          StadiumGeneratorMode        = new CSGStadiumGeneratorMode();

		static CSGPathedStairsGeneratorMode     PathedStairsGeneratorMode   = new CSGPathedStairsGeneratorMode();
		static CSGLinearStairsGeneratorMode     LinearStairsGeneratorMode   = new CSGLinearStairsGeneratorMode();
		static CSGSpiralStairsGeneratorMode		SpiralStairsGeneratorMode	= new CSGSpiralStairsGeneratorMode();

		static ICSGToolMode         prevToolMode = null;

		public static void OnSceneGUI(SceneView sceneView, Rect dragArea)
		{
			if (editModeWindow == null)
			{
				var minWidth	= 80;
				var minHeight	= 40;
				var rect		= new Rect(0, 0, 92, 24 + (editModes.Length * (kSingleLineHeight + kSingleSpacing)));
				editModeWindow = new GUIResizableWindow("Tools", rect, minWidth, minHeight, OnWindowGUI);
			}

			editModeWindow.Show(dragArea);
			

			ICSGToolMode currentToolMode = null;
			switch (CSGEditModeManager.EditMode)
			{
				case CSGEditMode.Object:		currentToolMode = ObjectEditMode;	break;
				case CSGEditMode.Pivot:			currentToolMode = PivotEditMode;	break;
				case CSGEditMode.SurfaceEdit:	currentToolMode = SurfaceEditMode;	break;
				case CSGEditMode.ShapeEdit:		currentToolMode = ShapeEditMode;	break;
				
				case CSGEditMode.FreeDraw:		currentToolMode = ExtrudedShapeGeneratorMode; break;
				case CSGEditMode.RevolvedShape:	currentToolMode = RevolvedShapeGeneratorMode; break;
				
				case CSGEditMode.Box:			currentToolMode = BoxGeneratorMode; break;
				case CSGEditMode.Cylinder:		currentToolMode = CylinderGeneratorMode; break;
				case CSGEditMode.Torus:			currentToolMode = TorusGeneratorMode; break;
				case CSGEditMode.Hemisphere:	currentToolMode = HemisphereGeneratorMode; break;
				case CSGEditMode.Sphere:		currentToolMode = SphereGeneratorMode; break;
				case CSGEditMode.Capsule:		currentToolMode = CapsuleGeneratorMode; break;
				case CSGEditMode.Stadium:		currentToolMode = StadiumGeneratorMode; break;
				
				case CSGEditMode.PathedStairs:	currentToolMode = PathedStairsGeneratorMode; break;
				case CSGEditMode.LinearStairs:	currentToolMode = LinearStairsGeneratorMode; break;
				case CSGEditMode.SpiralStairs:	currentToolMode = SpiralStairsGeneratorMode; break;
			}


			if (currentToolMode != prevToolMode)
			{
				if (prevToolMode    != null) prevToolMode.OnDisable();
				
				// Set defaults
				CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
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
