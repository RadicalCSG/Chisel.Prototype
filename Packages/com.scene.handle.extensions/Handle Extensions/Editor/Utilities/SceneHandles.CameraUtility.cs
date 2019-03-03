using UnityEngine;
using UnityEditor;

namespace UnitySceneExtensions
{
	public static class CameraUtility
	{
		public static Frustum GetSubFrustum(this Camera camera, Rect screenRect)
		{
			var oldMatrix = SceneHandles.matrix;
			SceneHandles.matrix = Matrix4x4.identity;

			var min_x = screenRect.x;
			var max_x = screenRect.x + screenRect.width;
			var min_y = screenRect.y;
			var max_y = screenRect.y + screenRect.height;

			var o0 = new Vector2(min_x, min_y);
			var o1 = new Vector2(max_x, min_y);
			var o2 = new Vector2(max_x, max_y);
			var o3 = new Vector2(min_x, max_y);
			
			var r0 = UnityEditor.HandleUtility.GUIPointToWorldRay(o0);
			var r1 = UnityEditor.HandleUtility.GUIPointToWorldRay(o1);
			var r2 = UnityEditor.HandleUtility.GUIPointToWorldRay(o2);
			var r3 = UnityEditor.HandleUtility.GUIPointToWorldRay(o3);
			SceneHandles.matrix = oldMatrix;
			
			var n0 = r0.origin;
			var n1 = r1.origin;
			var n2 = r2.origin;
			var n3 = r3.origin;

			var far = camera.farClipPlane;
			var f0	= n0 + (r0.direction * far);
			var f1	= n1 + (r1.direction * far);
			var f2	= n2 + (r2.direction * far);
			var f3	= n3 + (r3.direction * far);

			var frustum = new Frustum();
			frustum.Planes[0] = new Plane(n2, n1, f1); // right  +
			frustum.Planes[1] = new Plane(f3, f0, n0); // left   -
			frustum.Planes[2] = new Plane(n1, n0, f0); // top    -
			frustum.Planes[3] = new Plane(n3, n2, f2); // bottom +
			frustum.Planes[4] = new Plane(n0, n1, n2); // near   -
			frustum.Planes[5] = new Plane(f2, f1, f0); // far    +
			return frustum;
		}
	}
}
