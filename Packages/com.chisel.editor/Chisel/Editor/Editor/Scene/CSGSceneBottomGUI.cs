using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;

namespace Chisel.Editors
{
	// TODO: add tooltips
	public static class CSGSceneBottomGUI
	{
		const float kBottomBarHeight    = 20;
		const float kTopBarHeight       = 17;
		const float kFloatFieldWidth    = 60;

		static readonly int BottomBarGUIHash = typeof(CSGSceneBottomGUI).Name.GetHashCode();
		static GUIStyle toolbarStyle;
		static GUIStyle toggleStyle;
		static GUIStyle buttonStyle;
		static bool     prevSkin = false;

		static readonly int bottomBarGuiId					= 21001;


		public static void Rebuild()
		{
			CSGNodeHierarchyManager.Rebuild();
		}

		public static void ModifySnapDistance(float modifier) 
		{
			CSGEditorSettings.MoveSnapX = CSGEditorSettings.MoveSnapY = CSGEditorSettings.MoveSnapZ = CSGEditorSettings.MoveSnapX * modifier;
		}


		static void OnBottomBarUI(int windowID)
		{
			EditorGUI.BeginChangeCheck();
			GUILayout.BeginHorizontal();

			if (GUILayout.Button("Rebuild", buttonStyle)) { 
				Rebuild();
			}

			GUILayout.FlexibleSpace();

			CSGEditorSettings.ShowGrid = GUILayout.Toggle(CSGEditorSettings.ShowGrid, "Show Grid", toggleStyle);

			CSGEditorSettings.MoveSnapX = CSGEditorSettings.MoveSnapY = CSGEditorSettings.MoveSnapZ = EditorGUILayout.FloatField(CSGEditorSettings.MoveSnapX, GUILayout.Width(kFloatFieldWidth));
			if (GUILayout.Button("-", EditorStyles.miniButtonLeft)) { 
				ModifySnapDistance(0.5f);
			}
			if (GUILayout.Button("+", EditorStyles.miniButtonRight)) { 
				ModifySnapDistance(2.0f);
			}

			GUILayout.EndHorizontal();
			if (EditorGUI.EndChangeCheck())
				CSGEditorSettings.Save();
		}


		public static Rect OnSceneGUI(SceneView sceneView)
		{
			// TODO: put somewhere else
			var curSkin = EditorGUIUtility.isProSkin;
			if (toolbarStyle == null ||
				prevSkin != curSkin)
			{

				toolbarStyle = new GUIStyle(EditorStyles.toolbar);
				toolbarStyle.fixedHeight = kBottomBarHeight;

				toggleStyle = new GUIStyle(EditorStyles.toolbarButton);
				toggleStyle.fixedHeight = kBottomBarHeight;

				buttonStyle = new GUIStyle(EditorStyles.toolbarButton);
				buttonStyle.fixedHeight = kBottomBarHeight;

				prevSkin = curSkin;
				CSGEditorSettings.Load();
			}


			// Calculate size of bottom bar and draw it
			Rect position = sceneView.position;
			position.x		= 0;
			position.y		= position.height - kBottomBarHeight;
			position.height = kBottomBarHeight; 

			GUILayout.Window(bottomBarGuiId, position, OnBottomBarUI, "", toolbarStyle);
			CSGEditorUtility.ConsumeUnusedMouseEvents(BottomBarGUIHash, position);

			Rect dragArea = sceneView.position;
			dragArea.x = 0;
			dragArea.y = kTopBarHeight;
			dragArea.height -= kBottomBarHeight + kTopBarHeight;
			return dragArea;
		}
	}
}
