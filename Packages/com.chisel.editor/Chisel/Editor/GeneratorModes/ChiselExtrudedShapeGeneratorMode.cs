using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    [ChiselPlacementTool(name: "Free Draw", group: "Freeform")]
    public sealed class ChiselExtrudedShapePlacementTool : ScriptableObject, IChiselShapePlacementTool<ChiselExtrudedShapeDefinition>
    {
        public void OnCreate(ref ChiselExtrudedShapeDefinition definition, Curve2D shape)
        {
            definition.path     = new ChiselPath(ChiselPath.Default);
            definition.shape    = new Curve2D(shape);
        }

        public void OnUpdate(ref ChiselExtrudedShapeDefinition definition, float height)
        {
            definition.path.segments[1].position = ChiselPathPoint.kDefaultDirection * height;
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Curve2D shape, float height)
        {
            renderer.RenderShape(shape, height);
        }
    }
}
