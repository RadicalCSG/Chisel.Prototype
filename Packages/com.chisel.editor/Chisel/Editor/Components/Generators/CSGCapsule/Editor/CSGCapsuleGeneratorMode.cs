using Chisel.Assets;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
	// TODO: maybe just bevel top of cylinder instead of separate capsule generator??
	public sealed class CSGCapsuleGeneratorMode : ICSGToolMode
	{
		public void OnEnable()
		{
		}

		public void OnDisable()
		{
		}

		void Reset()
		{
		}
		
		public void OnSceneGUI(SceneView sceneView, Rect dragArea)
		{
		}
	}
}
