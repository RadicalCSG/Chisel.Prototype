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
    public sealed class ChiselSphereSettings : ScriptableObject, IChiselBoundsPlacementSettings<ChiselSphereDefinition>
    {
        public string   ToolName    => ChiselSphere.kNodeTypeName;
        public string   Group       => "Basic Primitives";

        public int      horizontalSegments      = ChiselSphereDefinition.kDefaultHorizontalSegments;
        public int      verticalSegments        = ChiselSphereDefinition.kDefaultVerticalSegments;
        
        [ToggleFlags]
        public PlacementFlags placement = Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.HeightEqualsXZ | Editors.PlacementFlags.GenerateFromCenterXZ |
                                            (ChiselSphereDefinition.kDefaultGenerateFromCenter ? Editors.PlacementFlags.GenerateFromCenterY : (PlacementFlags)0);
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselSphereDefinition definition) 
        {
            definition.verticalSegments     = verticalSegments;
            definition.horizontalSegments   = horizontalSegments;
            definition.generateFromCenter   = false;
        }

        public void OnUpdate(ref ChiselSphereDefinition definition, Bounds bounds)
        {
            definition.diameterXYZ = bounds.size;
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            // TODO: Make a RenderSphere method
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
