using UnityEngine;
using UnityEditor;
using System;
using Chisel.Core;
using UnityEngine.Profiling;

namespace Chisel.Editors
{
	[CustomEditor(typeof(ChiselSurfaceMetadata))]
	public sealed class ChiselSurfaceMetadataEditor : Editor
	{
		SerializedProperty surfaceDestinationFlagsProp;
		SerializedProperty physicsMaterialProp;

		internal void OnEnable()
		{
			if (!target)
			{
				surfaceDestinationFlagsProp = null;
				physicsMaterialProp = null;
				return;
			}

			surfaceDestinationFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kDestinationFlagsFieldName);
			physicsMaterialProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kPhysicsMaterialFieldName);
		}

		internal void OnDisable()
		{
			surfaceDestinationFlagsProp = null;
			physicsMaterialProp = null;
		}

		void OnDestroy() { OnDisable(); }

        public static void GetSurfaceDestinationFlags(SerializedProperty surfaceDestinationFlagsProp, out bool isRenderable, out bool isCollidable)
		{
			isCollidable = true;
			isRenderable = true;
			if (!surfaceDestinationFlagsProp.hasMultipleDifferentValues)
			{
				SurfaceDestinationFlags flags = (SurfaceDestinationFlags)surfaceDestinationFlagsProp.enumValueFlag;
				isCollidable = (flags & SurfaceDestinationFlags.Collidable) == SurfaceDestinationFlags.Collidable;
				isRenderable = (flags & SurfaceDestinationFlags.Renderable) == SurfaceDestinationFlags.Renderable;
			}
		}

		public override void OnInspectorGUI()
		{
			Profiler.BeginSample("OnInspectorGUI");
			try
			{
				EditorGUI.BeginChangeCheck();
				{
					EditorGUILayout.PropertyField(surfaceDestinationFlagsProp, true);
					GetSurfaceDestinationFlags(surfaceDestinationFlagsProp, out var isRenderable, out var isCollidable);
					if (isCollidable)
					{
						EditorGUILayout.PropertyField(physicsMaterialProp, true);
					}
				}
				if (EditorGUI.EndChangeCheck())
					serializedObject.ApplyModifiedProperties();
			}
			catch (ExitGUIException) { }
			catch (Exception ex) { Debug.LogException(ex); }
			Profiler.EndSample();
		}
	}
}
