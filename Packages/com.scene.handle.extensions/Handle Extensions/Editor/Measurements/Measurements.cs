using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnitySceneExtensions
{
	public class Measurements
	{
		public static bool Show = true;

		public static void DrawFlatArrow(UnityEngine.Vector3 center, UnityEngine.Vector3 direction, float handleSize)
		{
			var invMatrix	= SceneHandles.inverseMatrix;

			var camera		= UnityEngine.Camera.current;
			var camPos		= invMatrix.MultiplyPoint(camera.transform.position);
			var camDir		= (SceneHandleUtility.ProjectPointLine(camPos, center, center + direction) - camPos).normalized;
			DrawFlatArrow(center, direction, camDir, handleSize);
		}

		static readonly Vector3[] arrowPoints = new Vector3[3];
		public static void DrawFlatArrow(UnityEngine.Vector3 center, UnityEngine.Vector3 direction, UnityEngine.Vector3 forward, float handleSize)
		{
			var matrix		= SceneHandles.matrix;
			SceneHandles.matrix = UnityEngine.Matrix4x4.identity;

			center		= matrix.MultiplyPoint(center);
			var xdir	= matrix.MultiplyVector(direction).normalized;
			var ydir	= Vector3.Cross(xdir, matrix.MultiplyVector(forward)).normalized;

			ydir *= 0.3f * handleSize;
			xdir *= handleSize;

			arrowPoints[0] = center;
			arrowPoints[1] = center + (xdir - ydir);
			arrowPoints[2] = center + (xdir + ydir);
			SceneHandles.DrawAAConvexPolygon(arrowPoints);

			SceneHandles.matrix = matrix;
		}

		readonly struct LabelStyle : IEquatable<LabelStyle>
		{
			public LabelStyle(Color color, int padding) { this.color = color; this.padding = padding; }
			public readonly Color	color;
			public readonly int		padding;

            public bool Equals(LabelStyle other)
            {
				return (other.padding == padding && other.color == color);
            }

			public override int GetHashCode()
			{
				const uint hash = 0x9e3779b9;
				var seed = padding + hash;
				seed ^= color.GetHashCode() + hash + (seed << 6) + (seed >> 2);
				return (int)seed;
			}

            public override bool Equals(object obj)
			{
				if (!(obj is LabelStyle))
					return false;
				return Equals((LabelStyle)obj);
            }
        }

		static readonly Dictionary<LabelStyle, GUIStyle> labelStyles = new Dictionary<LabelStyle, GUIStyle>();
		static readonly GUIContent tempContent = new GUIContent();
		public static Rect DrawLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, int padding, string text)
		{
			var matrix = SceneHandles.matrix;
			var pt = UnityEngine.Camera.current.WorldToViewportPoint(matrix.MultiplyPoint(position));
			// cull if behind camera
			if (pt.z < 0)
				return new Rect();

			var labelStyle = new LabelStyle(SceneHandles.color, padding);
			if (!labelStyles.TryGetValue(labelStyle, out GUIStyle style))
			{
				style = new UnityEngine.GUIStyle();
				style.normal.textColor	= SceneHandles.color;
				style.alignment			= UnityEngine.TextAnchor.UpperLeft;

				// some eyeballed offsets because CalcSize returns a non-centered rect
				style.padding.left		= 4 + padding;
				style.padding.right		= padding;
				style.padding.top		= 1 + padding;
				style.padding.bottom	= 4 + padding;
				labelStyles[labelStyle] = style;
			}


			//SceneHandles.Label(position, text, style); = alignment is broken, positioning of text on coordinate is *weird*

			tempContent.text = text;
			var size		= style.CalcSize(tempContent);
			var halfSize	= size * 0.5f;
			var screenpos	= UnityEditor.HandleUtility.WorldToGUIPoint(position);
			var screendir	= (UnityEditor.HandleUtility.WorldToGUIPoint(position + alignmentDirection) - screenpos).normalized;

			// align on the rect around the text in the direction of alignmentDirection
			screenpos.x += (screendir.x - 1) * halfSize.x;
			screenpos.y += (screendir.y - 1) * halfSize.y;

			var rect = new Rect(screenpos.x, screenpos.y, size.x, size.y);

			if (Event.current.type == EventType.Repaint)
			{
				SceneHandles.BeginGUI();
				{
					GUI.Label(rect, tempContent, style);
				}
				SceneHandles.EndGUI();
			}
			return rect;
		}

		static bool canClick = false;
		static readonly int s_DistanceLabelHash = "DistanceLabel".GetHashCode();
		public static void DrawUnitLabel(UnityEngine.Vector3 position, UnityEngine.Vector3 alignmentDirection, int padding, float distance, string name = null)
		{
			// TODO: click on unit to change unit, but click on number to be able to type in number ...

			var rect = DrawLabel(position, alignmentDirection, padding, Units.ToDistanceString(distance, name));

			if (SceneHandles.disabled)
				return;

			var id = GUIUtility.GetControlID (s_DistanceLabelHash, FocusType.Keyboard);
			
			var evt = Event.current;
			var type = evt.GetTypeForControl(id);
			switch (type)
			{
				case EventType.Layout:
				{
					if (UnityEditor.Tools.current == UnityEditor.Tool.View ||
						UnityEditor.Tools.current == UnityEditor.Tool.None ||
						evt.alt)
						break;

					if (rect.Contains(Event.current.mousePosition))
						UnityEditor.HandleUtility.AddControl(id, 3);
					break;
				}
				case EventType.MouseDown:
				{
					if (UnityEditor.Tools.current == UnityEditor.Tool.View ||
						UnityEditor.Tools.current == UnityEditor.Tool.None ||
						evt.alt)
						break;

					if (GUIUtility.hotControl != 0)
						break;

					if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
						(GUIUtility.keyboardControl != id || evt.button != 2))
						break;
												
					
					GUIUtility.hotControl = GUIUtility.keyboardControl = id;
					evt.Use();
					UnityEditor.EditorGUIUtility.SetWantsMouseJumping(1);
					canClick = true;
					break;
				}
				case EventType.MouseDrag:
				{
					if (GUIUtility.hotControl != id)
						break;

					canClick = false;
					evt.Use();
					break;
				}
				case EventType.MouseUp:
				{
					if (GUIUtility.hotControl != id || (evt.button != 0 && evt.button != 2))
						break;
					
					GUIUtility.hotControl = 0;
					GUIUtility.keyboardControl = 0;
					evt.Use();
					UnityEditor.EditorGUIUtility.SetWantsMouseJumping(0);
					if (canClick)
						Units.ActiveDistanceUnit = Units.CycleToNextUnit(Units.ActiveDistanceUnit);
					canClick = false;
					break;
				}
			}
		}

		public static void DrawLength(UnityEngine.Vector3 from, UnityEngine.Vector3 to)
		{
			var prevColor = SceneHandles.color;
			SceneHandles.color = SceneHandles.StateColor(SceneHandles.measureColor);
			var invMatrix	= SceneHandles.inverseMatrix;

			var camera		= UnityEngine.Camera.current;
			var camPos		= invMatrix.MultiplyPoint(camera.transform.position);
			var camDir		= (SceneHandleUtility.ProjectPointLine(camPos, from, to) - camPos).normalized;
			var delta		= (to - from);
			var length		= delta.magnitude;
			var forward		= delta / length;
			var right		= Vector3.Cross(forward, camDir);
			var fromSize	= UnityEditor.HandleUtility.GetHandleSize(from);
			var toSize		= UnityEditor.HandleUtility.GetHandleSize(to);
			var center		= (to + from) * 0.5f;

			SceneHandles.DrawLine(from, to);
			DrawFlatArrow(from,  forward, camDir, fromSize * 0.2f);
			DrawFlatArrow(to,   -forward, camDir, toSize   * 0.2f);
		//	SceneHandles.DrawLine(from - right, from + right);
		//	SceneHandles.DrawLine(to   - right, to   + right);
			
			DrawUnitLabel(center, right, 2, length);
			SceneHandles.color = prevColor;
		}

		public static void DrawLengthsXY(Rect rect, Axes activeAxes = Axes.XYZ, Axes visibleAxes = Axes.XYZ, Axes selectedAxes = Axes.None)
		{
			// TODO: what if camera is inside of rect?
			// TODO: what if length is outside of camera, but other option is inside of camera view?
			// TODO: don't make the side lines move around when resizing, be smarter about handlesize
			//			-> maybe limit the projected line to the screen?
			
			var invMatrix	= SceneHandles.inverseMatrix;
			var prevColor	= SceneHandles.color;
			var color		= prevColor;
			var color2		= color;
			color2.a *= 0.5f;

			var camera		= UnityEngine.Camera.current;
			var camPos		= invMatrix.MultiplyPoint(camera.transform.position);

			var lengthX		= rect.width;
			var lengthY		= rect.height;
			
			var absLengthX	= Mathf.Abs(lengthX);
			var absLengthY	= Mathf.Abs(lengthY);

			var delta		= ((Vector3)rect.center - camPos) * 2;
			var dotX		= delta.x < 0 ? -1 : 1;
			var dotY		= delta.y < 0 ? -1 : 1;
			var dotZ		= delta.z < 0 ? -1 : 1;
			
			var insideX		= (delta.x >= -absLengthX && delta.x <= absLengthX);
			var insideY		= (delta.y >= -absLengthY && delta.y <= absLengthY);
			var insideZ		= (delta.z == 0);
			
			bool showX = !(insideY && insideZ);
			bool showY = !(insideX && insideZ);

			Vector3 fromX, toX;
			Vector3 fromY, toY;

			var min = (Vector3)rect.min;
			var max = (Vector3)rect.max;

			// min/max of rect can potentially be inverted
			min.x = Mathf.Min(min.x, max.x);
			min.y = Mathf.Min(min.y, max.y);
			min.z = Mathf.Min(min.z, max.z);
			
			fromX = min; toX = fromX + new Vector3(absLengthX, 0, 0);
			fromY = min; toY = fromY + new Vector3(0, absLengthY, 0);

			var signY = Vector3.one;
			if (showY)
			{ 				
				bool swapX = (dotX > 0) ^ insideZ;
				bool swapZ = (dotZ < 0);

				signY.x = swapX ? 1 : -1; signY.z = swapZ ? 1 : -1;
				var ofsX = swapX ? absLengthX : 0; fromY.x += ofsX; toY.x += ofsX;
			}

			var signX = Vector3.one;
			if (showX)
			{ 				
				bool swapY = (dotY > 0) ^ insideZ;
				bool swapZ = (dotZ < 0);

				signX.y = swapY ? 1 : -1; signX.z = swapZ ? 1 : -1;				
				var ofsY = swapY ? absLengthY : 0; fromX.y += ofsY; toX.y += ofsY;
			}

			var lineHandleX	= SceneHandleUtility.ProjectPointLine(camPos, fromX, toX);
			var lineHandleY	= SceneHandleUtility.ProjectPointLine(camPos, fromY, toY);
			var lineHandleZ = min;
			var lineOfsX	= UnityEditor.HandleUtility.GetHandleSize(lineHandleX) * 0.25f;
			var lineOfsY	= UnityEditor.HandleUtility.GetHandleSize(lineHandleY) * 0.25f;
			var lineOfsZ	= UnityEditor.HandleUtility.GetHandleSize(lineHandleZ) * 0.25f;
			
			var angleX		= lineHandleX - camPos;
			var angleY		= lineHandleY - camPos;
			var directionX	= Mathf.Abs(angleX.y) <= Mathf.Abs(angleX.z);
			var directionY	= Mathf.Abs(angleY.x) <= Mathf.Abs(angleY.z);
			
			var disabled	= SceneHandles.disabled;

			const int labelPadding = 2;
			if (showX && ((visibleAxes & Axes.X) == Axes.X))
			{
				var active		= ((activeAxes & Axes.X) == Axes.X) && !disabled;
				var selected	= ((selectedAxes & Axes.X) == Axes.X) && active;
				var offsetY		= new Vector3(0, (lineOfsY * signX.y), 0);
				var offsetZ		= new Vector3(0, 0, (lineOfsZ * signX.z));

				var offset		= directionX ? offsetY : offsetZ;
				var fromOfs		= fromX + offset;
				var toOfs		= toX   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthX / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthX / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;

				SceneHandles.color = SceneHandles.StateColor(color2, !active, selected);
				SceneHandles.DrawLine(fromX, fromOfs);
				SceneHandles.DrawLine(toX,   toOfs);

				SceneHandles.color = SceneHandles.StateColor(color, !active, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.right, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.right, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthX, "X");
			}
			
			if (showY && ((visibleAxes & Axes.Y) == Axes.Y))
			{
				var active		= ((activeAxes & Axes.Y) == Axes.Y) && !disabled;
				var selected	= ((selectedAxes & Axes.Y) == Axes.Y) && active;
				var offsetX		= new Vector3((lineOfsX * signY.x), 0, 0);
				var offsetZ		= new Vector3(0, 0, (lineOfsZ * signY.z));

				var offset		= directionY ? offsetX : offsetZ;
				var fromOfs		= fromY + offset;
				var toOfs		= toY   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthY / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthY / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs  ) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;

				SceneHandles.color = SceneHandles.StateColor(color2, !active, selected);
				SceneHandles.DrawLine(fromY, fromOfs);
				SceneHandles.DrawLine(toY,   toOfs);

				SceneHandles.color = SceneHandles.StateColor(color, !active, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.up, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.up, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthY, "Y");
			}
			SceneHandles.color = prevColor;
		}

		public static void DrawLengthsXZ(Matrix4x4 transformation, Rect rect, Axes activeAxes = Axes.XYZ, Axes visibleAxes = Axes.XYZ, Axes selectedAxes = Axes.None)
		{
			using (var drawingScope = new UnityEditor.Handles.DrawingScope(SceneHandles.measureColor, transformation))
			{
				DrawLengthsXZ(rect, activeAxes, visibleAxes, selectedAxes);
			}
		}

		public static void DrawLengthsXZ(Rect rect, Axes activeAxes = Axes.XYZ, Axes visibleAxes = Axes.XYZ, Axes selectedAxes = Axes.None)
		{
			// TODO: what if camera is inside of bounds?
			// TODO: what if length is outside of camera, but other option is inside of camera view?
			// TODO: don't make the side lines move around when resizing, be smarter about handlesize
			//			-> maybe limit the projected line to the screen?
			
			var invMatrix	= SceneHandles.inverseMatrix;
			var prevColor	= SceneHandles.color;
			var color		= prevColor;
			var color2		= color;
			color2.a *= 0.5f;

			var camera		= UnityEngine.Camera.current;
			var camPos		= invMatrix.MultiplyPoint(camera.transform.position);

			var lengthX		= rect.width;
			var lengthY		= 0;
			var lengthZ		= rect.height;
			
			var absLengthX	= Mathf.Abs(lengthX);
			var absLengthY	= Mathf.Abs(lengthY);
			var absLengthZ	= Mathf.Abs(lengthZ);

			var delta		= (new Vector3 { x = rect.center.x, z = rect.center.y } - camPos) * 2;
			var dotX		= delta.x < 0 ? -1 : 1;
			var dotY		= delta.y < 0 ? -1 : 1;
			var dotZ		= delta.z < 0 ? -1 : 1;
			
			var insideX		= (delta.x >= -absLengthX && delta.x <= absLengthX);
			var insideY		= (delta.y >= -absLengthY && delta.y <= absLengthY);
			var insideZ		= (delta.z >= -absLengthZ && delta.z <= absLengthZ);
			
			bool showX = !(insideY && insideZ);
			bool showY = !(insideX && insideZ);
			bool showZ = !(insideX && insideY);


			Vector3 fromX, toX;
			Vector3 fromZ, toZ;

			var min = new Vector3 { x = rect.min.x, z = rect.min.y };
			var max = new Vector3 { x = rect.max.x, z = rect.max.y };

			// min/max of bounds can potentially be inverted
			min.x = Mathf.Min(min.x, max.x);
			min.y = Mathf.Min(min.y, max.y);
			min.z = Mathf.Min(min.z, max.z);
			
			fromX = min; toX = fromX + new Vector3(absLengthX, 0, 0);
			fromZ = min; toZ = fromZ + new Vector3(0, 0, absLengthZ);

			var signZ = Vector3.one;
			if (showZ)
			{
				bool swapX = (dotX > 0) ^ insideY;
				bool swapY = (dotY < 0) ^ insideY;

				signZ.x = swapX ? 1 : -1; signZ.y = swapY ? 1 : -1;				
				var ofsX = swapX ? absLengthX : 0; fromZ.x += ofsX; toZ.x += ofsX;				
				var ofsY = swapY ? absLengthY : 0; fromZ.y += ofsY; toZ.y += ofsY;
			}

			var signX = Vector3.one;
			if (showX)
			{ 				
				bool swapY = (dotY > 0) ^ insideZ;
				bool swapZ = (dotZ < 0);

				signX.y = swapY ? 1 : -1; signX.z = swapZ ? 1 : -1;				
				var ofsY = swapY ? absLengthY : 0; fromX.y += ofsY; toX.y += ofsY;				
				var ofsZ = swapZ ? absLengthZ : 0; fromX.z += ofsZ; toX.z += ofsZ;
			}

			var lineHandleX	= SceneHandleUtility.ProjectPointLine(camPos, fromX, toX);
			var lineHandleY	= min;
			var lineHandleZ	= SceneHandleUtility.ProjectPointLine(camPos, fromZ, toZ);
			var lineOfsX	= (UnityEditor.HandleUtility.GetHandleSize(lineHandleX) * 0.25f);
			var lineOfsY	= (UnityEditor.HandleUtility.GetHandleSize(lineHandleY) * 0.25f);
			var lineOfsZ	= (UnityEditor.HandleUtility.GetHandleSize(lineHandleZ) * 0.25f);
			
			var angleX		= lineHandleX - camPos;
			var angleZ		= lineHandleZ - camPos;
			var directionX	= Mathf.Abs(angleX.y) <= Mathf.Abs(angleX.z);
			var directionZ	= Mathf.Abs(angleZ.y) <= Mathf.Abs(angleZ.x);

			var disabled	= SceneHandles.disabled;

			const int labelPadding = 2;
			if (showX && ((visibleAxes & Axes.X) == Axes.X))
			{
				var axisDisabled	= ((activeAxes & Axes.X) != Axes.X) || disabled;
				var selected		= ((selectedAxes & Axes.X) == Axes.X) && !axisDisabled;

				var offsetY		= new Vector3(0, (lineOfsY * signX.y), 0);
				var offsetZ		= new Vector3(0, 0, (lineOfsZ * signX.z));

				var offset		= directionX ? offsetY : offsetZ;
				var fromOfs		= fromX + offset;
				var toOfs		= toX   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthX / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthX / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs  ) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;
				
				SceneHandles.color = SceneHandles.StateColor(color2, axisDisabled, selected);
				SceneHandles.DrawLine(fromX, fromOfs);
				SceneHandles.DrawLine(toX,   toOfs);

				SceneHandles.color = SceneHandles.StateColor(color, axisDisabled, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.right, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.right, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthX, "X");
			}
			
			if (showZ && ((visibleAxes & Axes.Z) == Axes.Z))
			{ 
				var axisDisabled	= ((activeAxes & Axes.Z) != Axes.Z) || disabled;
				var selected		= ((selectedAxes & Axes.Z) == Axes.Z) && !axisDisabled;

				var offsetX		= new Vector3((lineOfsX * signZ.x), 0, 0);
				var offsetY		= new Vector3(0, (lineOfsY * signZ.y), 0);

				var offset		= directionZ ? offsetY : offsetX;
				var fromOfs		= fromZ + offset;
				var toOfs		= toZ   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthZ / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthZ / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs  ) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;
				
				SceneHandles.color = SceneHandles.StateColor(color2, axisDisabled, selected);
				SceneHandles.DrawLine(fromZ, fromOfs);
				SceneHandles.DrawLine(toZ,   toOfs);
				
				SceneHandles.color = SceneHandles.StateColor(color, axisDisabled, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.forward, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.forward, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthZ, "Z");
			}
			SceneHandles.color = prevColor;
		}

		public static void DrawLengths(Bounds bounds, Axes activeAxes = Axes.XYZ, Axes visibleAxes = Axes.XYZ, Axes selectedAxes = Axes.None)
		{
			// TODO: what if camera is inside of bounds?
			// TODO: what if length is outside of camera, but other option is inside of camera view?
			// TODO: don't make the side lines move around when resizing, be smarter about handlesize
			//			-> maybe limit the projected line to the screen?
			
			var invMatrix	= SceneHandles.inverseMatrix;
			var prevColor	= SceneHandles.color;
			var color		= prevColor;
			var color2		= color;
			color2.a *= 0.5f;

			var camera		= UnityEngine.Camera.current;
			var camPos		= invMatrix.MultiplyPoint(camera.transform.position);

			var lengthX		= bounds.size.x;
			var lengthY		= bounds.size.y;
			var lengthZ		= bounds.size.z;
			
			var absLengthX	= Mathf.Abs(lengthX);
			var absLengthY	= Mathf.Abs(lengthY);
			var absLengthZ	= Mathf.Abs(lengthZ);

			var delta		= (bounds.center - camPos) * 2;
			var dotX		= delta.x < 0 ? -1 : 1;
			var dotY		= delta.y < 0 ? -1 : 1;
			var dotZ		= delta.z < 0 ? -1 : 1;
			
			var insideX		= (delta.x >= -absLengthX && delta.x <= absLengthX);
			var insideY		= (delta.y >= -absLengthY && delta.y <= absLengthY);
			var insideZ		= (delta.z >= -absLengthZ && delta.z <= absLengthZ);
			
			bool showX = !(insideY && insideZ);
			bool showY = !(insideX && insideZ);
			bool showZ = !(insideX && insideY);


			Vector3 fromX, toX;
			Vector3 fromY, toY;
			Vector3 fromZ, toZ;

			var min = bounds.min;
			var max = bounds.max;

			// min/max of bounds can potentially be inverted
			min.x = Mathf.Min(min.x, max.x);
			min.y = Mathf.Min(min.y, max.y);
			min.z = Mathf.Min(min.z, max.z);
			
			fromX = min; toX = fromX + new Vector3(absLengthX, 0, 0);
			fromY = min; toY = fromY + new Vector3(0, absLengthY, 0);
			fromZ = min; toZ = fromZ + new Vector3(0, 0, absLengthZ);

			var signZ = Vector3.one;
			if (showZ)
			{
				bool swapX = (dotX > 0) ^ insideY;
				bool swapY = (dotY < 0) ^ insideY;

				signZ.x = swapX ? 1 : -1; signZ.y = swapY ? 1 : -1;				
				var ofsX = swapX ? absLengthX : 0; fromZ.x += ofsX; toZ.x += ofsX;				
				var ofsY = swapY ? absLengthY : 0; fromZ.y += ofsY; toZ.y += ofsY;
			}

			var signY = Vector3.one;
			if (showY)
			{ 				
				bool swapX = (dotX > 0) ^ insideZ;
				bool swapZ = (dotZ < 0);

				signY.x = swapX ? 1 : -1; signY.z = swapZ ? 1 : -1;
				var ofsX = swapX ? absLengthX : 0; fromY.x += ofsX; toY.x += ofsX;				
				var ofsZ = swapZ ? absLengthZ : 0; fromY.z += ofsZ; toY.z += ofsZ;
			}

			var signX = Vector3.one;
			if (showX)
			{ 				
				bool swapY = (dotY > 0) ^ insideZ;
				bool swapZ = (dotZ < 0);

				signX.y = swapY ? 1 : -1; signX.z = swapZ ? 1 : -1;				
				var ofsY = swapY ? absLengthY : 0; fromX.y += ofsY; toX.y += ofsY;				
				var ofsZ = swapZ ? absLengthZ : 0; fromX.z += ofsZ; toX.z += ofsZ;
			}

			var lineHandleX	= SceneHandleUtility.ProjectPointLine(camPos, fromX, toX);
			var lineHandleY	= SceneHandleUtility.ProjectPointLine(camPos, fromY, toY);
			var lineHandleZ	= SceneHandleUtility.ProjectPointLine(camPos, fromZ, toZ);
			var lineOfsX	= (UnityEditor.HandleUtility.GetHandleSize(lineHandleX) * 0.25f);
			var lineOfsY	= (UnityEditor.HandleUtility.GetHandleSize(lineHandleY) * 0.25f);
			var lineOfsZ	= (UnityEditor.HandleUtility.GetHandleSize(lineHandleZ) * 0.25f);
			
			var angleX		= lineHandleX - camPos;
			var angleY		= lineHandleY - camPos;
			var angleZ		= lineHandleZ - camPos;
			var directionX	= Mathf.Abs(angleX.y) <= Mathf.Abs(angleX.z);
			var directionY	= Mathf.Abs(angleY.x) <= Mathf.Abs(angleY.z);
			var directionZ	= Mathf.Abs(angleZ.y) <= Mathf.Abs(angleZ.x);

			var disabled	= SceneHandles.disabled;

			const int labelPadding = 2;
			if (showX && ((visibleAxes & Axes.X) == Axes.X))
			{
				var axisDisabled	= ((activeAxes & Axes.X) != Axes.X) || disabled;
				var selected		= ((selectedAxes & Axes.X) == Axes.X) && !axisDisabled;

				var offsetY		= new Vector3(0, (lineOfsY * signX.y), 0);
				var offsetZ		= new Vector3(0, 0, (lineOfsZ * signX.z));

				var offset		= directionX ? offsetY : offsetZ;
				var fromOfs		= fromX + offset;
				var toOfs		= toX   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthX / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthX / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs  ) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;
				
				SceneHandles.color = SceneHandles.StateColor(color2, axisDisabled, selected);
				SceneHandles.DrawLine(fromX, fromOfs);
				SceneHandles.DrawLine(toX,   toOfs);

				SceneHandles.color = SceneHandles.StateColor(color, axisDisabled, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.right, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.right, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthX, "X");
			}
			
			if (showY && ((visibleAxes & Axes.Y) == Axes.Y))
			{
				var axisDisabled	= ((activeAxes & Axes.Y) != Axes.Y) || disabled;
				var selected		= ((selectedAxes & Axes.Y) == Axes.Y) && !axisDisabled;

				var offsetX		= new Vector3((lineOfsX * signY.x), 0, 0);
				var offsetZ		= new Vector3(0, 0, (lineOfsZ * signY.z));

				var offset		= directionY ? offsetX : offsetZ;
				var fromOfs		= fromY + offset;
				var toOfs		= toY   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthY / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthY / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs  ) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;
				
				SceneHandles.color = SceneHandles.StateColor(color2, axisDisabled, selected);
				SceneHandles.DrawLine(fromY, fromOfs);
				SceneHandles.DrawLine(toY,   toOfs);
				
				SceneHandles.color = SceneHandles.StateColor(color, axisDisabled, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.up, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.up, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthY, "Y");
			}
			
			if (showZ && ((visibleAxes & Axes.Z) == Axes.Z))
			{ 
				var axisDisabled	= ((activeAxes & Axes.Z) != Axes.Z) || disabled;
				var selected		= ((selectedAxes & Axes.Z) == Axes.Z) && !axisDisabled;

				var offsetX		= new Vector3((lineOfsX * signZ.x), 0, 0);
				var offsetY		= new Vector3(0, (lineOfsY * signZ.y), 0);

				var offset		= directionZ ? offsetY : offsetX;
				var fromOfs		= fromZ + offset;
				var toOfs		= toZ   + offset;

				var camDir		= SceneHandleUtility.ProjectPointLine(camPos, fromOfs, toOfs) - camPos;
				var fromSize	= Mathf.Min(absLengthZ / 3.0f, UnityEditor.HandleUtility.GetHandleSize(fromOfs) * 0.2f);
				var toSize		= Mathf.Min(absLengthZ / 3.0f, UnityEditor.HandleUtility.GetHandleSize(toOfs  ) * 0.2f);
				var center		= (toOfs + fromOfs) * 0.5f;
				
				SceneHandles.color = SceneHandles.StateColor(color2, axisDisabled, selected);
				SceneHandles.DrawLine(fromZ, fromOfs);
				SceneHandles.DrawLine(toZ,   toOfs);
				
				SceneHandles.color = SceneHandles.StateColor(color, axisDisabled, selected);
				SceneHandles.DrawLine(fromOfs, toOfs);
				DrawFlatArrow(fromOfs,  Vector3.forward, camDir, fromSize);
				DrawFlatArrow(toOfs,   -Vector3.forward, camDir, toSize  );
				DrawUnitLabel(center, offset, labelPadding, lengthZ, "Z");
			}
			SceneHandles.color = prevColor;
		}

		static readonly UnityEngine.Vector3[] linePoints = new UnityEngine.Vector3[2];
		static readonly UnityEngine.Vector3[] anglePoints = new UnityEngine.Vector3[64];
		public static void DrawAngle(UnityEngine.Vector3 center, UnityEngine.Vector3 direction, UnityEngine.Vector3 axis, float angle)
		{
			var rotation	= UnityEngine.Quaternion.AngleAxis(angle, axis);
			var centerSize	= UnityEditor.HandleUtility.GetHandleSize(center);
			var maxSize		= direction.magnitude;
			var xdir		= direction / maxSize;
			//var ydir		= UnityEngine.Vector3.Cross(xdir, axis);
			var handleSize	= Mathf.Min(centerSize, maxSize);
			var drawAngle	= UnityEngine.Mathf.Clamp(angle, -360, 360);
			var realLength	= Mathf.Max(1, Mathf.CeilToInt((anglePoints.Length / 360.0f) * Mathf.Abs(drawAngle)));

			var pointSize = centerSize * 0.04f;
			SceneHandles.color = SceneHandles.StateColor(SceneHandles.measureColor);
			SceneHandles.DrawAAPolyLine(center + (xdir * pointSize), center + direction);
			SceneHandles.DrawAAPolyLine(center + (rotation * (xdir * pointSize)), center + (rotation * direction));
			
			SceneHandles.DrawWireDisc(center, Vector3.forward, pointSize);

			direction = xdir * handleSize;
			if (UnityEngine.Mathf.Abs(angle) > 0.0f)
			{
				var angleStep = drawAngle / realLength;
				var curAngle = 0.0f;
				for (int i = 0; i < realLength; i++)
				{
					anglePoints[i] = center + (UnityEngine.Quaternion.AngleAxis(curAngle, axis) * direction);
					curAngle += angleStep;
				}
				SceneHandles.DrawDottedLines(anglePoints.Take(realLength).ToArray(), 4.0f);

				rotation = UnityEngine.Quaternion.AngleAxis(angle, axis);
				var right		= rotation * Vector3.right;
				var endPoint	= center + (rotation * direction);
				var arrowSize	= handleSize * 0.2f;// UnityEditor.HandleUtility.GetHandleSize(endPoint) * 0.2f;

				if (angle < 0.0f)
					DrawFlatArrow(endPoint, -right, arrowSize);
				else
					DrawFlatArrow(endPoint,  right, arrowSize);
			}

			Vector3 facing;
			Vector3 position;
			if (handleSize == maxSize)
			{
				facing		= rotation * xdir;
				position	= center + (facing * handleSize);
			} else
			{
				var halfRotation = UnityEngine.Quaternion.AngleAxis(drawAngle / 2.0f, axis);
				facing		= halfRotation * xdir;
				position	= center;
			}
			
			DrawLabel(position, facing, 5, angle + "°");
		}
	}
}
