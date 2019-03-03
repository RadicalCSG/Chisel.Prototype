using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public sealed partial class SceneHandles
	{
		static Vector3		s_PlanarHandlesOctant	= Vector3.one;
		static Vector3[]	s_Vertices				= new Vector3[4];

		static Vector3 PlanarHandle(int id, PlaneAxes planarAxes, Vector3 position, Quaternion rotation, float handleSize, bool selectLockingAxisOnClick = false)
		{
			return PlanarHandle(id, planarAxes, new Vector3[] { position }, position, rotation, handleSize, selectLockingAxisOnClick)[0];
		}

		static Vector3[] PlanarHandle(int id, PlaneAxes planarAxes, Vector3[] points, Vector3 position, Quaternion rotation, float handleSize, bool selectLockingAxisOnClick = false)
		{
			int axis1index = 0;
			int axis2index = 0;
			var isStatic = (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
			var axes = Axes.None;
			switch (planarAxes)
			{
				case PlaneAxes.XZ: { axis1index = 0; axis2index = 2; axes = Axes.XZ; break; }
				case PlaneAxes.XY: { axis1index = 0; axis2index = 1; axes = Axes.XY; break; }
				case PlaneAxes.YZ: { axis1index = 1; axis2index = 2; axes = Axes.YZ; break; }
			}
			
			int axisNormalIndex = 3 - axis2index - axis1index;
			var prevColor = SceneHandles.color;

			var handleTransform = Matrix4x4.TRS(position, rotation, Vector3.one);
			var sceneView = SceneView.currentDrawingSceneView;
			var cameraToTransformToolVector = handleTransform.inverse.MultiplyPoint(sceneView.camera.transform.position).normalized;
			/*
			if (Mathf.Abs (cameraToTransformToolVector[axisNormalIndex]) < 0.05f && GUIUtility.hotControl != id)
			{
				Handles.color = prevColor;
				return points;
			}*/
			
			if (EditorGUIUtility.hotControl == 0)
			{
				s_PlanarHandlesOctant[axis1index] = (cameraToTransformToolVector[axis1index] < -0.01f ? -1 : 1);
				s_PlanarHandlesOctant[axis2index] = (cameraToTransformToolVector[axis2index] < -0.01f ? -1 : 1);
			}

			var handleOffset = s_PlanarHandlesOctant;
			handleOffset[axisNormalIndex] = 0;
			handleOffset = rotation * (handleOffset * handleSize * 0.5f);

			var axis1 = Vector3.zero;
			var axis2 = Vector3.zero;
			var axisNormal = Vector3.zero;
			axis1[axis1index] = 1;
			axis2[axis2index] = 1;
			axisNormal[axisNormalIndex] = 1;
			axis1 = rotation * axis1;
			axis2 = rotation * axis2;
			axisNormal = rotation * axisNormal;
			
			s_Vertices[0] = position + handleOffset + ( axis1 +axis2) * handleSize * 0.5f;
			s_Vertices[1] = position + handleOffset + (-axis1 +axis2) * handleSize * 0.5f;
			s_Vertices[2] = position + handleOffset + (-axis1 -axis2) * handleSize * 0.5f;
			s_Vertices[3] = position + handleOffset + ( axis1 -axis2) * handleSize * 0.5f;

			var innerColor = SceneHandles.color;
			var outerColor = Color.black;
			innerColor = new Color(innerColor.r, innerColor.g, innerColor.b, 0.1f);
			if (!isStatic && !SceneHandles.disabled)
				SceneHandles.DrawSolidRectangleWithOutline(s_Vertices, innerColor, outerColor);

			points = Slider2DHandle(id, points, position, handleOffset, axisNormal, axis1, axis2, handleSize * 0.5f, RectangleHandleCap, axes, selectLockingAxisOnClick);

			SceneHandles.color = prevColor;

			return points;
		}
	}
}
