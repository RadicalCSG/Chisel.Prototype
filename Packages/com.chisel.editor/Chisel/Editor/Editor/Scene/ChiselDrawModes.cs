using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.EditorTools;
using NUnit.Framework.Constraints;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;

namespace Chisel.Editors
{
	public sealed class ChiselDrawModes
	{
		const string kChiselSection			= "Chisel";

		static readonly (string, DrawModeFlags)[] s_DrawModes = new[]
		{
			("Shadow Only Surfaces",		DrawModeFlags.HideRenderables | DrawModeFlags.ShowShadowOnly),
			("Collider Surfaces",			DrawModeFlags.HideRenderables | DrawModeFlags.ShowColliders),
			("Shadow Casting Surfaces",		DrawModeFlags.HideRenderables | DrawModeFlags.ShowCasters | DrawModeFlags.ShowShadowOnly),
			("Shadow Receiving Surfaces",	DrawModeFlags.HideRenderables | DrawModeFlags.ShowReceivers),
			("Hidden Surfaces",				DrawModeFlags.ShowShadowOnly | DrawModeFlags.ShowCulled | DrawModeFlags.ShowDiscarded)
		};
		static readonly Dictionary<string, DrawModeFlags> s_DrawModeLookup = new Dictionary<string, DrawModeFlags>();


		static bool drawModesInitialized = false;
		static void SetupDrawModes()
		{
			if (drawModesInitialized)
				return;

			s_DrawModeLookup.Clear();
			SceneView.ClearUserDefinedCameraModes();
			foreach (var item in s_DrawModes)
            {
				s_DrawModeLookup[item.Item1] = item.Item2;
				SceneView.AddCameraMode(item.Item1, kChiselSection);
			}

			drawModesInitialized = true;
		}

		static HashSet<Camera>		knownCameras	= new HashSet<Camera>();

		public static void OnRenderModel(Camera camera)
        {
			// TODO: optimize
			var helperStateFlags = ChiselGeneratedComponentManager.BeginDrawModeForCamera(camera);
			if (!knownCameras.Contains(camera))
				return;
			ChiselGeneratedComponentManager.OnRenderModels(camera, helperStateFlags);
		}

		public static void OnPostRender(Camera camera)
		{
			// TODO: optimize
			var helperStateFlags = ChiselGeneratedComponentManager.EndDrawModeForCamera(); // <- ensures selection works (rendering partial meshes hides regular meshes)
			if (!knownCameras.Contains(camera))
				return;
			ChiselGeneratedComponentManager.OnRenderModels(camera, helperStateFlags);
		}

		[InitializeOnLoadMethod]
		static void OnProjectLoadedInEditor()
		{
            SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneLoaded += OnSceneLoaded;
			ChiselGeneratedModelMeshManager.PostUpdateModels -= OnPostUpdateModels;
			ChiselGeneratedModelMeshManager.PostUpdateModels += OnPostUpdateModels;
		}
		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ChiselGeneratedComponentManager.InitializeOnLoad(scene); // <- ensures selection works (rendering partial meshes hides regular meshes)
		}

		private static void OnPostUpdateModels()
		{
			ChiselGeneratedComponentManager.Update(); // <- ensures selection works (rendering partial meshes hides regular meshes)
		}

        [PostProcessScene(1)]
		public static void OnPostprocessScene()
		{
			ChiselGeneratedComponentManager.RemoveHelperSurfaces();
		}

		public static void HandleDrawMode(SceneView sceneView)
		{
			ChiselDrawModes.SetupDrawModes();

			var camera = sceneView.camera;
			if (camera == null)
				return;
			
			if (knownCameras.Add(camera))
			{
				Camera.onPreCull -= OnRenderModel;
				Camera.onPreCull += OnRenderModel;

				Camera.onPostRender -= OnPostRender;
				Camera.onPostRender += OnPostRender;
			}

			var desiredHelperStateFlags = DrawModeFlags.Default;
			if (sceneView.cameraMode.drawMode == DrawCameraMode.UserDefined)
			{
				if (s_DrawModeLookup.TryGetValue(sceneView.cameraMode.name, out var flags))
					desiredHelperStateFlags = flags;
			}
			var prevDrawMode = ChiselGeneratedComponentManager.GetCameraDrawMode(camera);
			if (prevDrawMode != desiredHelperStateFlags)
			{
				ChiselGeneratedComponentManager.SetCameraDrawMode(camera, desiredHelperStateFlags);
			}
		}
	}
}