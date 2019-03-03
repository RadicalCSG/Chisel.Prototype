using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
	public delegate void OnWindowGUI(Rect position);

	public class GUIResizableWindow
	{
		const float kGap					= 4;
		const float kDragHandleWidth		= 6;
		const float kDragHandleCorner		= 10;
		const float kWindowTitleBarHeight	= 14;

		static readonly int kWindowHash			= "Window".GetHashCode();
		static readonly int ResizeBorderHash	= "ResizeBorder".GetHashCode();
		
		public GUIResizableWindow(string title, Rect position, float minWidth, float minHeight, OnWindowGUI onWindowGUI)
		{
			this.title = title;
			this.position = position;
			this.onWindowGUI = onWindowGUI;
			this.minWidth = minWidth;
			this.minHeight = minHeight;
		}

		public string	title;
		public Rect		position;
		public float	minWidth;
		public float	minHeight;

		readonly OnWindowGUI onWindowGUI;

		Rect contentsRect;
		Rect prevWindowRect;
		Rect currentDraggableArea;

		public void Show(Rect draggableArea)
		{
			currentDraggableArea = draggableArea;
			if (GUIUtility.hotControl == 0)
				prevWindowRect = position;

			currentDraggableArea.x      += kGap;
			currentDraggableArea.y      += kGap;
			currentDraggableArea.width  -= kGap + kGap;
			currentDraggableArea.height -= kGap + kGap;

			int windowId = GUIUtility.GetControlID(kWindowHash, FocusType.Passive);
			var prevSkin = GUI.skin;
			GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
			position = GUI.Window(windowId, position, HandleWindowLogic, title);
			GUI.skin = prevSkin;

			if (position.width < minWidth)
				position.width = minWidth;
			if (position.height < minHeight)
				position.height = minHeight;

			position.x = Mathf.Clamp(position.x, currentDraggableArea.xMin, currentDraggableArea.xMax - position.width);
			position.y = Mathf.Clamp(position.y, currentDraggableArea.yMin, currentDraggableArea.yMax - position.height);

			position.xMax = Mathf.Clamp(position.xMax, currentDraggableArea.xMin + position.width, currentDraggableArea.xMax);
			position.yMax = Mathf.Clamp(position.yMax, currentDraggableArea.yMin + position.height, currentDraggableArea.yMax);

			var tempPosition = position;
			tempPosition.height -= kWindowTitleBarHeight;
			CSGEditorUtility.ConsumeUnusedMouseEvents(kWindowHash, tempPosition);
		}

		void HandleWindowLogic(int windowID)
		{
			var width  = position.width;
			var height = position.height;

			var rightBorder			= new Rect(width - kDragHandleWidth, kDragHandleCorner, kDragHandleWidth, height - (2 * kDragHandleCorner));
			var leftBorder			= new Rect(0, kDragHandleCorner, kDragHandleWidth, height - (2 * kDragHandleCorner));
			var bottomBorder		= new Rect(kDragHandleCorner, height - kDragHandleWidth, width - (2 * kDragHandleCorner), kDragHandleWidth);
			var topBorder			= new Rect(kDragHandleCorner, 0, width - (2 * kDragHandleCorner), kDragHandleWidth);
			
			var topLeftBorder		= new Rect(0, 0, kDragHandleCorner, kDragHandleCorner);
			var topRightBorder		= new Rect(width - kDragHandleCorner, 0, kDragHandleCorner, kDragHandleCorner);
			var bottomLeftBorder	= new Rect(0, height - kDragHandleCorner, kDragHandleCorner, kDragHandleCorner);
			var bottomRightBorder	= new Rect(width - kDragHandleCorner, height - kDragHandleCorner, kDragHandleCorner, kDragHandleCorner);

			float value;
			EditorGUI.BeginChangeCheck();
			value = ResizeBorder1D(rightBorder,  prevWindowRect.xMax, MouseCursor.ResizeHorizontal);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.xMax = value;
				value = Mathf.Clamp(value, position.xMin + minWidth, currentDraggableArea.xMax);
				position.xMax = value;
			}

			EditorGUI.BeginChangeCheck();
			value = ResizeBorder1D(leftBorder,   prevWindowRect.xMin, MouseCursor.ResizeHorizontal);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.xMin = value;
				value = Mathf.Clamp(value, currentDraggableArea.xMin, position.xMax - minWidth);
				position.xMin = value;
			}

			EditorGUI.BeginChangeCheck();
			value = ResizeBorder1D(bottomBorder, prevWindowRect.yMax, MouseCursor.ResizeVertical);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.yMax = value;
				value = Mathf.Clamp(value, position.yMin + minHeight, currentDraggableArea.yMax);
				position.yMax = value;
			}

			EditorGUI.BeginChangeCheck();
			value = ResizeBorder1D(topBorder,    prevWindowRect.yMin, MouseCursor.ResizeVertical);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.yMin = value;
				value = Mathf.Clamp(value, currentDraggableArea.yMin, position.yMax - minHeight);
				position.yMin = value;
			}

			var point = prevWindowRect.min;
			EditorGUI.BeginChangeCheck();
			point = ResizeBorder2D(topLeftBorder, point, MouseCursor.ResizeUpLeft);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.min = point;
				point.x = Mathf.Clamp(point.x, currentDraggableArea.xMin, position.xMax - minWidth);
				point.y = Mathf.Clamp(point.y, currentDraggableArea.yMin, position.yMax - minHeight);
				position.min = point;
			}

			point.x = prevWindowRect.xMax;
			point.y = prevWindowRect.yMin;
			EditorGUI.BeginChangeCheck();
			point = ResizeBorder2D(topRightBorder, point, MouseCursor.ResizeUpRight);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.xMax = point.x;
				prevWindowRect.yMin = point.y;
				point.x = Mathf.Clamp(point.x, position.xMin + minWidth, currentDraggableArea.xMax);
				point.y = Mathf.Clamp(point.y, currentDraggableArea.yMin, position.yMax - minHeight);
				position.xMax = point.x;
				position.yMin = point.y;
			}

			point.x = prevWindowRect.xMin;
			point.y = prevWindowRect.yMax;
			EditorGUI.BeginChangeCheck();
			point = ResizeBorder2D(bottomLeftBorder, point, MouseCursor.ResizeUpRight);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.xMin = point.x;
				prevWindowRect.yMax = point.y;
				point.x = Mathf.Clamp(point.x, currentDraggableArea.xMin, position.xMax - minWidth);
				point.y = Mathf.Clamp(point.y, position.yMin + minHeight, currentDraggableArea.yMax);
				position.xMin = point.x;
				position.yMax = point.y;
			}

			point = prevWindowRect.max;
			EditorGUI.BeginChangeCheck();
			point = ResizeBorder2D(bottomRightBorder, point, MouseCursor.ResizeUpLeft);
			if (EditorGUI.EndChangeCheck())
			{
				prevWindowRect.max = point;
				point.x = Mathf.Clamp(point.x, position.xMin + minWidth, currentDraggableArea.xMax);
				point.y = Mathf.Clamp(point.y, position.yMin + minHeight, currentDraggableArea.yMax);
				position.max = point;
			}

			if (onWindowGUI != null)
			{
				contentsRect = position;
				contentsRect.x = kDragHandleWidth;
				contentsRect.y = kDragHandleWidth + kWindowTitleBarHeight;
				contentsRect.width -= kDragHandleWidth * 2;
				contentsRect.height -= (kDragHandleWidth * 2) + kWindowTitleBarHeight;
				onWindowGUI(contentsRect);
			}

			GUI.DragWindow(new Rect(0, 0, position.width, kWindowTitleBarHeight + 4));
		}

		static float ResizeBorder1D(Rect area, float value, MouseCursor cursor)
		{
			EditorGUIUtility.AddCursorRect(area, cursor);

			int id = GUIUtility.GetControlID(ResizeBorderHash, FocusType.Keyboard, area);
			var evt = Event.current;
			var currentType = evt.GetTypeForControl(id);
			switch (currentType)
			{
				case EventType.MouseDown:
				{
					if (area.Contains(evt.mousePosition) && 
						GUIUtility.hotControl == 0)
					{
						GUIUtility.hotControl = id;
						Event.current.Use();
					}
					break;
				}
				case EventType.MouseDrag:
				{
					if (GUIUtility.hotControl != id)
						break;
					float curValue;
					if (cursor == MouseCursor.ResizeHorizontal)
						curValue = evt.delta.x;
					else
						curValue = evt.delta.y;
					value += curValue;
					GUI.changed = true;
					Event.current.Use();
					break;
				}
				case EventType.MouseUp:
				{
					if (GUIUtility.hotControl != id)
						break;
					GUIUtility.hotControl = 0;
					Event.current.Use();
					break;
				}
			}
			return value;
		}
		
		static Vector2 ResizeBorder2D(Rect area, Vector2 value, MouseCursor cursor)
		{
			EditorGUIUtility.AddCursorRect(area, cursor);

			int id = GUIUtility.GetControlID(ResizeBorderHash, FocusType.Keyboard, area);
			var evt = Event.current;
			var currentType = evt.GetTypeForControl(id);
			switch (currentType)
			{
				case EventType.MouseDown:
				{
					if (area.Contains(evt.mousePosition) && 
						GUIUtility.hotControl == 0)
					{
						GUIUtility.hotControl = id;
						Event.current.Use();
					}
					break;
				}
				case EventType.MouseDrag:
				{
					if (GUIUtility.hotControl != id)
						break;
					value += evt.delta;
					GUI.changed = true;
					Event.current.Use();
					break;
				}
				case EventType.MouseUp:
				{
					if (GUIUtility.hotControl != id)
						break;
					GUIUtility.hotControl = 0;
					Event.current.Use();
					break;
				}
			}
			return value;
		}
	}
}
