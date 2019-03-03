using System;
using System.Linq;
using Chisel.Assets;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Components
{
	// TODO: have concept of active model
	// TODO: reduce allocations, reuse existing arrays when possible
	// TODO: make it possible to generate brushes in specific scenes/specific parents
	// TODO: clean up
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
	{
		// TODO: make this part of Undo stack ...
		public static CSGModel ActiveModel
		{
			get;
			set;
		}

		// TODO: improve naming
		public static CSGModel GetModelForNode(CSGModel overrideModel = null)
		{
			if (overrideModel)
			{
				BrushMeshAssetFactory.ActiveModel = overrideModel;
				return overrideModel;
			}

			var activeModel = BrushMeshAssetFactory.ActiveModel;
			if (!activeModel)
			{
				activeModel = BrushMeshAssetFactory.Create<CSGModel>("Model");
				BrushMeshAssetFactory.ActiveModel = activeModel;
			}
			return activeModel;
		}
	}
}