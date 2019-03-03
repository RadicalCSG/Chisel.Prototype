using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public static class GridRenderer
	{
		const int kPlaneSize = 100;
		private static Mesh gridMesh;		
		internal static Mesh GridMesh
		{
			get
			{
				if (!gridMesh)
				{
					var vertices = new Vector3[kPlaneSize * kPlaneSize];
					var indices  = new int[(kPlaneSize -1) * (kPlaneSize-1)*6];
					var vertex	 = new Vector3();
					for (int y = 0, n = 0; y < kPlaneSize; y++)
					{
						vertex.y = (2.0f * (y / (float)(kPlaneSize - 1))) - 1.0f;
						for (int x = 0; x < kPlaneSize; x++, n++)
						{
							vertex.x = (2.0f * (x / (float)(kPlaneSize - 1))) - 1.0f;
							vertices[n] = vertex;
						}
					}

					for (int y = 0, n = 0; y < kPlaneSize - 1; y++)
					{
						var y0 = y;
						var y1 = y + 1;
						for (int x = 0; x < kPlaneSize - 1; x++, n += 6)
						{
							var x0 = x;
							var x1 = x + 1;

							var n00 = (y0 * kPlaneSize) + x0; var n10 = (y0 * kPlaneSize) + x1;
							var n01 = (y1 * kPlaneSize) + x0; var n11 = (y1 * kPlaneSize) + x1;

							indices[n + 0] = n00;
							indices[n + 1] = n10;
							indices[n + 2] = n01;

							indices[n + 3] = n10;
							indices[n + 4] = n01;
							indices[n + 5] = n11;
						}
					}

					gridMesh = new Mesh()
					{
						name = "Plane",
						vertices  = vertices,
						triangles = indices,
						hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset
					};
					gridMesh.bounds = new Bounds(Vector3.zero, new Vector3(float.MaxValue, 0.1f, float.MaxValue));
				}
				return gridMesh;
			}
		}

		private static MaterialPropertyBlock properties = null;
		
		private static Material gridMaterial;
		internal static Material GridMaterial
		{
			get
			{
				if (!gridMaterial)
				{
					gridMaterial = SceneHandleMaterialManager.GenerateDebugMaterial(SceneHandleMaterialManager.ShaderNameHandlesRoot + "Grid");
					gridMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
					gridMaterial.SetInt("_ZWrite", 0);   
					gridMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual); 
				}
				return gridMaterial;
			}
		}
		
		internal static float	prevOrthoInterpolation	= 0;
		internal static float	prevSceneViewSize		= 0;
		internal static Vector3 prevGridSpacing			= Vector3.zero;
		 
		public static void Render(this Grid grid, SceneView sceneView)
		{
			if (!sceneView)
				return;
			 
			var camera		= sceneView.camera;
			if (!camera)
				return;
			
			var gridMaterial	= GridMaterial;
			var gridMesh		= GridMesh;

			if (properties == null)
			{
				properties = new MaterialPropertyBlock();
				// TODO: somehow make this updatable
				var centerColor = ColorUtility.GetPreferenceColor("Scene/Center Axis", new Color(.8f, .8f, .8f, .93f));
				centerColor.a = 0.5f;
				var gridColor	= ColorUtility.GetPreferenceColor("Scene/Grid",        new Color(.5f, .5f, .5f, .4f));
				gridColor.a = 0.5f;

				properties.SetColor("_CenterColor",	centerColor);
				properties.SetColor("_GridColor",	gridColor);
			}

			float orthoInterpolation; // hack to get SceneView.m_Ortho.faded
			{
				const float kOneOverSqrt2 = 0.707106781f;
				const float kMinOrtho = 0.2f;
				const float kMaxOrtho = 0.95f;
				orthoInterpolation = ((Mathf.Atan(Mathf.Tan(camera.fieldOfView / (2 * Mathf.Rad2Deg)) * Mathf.Sqrt(camera.aspect) / kOneOverSqrt2) / (0.5f * Mathf.Deg2Rad)) / 90.0f);
				orthoInterpolation = Mathf.Clamp01((orthoInterpolation - kMinOrtho) / (kMaxOrtho - kMinOrtho));
			}
			if (prevOrthoInterpolation	!= orthoInterpolation)
			{
				properties.SetFloat("_OrthoInterpolation", orthoInterpolation);
				prevOrthoInterpolation = orthoInterpolation;
			}

			var sceneViewSize	= sceneView.size;
			if (prevSceneViewSize != sceneViewSize)
			{
				properties.SetFloat("_ViewSize", sceneViewSize);
				prevSceneViewSize = sceneViewSize;
			}

			var gridSpacing = grid.Spacing;
			if (prevGridSpacing != gridSpacing)
			{ 	
				Vector3 swizzledGridSpacing;
				swizzledGridSpacing.x = gridSpacing.x;
				swizzledGridSpacing.y = gridSpacing.z;
				swizzledGridSpacing.z = gridSpacing.y;

				properties.SetVector("_GridSpacing", swizzledGridSpacing);
				prevGridSpacing = gridSpacing;
			}
			
			Graphics.DrawMesh(gridMesh, grid.GridToWorldSpace, gridMaterial, 0, camera, 0, properties, false, false);
		}
	}
}
