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
    public sealed class ChiselCylinderSettings : ScriptableObject, IChiselBoundsPlacementSettings<ChiselCylinderDefinition>
    {
        public string   ToolName    => ChiselCylinder.kNodeTypeName;
        public string   Group       => "Basic Primitives";

        public CylinderShapeType    cylinderType		 = CylinderShapeType.Cylinder;
        public int				    sides			     = 16;
        

        [ToggleFlags(includeFlags: (int)(Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.GenerateFromCenterY | Editors.PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.GenerateFromCenterXZ;
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselCylinderDefinition definition) 
        {
            definition.isEllipsoid		= (placement & Editors.PlacementFlags.SameLengthXZ) != Editors.PlacementFlags.SameLengthXZ;
            definition.type				= cylinderType;
            definition.sides			= sides;
        }

        public void OnUpdate(ref ChiselCylinderDefinition definition, Bounds bounds)
        {
            var height = bounds.size[(int)Axis.Y];
            definition.BottomDiameterX  = bounds.size[(int)Axis.X];
            definition.top.height       = height;
            definition.BottomDiameterZ  = bounds.size[(int)Axis.Z];
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
