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
    [ChiselPlacementTool(name: ChiselCapsule.kNodeTypeName, group: "Basic Primitives")]
    public sealed class ChiselCapsulePlacementTool : ScriptableObject, IChiselBoundsPlacementTool<ChiselCapsuleDefinition>
    {
        public int      topSegments			 = ChiselCapsuleDefinition.kDefaultTopSegments;
        public int	    bottomSegments	     = ChiselCapsuleDefinition.kDefaultBottomSegments;
        public int      sides				 = ChiselCapsuleDefinition.kDefaultSides;
        

        [ToggleFlags(includeFlags: (int)(Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.GenerateFromCenterY | Editors.PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = Editors.PlacementFlags.SameLengthXZ | Editors.PlacementFlags.GenerateFromCenterXZ;        
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselCapsuleDefinition definition) 
        {
            definition.sides           = sides;
            definition.topSegments     = topSegments;
            definition.bottomSegments  = bottomSegments;
        }

        public void OnUpdate(ref ChiselCapsuleDefinition definition, Bounds bounds)
        {
            var height              = bounds.size[(int)Axis.Y];
            var hemisphereHeight    = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsuleDefinition.kDefaultHemisphereRatio;

            definition.topHeight    = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);
            definition.bottomHeight = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);

            definition.diameterX    = bounds.size[(int)Axis.X];
            definition.height       = height;
            definition.diameterZ    = bounds.size[(int)Axis.Z];
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            // TODO: render capsule here
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
