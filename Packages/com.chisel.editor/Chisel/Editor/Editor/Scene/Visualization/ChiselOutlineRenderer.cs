using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Chisel.Editors
{
    public enum VisualizationMode
    {
        None = 0,
        Outline = 1,
        SimpleOutline = 2,
        Surface = 4
    }

    public sealed class ChiselOutlineRenderer : ScriptableObject
    {
        #region BrushOutline
        sealed class BrushOutline
        {
            public BrushOutline(Transform transform, CSGTreeBrush brush)
            {
                this.transform = transform;
                this.brush = brush;
            }
            public Transform    transform;
            public CSGTreeBrush brush;
            
            #region Comparison
            public static bool operator == (BrushOutline left, BrushOutline right) { return left.brush == right.brush; }
            public static bool operator != (BrushOutline left, BrushOutline right) { return left.brush != right.brush; }

            public override bool Equals(object obj) { if (!(obj is BrushOutline)) return false; var type = (BrushOutline)obj; return brush == type.brush; }
            public override int GetHashCode() { return brush.GetHashCode() ; }		
            #endregion
        }
        #endregion
        
        #region SurfaceOutline
        sealed class SurfaceOutline
        {
            public SurfaceOutline(Transform transform, SurfaceReference surface)
            {
                this.transform = transform;
                this.surface = surface;
            }
            public Transform		transform;
            public SurfaceReference surface;
            
            #region Comparison
            public static bool operator == (SurfaceOutline left, SurfaceOutline right) { return left.surface == right.surface; }
            public static bool operator != (SurfaceOutline left, SurfaceOutline right) { return left.surface != right.surface; }

            public override bool Equals(object obj) { if (!(obj is SurfaceOutline)) return false; var type = (SurfaceOutline)obj; return surface == type.surface; }
            public override int GetHashCode() { return surface.GetHashCode() ; }		
            #endregion
        }
        #endregion

        #region Instance
        static ChiselOutlineRenderer _instance;
        public static ChiselOutlineRenderer Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                var foundInstances = UnityEngine.Object.FindObjectsOfType<ChiselOutlineRenderer>();
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<ChiselOutlineRenderer>();					
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    return _instance;
                }

                _instance = foundInstances[0];
                return _instance;
            }
        }
        #endregion

        ChiselRenderer	brushOutlineRenderer;
        ChiselRenderer	surfaceOutlineRenderer;

        // NOTE: handle-renderers often take the orientation of the camera into account (for example: backfaced surfaces) so they need to be camera specific
        Dictionary<Camera, ChiselRenderer>	handleRenderers = new Dictionary<Camera, ChiselRenderer>();
        
        readonly Dictionary<SurfaceOutline, ChiselWireframe>	surfaceOutlines		= new Dictionary<SurfaceOutline, ChiselWireframe>();
        readonly Dictionary<SurfaceOutline, ChiselWireframe>	surfaceOutlineFixes	= new Dictionary<SurfaceOutline, ChiselWireframe>();
        readonly HashSet<SurfaceOutline>	foundSurfaceOutlines	= new HashSet<SurfaceOutline>();
        readonly HashSet<SurfaceOutline>	removedSurfaces			= new HashSet<SurfaceOutline>();

        readonly Dictionary<BrushOutline, ChiselWireframe>		brushOutlines		= new Dictionary<BrushOutline, ChiselWireframe>();
        readonly Dictionary<BrushOutline, ChiselWireframe>		brushOutlineFixes	= new Dictionary<BrushOutline, ChiselWireframe>();
        readonly HashSet<CSGTreeBrush>		brushDirectlySelected	= new HashSet<CSGTreeBrush>();
        readonly HashSet<CSGTreeBrush>		foundTreeBrushes		= new HashSet<CSGTreeBrush>();
        readonly HashSet<BrushOutline>		foundBrushOutlines		= new HashSet<BrushOutline>();
        readonly HashSet<BrushOutline>		removedBrushes			= new HashSet<BrushOutline>();

        static bool updateBrushSelection	= false;
        static bool updateBrushWireframe	= false;
        static bool updateBrushLineCache	= false;

        static bool updateSurfaceSelection	= false;
        static bool updateSurfaceWireframe	= false;
        static bool updateSurfaceLineCache	= false;

        static VisualizationMode visualizationMode = VisualizationMode.Outline;
        public static VisualizationMode VisualizationMode
        {
            get { return visualizationMode; }
            set
            {
                visualizationMode = value;
                updateBrushWireframe	= true;
                updateSurfaceWireframe	= true;
            }
        }


        void OnEnable()
        {
            brushOutlineRenderer	= new ChiselRenderer();
            surfaceOutlineRenderer	= new ChiselRenderer();
            handleRenderers.Clear();
        }

        void OnDisable()
        {
            foreach(var item in handleRenderers)
                item.Value.Destroy();
            handleRenderers.Clear();
            brushOutlineRenderer	.Destroy();
            surfaceOutlineRenderer	.Destroy();

            brushOutlines	.Clear();
            surfaceOutlines	.Clear();
        }

        internal void OnReset()
        {
            Reset();
        }

        internal void OnEditModeChanged(IChiselToolMode prevEditMode, IChiselToolMode newEditMode)
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            updateBrushSelection = true;
            updateSurfaceSelection = true;
        }

        internal void OnSyncedBrushesChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            updateBrushSelection = true;
            updateSurfaceSelection = true;
        }

        internal void OnSelectionChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            updateBrushSelection = true;
            updateSurfaceSelection = true;
        }

        internal void OnSurfaceSelectionChanged()
        {
            // Defer since we could potentially get several events before we actually render
            updateSurfaceSelection = true;
        }
        
        internal void OnSurfaceHoverChanged()
        {
            // Defer since we could potentially get several events before we actually render
            updateSurfaceSelection = true;
        }
        

        internal void OnGeneratedMeshesChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            updateBrushWireframe = true;
            updateSurfaceWireframe = true;
        }

        internal void OnTransformationChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            updateBrushLineCache = true;
            updateSurfaceLineCache = true;
        }


        public void Reset()
        {
            surfaceOutlineFixes.Clear();
            surfaceOutlines.Clear();
            foundSurfaceOutlines.Clear();
            removedSurfaces.Clear();

            brushOutlines.Clear();
            brushOutlineFixes.Clear();
            foundTreeBrushes.Clear();
            foundBrushOutlines.Clear();
            removedBrushes.Clear();
            
            updateBrushSelection = true;
            updateBrushWireframe = false;
            updateBrushLineCache = false;

            updateSurfaceSelection = true;
            updateSurfaceWireframe = false;
            updateSurfaceLineCache = false;
        }

        void UpdateBrushSelection()
        {
            brushDirectlySelected.Clear();
            var objects = Selection.objects;
            if (objects.Length > 0)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    var obj = objects[i];
                    ChiselNode[] nodes = null;
                    var gameObject = obj as GameObject;
                    if (!Equals(null, gameObject))
                    {
                        nodes = gameObject.GetComponentsInChildren<ChiselNode>();
                    } else
                    {
                        var behaviour = obj as Behaviour;
                        if (!Equals(null, behaviour))
                        {
                            nodes = behaviour.GetComponents<ChiselNode>();
                        }
                    }

                    if (nodes != null &&
                        nodes.Length > 0)
                    {
                        for (int n = 0; n < nodes.Length; n++)
                        {
                            var node = nodes[n];
                            foundTreeBrushes.Clear();
                            node.GetAllTreeBrushes(foundTreeBrushes, ignoreSynchronizedBrushes: true);
                            if (foundTreeBrushes.Count > 0)
                            {
                                var directSelected = (// if component is directly select
                                                      (gameObject == null) ||
                                                      // or when the component is part of the selected gameObject
                                                      (gameObject == node.gameObject)) &&
                                                      // if we find CSGTreeBrushes directly on this node, but this node
                                                      // can also have child nodes, then we assume the CSGTreeBrushes are generated
                                                      // and we don't want to show those as directly selected
                                                      !node.CanHaveChildNodes;
                                var transform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(node.hierarchyItem.Transform);
                                foreach (var treeBrush in foundTreeBrushes)
                                {
                                    if (directSelected)
                                        brushDirectlySelected.Add(treeBrush);
                                    var outline = new BrushOutline(transform, treeBrush);
                                    foundBrushOutlines.Add(outline);
                                }
                            }
                        }
                    }
                }
            }

            if (foundTreeBrushes.Count == 0)
            {
                brushOutlines.Clear();
                brushOutlineRenderer.Clear();
            } else
            {
                foreach (var outline in brushOutlines.Keys)
                {
                    if (!foundBrushOutlines.Contains(outline) ||
                        !outline.brush.Valid ||
                        outline.brush.BrushMesh == BrushMeshInstance.InvalidInstance)
                        removedBrushes.Add(outline);
                }
                
                if (removedBrushes.Count > 0)
                {
                    foreach(var outline in removedBrushes)
                        brushOutlines.Remove(outline);
                }
                removedBrushes.Clear();
                
                foreach (var outline in foundBrushOutlines)
                {
                    if (!outline.brush.Valid ||
                        outline.brush.BrushMesh == BrushMeshInstance.InvalidInstance)
                        continue;
                    
                    var wireframe = ChiselWireframe.CreateWireframe(outline.brush);
                    brushOutlines[outline] = wireframe;
                }
            }
            
            foundBrushOutlines.Clear();
            updateBrushWireframe = true;
        }
        
        void UpdateSurfaceSelection()
        {	
            surfaceOutlines.Clear();
            var selection	= ChiselSurfaceSelectionManager.Selection;
            var hovered		= ChiselSurfaceSelectionManager.Hovered;
            if (selection.Count == 0 &&
                hovered.Count == 0)
            {
                surfaceOutlines.Clear();
                surfaceOutlineRenderer.Clear();
            } else
            {
                var allSurfaces = new HashSet<SurfaceReference>(selection);
                allSurfaces.AddRange(hovered);
                foreach (var outline in surfaceOutlines.Keys)
                {
                    var surface = outline.surface;
                    if (!allSurfaces.Contains(surface) ||
                        !surface.TreeBrush.Valid ||
                        surface.TreeBrush.BrushMesh == BrushMeshInstance.InvalidInstance)
                    {
                        removedSurfaces.Add(outline);
                    } else
                        allSurfaces.Remove(surface);
                }
                
                if (removedSurfaces.Count > 0)
                {
                    foreach(var outline in removedSurfaces)
                        surfaceOutlines.Remove(outline);
                }
                removedSurfaces.Clear();
                
                foreach (var surface in allSurfaces)
                {
                    var transform	= ChiselNodeHierarchyManager.FindModelTransformOfTransform(surface.node.hierarchyItem.Transform);
                    var outline		= new SurfaceOutline(transform, surface);
                    foundSurfaceOutlines.Add(outline);
                }
                
                foreach (var outline in foundSurfaceOutlines)
                {
                    if (!outline.surface.TreeBrush.Valid ||
                        outline.surface.TreeBrush.BrushMesh == BrushMeshInstance.InvalidInstance)
                        continue;
                    
                    var wireframe = ChiselWireframe.CreateWireframe(outline.surface.TreeBrush, outline.surface.surfaceID);
                    surfaceOutlines[outline] = wireframe;
                }
            }
            foundSurfaceOutlines.Clear();
            updateSurfaceWireframe = true;
        }

        static void CleanUpHandleRenderers()
        {
            var handleRenderers = Instance.handleRenderers;

            bool haveInvalidCameras = false;
            foreach (var camera in handleRenderers.Keys)
            {
                if (camera)
                    continue;
                haveInvalidCameras = true;
                break;
            }

            if (!haveInvalidCameras)
                return;

            var allCameras = handleRenderers.Keys.ToArray();
            foreach (var camera in allCameras)
            {
                if (camera)
                    continue;
                handleRenderers[camera].Destroy();
                handleRenderers.Remove(camera);
            }
        }

        static ChiselRenderer GetHandleRenderer(Camera camera)
        {
            var handleRenderers = Instance.handleRenderers;
            ChiselRenderer renderer;
            if (handleRenderers.TryGetValue(camera, out renderer))
                return renderer;

            CleanUpHandleRenderers();

            renderer = new ChiselRenderer();
            handleRenderers[camera] = renderer;
            return renderer;
        }
        
        static ChiselRenderer HandleRenderer { get { return GetHandleRenderer(Camera.current); } }

        public static void DrawLine(Matrix4x4 transformation, Vector3 from, Vector3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(transformation, from, to, color, lineMode, thickness, dashSize); }
        public static void DrawLine(Matrix4x4 transformation, Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(transformation, from, to, lineMode, thickness, dashSize); }
        public static void DrawLine(Vector3 from, Vector3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(from, to, color, lineMode, thickness, dashSize); }
        public static void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(from, to, lineMode, thickness, dashSize); }


        public static void DrawContinuousLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(transformation, points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(transformation, points, startIndex, length, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(points, startIndex, length, lineMode, thickness, dashSize); }

        public static void DrawLineLoop(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, startIndex, length, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(points, startIndex, length, lineMode, thickness, dashSize); }
        
        public static void DrawPolygon(Matrix4x4 transformation, Vector3[] points, int[] indices, Color color) { HandleRenderer.DrawPolygon(transformation, points, indices, color); }
        public static void DrawPolygon(Matrix4x4 transformation, Vector3[] points, Color color) { HandleRenderer.DrawPolygon(transformation, points, color); }
        public static void DrawPolygon(Matrix4x4 transformation, List<Vector3> points, Color color) { HandleRenderer.DrawPolygon(transformation, points, color); }

        void UpdateBrushWireframe()
        {
            foreach (var pair in brushOutlines)
            {
                var wireframe = pair.Value;
                var outline = pair.Key;
                var brush = outline.brush;
                if (wireframe == null)
                {
                    if (brush.Valid &&
                        brush.BrushMesh != BrushMeshInstance.InvalidInstance)
                        brushOutlineFixes[outline] = ChiselWireframe.CreateWireframe(brush);
                    else
                        brushOutlineFixes[outline] = null;
                    continue;
                } else
                {
                    if (!brush.Valid ||
                        brush.BrushMesh == BrushMeshInstance.InvalidInstance)
                    {
                        brushOutlineFixes[outline] = null;
                        continue;
                    }
                }
                
                if (!wireframe.Dirty)
                    continue;
                
                wireframe.UpdateWireframe();
            }
            foreach (var pair in brushOutlineFixes)
            {
                brushOutlines[pair.Key] = pair.Value;
            }
            brushOutlineFixes.Clear();
            updateBrushLineCache = true;
        }
        
        void UpdateSurfaceWireframe()
        {
            foreach (var pair in surfaceOutlines)
            {
                var wireframe	= pair.Value;
                var outline		= pair.Key;
                var surface		= outline.surface;
                var treeBrush	= surface.TreeBrush;
                if (wireframe == null)
                {
                    if (treeBrush.Valid &&
                        treeBrush.BrushMesh != BrushMeshInstance.InvalidInstance)
                        surfaceOutlineFixes[outline] = ChiselWireframe.CreateWireframe(treeBrush, surface.surfaceID);
                    else
                        surfaceOutlineFixes[outline] = null;
                    continue;
                } else
                {
                    if (!treeBrush.Valid ||
                        treeBrush.BrushMesh == BrushMeshInstance.InvalidInstance)
                    {
                        surfaceOutlineFixes[outline] = null;
                        continue;
                    }
                }
                
                if (!wireframe.Dirty)
                    continue;
                
                wireframe.UpdateWireframe();
            }
            foreach (var pair in surfaceOutlineFixes)
            {
                surfaceOutlines[pair.Key] = pair.Value;
            }
            surfaceOutlineFixes.Clear();
            updateSurfaceLineCache = true;
        }

        void UpdateBrushState()
        {
            if (updateBrushSelection)
            {
                updateBrushSelection = false;
                UpdateBrushSelection();
                updateBrushWireframe = true;
            }
            if (updateBrushWireframe)
            {
                updateBrushWireframe = false;
                UpdateBrushWireframe();
                updateBrushLineCache = true;
            }
            if (updateBrushLineCache)
            {
                updateBrushLineCache = false;
                brushOutlineRenderer.Begin();

                foreach (var pair in brushOutlines)
                {
                    var wireframe = pair.Value;
                    if (wireframe == null)
                        continue;

                    var outline = pair.Key;
                    if (!outline.brush.Valid)
                        continue;

                    // TODO: simplify this
                    var wireframeValue	= pair.Value;
                    var modelTransform	= outline.transform;
                    //var brushes		= outline.brush.AllSynchronizedVariants;
                    //var anySelected	= ChiselSyncSelection.IsAnyBrushVariantSelected(brushes);

                    //foreach (var brush in brushes)
                    var brush = outline.brush;
                    var anySelected = ChiselSyncSelection.IsBrushVariantSelected(brush);
                    {
                        Matrix4x4 transformation;
                        if (modelTransform)
                            transformation = modelTransform.localToWorldMatrix * brush.NodeToTreeSpaceMatrix;
                        else
                            transformation = brush.NodeToTreeSpaceMatrix;

                        if ((VisualizationMode & VisualizationMode.Outline) == VisualizationMode.Outline)
                        {
                            var directSelect = !ChiselEditModeManager.EditMode.ShowCompleteOutline &&
                                               ((brush == outline.brush && !anySelected) || (anySelected && ChiselSyncSelection.IsBrushVariantSelected(brush)));

                            // TODO: tweak look of selection, figure out how to do backfaced lighting of edges, for clarity
                            // TODO: support selecting surfaces/edges/points (without showing the entire object selection)
                            if (directSelect)
                                brushOutlineRenderer.DrawOutlines(transformation, wireframeValue, ColorManager.kSelectedOutlineColor, thickness: 3.0f, onlyInnerLines: false);
                            else
                                brushOutlineRenderer.DrawOutlines(transformation, wireframeValue, ColorManager.kUnselectedOutlineColor, thickness: 1.0f, onlyInnerLines: false);// (ChiselEditModeManager.EditMode == CSGEditMode.ShapeEdit));
                        }
                            
                        if ((VisualizationMode & VisualizationMode.SimpleOutline) == VisualizationMode.SimpleOutline)
                        {
                            brushOutlineRenderer.DrawSimpleOutlines(transformation, wireframeValue, ColorManager.kUnselectedOutlineColor);
                        }
                    }
                }
                brushOutlineRenderer.End();
            }
        }

        void UpdateSurfaceState()
        {
            if (updateSurfaceSelection)
            {
                updateSurfaceSelection = false;
                UpdateSurfaceSelection();
                updateSurfaceWireframe = true;
            }
            if (updateSurfaceWireframe)
            {
                updateSurfaceWireframe = false;
                UpdateSurfaceWireframe();
                updateSurfaceLineCache = true;
            }
            if (updateSurfaceLineCache)
            {
                var selection	= ChiselSurfaceSelectionManager.Selection;
                var hovered		= ChiselSurfaceSelectionManager.Hovered;
                updateSurfaceLineCache = false;
                surfaceOutlineRenderer.Begin();
                foreach (var pair in surfaceOutlines)
                {
                    var wireframe = pair.Value;
                    if (wireframe == null)
                        continue;

                    var outline			= pair.Key;
                    var surface			= outline.surface;
                    if (hovered.Contains(surface))
                        continue;

                    var brush			= surface.TreeBrush;
                    if (!brush.Valid)
                        continue;
                        
                    var modelTransform	= outline.transform;

                    Matrix4x4 transformation;
                    if (modelTransform)
                        transformation = modelTransform.localToWorldMatrix * brush.NodeToTreeSpaceMatrix;
                    else
                        transformation = brush.NodeToTreeSpaceMatrix;

                    if (selection.Contains(surface))
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.kSelectedOutlineColor, thickness: 3);
                }
                foreach (var pair in surfaceOutlines)
                {
                    var wireframe = pair.Value;
                    if (wireframe == null)
                        continue;

                    var outline			= pair.Key;
                    var surface			= outline.surface;
                    if (!hovered.Contains(surface))
                        continue;

                    var brush			= surface.TreeBrush;
                    if (!brush.Valid)
                        continue;
                        
                    var modelTransform	= outline.transform;

                    Matrix4x4 transformation;
                    if (modelTransform)
                        transformation = modelTransform.localToWorldMatrix * brush.NodeToTreeSpaceMatrix;
                    else
                        transformation = brush.NodeToTreeSpaceMatrix;

                    if (selection.Contains(surface))
                    {
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.kSelectedHoverOutlineColor, thickness: 3);
                    } else
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.kPreSelectedOutlineColor, thickness: 3);
                }
                surfaceOutlineRenderer.End();
            }
        }

        int prevFocus = 0;

        public void OnSceneGUI(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var camera = sceneView.camera;

            // defer surface updates when it's not currently visible
            if ((VisualizationMode & (VisualizationMode.Outline | VisualizationMode.SimpleOutline)) != VisualizationMode.None)
            {
                UpdateBrushState();
                brushOutlineRenderer.RenderAll(camera);
            }

            if ((VisualizationMode & VisualizationMode.Surface) == VisualizationMode.Surface)
            {
                UpdateSurfaceState();
                surfaceOutlineRenderer.RenderAll(camera);
            }

            var handleRenderer = GetHandleRenderer(camera);
            handleRenderer.End();
            handleRenderer.RenderAll(camera);
            handleRenderer.Begin();
            
            var focus = UnitySceneExtensions.SceneHandleUtility.focusControl;
            if (prevFocus != focus)
            {
                prevFocus = focus;
                SceneView.RepaintAll();
            }
        }
    }
}
