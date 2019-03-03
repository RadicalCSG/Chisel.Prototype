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
		const float kBottomBarHeight    = 18;
		const float kButtonSpace        = 6;
		const float kButtonWidth        = 18;
		const float kDropdownWidth      = kButtonWidth + 8;
		const float kTopBarHeight       = 17;
		const float kFloatFieldWidth    = 60;

		static readonly int BottomBarGUIHash = typeof(CSGSceneBottomGUI).Name.GetHashCode();
		static GUIStyle miniLabel;
		static GUIStyle lockXButtonStyle;
		static GUIStyle lockYButtonStyle;
		static GUIStyle lockZButtonStyle;
		static Color[]  lockButtonToggleColor;
		static bool     prevSkin = false;
		static GUIContent rebuildContent;
		static GUIContent viewContent;

		static readonly GUIContent lockContent              = new GUIContent("Lock");   
		static readonly GUIContent lockXAxisContent         = new GUIContent("X");
		static readonly GUIContent lockYAxisContent         = new GUIContent("Y");
		static readonly GUIContent lockZAxisContent         = new GUIContent("Z");

		static readonly GUIContent boundsSnappingContent	= new GUIContent("Snap-Bounds");
		static readonly GUIContent pivotSnappingContent		= new GUIContent("Snap-Pivot");
		static readonly GUIContent rotationSnappingContent  = new GUIContent("Snap-Rotate");
		static readonly GUIContent scaleSnappingContent     = new GUIContent("Snap-Scale");

		static readonly GUIContent boundsContent			= new GUIContent("Bounds");
		static readonly GUIContent pivotContent				= new GUIContent("Pivot");
		static readonly GUIContent rotationContent          = new GUIContent("Rotate");
		static readonly GUIContent scaleContent             = new GUIContent("Scale");

		static readonly GUIContent allAxiContent            = new GUIContent("XYZ"); 
		static readonly GUIContent xAxisContent             = new GUIContent("x");
		static readonly GUIContent yAxisContent             = new GUIContent("y");
		static readonly GUIContent zAxisContent             = new GUIContent("y");

		static readonly GUIContent increaseContent          = new GUIContent("+");
		static readonly GUIContent decreaseContent          = new GUIContent("-");


		// TODO: put somewhere else
		// TODO: use "Editor Default Resources" directory?
		// TODO: make this a scriptable object and just set defaults on script?
		static GUIContent IconContent(string name)
		{
            var path = "Assets/Plugins/Chisel/PluginAssets/Icons/" + name + ".png";
			var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			return new GUIContent(image);
		}


		public static void Rebuild()
		{
			CSGNodeHierarchyManager.Rebuild();
		}

		public static void CycleDistanceUnits()
		{
			CSGEditorSettings.Load();
			CSGEditorSettings.DistanceUnit = Units.CycleToNextUnit(CSGEditorSettings.DistanceUnit);
			CSGEditorSettings.Save();
		}

		static void ChangeViewOptions(int value)
		{
			CSGEditorSettings.Load();
			CSGEditorSettings.ViewOptions = (CSGViewOptions)value;
			CSGEditorSettings.Save();
			SceneView.RepaintAll();
		}


		// TODO: put somewhere else
		public static void IncreaseRotateSnap() { CSGEditorSettings.Load(); CSGEditorSettings.RotateSnap += 15.0f; CSGEditorSettings.Save(); }
		public static void DecreaseRotateSnap() { CSGEditorSettings.Load(); CSGEditorSettings.RotateSnap -= 15.0f; CSGEditorSettings.Save(); }

		public static void IncreaseScaleSnap() { CSGEditorSettings.Load(); CSGEditorSettings.ScaleSnap *= 10.0f; CSGEditorSettings.Save(); }
		public static void DecreaseScaleSnap() { CSGEditorSettings.Load(); CSGEditorSettings.ScaleSnap /= 10.0f; CSGEditorSettings.Save(); }

		public static void IncreaseMoveSnapAll() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapX *= 2.0f; CSGEditorSettings.MoveSnapY *= 2.0f; CSGEditorSettings.MoveSnapZ *= 2.0f; CSGEditorSettings.Save(); }
		public static void DecreaseMoveSnapAll() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapX /= 2.0f; CSGEditorSettings.MoveSnapY /= 2.0f; CSGEditorSettings.MoveSnapZ /= 2.0f; CSGEditorSettings.Save(); }

		public static void IncreaseMoveSnapX() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapX *= 2.0f; CSGEditorSettings.Save(); }
		public static void DecreaseMoveSnapX() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapX /= 2.0f; CSGEditorSettings.Save(); }

		public static void IncreaseMoveSnapY() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapY *= 2.0f; CSGEditorSettings.Save(); }
		public static void DecreaseMoveSnapY() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapY /= 2.0f; CSGEditorSettings.Save(); }

		public static void IncreaseMoveSnapZ() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapZ *= 2.0f; CSGEditorSettings.Save(); }
		public static void DecreaseMoveSnapZ() { CSGEditorSettings.Load(); CSGEditorSettings.MoveSnapZ /= 2.0f; CSGEditorSettings.Save(); }


		#region GUI Helpers
		static bool directionForward = true;


		static void MaskField<T>(ref Rect position, GUIContent label, T value, Action<int> action, bool showExtraOptions) { MaskField(ref position, label, value, action, showExtraOptions, EditorStyles.toolbarDropDown); }

		static void MaskField<T>(ref Rect position, GUIContent label, T value, Action<int> action, bool showExtraOptions, GUIStyle style)
		{
			var size = style.CalcSize(label);
			position.width = size.x + 1;
			if (!directionForward) position.x -= position.width;
			var intValue = (int)Enum.ToObject(typeof(T), value);
			FunctioningMaskField.MaskField(position, label, typeof(T), intValue, action, showExtraOptions, style);
			if (directionForward) position.x += position.width;
		}

		static bool Dropdown(ref Rect position, GUIContent label) { return Dropdown(ref position, label, EditorStyles.toolbarDropDown); }

		static bool Dropdown(ref Rect position, GUIContent label, GUIStyle style)
		{
			var size = style.CalcSize(label);
			position.width = size.x + 1;
			if (!directionForward) position.x -= position.width;
			var result = GUI.Button(position, label, style);
			if (directionForward) position.x += position.width;
			return result;
		}

		static bool Button(ref Rect position, GUIContent label) { return Button(ref position, label, EditorStyles.toolbarButton); }

		static bool Button(ref Rect position, GUIContent label, GUIStyle style)
		{
			var size = style.CalcSize(label);
			position.width = size.x + 1;
			if (!directionForward) position.x -= position.width;
			var result = GUI.Button(position, label, style);
			if (directionForward) position.x += position.width;
			return result;
		}

		static bool TinyButton(ref Rect position, GUIContent label) { return TinyButton(ref position, label, EditorStyles.toolbarButton); }

		static bool TinyButton(ref Rect position, GUIContent label, GUIStyle style)
		{
			var size = style.CalcSize(label);
			position.width = size.x - 2;
			var prevHeight = position.height;
			var prevY = position.y;
			position.y += 2;
			position.height -= 4;
			if (!directionForward) position.x -= position.width;
			var result = GUI.Button(position, label, style);
			if (directionForward) position.x += position.width;
			position.height = prevHeight;
			position.y = prevY;
			return result;
		}

		static bool Toggle(ref Rect position, bool value, GUIContent label, Color[] bgColors = null) { return Toggle(ref position, value, label, bgColors, EditorStyles.toolbarButton); }

		static bool Toggle(ref Rect position, bool value, GUIContent label, Color[] bgColors, GUIStyle style)
		{
			var size = style.CalcSize(label);
			var prevColor = GUI.backgroundColor;
			position.width = size.x + 1;
			if (!directionForward) position.x -= position.width;
			if (bgColors != null)
				GUI.backgroundColor = bgColors[value ? 1 : 0];
			var result = GUI.Toggle(position, value, label, style);
			if (directionForward) position.x += position.width;
			if (bgColors != null)
				GUI.backgroundColor = prevColor;
			return result;
		}

		static void Label(ref Rect position, GUIContent label) { Label(ref position, label, miniLabel); }

		static void Label(ref Rect position, GUIContent label, GUIStyle style)
		{
			var size = style.CalcSize(label);
			position.width = size.x + 1;
			if (!directionForward) position.x -= position.width;
			if (Event.current.type == EventType.Repaint)
				style.Draw(position, label, false, false, false, false);
			if (directionForward) position.x += position.width;
		}


		static float FloatField(ref Rect position, float value, float width) { return FloatField(ref position, value, width, EditorStyles.toolbarTextField); }

		static float FloatField(ref Rect position, float value, float width, GUIStyle style)
		{
			var label = new GUIContent(Convert.ToString(value));
			var size = style.CalcSize(label);
			position.width = Mathf.Max(width, size.x + 1);
			position.height -= 2;
			position.y += 2;
			if (!directionForward) position.x -= position.width;
			value = EditorGUI.FloatField(position, value, style);
			if (directionForward) position.x += position.width;
			position.y -= 2;
			position.height += 2;
			return value;
		}


		static float UnitField(ref Rect position, DistanceUnit distanceUnit, float value, float width) { return UnitField(ref position, distanceUnit, value, width, EditorStyles.toolbarTextField); }

		static float UnitField(ref Rect position, DistanceUnit distanceUnit, float value, float width, GUIStyle style)
		{
			var label = new GUIContent(Convert.ToString(value));
			var size = style.CalcSize(label);
			position.width = Mathf.Max(width, size.x + 1);
			position.height -= 2;
			position.y += 2;
			if (!directionForward) position.x -= position.width;
			EditorGUI.BeginChangeCheck();
			var output = EditorGUI.DoubleField(position, Units.UnityToDistanceUnit(distanceUnit, value), style);
			if (EditorGUI.EndChangeCheck())
			{
				value = Units.DistanceUnitToUnity(distanceUnit, output);
			}
			if (directionForward) position.x += position.width;
			position.y -= 2;
			position.height += 2;
			return value;
		}

		static void Space(ref Rect position, float space = kButtonSpace)
		{
			if (directionForward) position.x += space;
			else position.x -= space;
		}
		#endregion

		static float StyleWidth(GUIStyle style, GUIContent label = null, float minWidth = 0)
		{
			var size = style.CalcSize(label ?? GUIContent.none);
			return Mathf.Max(minWidth, size.x + 1);
		}

		#region OnGUI

		#region MoveSnapping

		#region All Axis
		static void AllAxisMoveSnappingGUI(ref Rect position, bool showIncDec, bool showUnits)
		{
			Space(ref position, 2);

			//
			// All axis snapping
			//
			float value = CSGEditorSettings.MoveSnapY;
			var prevChanged = GUI.changed;
			GUI.changed = false;
			EditorGUI.showMixedValue = (CSGEditorSettings.MoveSnapX != value) || (CSGEditorSettings.MoveSnapY != value);
			value = UnitField(ref position, CSGEditorSettings.DistanceUnit, value, kFloatFieldWidth);
			EditorGUI.showMixedValue = false;
			if (GUI.changed)
			{
				CSGEditorSettings.MoveSnapX = CSGEditorSettings.MoveSnapY = CSGEditorSettings.MoveSnapZ = value;
			}
			GUI.changed = prevChanged | GUI.changed;

			if (showIncDec)
			{
				if (TinyButton(ref position, increaseContent, EditorStyles.miniButtonMid)) IncreaseMoveSnapAll();
				if (TinyButton(ref position, decreaseContent, EditorStyles.miniButtonRight)) DecreaseMoveSnapAll();
			}
			if (showUnits && Button(ref position, Units.GetUnitGUIContent(CSGEditorSettings.DistanceUnit), EditorStyles.miniLabel))
				CycleDistanceUnits();
		}

		static float AllAxisMoveSnappingWidth(bool showUnits, bool showIncDec)
		{
			return 2 +

					StyleWidth(EditorStyles.toolbarTextField, minWidth: kFloatFieldWidth) +

					(showIncDec ? (StyleWidth(EditorStyles.miniButtonMid, increaseContent) +
								   StyleWidth(EditorStyles.miniButtonRight, decreaseContent)) : 0) +

					(showUnits ? StyleWidth(EditorStyles.miniLabel, Units.GetUnitGUIContent(CSGEditorSettings.DistanceUnit)) : 0);
		}
		#endregion

		#region Separate Axis
		static void SeparateAxisMoveSnappingGUI(ref Rect position, bool showUnits, bool showIncDec, bool showAxi)
		{
			//
			// Separate axis snapping
			//
			Space(ref position, 2);

			if (showAxi) Label(ref position, xAxisContent);
			CSGEditorSettings.MoveSnapX = UnitField(ref position, CSGEditorSettings.DistanceUnit, CSGEditorSettings.MoveSnapX, kFloatFieldWidth);
			if (showIncDec)
			{
				if (TinyButton(ref position, increaseContent, EditorStyles.miniButtonMid)) IncreaseMoveSnapX();
				if (TinyButton(ref position, decreaseContent, EditorStyles.miniButtonRight)) DecreaseMoveSnapX();
			}

			Space(ref position, 2);

			if (showAxi) Label(ref position, yAxisContent);
			CSGEditorSettings.MoveSnapY = UnitField(ref position, CSGEditorSettings.DistanceUnit, CSGEditorSettings.MoveSnapY, kFloatFieldWidth);
			if (showIncDec)
			{
				if (TinyButton(ref position, increaseContent, EditorStyles.miniButtonMid)) IncreaseMoveSnapY();
				if (TinyButton(ref position, decreaseContent, EditorStyles.miniButtonRight)) DecreaseMoveSnapY();
			}

			Space(ref position, 2);

			if (showAxi) Label(ref position, zAxisContent);
			CSGEditorSettings.MoveSnapZ = UnitField(ref position, CSGEditorSettings.DistanceUnit, CSGEditorSettings.MoveSnapZ, kFloatFieldWidth);
			if (showIncDec)
			{
				if (TinyButton(ref position, increaseContent, EditorStyles.miniButtonMid)) IncreaseMoveSnapZ();
				if (TinyButton(ref position, decreaseContent, EditorStyles.miniButtonRight)) DecreaseMoveSnapZ();
			}

			if (showUnits && Button(ref position, Units.GetUnitGUIContent(CSGEditorSettings.DistanceUnit), EditorStyles.miniLabel))
				CycleDistanceUnits();
		}

		static float SeparateAxisMoveSnappingWidth(bool showUnits, bool showIncDec, bool showAxiLabels)
		{
			return 2 +
					(showAxiLabels ? (StyleWidth(miniLabel, xAxisContent) + StyleWidth(miniLabel, yAxisContent) + StyleWidth(miniLabel, zAxisContent)) : 0) +

					(3 *
						(StyleWidth(EditorStyles.toolbarTextField, minWidth: kFloatFieldWidth) +
						 (showIncDec ? (StyleWidth(EditorStyles.miniButtonMid, increaseContent) +
										StyleWidth(EditorStyles.miniButtonRight, decreaseContent)) : 0))) +

					(showUnits ? StyleWidth(EditorStyles.miniLabel, Units.GetUnitGUIContent(CSGEditorSettings.DistanceUnit)) : 0);
		}
		#endregion

		static void MoveSnappingGUI(ref Rect position, bool largeLabels, bool showUnits, bool showIncDec, bool showAxiLabels, bool forceOneAxis)
		{
			Space(ref position, kButtonSpace);

			//
			// Move snapping
			//
			CSGEditorSettings.PivotSnapping = Toggle(ref position, CSGEditorSettings.PivotSnapping, largeLabels ? pivotSnappingContent : pivotContent);
			CSGEditorSettings.BoundsSnapping = Toggle(ref position, CSGEditorSettings.BoundsSnapping, largeLabels ? boundsSnappingContent : boundsContent);
			if (CSGEditorSettings.MoveSnapping)
			{
				if (!forceOneAxis) CSGEditorSettings.ShowAllAxi = Toggle(ref position, CSGEditorSettings.ShowAllAxi, allAxiContent);
				if (!forceOneAxis && CSGEditorSettings.ShowAllAxi)
				{
					SeparateAxisMoveSnappingGUI(ref position, showUnits, showIncDec, showAxiLabels);
				} else
				{
					AllAxisMoveSnappingGUI(ref position, showUnits, showIncDec);
				}
			}
		}

		static float MoveSnappingWidth(bool largeLabels, bool showUnits, bool showIncDec, bool showAxiLabels, bool forceOneAxis)
		{
			return kButtonSpace +
					StyleWidth(EditorStyles.toolbarButton, largeLabels ? rotationSnappingContent : rotationContent) +
					(CSGEditorSettings.MoveSnapping ?
						(!forceOneAxis ? StyleWidth(EditorStyles.toolbarButton, allAxiContent) : 0) +
						((!forceOneAxis && CSGEditorSettings.ShowAllAxi) ? SeparateAxisMoveSnappingWidth(showUnits, showIncDec, showAxiLabels) : AllAxisMoveSnappingWidth(showUnits, showIncDec)) : 0);
		}
		#endregion

		#region RotateSnapping
		static void RotateSnappingGUI(ref Rect position, bool largeLabels, bool showUnits, bool showIncDec)
		{
			Space(ref position, 2);

			//
			// Rotate snapping
			//
			CSGEditorSettings.RotateSnapping = Toggle(ref position, CSGEditorSettings.RotateSnapping, largeLabels ? rotationSnappingContent : rotationContent);
			if (CSGEditorSettings.RotateSnapping)
			{
				CSGEditorSettings.RotateSnap = FloatField(ref position, CSGEditorSettings.RotateSnap, kFloatFieldWidth);
				if (showIncDec)
				{
					if (TinyButton(ref position, increaseContent, EditorStyles.miniButtonMid)) IncreaseRotateSnap();
					if (TinyButton(ref position, decreaseContent, EditorStyles.miniButtonRight)) DecreaseRotateSnap();
				}
				if (showUnits) Label(ref position, Units.DegreeUnitContent);
			}
		}

		static float RotateSnappingWidth(bool largeLabels, bool showUnits, bool showIncDec)
		{
			return 2 +
					StyleWidth(EditorStyles.toolbarButton, largeLabels ? rotationSnappingContent : rotationContent) +
					StyleWidth(EditorStyles.toolbarTextField, minWidth: kFloatFieldWidth) +
					(showIncDec ? (StyleWidth(EditorStyles.miniButtonMid, increaseContent) +
								   StyleWidth(EditorStyles.miniButtonRight, decreaseContent)) : 0) +
					(showUnits ? StyleWidth(miniLabel, Units.DegreeUnitContent) : 0);
		}
		#endregion

		#region ScaleSnapping
		static void ScaleSnappingGUI(ref Rect position, bool largeLabels, bool showUnits, bool showIncDec)
		{
			Space(ref position, 2);

			//
			// Scale snapping
			//
			CSGEditorSettings.ScaleSnapping = Toggle(ref position, CSGEditorSettings.ScaleSnapping, largeLabels ? scaleSnappingContent : scaleContent);
			if (CSGEditorSettings.ScaleSnapping)
			{
				CSGEditorSettings.ScaleSnap = FloatField(ref position, CSGEditorSettings.ScaleSnap, kFloatFieldWidth);
				if (showIncDec)
				{
					if (TinyButton(ref position, increaseContent, EditorStyles.miniButtonMid)) IncreaseScaleSnap();
					if (TinyButton(ref position, decreaseContent, EditorStyles.miniButtonRight)) DecreaseScaleSnap();
				}
				if (showUnits) Label(ref position, Units.PercentageUnitContent);
			}
		}

		static float ScaleSnappingWidth(bool largeLabels, bool showUnits, bool showIncDec)
		{
			return 2 +
					StyleWidth(EditorStyles.toolbarButton, largeLabels ? scaleSnappingContent : scaleContent) +
					StyleWidth(EditorStyles.toolbarTextField, minWidth: kFloatFieldWidth) +
					(showIncDec ? (StyleWidth(EditorStyles.miniButtonMid, increaseContent) +
								   StyleWidth(EditorStyles.miniButtonRight, decreaseContent)) : 0) +
					(showUnits ? StyleWidth(miniLabel, Units.PercentageUnitContent) : 0);
		}
		#endregion

		#region AxisLocking
		static void AxisLockingGUI(ref Rect position, bool showLockLabel)
		{
			Space(ref position, kButtonSpace);

			//
			// Axis locking
			//
			if (showLockLabel) Label(ref position, lockContent);
			CSGEditorSettings.AxisLockX = Toggle(ref position, CSGEditorSettings.AxisLockX, lockXAxisContent, lockButtonToggleColor, lockXButtonStyle);
			CSGEditorSettings.AxisLockY = Toggle(ref position, CSGEditorSettings.AxisLockY, lockYAxisContent, lockButtonToggleColor, lockYButtonStyle);
			CSGEditorSettings.AxisLockZ = Toggle(ref position, CSGEditorSettings.AxisLockZ, lockZAxisContent, lockButtonToggleColor, lockZButtonStyle);
		}

		static float AxisLockingWidth(bool showLockLabel)
		{
			return kButtonSpace +
					(showLockLabel ? StyleWidth(miniLabel, lockContent) : 0) +
					StyleWidth(lockXButtonStyle, lockXAxisContent) +
					StyleWidth(lockYButtonStyle, lockYAxisContent) +
					StyleWidth(lockZButtonStyle, lockZAxisContent);
		}
		#endregion

		#region ViewOptions
		static void ViewOptionsGUI(ref Rect position)
		{
			Space(ref position, kButtonSpace);

			//
			// View Options
			//
			MaskField(ref position, viewContent, CSGEditorSettings.ViewOptions, ChangeViewOptions, false);
		}

		static float ViewOptionsWidth()
		{
			return kButtonSpace +
					StyleWidth(EditorStyles.toolbarDropDown, viewContent);
		}
		#endregion

		static void DrawLeftButtons(Rect position, SceneViewBarState state)
		{
			directionForward = true;

			position.x--;

			ViewOptionsGUI(ref position);
			if (!state.hideLocking) AxisLockingGUI(ref position, state.showLockLabel);
			if (!state.hideMove) MoveSnappingGUI(ref position, state.largeLabels, state.showUnits, state.showIncDec, state.showAxiLabels, state.forceOneAxis);
			if (!state.hideRotate) RotateSnappingGUI(ref position, state.largeLabels, state.showUnits, state.showIncDec);
			if (!state.hideScale) ScaleSnappingGUI(ref position, state.largeLabels, state.showUnits, state.showIncDec);
		}

		static float CalcLeftButtonsWidth(SceneViewBarState state)
		{
			return ViewOptionsWidth() +
				   (!state.hideLocking ? AxisLockingWidth(state.showLockLabel) : 0) +
				   (!state.hideMove ? MoveSnappingWidth(state.largeLabels, state.showUnits, state.showIncDec, state.showAxiLabels, state.forceOneAxis) : 0) +
				   (!state.hideRotate ? RotateSnappingWidth(state.largeLabels, state.showUnits, state.showIncDec) : 0) +
				   (!state.hideScale ? ScaleSnappingWidth(state.largeLabels, state.showUnits, state.showIncDec) : 0);
		}

		static float CalcRightButtonsWidth()
		{
			return kButtonSpace +
				   StyleWidth(EditorStyles.toolbarButton, rebuildContent);
		}

		static void DrawRightButtons(Rect position)
		{
			directionForward = false;

			position.x = position.width;

			Space(ref position, kButtonSpace);

			//
			// Rebuild all
			//
			if (Button(ref position, rebuildContent))
			{
				Rebuild();
			}
		}

		static float CalcBottomBarWidth(SceneViewBarState state)
		{
			return CalcLeftButtonsWidth(state) +
				   CalcRightButtonsWidth();
		}

		class SceneViewBarState
		{
			public bool     largeLabels     = true;
			public bool     showLockLabel   = true;
			public bool     showUnits       = true;
			public bool     showIncDec      = true;
			public bool     showAxiLabels   = true;
			public bool     forceOneAxis    = false;
			public bool     hideScale       = false;
			public bool     hideRotate      = false;
			public bool     hideLocking     = false;
			public bool     hideMove        = false;
			public float    prevBarWidth    = 0;

			public void Reset(float width)
			{
				largeLabels = true;
				showLockLabel = true;
				showAxiLabels = true;
				showUnits = true;

				showIncDec = true;
				forceOneAxis = false;
				hideScale = false;
				hideRotate = false;
				hideLocking = false;
				hideMove = false;

				prevBarWidth = width;
			}
		}

		static void UpdateState(Rect position, SceneViewBarState state)
		{
			if (state.prevBarWidth == position.width)
				return;

			var availableWidth = position.width;
			state.Reset(position.width);

			float requiredWidth;
			requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;

			state.largeLabels = false; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.showLockLabel = false; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.showAxiLabels = false; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.showUnits = false; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.showUnits = false; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.showIncDec = false; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.forceOneAxis = true; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.hideScale = true; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.hideRotate = true; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.hideLocking = true; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
			state.hideMove = true; requiredWidth = CalcBottomBarWidth(state); if (requiredWidth < availableWidth) return;
		}

		static void DrawBottomBar(Rect position, SceneViewBarState state)
		{
			EditorGUI.BeginChangeCheck();

			UpdateState(position, state);

			DrawLeftButtons(position, state);
			DrawRightButtons(position);

			if (EditorGUI.EndChangeCheck())
				CSGEditorSettings.Save();
		}



		static Dictionary<SceneView, SceneViewBarState> sceneViewStateLookup = new Dictionary<SceneView, SceneViewBarState>();
		
		public static Rect OnSceneGUI(SceneView sceneView)
		{
			// TODO: put somewhere else
			var curSkin = EditorGUIUtility.isProSkin;
			if (miniLabel == null ||
				prevSkin != curSkin)
			{
				miniLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };

				rebuildContent = curSkin ? IconContent("icon_pro_rebuild") : IconContent("icon_pers_rebuild");
				viewContent    = curSkin ? IconContent("icon_pro_grid")    : IconContent("icon_pers_grid"); // TODO: replace icon

				lockXButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
				lockXButtonStyle.normal  .textColor = Color.Lerp(UnityEditor.Handles.xAxisColor, curSkin ? Color.white : Color.black, 0.6f);
				lockXButtonStyle.onNormal.textColor = Color.Lerp(UnityEditor.Handles.xAxisColor, curSkin ? Color.black : Color.white, curSkin ? 0.25f : 0.5f);

				lockYButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
				lockYButtonStyle.normal  .textColor = Color.Lerp(UnityEditor.Handles.yAxisColor, curSkin ? Color.white : Color.black, 0.6f);
				lockYButtonStyle.onNormal.textColor = Color.Lerp(UnityEditor.Handles.yAxisColor, curSkin ? Color.black : Color.white, curSkin ? 0.25f : 0.5f);

				lockZButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
				lockZButtonStyle.normal  .textColor = Color.Lerp(UnityEditor.Handles.zAxisColor, curSkin ? Color.white : Color.black, 0.6f);
				lockZButtonStyle.onNormal.textColor = Color.Lerp(UnityEditor.Handles.zAxisColor, curSkin ? Color.black : Color.white, curSkin ? 0.25f : 0.5f);

				lockButtonToggleColor = new Color[2];
				lockButtonToggleColor[0] = Color.white;
				lockButtonToggleColor[1] = (curSkin) ? Color.Lerp(Color.white, Color.red, 0.5f) : Color.Lerp(Color.black, Color.red, 0.5f);
				prevSkin = curSkin;
				CSGEditorSettings.Load();
			}


			// Calculate size of bottom bar
			Rect position = sceneView.position;
			position.x		= 0;
			position.y		= position.height - (kBottomBarHeight + kTopBarHeight);
			position.height = kBottomBarHeight;

			try
			{
				UnityEditor.Handles.BeginGUI();
				if (Event.current.type == EventType.Repaint)
					EditorStyles.toolbar.Draw(position, false, false, false, false);

				SceneViewBarState state;
				if (!sceneViewStateLookup.TryGetValue(sceneView, out state))
				{
					state = new SceneViewBarState();
					sceneViewStateLookup[sceneView] = state;
				}

				DrawBottomBar(position, state);

				CSGEditorUtility.ConsumeUnusedMouseEvents(BottomBarGUIHash, position);
			}
			finally
			{
				UnityEditor.Handles.EndGUI();
			}



			Rect dragArea = sceneView.position;
			dragArea.x = 0;
			dragArea.y = kTopBarHeight;
			dragArea.height -= kBottomBarHeight + kTopBarHeight;
			return dragArea;
		}
		#endregion
	}
}
