using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
	public class CSGShapeEditMode : ICSGToolMode
	{
		public void OnEnable()
		{
			CSGOutlineRenderer.VisualizationMode = VisualizationMode.None;
			// TODO: shouldn't just always set this param
			Tools.hidden = true; 
		}

		public void OnDisable()
		{

		}

		public void OnSceneGUI(SceneView sceneView, Rect dragArea)
		{
			// NOTE: Actual work is done by Editor classes
		}
	}
}
