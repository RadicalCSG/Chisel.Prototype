using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public sealed partial class SceneHandles
	{
		static float Size2DSlider(Vector3 center, Vector3 direction, Vector3 forward, Vector3 up, Vector3 right, float radius, Axes axes = Axes.None, bool renderDisc = true)
		{
			var id = GUIUtility.GetControlID (s_Slider2DHash, FocusType.Keyboard);
			return Size2DSlider(id, center, direction, forward, up, right, radius, axes, renderDisc);
		}

		static float Size2DSlider(int id, Vector3 center, Vector3 direction, Vector3 forward, Vector3 up, Vector3 right, float radius, Axes axes = Axes.None, bool renderDisc = true)
		{
			Vector3 position = center + direction * radius;
			float size = UnityEditor.HandleUtility.GetHandleSize(position);
			bool temp = GUI.changed;
			GUI.changed = false;
			position = Slider2DHandle(id, position, Vector3.zero, forward, up, right, size * 0.05f, OutlinedDotHandleCap, axes, renderDisc);
			
			if (GUI.changed)
				radius = Vector3.Dot(position - center, direction);
			GUI.changed |= temp;
			return radius;
		}

		public static float Radius2DHandle(Quaternion rotation, Vector3 position, float radius, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true)
		{
			minRadius = Mathf.Abs(minRadius);
			maxRadius = Mathf.Abs(maxRadius); if (maxRadius < minRadius) maxRadius = minRadius;

			var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
			var prevDisabled	= SceneHandles.disabled;
			var prevColor		= SceneHandles.color;

			var forward = rotation * Vector3.forward;
			var up		= rotation * Vector3.up;
			var right	= rotation * Vector3.right;

			bool temp = GUI.changed;
			GUI.changed = false;
			
			var isDisabled =  isStatic || prevDisabled || Snapping.AxisLocking[1];
			SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

			radius = Size2DSlider(position,     up, forward, up, right, radius);
			radius = Size2DSlider(position,    -up, forward, up, right, radius);
			
			isDisabled =  isStatic || prevDisabled || Snapping.AxisLocking[0];
			SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

			radius = Size2DSlider(position,  right, forward, up, right, radius);
			radius = Size2DSlider(position, -right, forward, up, right, radius);
			
			radius = Mathf.Max(minRadius, Mathf.Min(Mathf.Abs(radius), maxRadius)); 
			
			GUI.changed |= temp;
			
			if (radius > 0 && renderDisc)
			{ 
				isDisabled =  isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
				SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);
				
				SceneHandles.DrawWireDisc(position, forward, radius);
			
				SceneHandles.disabled = prevDisabled;
				SceneHandles.color = prevColor;
			}
			return radius;
		}

		public static Vector3 Radius2DHandle(Vector3 center, Vector3 up, Vector3 radius, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true)
		{
			minRadius = Mathf.Abs(minRadius);
			maxRadius = Mathf.Abs(maxRadius); if (maxRadius < minRadius) maxRadius = minRadius;

			var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
			var prevDisabled	= SceneHandles.disabled;
			var prevColor		= SceneHandles.color;
			var prevChanged		= GUI.changed;
			var hasChanged		= false;

			
			var plane	= new Plane(up, Vector3.zero);

			var delta1 = GeometryUtility.ProjectPointPlane(radius - center, plane);
			var delta2 = Quaternion.AngleAxis(90, up) * delta1;

			var position0 = center + delta1;
			var position1 = center - delta1;
			var position2 = center + delta2;
			var position3 = center - delta2;


			float size;
			Vector3 forward;
			Vector3 right;
			GeometryUtility.CalculateTangents(up, out right, out forward);
			

			
			bool noRotation = Event.current.shift;
			
			var isDisabled =  isStatic || prevDisabled;
			SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

			Vector3 moveDelta = Vector3.zero;
			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position0);
			position0 = Slider2DHandle(position0, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed) { moveDelta = position0 - center; hasChanged = true; }
			
			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position1);
			position1 = Slider2DHandle(position1, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed) { moveDelta = -(position1 - center); hasChanged = true; }
			
			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position2);
			position2 = Slider2DHandle(position2, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed) { moveDelta = (Quaternion.AngleAxis(-90, up) * (position2 - center)); hasChanged = true; }
			
			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position3);
			position3 = Slider2DHandle(position3, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed) { moveDelta = -(Quaternion.AngleAxis(-90, up) * (position3 - center)); hasChanged = true; }

			if (hasChanged)
			{
				Vector3 prevRadius = radius;
				var newRadius = center + moveDelta;

				var newDelta	= GeometryUtility.ProjectPointPlane((newRadius - center), plane);
				var length		= newDelta.magnitude;
				if (length < minRadius || length > maxRadius)
				{
					var direction = (length > Vector3.kEpsilon) ? newDelta.normalized : ((delta1.magnitude > Vector3.kEpsilon) ? delta1.normalized : Vector3.up);
					length = Mathf.Max(minRadius, Mathf.Min(length, maxRadius));
					radius = center + (length * direction);
				} else
					radius = newRadius;

				if (noRotation)
				{
					var magnitude = newRadius.magnitude;
					radius = prevRadius.normalized * magnitude;
				}
			}

			
			GUI.changed |= prevChanged | hasChanged;
			
			isDisabled =  isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
			SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

			
			float radiusMagnitude = delta1.magnitude;
			if (radiusMagnitude > 0 && renderDisc)
				SceneHandles.DrawWireDisc(center, up, radiusMagnitude);
			
			
			SceneHandles.disabled = prevDisabled;
			SceneHandles.color = prevColor;
			return radius;
		}

		
		internal static int s_Radius2DHash = "Radius2DHash".GetHashCode();
		public static bool Radius2DHandle(Vector3 center, Vector3 up, ref Vector3 radius1, ref Vector3 radius2, float minRadius1 = 0, float minRadius2 = 0, float maxRadius1 = float.PositiveInfinity, float maxRadius2 = float.PositiveInfinity, bool renderDisc = true)
		{
			var positionId0 = GUIUtility.GetControlID (s_Radius2DHash, FocusType.Keyboard);
			var positionId1 = GUIUtility.GetControlID (s_Radius2DHash, FocusType.Keyboard);
			var positionId2 = GUIUtility.GetControlID (s_Radius2DHash, FocusType.Keyboard);
			var positionId3 = GUIUtility.GetControlID (s_Radius2DHash, FocusType.Keyboard);

			minRadius1 = Mathf.Abs(minRadius1); 
			minRadius2 = Mathf.Abs(minRadius2); 
			maxRadius1 = Mathf.Abs(maxRadius1); if (maxRadius1 < minRadius1) maxRadius1 = minRadius1;
			maxRadius2 = Mathf.Abs(maxRadius2); if (maxRadius2 < minRadius2) maxRadius2 = minRadius2;

			var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
			var prevColor		= SceneHandles.color;
			var prevMatrix		= SceneHandles.matrix;
			var prevDisabled	= SceneHandles.disabled;
			var prevChanged		= GUI.changed;

			float size;
			Vector3 forward;
			Vector3 right;
			GeometryUtility.CalculateTangents(up, out right, out forward);

			var plane = new Plane(up, Vector3.zero);

			var delta1 = GeometryUtility.ProjectPointPlane(radius1 - center, plane);
			var delta2 = GeometryUtility.ProjectPointPlane(radius2 - center, plane);

			var delta1Magnitude = delta1.magnitude;
			var delta2Magnitude = delta2.magnitude;
			
			var delta1Normalized = (delta1Magnitude < Vector3.kEpsilon) ? Vector3.zero : (delta1 / delta1Magnitude);
			var delta2Normalized = (delta2Magnitude < Vector3.kEpsilon) ? Vector3.zero : (delta2 / delta2Magnitude);

			// useful to have when modifying the 'other' one
			var rotatedDelta1 = Quaternion.AngleAxis(-90, up) * delta1Normalized;
			var rotatedDelta2 = Quaternion.AngleAxis( 90, up) * delta2Normalized;


			var position0 = center + delta1;
			var position1 = center - delta1;
			var position2 = center + delta2;
			var position3 = center - delta2;

			var isDisabled =  isStatic || prevDisabled;
			SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

			bool noRotation = Event.current.shift;

			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position0);
			position0 = Slider2DHandle(positionId0, position0, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed)
			{
				var moveDelta = (position0 - center);
				if (noRotation) { radius1 = GeometryUtility.ProjectPointRay(center + moveDelta, center, rotatedDelta2); }
				else			{ radius1 = center + moveDelta; }

				delta1 = GeometryUtility.ProjectPointPlane(radius1 - center, plane);
				delta1Magnitude = delta1.magnitude;

				if (!noRotation && delta1Magnitude > Vector3.kEpsilon)
				{
					radius2 = center + ((Quaternion.AngleAxis(-90, up) * (delta1 / delta1Magnitude)) * delta2Magnitude);
				}
				prevChanged = true;
			}

			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position1);
			position1 = Slider2DHandle(positionId1, position1, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed)
			{
				var moveDelta = (position1 - center);
				if (noRotation) { radius1 = GeometryUtility.ProjectPointRay(center - moveDelta, center, rotatedDelta2); }
				else			{ radius1 = center - moveDelta; }

				delta1 = GeometryUtility.ProjectPointPlane(radius1 - center, plane);
				delta1Magnitude = delta1.magnitude;

				if (!noRotation && delta1Magnitude > Vector3.kEpsilon)
				{
					radius2 = center + ((Quaternion.AngleAxis(-90, up) * (delta1 / delta1Magnitude)) * delta2Magnitude);
				}
				prevChanged = true;
			}
			
			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position2);
			position2 = Slider2DHandle(positionId2, position2, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed)
			{
				var moveDelta = (position2 - center);
				if (noRotation) { radius2 = GeometryUtility.ProjectPointRay(center + moveDelta, center, rotatedDelta1); }
				else			{ radius2 = center + moveDelta; }

				delta2 = GeometryUtility.ProjectPointPlane(radius2 - center, plane);
				delta2Magnitude = delta2.magnitude;

				if (!noRotation && delta2Magnitude > Vector3.kEpsilon)
				{
					radius1 = center + ((Quaternion.AngleAxis(90, up) * (delta2 / delta2Magnitude)) * delta1Magnitude);
				}
				prevChanged = true;
			}

			GUI.changed = false;
			size = UnityEditor.HandleUtility.GetHandleSize(position3);
			position3 = Slider2DHandle(positionId3, position3, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
			if (GUI.changed)
			{
				var moveDelta = (position3 - center);
				if (noRotation) { radius2 = GeometryUtility.ProjectPointRay(center - moveDelta, center, rotatedDelta1); }
				else			{ radius2 = center - moveDelta; }

				delta2 = GeometryUtility.ProjectPointPlane(radius2 - center, plane);
				delta2Magnitude = delta2.magnitude;

				if (!noRotation && delta2Magnitude > Vector3.kEpsilon)
				{
					radius1 = center + ((Quaternion.AngleAxis(90, up) * (delta2 / delta2Magnitude)) * delta1Magnitude);
				}
				prevChanged = true;
			} 

			GUI.changed |= prevChanged;


			if (delta1Magnitude < minRadius1 || delta1Magnitude > maxRadius1)
			{
				if (delta2Magnitude < minRadius2 || delta2Magnitude > maxRadius2)
				{
					delta1Magnitude = Mathf.Max(minRadius1, Mathf.Min(delta1Magnitude, maxRadius1));
					delta2Magnitude = Mathf.Max(minRadius2, Mathf.Min(delta2Magnitude, maxRadius2));

					delta1Normalized = right;
					delta2Normalized = up;

					delta1 = GeometryUtility.ProjectPointPlane(delta1Normalized * delta1Magnitude, plane);
					delta2 = GeometryUtility.ProjectPointPlane(delta2Normalized * delta2Magnitude, plane);

					rotatedDelta2 = delta2Normalized;
					rotatedDelta1 = delta1Normalized;

					radius1 = center + delta1;
					radius2 = center + delta2;
					GUI.changed = true;
				} else
				{
					delta1Magnitude  = Mathf.Max(minRadius1, Mathf.Min(delta1Magnitude, maxRadius1));
					delta1Normalized = rotatedDelta2;
					delta1 = GeometryUtility.ProjectPointPlane(delta1Normalized * delta1Magnitude, plane);
					radius1 = center + delta1;
					GUI.changed = true;
				}
			} else
			if (delta2Magnitude < minRadius2 || delta2Magnitude > maxRadius2)
			{
				delta2Magnitude = Mathf.Max(minRadius2, Mathf.Min(delta2Magnitude, maxRadius2));
				delta2Normalized = rotatedDelta1;
				delta2 = delta2Normalized * delta2Magnitude;
				radius2 = center + delta2;
				GUI.changed = true;
			}
			

			if (Event.current.type == EventType.Repaint)
			{ 
				isDisabled =  isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
				SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

			
				if (delta1Magnitude > Vector3.kEpsilon && delta2Magnitude > Vector3.kEpsilon)
				{
					var ellipsis = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
				
					ellipsis.m00 = delta1.x;
					ellipsis.m10 = delta1.y;
					ellipsis.m20 = delta1.z;
				
					ellipsis.m01 = delta2.x;
					ellipsis.m11 = delta2.y;
					ellipsis.m21 = delta2.z;
				
					ellipsis.m02 = up.x;
					ellipsis.m12 = up.y;
					ellipsis.m22 = up.z;

					ellipsis *= Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);

					var newMatrix	= prevMatrix * ellipsis;

					SceneHandles.matrix = newMatrix; 
					if (renderDisc)
						SceneHandles.DrawWireDisc(center, Vector3.forward, 1.0f);
				} else
				if (delta1Magnitude > Vector3.kEpsilon)
					SceneHandles.DrawLine(position0, position1);
				else
				if (delta2Magnitude > Vector3.kEpsilon)
					SceneHandles.DrawLine(position2, position3);
			}

			SceneHandles.disabled = prevDisabled;
			SceneHandles.matrix = prevMatrix;
			SceneHandles.color = prevColor;

			var focus = SceneHandleUtility.focusControl;
			return  (focus == positionId0) ||
					(focus == positionId1) ||
					(focus == positionId2) ||
					(focus == positionId3);
		}
	}
}
