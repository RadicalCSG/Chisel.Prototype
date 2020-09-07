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
    public sealed class ChiselHemisphereSettings : ScriptableObject, IChiselBoundsPlacementSettings<ChiselHemisphereDefinition>
    {
        public string   ToolName    => ChiselHemisphere.kNodeTypeName;
        public string   Group       => "Basic Primitives";

        public int      horizontalSegments      = ChiselHemisphereDefinition.kDefaultHorizontalSegments;
        public int      verticalSegments        = ChiselHemisphereDefinition.kDefaultVerticalSegments;
        

        [ToggleFlags(includeFlags: (int)(Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.HeightEqualsHalfXZ | Editors.PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.HeightEqualsHalfXZ | Editors.PlacementFlags.GenerateFromCenterXZ;
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselHemisphereDefinition definition) 
        {
            definition.verticalSegments     = verticalSegments;
            definition.horizontalSegments   = horizontalSegments;
        }

        public void OnUpdate(ref ChiselHemisphereDefinition definition, Bounds bounds)
        {
            definition.diameterXYZ = bounds.size;
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
