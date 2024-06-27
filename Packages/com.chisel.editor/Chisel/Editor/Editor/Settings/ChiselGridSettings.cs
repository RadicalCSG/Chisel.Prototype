using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public static class ChiselGridSettings
    {
        static ReflectedInstanceProperty<object>    sceneViewGridsProperty  = typeof(SceneView).GetProperty<object>("sceneViewGrids");
        static ReflectedInstanceProperty<float>     gridOpacityProperty     = ReflectionExtensions.GetProperty<float>("UnityEditor.SceneViewGrid", "gridOpacity");
        static ReflectedInstanceProperty<int>       gridAxisProperty        = ReflectionExtensions.GetProperty<int>("UnityEditor.SceneViewGrid", "gridAxis");

        public static readonly ReflectedProperty<Vector3> Size              = ReflectionExtensions.GetStaticProperty<Vector3>("UnityEditor.GridSettings", "size");

        
        internal static void GridOnSceneGUI(SceneView sceneView)
        {
            if (sceneView.showGrid)
            {
                sceneView.showGrid = false;
                ChiselEditorSettings.Load();
                ChiselEditorSettings.ShowGrid = true;
                ChiselEditorSettings.Save();
            }

            var sceneViewGrids = sceneViewGridsProperty?.GetValue(sceneView);
            GridRenderer.Opacity = gridOpacityProperty?.GetValue(sceneViewGrids) ?? 1.0f;

            var activeTransform = Selection.activeTransform;

            if (Tools.pivotRotation == PivotRotation.Local && activeTransform)
            {
                var rotation = Tools.handleRotation;
                var center = (activeTransform && activeTransform.parent) ? activeTransform.parent.position : Vector3.zero;

                UnitySceneExtensions.Grid.defaultGrid.GridToWorldSpace = Matrix4x4.TRS(center, rotation, Vector3.one);
            } else
            {
                var gridAxis = gridAxisProperty?.GetValue(sceneViewGrids) ?? 1;
                switch (gridAxis)
                {
                    case 0: UnitySceneExtensions.Grid.defaultGrid.GridToWorldSpace = UnitySceneExtensions.Grid.XYPlane; break;
                    case 1: UnitySceneExtensions.Grid.defaultGrid.GridToWorldSpace = UnitySceneExtensions.Grid.XZPlane; break;
                    case 2: UnitySceneExtensions.Grid.defaultGrid.GridToWorldSpace = UnitySceneExtensions.Grid.YZPlane; break;
                }
            }

            if (Event.current.type != EventType.Repaint)
                return;


            if (ChiselEditorSettings.ShowGrid)
            {
                var grid = UnitySceneExtensions.Grid.HoverGrid;
                if (grid != null)
                {
                    grid.Spacing = UnitySceneExtensions.Grid.defaultGrid.Spacing;
                }
                else
                {
                    grid = UnitySceneExtensions.Grid.ActiveGrid;
                }
                grid.Render(sceneView);
            }

            if (UnitySceneExtensions.Grid.debugGrid != null)
            {
                //static ReflectedInstanceProperty<object> sceneViewGrids = typeof(SceneView).GetProperty<object>("sceneViewGrids");
                //static ReflectedInstanceProperty<float> gridOpacity = ReflectionExtensions.GetProperty<float>("SceneViewGrid", "gridOpacity");
                UnitySceneExtensions.Grid.debugGrid.Render(sceneView);
            }
        }
    }
}
