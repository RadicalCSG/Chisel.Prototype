using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.EditorTools;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using Grid = UnitySceneExtensions.Grid;
using UnityEngine.Pool;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    //[EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    class ChiselMovePivotTool : ChiselEditToolBase
    {
        const string kToolName = "Move Pivot";
        public override string ToolName => kToolName;

        #region Activation
        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselMovePivotTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToPivotEditMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselMovePivotTool>(); }
        #endregion

        public override void OnActivate()
        {
            base.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
        }
        #endregion

        
        public override SnapSettings ToolUsedSnappingModes { get { return UnitySceneExtensions.SnapSettings.AllGeometry & ~SnapSettings.GeometryBoundsToGrid; } }


        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.KeyDown:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (GUIUtility.hotControl == 0)
                        {
                            evt.Use();
                            break;
                        }
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (GUIUtility.hotControl == 0)
                        {
                            Selection.activeTransform = null;
                            evt.Use();
                            GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                            break;
                        }
                    }
                    break;
                }
            }

            var position = Tools.handlePosition;
            var rotation = Tools.handleRotation;

            EditorGUI.BeginChangeCheck();
            var handleIDs = new SceneHandles.PositionHandleIDs();
            SceneHandles.Initialize(ref handleIDs);

            Snapping.FindCustomSnappingPointsRayMethod = FindCustomSnappingRayPoints;
            Snapping.FindCustomSnappingPointsMethod = FindCustomSnappingPoints;
            Snapping.CustomSnappedEvent = OnCustomSnappedEvent;
            var newPosition = SceneHandles.PositionHandle(ref handleIDs, position, rotation);
            Snapping.FindCustomSnappingPointsRayMethod = null;
            Snapping.FindCustomSnappingPointsMethod = null;
            Snapping.CustomSnappedEvent = null;
            if (EditorGUI.EndChangeCheck())
            {
                var nodes = ChiselSelectionManager.SelectedNodes;
                if (nodes == null || nodes.Count == 0)
                    return;
                PIvotUtility.MovePivotTo(nodes, newPosition);
            }

            if (Event.current.type == EventType.Repaint)
            {
                var handleSize = UnityEditor.HandleUtility.GetHandleSize(position);
                HandleRendering.DrawCameraAlignedCircle(position, handleSize * 0.1f, Color.white, Color.black);

                if ((handleIDs.combinedState & (ControlState.Focused | ControlState.Active)) != ControlState.None)
                {
                    var prevColor = Handles.color;
                    var selectedColor = UnityEditor.Handles.selectedColor;
                    selectedColor.a = 0.5f;
                    Handles.color = selectedColor;
                    
                    //if ((handleIDs.xAxisIndirectState & (ControlState.Focused | ControlState.Active)) != ControlState.None)
                    //    HandleRendering.DrawInfiniteLine(position, Axis.X);
                    //if ((handleIDs.yAxisIndirectState & (ControlState.Focused | ControlState.Active)) != ControlState.None)
                    //    HandleRendering.DrawInfiniteLine(position, Axis.Y);
                    //if ((handleIDs.zAxisIndirectState & (ControlState.Focused | ControlState.Active)) != ControlState.None)
                    //    HandleRendering.DrawInfiniteLine(position, Axis.Z);

                    DrawCustomSnappedEvent();

                    Handles.color = prevColor;
                }
            }
        }


        const float kEdgeDistanceEpsilon    = 0.01f;
        const float kVertexDistanceEpsilon  = 0.02f;
        const float kPlaneDistanceEpsilon   = 0.02f;

        enum ClosestLineResult
        {
            None,
            Aligned,
            Intersection
        }

        // TODO: put somewhere else
        static ClosestLineResult ClosestPointsBetweenTwoLines(Vector3 rayOriginA, Vector3 rayDirectionA, Vector3 lineB1, Vector3 lineB2, out Vector3 pointA, out Vector3 pointB)
        {
            var deltaB = lineB2 - lineB1;

            var a = Vector3.Dot(rayDirectionA, rayDirectionA);
            var e = Vector3.Dot(deltaB, deltaB);
            var b = Vector3.Dot(rayDirectionA, deltaB);
		    var d = a * e - b * b;
            if (Mathf.Abs(d) < kEdgeDistanceEpsilon)
            {
                pointA = Vector3.zero;
                pointB = Vector3.zero;
                return ClosestLineResult.Aligned;
            }

            var r = rayOriginA - lineB1;
            var c = Vector3.Dot(rayDirectionA, r);
            var f = Vector3.Dot(deltaB, r);
            var t = (a * f - c * b) / d;

            if (t < -kVertexDistanceEpsilon || t > 1.0f + kVertexDistanceEpsilon)
            {
                pointA = Vector3.zero;
                pointB = Vector3.zero;
                return ClosestLineResult.None;
            }

            var s = (b * f - c * e) / d;
 
			pointA = rayOriginA + rayDirectionA * s;
			pointB = lineB1 + deltaB * t; 
			return ClosestLineResult.Intersection;
	    }


        // TODO: put somewhere else
        static bool ClosestPointToLine(Vector3 point, Vector3 lineB1, Vector3 lineB2, out Vector3 pointB)
        {
            // TODO: optimize
            var deltaA = (lineB2 - lineB1).normalized;
            var deltaB = (lineB2 - point);
            var t = Vector3.Dot(deltaB, deltaA);
            pointB = lineB2 - deltaA * t;

            deltaA = (lineB2 - lineB1);
            deltaB = (lineB2 - pointB);
            var dot1 = Vector3.Dot(deltaA, deltaB);
            if (dot1 < -kVertexDistanceEpsilon) { return false; }

            var dot2 = Vector3.Dot(deltaB, deltaB);
            if (dot2 - dot1 > kVertexDistanceEpsilon) { return false; }

            return true;
        }


        // For each brush

        // 1D:
        // Find surfaces that intersect with ray
        // Find edges on that surface, if intersection is close enough to edge, find closest point on edge
        static void FindSnapPointsAlongRay(GameObject[] selection, Vector3 worldRayStart, Vector3 worldRayDirection, List<SurfaceSnap> allSurfaceSnapEvents, List<EdgeSnap> allEdgeSnapEvents, List<VertexSnap> allVertexSnapEvents)
        {
            if (selection == null || selection.Length == 0)
                return;

            if (allSurfaceSnapEvents == null &&
                allEdgeSnapEvents    == null &&
                allVertexSnapEvents  == null)
                return;

            s_FoundIntersections.Clear();
            if (ChiselSceneQuery.FindFirstWorldIntersection(s_FoundIntersections, worldRayStart - worldRayDirection, worldRayStart + worldRayDirection, filter: selection))
            {
                if (allSurfaceSnapEvents != null)
                {
                    for (int i = 0; i < s_FoundIntersections.Count; i++)
                    {
                        var intersection = s_FoundIntersections[i];
                        allSurfaceSnapEvents.Add(new SurfaceSnap 
                        { 
                            brush           = intersection.brushIntersection.brush,
                            surfaceIndex    = intersection.brushIntersection.surfaceIndex,
                            intersection    = intersection.worldPlaneIntersection,
                            normal          = intersection.worldPlane.normal,
                        });
                    }
                }
            }

            if (allEdgeSnapEvents == null && allVertexSnapEvents == null)
                return;

            foreach (var intersection in s_FoundIntersections)
            {
                var csgBrush        = intersection.brushIntersection.brush;
                var csgTree         = intersection.brushIntersection.tree;
                var brushMeshBlob   = BrushMeshManager.GetBrushMeshBlob(csgBrush.BrushMesh);
                if (!brushMeshBlob.IsCreated)
                    continue;

                ref var brushMesh               = ref brushMeshBlob.Value;
                ref var polygons                = ref brushMesh.polygons;
                ref var halfEdges               = ref brushMesh.halfEdges;
                ref var vertices                = ref brushMesh.localVertices;
                ref var halfEdgePolygonIndices  = ref brushMesh.halfEdgePolygonIndices;

                var model           = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(csgTree.UserID) as ChiselModel;
                var worldToNode     = (Matrix4x4)csgBrush.TreeToNodeSpaceMatrix * model.hierarchyItem.WorldToLocalMatrix;
                var nodeToWorld     = model.hierarchyItem.LocalToWorldMatrix * (Matrix4x4)csgBrush.NodeToTreeSpaceMatrix;
                
                var brushRayStart       = worldToNode.MultiplyPoint(worldRayStart);
                var brushRayDirection   = worldToNode.MultiplyVector(worldRayDirection).normalized;

                var surfaceIndex        = intersection.brushIntersection.surfaceIndex;
                    
                var polygon     = polygons[surfaceIndex];
                var firstEdge   = polygon.firstEdge;
                var lastEdge    = firstEdge + polygon.edgeCount;
                for (int e0 = lastEdge - 1, e1 = firstEdge; e1 < lastEdge; e0 = e1, e1++)
                {
                    var i0 = halfEdges[e0].vertexIndex;
                    var i1 = halfEdges[e1].vertexIndex;

                    var v0 = vertices[i0];
                    var v1 = vertices[i1];

                    var result = ClosestPointsBetweenTwoLines(brushRayStart, brushRayDirection, v0, v1, out Vector3 A, out Vector3 B);
                    if (result == ClosestLineResult.None)
                        continue;

                    if (result == ClosestLineResult.Aligned)
                    {
                        // TODO: draw edge as being intersecting if we're on the edge right now
                        continue;
                    }


                    var dist = (A - B).magnitude;
                    if (dist > kEdgeDistanceEpsilon)
                        continue;

                    if (allVertexSnapEvents != null)
                    { 
                        var vertDist = ((Vector3)v0 - B).magnitude;
                        if (vertDist < kVertexDistanceEpsilon)
                        {
                            allVertexSnapEvents.Add(new VertexSnap
                            {
                                brush        = csgBrush,
                                surfaceIndex = surfaceIndex,
                                vertexIndex  = i0,
                                intersection = nodeToWorld.MultiplyPoint(v0)
                            });
                        }
                        vertDist = ((Vector3)v1 - B).magnitude;
                        if (vertDist < kVertexDistanceEpsilon)
                        {
                            allVertexSnapEvents.Add(new VertexSnap
                            {
                                brush        = csgBrush,
                                surfaceIndex = surfaceIndex,
                                vertexIndex  = i1,
                                intersection = nodeToWorld.MultiplyPoint(v1)
                            });
                        }
                    }

                    if (allEdgeSnapEvents != null)
                    { 
                        allEdgeSnapEvents.Add(new EdgeSnap
                        {
                            brush           = csgBrush,
                            surfaceIndex0   = surfaceIndex,
                            surfaceIndex1   = halfEdgePolygonIndices[halfEdges[e1].twinIndex],
                            vertexIndex0    = i0,
                            vertexIndex1    = i1,
                            intersection    = nodeToWorld.MultiplyPoint(B),
                            from            = nodeToWorld.MultiplyPoint(v0),
                            to              = nodeToWorld.MultiplyPoint(v1)
                        });
                    }
                }
            }
        }


        // TODO: When grid snapping, make it only get points that go through grid lines

        // 2D:
        // Find edges that intersect with plane
        //      each surface will have 0, 1 or 2 intersections
        //          1 intersection  => use this edge to find closest vertex to pivot
        //          2 intersections => create virutal edge to and find closest vertex to pivot (surface vertex)
        static void FindClosestSnapPointsToPlane(GameObject[] selection, Vector3 startWorldPoint, Vector3 currentWorldPoint, Grid worldSlideGrid, float maxSnapDistance, List<SurfaceSnap> allSurfaceSnapEvents, List<EdgeSnap> allEdgeSnapEvents, List<VertexSnap> allVertexSnapEvents)
        {
            if (selection == null || selection.Length == 0)
                return;

            if (allSurfaceSnapEvents == null &&
                allEdgeSnapEvents    == null &&
                allVertexSnapEvents  == null)
                return;

            var worldSlidePlane = worldSlideGrid.PlaneXZ;

            var gridSnapping = Snapping.GridSnappingActive;
            if (gridSnapping)
            {
                var vectorX      = worldSlideGrid.Right * maxSnapDistance;
                var vectorZ      = worldSlideGrid.Forward * maxSnapDistance;

                var snappedWorldPoint = Snapping.SnapPoint(currentWorldPoint, worldSlideGrid);
                FindSnapPointsAlongRay(selection, snappedWorldPoint, vectorX, allSurfaceSnapEvents, allEdgeSnapEvents, allVertexSnapEvents);
                FindSnapPointsAlongRay(selection, snappedWorldPoint, vectorZ, allSurfaceSnapEvents, allEdgeSnapEvents, allVertexSnapEvents);

                snappedWorldPoint = Snapping.SnapPoint(startWorldPoint, worldSlideGrid);
                FindSnapPointsAlongRay(selection, snappedWorldPoint, vectorX, allSurfaceSnapEvents, allEdgeSnapEvents, allVertexSnapEvents);
                FindSnapPointsAlongRay(selection, snappedWorldPoint, vectorZ, allSurfaceSnapEvents, allEdgeSnapEvents, allVertexSnapEvents);
            }


            worldSlidePlane = new Plane(worldSlidePlane.normal, currentWorldPoint);

            var selectedBrushes = ListPool<CSGTreeBrush>.Get();
            var selectedNodes = ListPool<CSGTreeNode>.Get();
            try
            {
                foreach (var go in selection)
                {
                    if (!go)
                        continue;

                    var node = go.GetComponent<ChiselNode>();
                    if (!node)
                        continue;

                    selectedNodes.Clear();
                    ChiselNodeHierarchyManager.GetChildrenOfHierarchyItem(selectedNodes, node.hierarchyItem);
                    foreach (var child in selectedNodes)
                    {
                        if (!child.Valid || child.Type != CSGNodeType.Brush)
                            continue;
                        selectedBrushes.Add((CSGTreeBrush)child);
                    }
                }

                if (selectedBrushes.Count == 0)
                    return;

                var snapDistanceSqr = maxSnapDistance * maxSnapDistance;

                EdgeSnap[] foundEdges = new EdgeSnap[2];
                int foundEdgeCount;

                foreach (var csgBrush in selectedBrushes)
                {
                    var csgTree = csgBrush.Tree;
                    var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(csgBrush.BrushMesh);
                    if (!brushMeshBlob.IsCreated)
                        continue;

                    ref var brushMesh = ref brushMeshBlob.Value;
                    ref var polygons = ref brushMesh.polygons;
                    ref var halfEdges = ref brushMesh.halfEdges;
                    ref var vertices = ref brushMesh.localVertices;
                    ref var planes = ref brushMesh.localPlanes;
                    ref var halfEdgePolygonIndices = ref brushMesh.halfEdgePolygonIndices;

                    // TODO: store this information with brush 
                    var model = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(csgTree.UserID) as ChiselModel;
                    var worldToNode = (Matrix4x4)csgBrush.TreeToNodeSpaceMatrix * model.hierarchyItem.WorldToLocalMatrix;
                    var nodeToWorld = model.hierarchyItem.LocalToWorldMatrix * (Matrix4x4)csgBrush.NodeToTreeSpaceMatrix;

                    var brushPoint = worldToNode.MultiplyPoint(currentWorldPoint);
                    var brushPlane = worldToNode.TransformPlane(worldSlidePlane);

                    if (allVertexSnapEvents != null)
                    {
                        if (gridSnapping)
                        {
                            for (int i = 0; i < vertices.Length; i++)
                            {
                                var vertex = vertices[i];
                                var dist0 = brushPlane.GetDistanceToPoint(vertex);
                                if (math.abs(dist0) > snapDistanceSqr)
                                    continue;
                                allVertexSnapEvents.Add(new VertexSnap
                                {
                                    brush = csgBrush,
                                    vertexIndex = i,
                                    intersection = nodeToWorld.MultiplyPoint(vertex)
                                });
                            }
                        }
                        else
                        {
                            for (int i = 0; i < vertices.Length; i++)
                            {
                                var vertex = vertices[i];
                                if (math.lengthsq(vertex - (float3)brushPoint) > snapDistanceSqr)
                                    continue;
                                var dist0 = brushPlane.GetDistanceToPoint(vertex);
                                if (math.abs(dist0) > snapDistanceSqr)
                                    continue;
                                allVertexSnapEvents.Add(new VertexSnap
                                {
                                    brush = csgBrush,
                                    vertexIndex = i,
                                    intersection = nodeToWorld.MultiplyPoint(vertex)
                                });
                            }
                        }
                    }


                    if (allSurfaceSnapEvents == null &&
                        allEdgeSnapEvents == null)
                        continue;


                    for (int surfaceIndex = 0; surfaceIndex < polygons.Length; surfaceIndex++)
                    {
                        var polygon = polygons[surfaceIndex];
                        var firstEdge = polygon.firstEdge;
                        var lastEdge = firstEdge + polygon.edgeCount;

                        // TODO: If point is ON plane, ignore. We don't want to "snap" to every point on that surface b/c then we won't be snapping at all

                        foundEdgeCount = 0;
                        for (int e0 = lastEdge - 1, e1 = firstEdge; e1 < lastEdge; e0 = e1, e1++)
                        {
                            var i0 = halfEdges[e0].vertexIndex;
                            var i1 = halfEdges[e1].vertexIndex;

                            var vertex0 = vertices[i0];
                            var vertex1 = vertices[i1];

                            var distance0 = brushPlane.GetDistanceToPoint(vertex0);
                            var distance1 = brushPlane.GetDistanceToPoint(vertex1);

                            // Edge is plane aligned
                            if (math.abs(distance0) < kPlaneDistanceEpsilon &&
                                math.abs(distance1) < kPlaneDistanceEpsilon)
                            {
                                if (i0 < i1 && // skip duplicate edges
                                    allEdgeSnapEvents != null)
                                {
                                    if (gridSnapping)
                                    {
                                    }
                                    else
                                    {
                                        if (ClosestPointToLine(brushPoint, vertex0, vertex1, out Vector3 newVertex))
                                        {
                                            allEdgeSnapEvents.Add(new EdgeSnap
                                            {
                                                brush = csgBrush,
                                                surfaceIndex0 = surfaceIndex,
                                                surfaceIndex1 = halfEdgePolygonIndices[halfEdges[e1].twinIndex],
                                                vertexIndex0 = i0,
                                                vertexIndex1 = i1,
                                                intersection = nodeToWorld.MultiplyPoint(newVertex),
                                                from = nodeToWorld.MultiplyPoint(vertex0),
                                                to = nodeToWorld.MultiplyPoint(vertex1)
                                            });
                                        }
                                    }
                                }
                                continue;
                            }

                            {
                                if ((distance0 < -snapDistanceSqr && distance1 < -snapDistanceSqr) ||
                                    (distance0 > snapDistanceSqr && distance1 > snapDistanceSqr))
                                    continue;

                                // TODO: Find intersection between plane and edge
                                var vector = vertex0 - vertex1;
                                var length = distance0 - distance1;
                                var delta = distance0 / length;

                                if (float.IsNaN(delta) || float.IsInfinity(delta))
                                    continue;

                                var newVertex = (Vector3)(vertex0 - (vector * delta));
                                var distanceN = brushPlane.GetDistanceToPoint(newVertex);

                                if ((distanceN <= distance0 && distanceN <= distance1) ||
                                    (distanceN >= distance0 && distanceN >= distance1))
                                    continue;

                                if ((newVertex - brushPoint).sqrMagnitude > snapDistanceSqr)
                                    continue;

                                foundEdges[foundEdgeCount] = new EdgeSnap
                                {
                                    brush = csgBrush,
                                    surfaceIndex0 = surfaceIndex,
                                    surfaceIndex1 = halfEdgePolygonIndices[halfEdges[e1].twinIndex],
                                    vertexIndex0 = i0,
                                    vertexIndex1 = i1,
                                    intersection = nodeToWorld.MultiplyPoint(newVertex),
                                    from = nodeToWorld.MultiplyPoint(vertex0),
                                    to = nodeToWorld.MultiplyPoint(vertex1)
                                };
                                if (i0 < i1 && // skip duplicate edges
                                    allEdgeSnapEvents != null)
                                    allEdgeSnapEvents.Add(foundEdges[foundEdgeCount]);

                                foundEdgeCount++;
                                if (foundEdgeCount == 2)
                                    break;
                            }
                        }

                        if (allSurfaceSnapEvents != null && foundEdgeCount > 0 && !gridSnapping)
                        {
                            if (foundEdgeCount == 2)
                            {
                                var plane = planes[surfaceIndex];
                                var unityPlane = new Plane(plane.xyz, plane.w);

                                var vertex0 = foundEdges[0].intersection;
                                var vertex1 = foundEdges[1].intersection;

                                if (ClosestPointToLine(currentWorldPoint, vertex0, vertex1, out Vector3 closestWorldPoint))
                                {
                                    allSurfaceSnapEvents.Add(new SurfaceSnap
                                    {
                                        brush = csgBrush,
                                        surfaceIndex = surfaceIndex,
                                        intersection = closestWorldPoint,
                                        normal = nodeToWorld.MultiplyVector(unityPlane.normal),
                                    });
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                ListPool<CSGTreeBrush>.Release(selectedBrushes);
                ListPool<CSGTreeNode>.Release(selectedNodes);
            }
        }

        struct SurfaceSnap 
        { 
            public CSGTreeBrush brush;
            public int          surfaceIndex;
            public Vector3      intersection;
            public Vector3      normal;
        }

        struct EdgeSnap
        {
            public CSGTreeBrush brush;
            public int          surfaceIndex0;
            public int          surfaceIndex1;
            public int          vertexIndex0;
            public int          vertexIndex1;
            public Vector3      intersection;
            public Vector3      from;
            public Vector3      to;
        }

        struct VertexSnap
        {
            public CSGTreeBrush brush;
            public int          surfaceIndex;
            public int          vertexIndex;
            public Vector3      intersection;
        }

        class SnappingContext
        {
            public List<SurfaceSnap>           s_AllSurfaceSnapEvents  = new List<SurfaceSnap>();
            public List<EdgeSnap>              s_AllEdgeSnapEvents     = new List<EdgeSnap>();
            public List<VertexSnap>            s_AllVertexSnapEvents   = new List<VertexSnap>();
            
            public void Clear()
            {
                s_AllSurfaceSnapEvents.Clear();
                s_AllEdgeSnapEvents.Clear();
                s_AllVertexSnapEvents.Clear();
            }

            public void CollectAllSnapPoints(List<Vector3> worldSnapPoints)
            {
                foreach (var evt in s_AllVertexSnapEvents)
                    worldSnapPoints.Add(evt.intersection);
                
                foreach (var evt in s_AllEdgeSnapEvents)
                    worldSnapPoints.Add(evt.intersection);
                
                foreach (var evt in s_AllSurfaceSnapEvents)
                    worldSnapPoints.Add(evt.intersection);
            }
        }

        static readonly List<ChiselIntersection>            s_FoundIntersections    = new List<ChiselIntersection>();
        static readonly Dictionary<int, SnappingContext>    s_SnappingContext       = new Dictionary<int, SnappingContext>();
        static readonly List<SurfaceSnap>                   s_DrawSurfaceSnapEvents = new List<SurfaceSnap>();
        static readonly List<EdgeSnap>                      s_DrawEdgeSnapEvents    = new List<EdgeSnap>();
        static readonly List<VertexSnap>                    s_DrawVertexSnapEvents  = new List<VertexSnap>();
        

        const float kArbitrarySnapDistance = 5; // TODO: use fixed step when no grid snapping, otherwise grid step size

        static bool FindCustomSnappingRayPoints(Vector3 worldRayStart, Vector3 worldRayDirection, int contextIndex, List<Vector3> foundWorldspacePoints)
        {
            if (!s_SnappingContext.TryGetValue(contextIndex, out var snappingContext))
                s_SnappingContext[contextIndex] = snappingContext = new SnappingContext();
            
            snappingContext.Clear();
            s_DrawSurfaceSnapEvents.Clear();
            s_DrawEdgeSnapEvents.Clear();
            s_DrawVertexSnapEvents.Clear();
            
            var edgeSnappingActive      = Snapping.EdgeSnappingActive;
            var vertexSnappingActive    = Snapping.VertexSnappingActive;
            var surfaceSnappingActive   = Snapping.SurfaceSnappingActive;

            if (!edgeSnappingActive && !vertexSnappingActive && !surfaceSnappingActive)
                return false;

            var selection   = Selection.gameObjects;
            var step        = worldRayDirection * kArbitrarySnapDistance;
            FindSnapPointsAlongRay(selection, worldRayStart, step,
                                   surfaceSnappingActive ? snappingContext.s_AllSurfaceSnapEvents : null,
                                   edgeSnappingActive    ? snappingContext.s_AllEdgeSnapEvents    : null,
                                   vertexSnappingActive  ? snappingContext.s_AllVertexSnapEvents  : null);

            snappingContext.CollectAllSnapPoints(foundWorldspacePoints);
            return true;
        }

        static bool FindCustomSnappingPoints(Vector3 startWorldPoint, Vector3 currentWorldPoint, Grid worldSlideGrid, int contextIndex, List<Vector3> foundWorldspacePoints)
        {
            if (!s_SnappingContext.TryGetValue(contextIndex, out var snappingContext))
                s_SnappingContext[contextIndex] = snappingContext = new SnappingContext();
            
            snappingContext.Clear();
            s_DrawSurfaceSnapEvents.Clear();
            s_DrawEdgeSnapEvents.Clear();
            s_DrawVertexSnapEvents.Clear();

            var edgeSnappingActive      = Snapping.EdgeSnappingActive;
            var vertexSnappingActive    = Snapping.VertexSnappingActive;
            var surfaceSnappingActive   = Snapping.SurfaceSnappingActive;

            if (!edgeSnappingActive &&
                !vertexSnappingActive &&
                !surfaceSnappingActive)
                return false;

            var surfaceSnapEvents   = surfaceSnappingActive ? snappingContext.s_AllSurfaceSnapEvents : null;
            var edgeSnapEvents      = edgeSnappingActive    ? snappingContext.s_AllEdgeSnapEvents    : null;
            var vertexSnapEvents    = vertexSnappingActive  ? snappingContext.s_AllVertexSnapEvents  : null;

            var selection = Selection.gameObjects; 
            FindClosestSnapPointsToPlane(selection, startWorldPoint, currentWorldPoint, worldSlideGrid, kArbitrarySnapDistance, surfaceSnapEvents, edgeSnapEvents, vertexSnapEvents); 

            snappingContext.CollectAllSnapPoints(foundWorldspacePoints);
            return true;
        }

        static void OnCustomSnappedEvent(int index, int contextIndex)
        {
            if (index < 0)
                return;

            if (!s_SnappingContext.TryGetValue(contextIndex, out var snappingContext))
                return;


            if (index < snappingContext.s_AllVertexSnapEvents.Count)
            {
                s_DrawVertexSnapEvents.Add(snappingContext.s_AllVertexSnapEvents[index]);
                return;
            }
            index -= snappingContext.s_AllVertexSnapEvents.Count;

            if (index < snappingContext.s_AllEdgeSnapEvents.Count)
            {
                s_DrawEdgeSnapEvents.Add(snappingContext.s_AllEdgeSnapEvents[index]);
                return;
            }
            index -= snappingContext.s_AllEdgeSnapEvents.Count;

            if (index < snappingContext.s_AllSurfaceSnapEvents.Count)
            {
                s_DrawSurfaceSnapEvents.Add(snappingContext.s_AllSurfaceSnapEvents[index]);
                return;
            }
            index -= snappingContext.s_AllSurfaceSnapEvents.Count;
        }

        static readonly List<Vector3> s_PolygonVertices = new List<Vector3>();

        static readonly HashSet<(CSGTreeNode, int)> s_usedVertices = new HashSet<(CSGTreeNode, int)>();
        static readonly HashSet<(CSGTreeNode, int)> s_usedSurfaces = new HashSet<(CSGTreeNode, int)>();
        static void DrawCustomSnappedEvent()
        {
#if false
            // Debug code to render snapping points close to mouse position
            if (s_SnappingContext.TryGetValue(0, out var snappingContext))
            {
                var foundWorldspacePoints = new List<Vector3>();
                snappingContext.CollectAllSnapPoints(foundWorldspacePoints);
                foreach (var vertex in foundWorldspacePoints)
                {
                    HandleRendering.DrawIntersectionPoint(vertex);
                }
            }
#endif

            s_usedVertices.Clear();
            s_usedSurfaces.Clear();
            var prevMatrix = Handles.matrix;
            for (int i = 0; i < s_DrawVertexSnapEvents.Count; i++)
            {
                var intersection    = s_DrawVertexSnapEvents[i];
                var vertexKey       = (intersection.brush, intersection.vertexIndex);
                if (!s_usedVertices.Add(vertexKey))
                    continue;
                HandleRendering.RenderVertexBox(intersection.intersection);
            }

            for (int i = 0; i < s_DrawEdgeSnapEvents.Count; i++)
            {
                var intersection    = s_DrawEdgeSnapEvents[i];
                s_usedSurfaces.Add((intersection.brush, intersection.surfaceIndex0));
                s_usedSurfaces.Add((intersection.brush, intersection.surfaceIndex1));

                var vertexKey0      = (intersection.brush, intersection.vertexIndex0);
                var vertexKey1      = (intersection.brush, intersection.vertexIndex1);
                if (!s_usedVertices.Add(vertexKey0) ||
                    !s_usedVertices.Add(vertexKey1))
                    continue;
                ChiselOutlineRenderer.DrawLine(intersection.from, intersection.to);
            }

            for (int i = 0; i < s_DrawSurfaceSnapEvents.Count; i++)
            {
                var surfaceSnapEvent    = s_DrawSurfaceSnapEvents[i];
                var surfaceKey          = (surfaceSnapEvent.brush, surfaceSnapEvent.surfaceIndex);
                if (!s_usedSurfaces.Add(surfaceKey))
                    continue;

                var center          = s_DrawSurfaceSnapEvents[i].intersection;
                var rotation        = Quaternion.LookRotation(surfaceSnapEvent.normal);
                var transformation = Matrix4x4.TRS(center, rotation, Vector3.one);
                Handles.matrix = transformation;
                //HandleRendering.DrawInfiniteLine(Vector3.zero, Axis.X);
                //HandleRendering.DrawInfiniteLine(Vector3.zero, Axis.Y);

                var brush           = surfaceSnapEvent.brush;
                var surfaceIndex    = surfaceSnapEvent.surfaceIndex;
                var brushMeshBlob   = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
                if (!brushMeshBlob.IsCreated)
                    continue;

                ref var brushMesh   = ref brushMeshBlob.Value;
                var polygon         = brushMesh.polygons[surfaceIndex];
                var firstEdge       = polygon.firstEdge;
                var lastEdge        = firstEdge + polygon.edgeCount;
                s_PolygonVertices.Clear();
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var vertex = brushMesh.localVertices[brushMesh.halfEdges[e].vertexIndex];
                    s_PolygonVertices.Add(vertex);
                }
                
                var model           = ChiselNodeHierarchyManager.FindChiselNodeByInstanceID(brush.Tree.UserID) as ChiselModel;
                var nodeToWorld     = model.hierarchyItem.LocalToWorldMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                ChiselOutlineRenderer.DrawLineLoop(nodeToWorld, s_PolygonVertices, Handles.color, thickness: 2);
            }

            
            Handles.matrix = prevMatrix;
        }
    }
}
