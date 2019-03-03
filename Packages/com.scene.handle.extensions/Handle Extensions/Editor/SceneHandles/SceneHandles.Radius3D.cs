using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public sealed partial class SceneHandles
	{
		public static float Radius3DHandle(Quaternion rotation, Vector3 position, float radius, float minRadius = 0, float maxRadius = float.PositiveInfinity)
        {
			minRadius = Mathf.Abs(minRadius);
			maxRadius = Mathf.Abs(maxRadius); if (maxRadius < minRadius) maxRadius = minRadius;
			
			const float kEpsilon = 0.000001F;
			
			var camera					= Camera.current;
            var cameraLocalPos			= SceneHandles.inverseMatrix.MultiplyPoint(camera.transform.position);
			var cameraLocalForward		= SceneHandles.inverseMatrix.MultiplyVector(camera.transform.forward);
			var isCameraInsideSphere	= (cameraLocalPos - position).magnitude < radius;
			var isCameraOrthographic	= camera.orthographic;

			var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
			var prevDisabled	= SceneHandles.disabled;
			var prevColor		= SceneHandles.color;
			
			var forward = rotation * Vector3.forward;
            var up		= rotation * Vector3.up;
            var right	= rotation * Vector3.right;

            bool guiHasChanged = GUI.changed;
            GUI.changed = false;
			
            Vector3 positiveXDir	=  right;
            Vector3 negativeXDir	= -right;
            Vector3 positiveYDir	=  up;
            Vector3 negativeYDir	= -up;
            Vector3 positiveZDir	=  forward;
            Vector3 negativeZDir	= -forward;
			
            Vector3 positiveXHandle = position + positiveXDir * radius;
            Vector3 negativeXHandle = position + negativeXDir * radius;
            Vector3 positiveYHandle = position + positiveYDir * radius;
            Vector3 negativeYHandle = position + negativeYDir * radius;
            Vector3 positiveZHandle = position + positiveZDir * radius;
            Vector3 negativeZHandle = position + negativeZDir * radius;

            bool positiveXBackfaced = false;
            bool negativeXBackfaced = false;
            bool positiveYBackfaced = false;
            bool negativeYBackfaced = false;
            bool positiveZBackfaced = false;
            bool negativeZBackfaced = false;
			if (!isCameraInsideSphere)
			{
				float cosV;

				cosV = isCameraOrthographic ? Vector3.Dot(positiveXDir, -cameraLocalForward) :
											  Vector3.Dot(positiveXDir, (cameraLocalPos - positiveXHandle));
				positiveXBackfaced = (cosV < -0.0001f);

				cosV = isCameraOrthographic ? Vector3.Dot(negativeXDir, -cameraLocalForward) :
											  Vector3.Dot(negativeXDir, (cameraLocalPos - negativeXHandle));
				negativeXBackfaced = (cosV < -0.0001f);
				

				cosV = isCameraOrthographic ? Vector3.Dot(positiveYDir, -cameraLocalForward) :
											  Vector3.Dot(positiveYDir, (cameraLocalPos - positiveYHandle));
				positiveYBackfaced = (cosV < -0.0001f);

				cosV = isCameraOrthographic ? Vector3.Dot(negativeYDir, -cameraLocalForward) :
											  Vector3.Dot(negativeYDir, (cameraLocalPos - negativeYHandle));
				negativeYBackfaced = (cosV < -0.0001f);
				

				cosV = isCameraOrthographic ? Vector3.Dot(positiveZDir, -cameraLocalForward) :
											  Vector3.Dot(positiveZDir, (cameraLocalPos - positiveZHandle));
				positiveZBackfaced = (cosV < -0.0001f);

				cosV = isCameraOrthographic ? Vector3.Dot(negativeZDir, -cameraLocalForward) :
											  Vector3.Dot(negativeZDir, (cameraLocalPos - negativeZHandle));
				negativeZBackfaced = (cosV < -0.0001f);
			}
						
            float positiveXSize = UnityEditor.HandleUtility.GetHandleSize(positiveXHandle) * 0.05f * (positiveXBackfaced ? backfaceSizeMultiplier : 1);
            float negativeXSize = UnityEditor.HandleUtility.GetHandleSize(negativeXHandle) * 0.05f * (negativeXBackfaced ? backfaceSizeMultiplier : 1);
            float positiveYSize = UnityEditor.HandleUtility.GetHandleSize(positiveYHandle) * 0.05f * (positiveYBackfaced ? backfaceSizeMultiplier : 1);
            float negativeYSize = UnityEditor.HandleUtility.GetHandleSize(negativeYHandle) * 0.05f * (negativeYBackfaced ? backfaceSizeMultiplier : 1);
            float positiveZSize = UnityEditor.HandleUtility.GetHandleSize(positiveZHandle) * 0.05f * (positiveZBackfaced ? backfaceSizeMultiplier : 1);
            float negativeZSize = UnityEditor.HandleUtility.GetHandleSize(negativeZHandle) * 0.05f * (negativeZBackfaced ? backfaceSizeMultiplier : 1);
			
			

			var isDisabled		=  isStatic || prevDisabled || Snapping.AxisLocking[0];
			var color			= SceneHandles.StateColor(prevColor, isDisabled, false);
			var backfacedColor	= SceneHandles.MultiplyTransparency(color, SceneHandles.backfaceAlphaMultiplier);

            GUI.changed = false;
			SceneHandles.color = positiveXBackfaced ? backfacedColor : color;
            positiveXHandle = Slider2DHandle(positiveXHandle, Vector3.zero, forward, up, right, positiveXSize, OutlinedDotHandleCap);			
            if (GUI.changed) { radius = Vector3.Dot(positiveXHandle - position, positiveXDir); guiHasChanged = true; }
			
            GUI.changed = false;
			SceneHandles.color = negativeXBackfaced ? backfacedColor : color;
            negativeXHandle = Slider2DHandle(negativeXHandle, Vector3.zero, forward, up, right, negativeXSize, OutlinedDotHandleCap);			
            if (GUI.changed) { radius = Vector3.Dot(negativeXHandle - position, negativeXDir); guiHasChanged = true; }



			isDisabled		=  isStatic || prevDisabled || Snapping.AxisLocking[1];
			color			= SceneHandles.StateColor(prevColor, isDisabled, false);
			backfacedColor	= SceneHandles.MultiplyTransparency(color, SceneHandles.backfaceAlphaMultiplier);

            GUI.changed = false;
			SceneHandles.color = positiveYBackfaced ? backfacedColor : color;
            positiveYHandle = Slider2DHandle(positiveYHandle, Vector3.zero, forward, up, right, positiveYSize, OutlinedDotHandleCap);			
            if (GUI.changed) { radius = Vector3.Dot(positiveYHandle - position, positiveYDir); guiHasChanged = true; }
			
            GUI.changed = false;
			SceneHandles.color = negativeYBackfaced ? backfacedColor : color;
            negativeYHandle = Slider2DHandle(negativeYHandle, Vector3.zero, forward, up, right, negativeYSize, OutlinedDotHandleCap);			
            if (GUI.changed) { radius = Vector3.Dot(negativeYHandle - position, negativeYDir); guiHasChanged = true; }
			

			
			isDisabled		=  isStatic || prevDisabled || Snapping.AxisLocking[2];
			color			= SceneHandles.StateColor(prevColor, isDisabled, false);
			backfacedColor	= SceneHandles.MultiplyTransparency(color, SceneHandles.backfaceAlphaMultiplier);

            GUI.changed = false;
			SceneHandles.color = positiveZBackfaced ? backfacedColor : color;
            positiveZHandle = Slider2DHandle(positiveZHandle, Vector3.zero, up, forward, right, positiveZSize, OutlinedDotHandleCap);			
            if (GUI.changed) { radius = Vector3.Dot(positiveZHandle - position, positiveZDir); guiHasChanged = true; }
			
            GUI.changed = false;
			SceneHandles.color = negativeZBackfaced ? backfacedColor : color;
            negativeZHandle = Slider2DHandle(negativeZHandle, Vector3.zero, up, forward, right, negativeZSize, OutlinedDotHandleCap);			
            if (GUI.changed) { radius = Vector3.Dot(negativeZHandle - position, negativeZDir); guiHasChanged = true; }

			
			radius = Mathf.Max(minRadius, Mathf.Min(Mathf.Abs(radius), maxRadius)); 
			

            GUI.changed |= guiHasChanged;
			
			if (radius > 0)
			{
				isDisabled		= isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
				color			= SceneHandles.StateColor(prevColor, isDisabled, false);
				backfacedColor	= SceneHandles.MultiplyTransparency(color, SceneHandles.backfaceAlphaMultiplier);
				var discOrientations = new Vector3[]
				{
					rotation * Vector3.right,
					rotation * Vector3.up,
					rotation * Vector3.forward
				};
				
				var currentCamera		= Camera.current;
				var cameraTransform		= currentCamera.transform;
				if (currentCamera.orthographic)
				{
					var planeNormal = cameraTransform.forward;
					SceneHandles.DrawWireDisc(position, planeNormal, radius);
					planeNormal.Normalize();
					for (int i = 0; i < 3; i++)
					{
						var discOrientation = discOrientations[i];
						var discTangent		= Vector3.Cross(discOrientation, planeNormal);

						// we may have view dir locked to one axis
						if (discTangent.sqrMagnitude > kEpsilon)
						{
							SceneHandles.color = color;
							SceneHandles.DrawWireArc(position, discOrientation, discTangent, 180, radius);
							SceneHandles.color = backfacedColor;
							SceneHandles.DrawWireArc(position, discOrientation, discTangent, -180, radius);
						}
					}
				} else
				{ 
					// Since the geometry is transformed by Handles.matrix during rendering, we transform the camera position
					// by the inverse matrix so that the two-shaded wireframe will have the proper orientation.
					var invMatrix				= SceneHandles.inverseMatrix;

					var cameraCenter			= cameraTransform.position;
					var cameraToCenter			= position - invMatrix.MultiplyPoint(cameraCenter); // vector from camera to center
					var sqrDistCameraToCenter	= cameraToCenter.sqrMagnitude;
					var sqrRadius				= radius * radius;					// squared radius
					var sqrOffset				= sqrRadius * sqrRadius / sqrDistCameraToCenter;	// squared distance from actual center to drawn disc center
					var insideAmount			= sqrOffset / sqrRadius;
					if (insideAmount < 1)
					{
						if (Mathf.Abs(sqrDistCameraToCenter) < kEpsilon)
							return radius;

						var horizonRadius = Mathf.Sqrt(sqrRadius - sqrOffset);
						var horizonCenter = position - sqrRadius * cameraToCenter / sqrDistCameraToCenter;
						SceneHandles.color = color;
						SceneHandles.DrawWireDisc(horizonCenter, cameraToCenter, horizonRadius);

						var planeNormal = cameraToCenter.normalized;
						for (int i = 0; i < 3; i++)
						{
							var discOrientation = discOrientations[i];
							
							var angleBetweenDiscAndNormal = Mathf.Acos(Vector3.Dot(discOrientation, planeNormal));
							angleBetweenDiscAndNormal = (Mathf.PI * 0.5f) - Mathf.Min(angleBetweenDiscAndNormal, Mathf.PI - angleBetweenDiscAndNormal);

							float f = Mathf.Tan(angleBetweenDiscAndNormal);
							float g = Mathf.Sqrt(sqrOffset + f * f * sqrOffset) / radius;
							if (g < 1)
							{
								var angleToHorizon			= Mathf.Asin(g) * Mathf.Rad2Deg;
								var discTangent				= Vector3.Cross(discOrientation, planeNormal);
								var vectorToPointOnHorizon	= Quaternion.AngleAxis(angleToHorizon, discOrientation) * discTangent;
								var horizonArcLength		= (90 - angleToHorizon) * 2.0f;
							
								SceneHandles.color = color;
								SceneHandles.DrawWireArc(position, discOrientation, vectorToPointOnHorizon, horizonArcLength, radius);
								SceneHandles.color = backfacedColor;
								SceneHandles.DrawWireArc(position, discOrientation, vectorToPointOnHorizon, horizonArcLength - 360, radius);
							} else
							{
								SceneHandles.color = backfacedColor;
								SceneHandles.DrawWireDisc(position, discOrientation, radius);
							}
						}
					} else
					{
						SceneHandles.color = backfacedColor;
						for (int i = 0; i < 3; i++)
						{
							var discOrientation = discOrientations[i];
							SceneHandles.DrawWireDisc(position, discOrientation, radius);
						}
					}
				}
			}

			SceneHandles.disabled = prevDisabled;
			SceneHandles.color = prevColor;

			return radius;
        }
	}
}
