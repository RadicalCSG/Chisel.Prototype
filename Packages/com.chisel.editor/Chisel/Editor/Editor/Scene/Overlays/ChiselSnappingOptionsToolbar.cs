using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;
using System.Reflection;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    abstract class ChiselToggle : EditorToolbarToggle, IEventHandler
    {
        public ChiselToggle(string iconName, string tooltipName)
        {
            ChiselToolbarUtility.SetupToolbarElement(this, iconName, tooltipName);
            UpdateEnabledState();
        }
        
        protected abstract void SetValue(bool newValue);

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            SetValue(newValue);
        }

        protected abstract void UpdateEnabledState();
    }

    abstract class ChiselSnappingToggle : ChiselToggle
    {
        readonly SnapSettings settingsFlag;
        public ChiselSnappingToggle(string iconName, string tooltipName, SnapSettings flags)
            : base(iconName, tooltipName)
        {
            settingsFlag = flags;
            ToolManager.activeToolChanged -= OnToolModeChanged;
            ToolManager.activeToolChanged += OnToolModeChanged;
            ChiselEditToolBase.SnapSettingChanged -= OnSnapSettingChanged;
            ChiselEditToolBase.SnapSettingChanged += OnSnapSettingChanged;
        }

        private void OnToolModeChanged() { this.UpdateEnabledState(); }
        private void OnSnapSettingChanged() { this.UpdateEnabledState(); }

        protected override void UpdateEnabledState()
        {
            bool enabled = ChiselSnappingToggleUtility.IsSnappingModeEnabled(settingsFlag);
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class BoundsSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.BoundsSnappingEnabled);
        public const string kTooltip    = "Snap bounds against grid";
        public const string kIcon       = "BoundsSnap";

        public BoundsSnappingToggle() : base(kIcon, kTooltip, SnapSettings.GeometryBoundsToGrid) { this.value = Snapping.BoundsSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.BoundsSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class PivotSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.PivotSnappingEnabled);
        public const string kTooltip    = "Snap pivots against grid";
        public const string kIcon       = "PivotSnap";

        public PivotSnappingToggle() : base(kIcon, kTooltip, SnapSettings.GeometryPivotToGrid) { this.value = Snapping.PivotSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.PivotSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class EdgeSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.EdgeSnappingEnabled);
        public const string kTooltip    = "Snap against edges";
        public const string kIcon       = "EdgeSnap";

        public EdgeSnappingToggle() : base(kIcon, kTooltip, SnapSettings.GeometryEdge) { this.value = Snapping.EdgeSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.EdgeSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class VertexSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.VertexSnappingEnabled);
        public const string kTooltip    = "Snap against vertices";
        public const string kIcon       = "VertexSnap";

        public VertexSnappingToggle() : base(kIcon, kTooltip, SnapSettings.GeometryVertex) { this.value = Snapping.VertexSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.VertexSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class SurfaceSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.SurfaceSnappingEnabled);
        public const string kTooltip    = "Snap against surfaces";
        public const string kIcon       = "SurfaceSnap";

        public SurfaceSnappingToggle() : base(kIcon, kTooltip, SnapSettings.GeometrySurface) { this.value = Snapping.SurfaceSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.SurfaceSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class UVGridSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.UVGridSnappingEnabled);
        public const string kTooltip    = "Snap UV against grid";
        public const string kIcon       = "UVGridSnap";

        public UVGridSnappingToggle() : base(kIcon, kTooltip, SnapSettings.UVGeometryGrid) { this.value = Snapping.UVGridSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.UVGridSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class UVEdgeSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.UVEdgeSnappingEnabled);
        public const string kTooltip    = "Snap UV against surface edges";
        public const string kIcon       = "UVEdgeSnap";

        public UVEdgeSnappingToggle() : base(kIcon, kTooltip, SnapSettings.UVGeometryEdges) { this.value = Snapping.UVEdgeSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.UVEdgeSnappingEnabled = newValue; }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class UVVertexSnappingToggle : ChiselSnappingToggle
    {
        public const string id          = nameof(ChiselSnappingOptionsToolbar) + "/" + nameof(Snapping.UVVertexSnappingEnabled);
        public const string kTooltip    = "Snap UV against surface vertices";
        public const string kIcon       = "UVVertexSnap";

        public UVVertexSnappingToggle() : base(kIcon, kTooltip, SnapSettings.UVGeometryVertices) { this.value = Snapping.UVVertexSnappingEnabled; }
        protected override void SetValue(bool newValue) { Snapping.UVVertexSnappingEnabled = newValue; }
    }


    [Overlay(typeof(SceneView), "Chisel Snap Settings")]
    public class ChiselSnappingOptionsToolbar : ToolbarOverlay
    {
        ChiselSnappingOptionsToolbar() : base( 
            BoundsSnappingToggle  .id, 
            PivotSnappingToggle   .id,
            EdgeSnappingToggle    .id,
            VertexSnappingToggle  .id,
            SurfaceSnappingToggle .id,
            UVGridSnappingToggle  .id,
            UVEdgeSnappingToggle  .id,
            UVVertexSnappingToggle.id)
        {
        }
    }
}
