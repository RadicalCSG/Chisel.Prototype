using System;
using System.Linq;
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
    public sealed partial class ChiselModelManager
    {
        // TODO: make this part of Undo stack ...
        public static ChiselModel ActiveModel
        {
            get;
            set;
        }

        // TODO: improve naming
        public static ChiselModel GetModelForNode(ChiselModel overrideModel = null)
        {
            if (overrideModel)
            {
                ChiselModelManager.ActiveModel = overrideModel;
                return overrideModel;
            }

            var activeModel = ChiselModelManager.ActiveModel;
            if (!activeModel)
            {
                activeModel = ChiselModelManager.Create<ChiselModel>("Model");
                ChiselModelManager.ActiveModel = activeModel;
            }
            return activeModel;
        }
    }
}