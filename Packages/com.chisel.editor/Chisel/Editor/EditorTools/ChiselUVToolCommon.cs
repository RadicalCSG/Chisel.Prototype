using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using Chisel.Utilities;
using UnityEditor.ShortcutManagement;
using UnityEditor.EditorTools;
using Snapping          = UnitySceneExtensions.Snapping;
using Grid              = UnitySceneExtensions.Grid;
using SnapSettings    = UnitySceneExtensions.SnapSettings;

namespace Chisel.Editors
{

    sealed class ChiselSurfaceContainer : ScriptableObject
    {
        public const string kSurfaceDescriptionName = nameof(SurfaceDescription);
        public const string kBrushMaterialName      = nameof(BrushMaterial);
        
        public SurfaceDescription               SurfaceDescription;
        public ChiselBrushMaterial              BrushMaterial;
        [NonSerialized] public SurfaceReference SurfaceReference;

        public static ChiselSurfaceContainer Create(SurfaceReference surfaceReference)
        {
            var container = ScriptableObject.CreateInstance<ChiselSurfaceContainer>();
            container.SurfaceReference      = surfaceReference;
            container.SurfaceDescription    = surfaceReference.SurfaceDescription;
            container.BrushMaterial         = surfaceReference.BrushMaterial;
            container.hideFlags             = HideFlags.DontSave;
            return container;
        }

        public void Load()
        {
            SurfaceDescription = SurfaceReference.SurfaceDescription;
        }

        public void Store()
        {
            SurfaceReference.SurfaceDescription = SurfaceDescription;
        }
    }

    sealed class ChiselUVToolCommon : ScriptableObject
    {
        #region Instance
        static ChiselUVToolCommon _instance;
        public static ChiselUVToolCommon Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = ScriptableObject.CreateInstance<ChiselUVToolCommon>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;
            }
        }
        #endregion

        static readonly int kSurfaceDragSelectionHash = "SurfaceDragSelection".GetHashCode();

        internal static bool InEditCameraMode	{ get { return (Tools.viewTool == ViewTool.Pan || Tools.viewTool == ViewTool.None); } }
        internal static bool ToolIsDragging		{ get; set; }
        internal static bool MouseIsDown	    { get; set; }


        public void OnActivate()
        {
            ChiselSurfaceSelectionManager.selectionChanged -= UpdateSurfaceSelection;
            ChiselSurfaceSelectionManager.selectionChanged += UpdateSurfaceSelection;

            Reset();
            UpdateSurfaceSelection();
            ChiselSurfaceOverlay.Show();
        }

        public void OnDeactivate()
        {
            ChiselSurfaceSelectionManager.selectionChanged -= UpdateSurfaceSelection;
            Reset();
            DestroyOldSurfaces();
            ChiselSurfaceOverlay.Hide();
        }

        void Reset()
        {
            if (serializedObject != null)
            {
                serializedObject.Dispose();
                serializedObject = null;
            }
            layerUsageProp = null;
            renderMaterialProp = null;
            physicsMaterialProp = null;
            initialized = false;
        }

        void DestroyOldSurfaces()
        {
            if (surfaces != null &&
                surfaces.Length > 0)
            {
                foreach (var surface in surfaces)
                    UnityEngine.Object.DestroyImmediate(surface);
            }
            surfaces = Array.Empty<ChiselSurfaceContainer>();
        }

        void UpdateSurfaceSelection()
        {
            DestroyOldSurfaces();
            surfaces = (from reference in ChiselSurfaceSelectionManager.Selection select ChiselSurfaceContainer.Create(reference)).ToArray();
            var undoableObjectList = (from surface in surfaces select (UnityEngine.Object)surface.SurfaceReference.node).ToList();                
            //undoableObjectList.AddRange(from surface in surfaces select (UnityEngine.Object)surface.SurfaceReference.brushContainerAsset);
            undoableObjects = undoableObjectList.ToArray();

            if (surfaces.Length == 0)
            {
                Reset();
                return;
            }

            serializedObject = new SerializedObject(surfaces);
            var surfaceDescriptionProp = serializedObject.FindProperty(ChiselSurfaceContainer.kSurfaceDescriptionName);
            if (surfaceDescriptionProp != null)
            {
                smoothingGroupProp  = surfaceDescriptionProp.FindPropertyRelative(SurfaceDescription.kSmoothingGroupName);
                surfaceFlagsProp    = surfaceDescriptionProp.FindPropertyRelative(SurfaceDescription.kSurfaceFlagsName);
                UV0Prop             = surfaceDescriptionProp.FindPropertyRelative(SurfaceDescription.kUV0Name);
            }
            var brushMaterialProp       = serializedObject.FindProperty(ChiselSurfaceContainer.kBrushMaterialName);
            if (brushMaterialProp != null)
            {
                layerUsageProp      = brushMaterialProp.FindPropertyRelative(ChiselBrushMaterial.kLayerUsageFieldName);
                renderMaterialProp  = brushMaterialProp.FindPropertyRelative(ChiselBrushMaterial.kRenderMaterialFieldName);
                physicsMaterialProp = brushMaterialProp.FindPropertyRelative(ChiselBrushMaterial.kPhysicsMaterialFieldName);
            }
            initialized = true;
        }

        void UpdateAllSurfaces()
        {
            if (surfaces == null)
                return;
            foreach(var surface in surfaces)
                surface.Load();
        }

        bool initialized = false;
        SerializedObject serializedObject;
        SerializedProperty layerUsageProp;
        SerializedProperty renderMaterialProp;
        SerializedProperty physicsMaterialProp;

        SerializedProperty smoothingGroupProp;
        SerializedProperty surfaceFlagsProp;
        SerializedProperty UV0Prop;
        [SerializeField] UnityEngine.Object[]       undoableObjects = Array.Empty<UnityEngine.Object>();
        [SerializeField] ChiselSurfaceContainer[]   surfaces        = Array.Empty<ChiselSurfaceContainer>();

        public void OnSceneSettingsGUI(SceneView sceneView)
        {
            if (!initialized)
                UpdateSurfaceSelection();
            else
                UpdateAllSurfaces();

            if (PreviewTextureManager.Update())
                sceneView.Repaint();

            if (surfaces.Length == 0)
            {
                GUILayout.Box("No surfaces selected. Please select a surface", GUILayout.ExpandWidth(true));
            }

            if (!initialized)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EditorGUI.BeginChangeCheck();
            {
                var desiredHeight   = ChiselBrushMaterialPropertyDrawer.DefaultMaterialLayerUsageHeight;
                var position        = EditorGUILayout.GetControlRect(false, desiredHeight);
                ChiselBrushMaterialPropertyDrawer.ShowMaterialLayerUsage(position, renderMaterialProp, layerUsageProp);

                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 85;
                position = EditorGUILayout.GetControlRect(true, SurfaceFlagsPropertyDrawer.DefaultHeight);
                if (surfaceFlagsProp != null)
                    EditorGUI.PropertyField(position, surfaceFlagsProp, SurfaceDescriptionPropertyDrawer.kSurfaceFlagsContents, true);

                position = EditorGUILayout.GetControlRect(true, UVMatrixPropertyDrawer.DefaultHeight);
                if (UV0Prop != null)
                    EditorGUI.PropertyField(position, UV0Prop, SurfaceDescriptionPropertyDrawer.kUV0Contents, true);

                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            if (EditorGUI.EndChangeCheck() && undoableObjects.Length > 0)
            {
                Undo.RegisterCompleteObjectUndo(undoableObjects, undoableObjects.Length > 1 ? "Modified materials" : "Modified material");
                serializedObject.ApplyModifiedProperties();
                foreach (var item in surfaces)
                    item.Store();
            }
        }

        
        #region Snapping

        internal static SnapSettings toolSnapOverrides = SnapSettings.All;

        internal static SnapSettings CurrentSnapSettings { get { return Snapping.SnapSettings & toolSnapOverrides; } }
        internal static bool pointHasSnapped     = false;
        internal static bool forceVertexSnapping = false;

        const float kMinSnapDistance    = 0.5f;
        const float kDistanceEpsilon    = 0.0006f;
        const float kAlignmentEpsilon   = 1 - kDistanceEpsilon;


        struct UVSnapState
        {
            public SnapSettings   SnapCause;
            public Vector3          SnapPosition;
            public Vector3          StartEdge;
            public Vector3          EndEdge;
            public SnapAxis         SnapGridAxis;
        }

        static UVSnapState snapState = new UVSnapState();

        static void SnapGridIntersection(SnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist, ref UVSnapState snapState)
        {
            if ((snapSettings & SnapSettings.UVGeometryGrid) == SnapSettings.None)
                return;

            var worldPlane = surfaceReference?.WorldPlane ?? null;
            if (worldPlane == null ||
                !worldPlane.HasValue)
                return;

            var worldPlaneValue = worldPlane.Value;

            var grid				= UnitySceneExtensions.Grid.defaultGrid;
            var gridSnappedPoint	= Snapping.SnapPoint(intersectionPoint, grid);

            var xAxis       = grid.Right;
            var yAxis       = grid.Up;
            var zAxis       = grid.Forward;
            var snapAxis    = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, worldPlaneValue.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, worldPlaneValue.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, worldPlaneValue.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;
            if (Mathf.Abs(worldPlaneValue.GetDistanceToPoint(gridSnappedPoint)) < kDistanceEpsilon)
            {
                var abs_dist = (gridSnappedPoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                if (abs_dist < bestDist)
                {
                    bestDist = abs_dist;
                    snapState.SnapCause = SnapSettings.UVGeometryGrid;
                    snapState.SnapPosition = gridSnappedPoint;
                    snappedPoint = gridSnappedPoint;
                }
            } else
            {
                float dist;
                var ray = new Ray(gridSnappedPoint, xAxis);
                if ((snapAxis & Axis.X) != Axis.None && worldPlaneValue.SignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snapState.SnapCause = SnapSettings.UVGeometryGrid;
                        snapState.SnapPosition = planePoint;
                        snappedPoint = planePoint;
                    }
                }
                ray.direction = yAxis;
                if ((snapAxis & Axis.Y) != Axis.None && worldPlaneValue.SignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snapState.SnapCause = SnapSettings.UVGeometryGrid;
                        snapState.SnapPosition = planePoint;
                        snappedPoint = planePoint;
                    }
                }
                ray.direction = zAxis;
                if ((snapAxis & Axis.Z) != Axis.None && worldPlaneValue.SignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snapState.SnapCause = SnapSettings.UVGeometryGrid;
                        snapState.SnapPosition = planePoint;
                        snappedPoint = planePoint;
                    }
                }
            }
        }

        static bool SnapSurfaceVertices(SnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist, ref UVSnapState snapState)
        {
            if (surfaceReference == null)
                return false;

            if ((snapSettings & SnapSettings.UVGeometryVertices) == SnapSettings.None)
                return false;

            var brushMeshBlob = surfaceReference.BrushMesh;
            if (!brushMeshBlob.IsCreated)
                return false;

            ref var brushMesh = ref brushMeshBlob.Value;
            
            var localToWorldSpace	= surfaceReference.LocalToWorldSpace;
            
            var bestDistSqr			= float.PositiveInfinity;
            var bestVertex			= snappedPoint;

            // TODO: when we use new CSG algorithm, use its intersection loops instead
            var outline = ChiselOutlineRenderer.GetSurfaceWireframe(surfaceReference);
            if (outline != null)
            {
                var vertices = outline.Vertices;
            
                for (int v = 0; v < vertices.Length; v++)
                {
                    var worldSpaceVertex = localToWorldSpace.MultiplyPoint(vertices[v]);
                    var dist_sqr         = (worldSpaceVertex - intersectionPoint).sqrMagnitude;
                    if (dist_sqr < bestDistSqr)
                    {
                        bestDistSqr = dist_sqr;
                        bestVertex = worldSpaceVertex;
                    }
                }
            } else
            {
                Debug.Assert(surfaceReference.surfaceIndex >= 0 && surfaceReference.surfaceIndex < brushMesh.polygons.Length);

                var polygon             = brushMesh.polygons[surfaceReference.surfaceIndex];
                ref var edges           = ref brushMesh.halfEdges;
                ref var localVertices   = ref brushMesh.localVertices;
                var firstEdge           = polygon.firstEdge;
                var lastEdge            = firstEdge + polygon.edgeCount;
            
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var worldSpaceVertex = localToWorldSpace.MultiplyPoint(localVertices[edges[e].vertexIndex]);
                    var dist_sqr         = (worldSpaceVertex - intersectionPoint).sqrMagnitude;
                    if (dist_sqr < bestDistSqr)
                    {
                        bestDistSqr = dist_sqr;
                        bestVertex = worldSpaceVertex;
                    }
                }
            }

            if (bestDistSqr >= 0.25f)
                return false;

            var closestVertexDistance = bestDistSqr * preferenceFactor;
            if (closestVertexDistance < bestDist)
            {
                bestDist = closestVertexDistance;
                snapState.SnapCause = SnapSettings.UVGeometryVertices;
                snapState.SnapPosition = bestVertex;
                snappedPoint = bestVertex;
                return true;
            }
            return false;
        }

        static SnapAxis GetSnappedAxi(Grid grid, Vector3 planePoint)
        {
            var gridPlanePoint = planePoint - grid.Center;
            var axisX = Math.Abs(Vector3.Dot(gridPlanePoint, grid.Right  ) % Snapping.MoveSnappingSteps.x) < kDistanceEpsilon;
            var axisY = Math.Abs(Vector3.Dot(gridPlanePoint, grid.Up     ) % Snapping.MoveSnappingSteps.y) < kDistanceEpsilon;
            var axisZ = Math.Abs(Vector3.Dot(gridPlanePoint, grid.Forward) % Snapping.MoveSnappingSteps.z) < kDistanceEpsilon;

            return ((axisX) ? SnapAxis.X : SnapAxis.None) |
                   ((axisY) ? SnapAxis.Y : SnapAxis.None) |
                   ((axisZ) ? SnapAxis.Z : SnapAxis.None);
        }

        static bool SnapSurfaceEdges(SnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist, ref UVSnapState snapState)
        {
            if (surfaceReference == null)
                return false;

            if ((snapSettings & SnapSettings.UVGeometryEdges) == SnapSettings.None)
                return false;

            var brushMeshBlob = surfaceReference.BrushMesh;
            if (!brushMeshBlob.IsCreated)
                return false;

            ref var brushMesh = ref brushMeshBlob.Value;
            var localToWorldSpace	= surfaceReference.LocalToWorldSpace;

            Debug.Assert(surfaceReference.surfaceIndex >= 0 && surfaceReference.surfaceIndex < brushMesh.polygons.Length);

            var grid            = UnitySceneExtensions.Grid.defaultGrid;
            var xAxis           = grid.Right;
            var yAxis           = grid.Up;
            var zAxis           = grid.Forward;
            var surfacePlane    = surfaceReference.WorldPlane.Value;

            var snapAxis    = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, surfacePlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, surfacePlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, surfacePlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;

            ref var polygons                = ref brushMesh.polygons;
            var polygon                     = polygons[surfaceReference.surfaceIndex];
            ref var halfEdgePolygonIndices  = ref brushMesh.halfEdgePolygonIndices;
            ref var halfEdges               = ref brushMesh.halfEdges;
            ref var localVertices           = ref brushMesh.localVertices;
            ref var localPlanes             = ref brushMesh.localPlanes;
            var firstEdge                   = polygon.firstEdge;
            var lastEdge                    = firstEdge + polygon.edgeCount;

            bool found = false;
            if ((CurrentSnapSettings & SnapSettings.UVGeometryGrid) != SnapSettings.None)
            {
                var minSnappedPoint = snappedPoint - grid.Center;
                var dotX            = Vector3.Dot(minSnappedPoint, grid.Right);
                var dotY            = Vector3.Dot(minSnappedPoint, grid.Up);
                var dotZ            = Vector3.Dot(minSnappedPoint, grid.Forward);

                dotX = Mathf.Round(dotX / Snapping.MoveSnappingSteps.x) * Snapping.MoveSnappingSteps.x;
                dotY = Mathf.Round(dotY / Snapping.MoveSnappingSteps.y) * Snapping.MoveSnappingSteps.y;
                dotZ = Mathf.Round(dotZ / Snapping.MoveSnappingSteps.z) * Snapping.MoveSnappingSteps.z;

                var axisPlaneX  = new Plane(grid.Right,   -dotX);
                var axisPlaneY  = new Plane(grid.Up,      -dotY);
                var axisPlaneZ  = new Plane(grid.Forward, -dotZ);
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var twin            = halfEdges[e].twinIndex;
                    var polygonIndex    = halfEdgePolygonIndices[twin];

                    var surfaceIndex    = polygonIndex; // FIXME: throughout the code we're making assumptions about polygonIndices being the same as surfaceIndices, 
                                                        //        this needs to be fixed

                    var localPlaneVector = localPlanes[surfaceIndex];
                    var edgeLocalPlane   = new Plane(localPlaneVector.xyz, localPlaneVector.w);
                    var edgeWorldPlane   = localToWorldSpace.TransformPlane(edgeLocalPlane);
                    var edgeDirection    = Vector3.Cross(surfacePlane.normal, edgeWorldPlane.normal);

                    var edgeSnapAxis = snapAxis;
                    if (Mathf.Abs(Vector3.Dot(xAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.X;
                    if (Mathf.Abs(Vector3.Dot(yAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.Y;
                    if (Mathf.Abs(Vector3.Dot(zAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.Z;

                    if (edgeSnapAxis == Axis.None)
                        continue;

                    if ((edgeSnapAxis & Axis.X) != Axis.None)
                    {
                        var planePoint  = Chisel.Core.PlaneExtensions.Intersection(surfacePlane, edgeWorldPlane, axisPlaneX);
                        if (!float.IsNaN(planePoint.x) && !float.IsNaN(planePoint.y) && !float.IsNaN(planePoint.z) &&
                            Mathf.Abs(surfacePlane  .GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(edgeWorldPlane.GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(axisPlaneX    .GetDistanceToPoint(planePoint)) < kDistanceEpsilon)
                        { 
                            var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                            if (abs_dist < bestDist)
                            {
                                bestDist = abs_dist;
                                snappedPoint = planePoint;
                                snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                                snapState.SnapPosition  = planePoint;
                                snapState.StartEdge     = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[firstEdge + ((e - firstEdge + polygon.edgeCount - 1) % polygon.edgeCount)].vertexIndex]);
                                snapState.EndEdge       = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[e].vertexIndex]);                                
                                snapState.SnapGridAxis  = GetSnappedAxi(grid, planePoint);
                                found = true;
                            }
                        }
                    }
                    if ((edgeSnapAxis & Axis.Y) != Axis.None)
                    {
                        var planePoint  = Chisel.Core.PlaneExtensions.Intersection(surfacePlane, edgeWorldPlane, axisPlaneY);
                        if (!float.IsNaN(planePoint.x) && !float.IsNaN(planePoint.y) && !float.IsNaN(planePoint.z) &&
                            Mathf.Abs(surfacePlane  .GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(edgeWorldPlane.GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(axisPlaneY    .GetDistanceToPoint(planePoint)) < kDistanceEpsilon)
                        {
                            var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                            if (abs_dist < bestDist)
                            {
                                bestDist = abs_dist;
                                snappedPoint = planePoint;
                                snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                                snapState.SnapPosition  = planePoint;
                                snapState.StartEdge     = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[firstEdge + ((e - firstEdge + polygon.edgeCount - 1) % polygon.edgeCount)].vertexIndex]);
                                snapState.EndEdge       = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[e].vertexIndex]);
                                snapState.SnapGridAxis  = GetSnappedAxi(grid, planePoint);
                                found = true;
                            }
                        }
                    }
                    if ((edgeSnapAxis & Axis.Z) != Axis.None)
                    {
                        var planePoint  = Chisel.Core.PlaneExtensions.Intersection(surfacePlane, edgeWorldPlane, axisPlaneZ);
                        if (!float.IsNaN(planePoint.x) && !float.IsNaN(planePoint.y) && !float.IsNaN(planePoint.z) &&
                            Mathf.Abs(surfacePlane  .GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(edgeWorldPlane.GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(axisPlaneZ    .GetDistanceToPoint(planePoint)) < kDistanceEpsilon)
                        {
                            var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                            if (abs_dist < bestDist)
                            {
                                bestDist = abs_dist;
                                snappedPoint = planePoint;
                                snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                                snapState.SnapPosition  = planePoint;
                                snapState.StartEdge     = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[firstEdge + ((e - firstEdge + polygon.edgeCount - 1) % polygon.edgeCount)].vertexIndex]);
                                snapState.EndEdge       = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[e].vertexIndex]);
                                snapState.SnapGridAxis  = GetSnappedAxi(grid, planePoint);
                                found = true;
                            }
                        }
                    }
                }
            } else
            {
                var prevVertex = localToWorldSpace.MultiplyPoint(localVertices[halfEdges[lastEdge - 1].vertexIndex]);
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var localPlaneVector = localPlanes[halfEdgePolygonIndices[halfEdges[e].twinIndex]];
                    var edgeLocalPlane   = new Plane(localPlaneVector.xyz, localPlaneVector.w);
                    var edgeWorldPlane   = localToWorldSpace.TransformPlane(edgeLocalPlane);
                    
                    var currVertex      = localToWorldSpace.MultiplyPoint(localVertices[halfEdges[e].vertexIndex]);
                    var closestPoint    = Chisel.Core.GeometryMath.ProjectPointLine(intersectionPoint, prevVertex, currVertex);
                    var abs_dist        = (closestPoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snappedPoint = closestPoint;
                        snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                        snapState.SnapPosition  = snappedPoint;
                        snapState.StartEdge     = prevVertex;
                        snapState.EndEdge       = currVertex;

                        found = true;
                    }

                    prevVertex = currVertex;
                }
            }
            return found;
        }

        static bool SelectedSnapIntersection(Vector2 mousePosition, Vector3 originalIntersectionPoint, out SurfaceReference surfaceReference, out Vector3 worldIntersectionPoint, ref UVSnapState snapState)
        {
            var bestDist = float.PositiveInfinity;

            worldIntersectionPoint = default;
            surfaceReference = null;

            foreach (var selectedSurface in ChiselSurfaceSelectionManager.Selection)
            {
                if (!selectedSurface.WorldPlane.HasValue)
                    continue;
                if (GetCurrentWorldClick(selectedSurface.WorldPlane.Value, mousePosition, out Vector3 currentWorldIntersection))
                {
                    currentWorldIntersection = SnapIntersection(currentWorldIntersection, selectedSurface, out pointHasSnapped, ref snapState);
                    if (pointHasSnapped)
                    {
                        var dist = (originalIntersectionPoint - currentWorldIntersection).sqrMagnitude;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            worldIntersectionPoint = currentWorldIntersection;
                            surfaceReference = selectedSurface;
                        }
                    }
                }
            }
            return surfaceReference != null;
        }


        static Vector3 SnapIntersection(Vector3 intersectionPoint, SurfaceReference surfaceReference, out bool haveWeSnapped, ref UVSnapState snapState)
        {
            if (surfaceReference == null)
            {
                haveWeSnapped = false;
                return intersectionPoint;
            }

            // TODO: visualize what we're snapping against

            var bestDist			= float.PositiveInfinity;
            var snappedPoint		= intersectionPoint;
            var handleSize          = HandleUtility.GetHandleSize(intersectionPoint);
            // When holding V we force to ONLY and ALWAYS snap against vertices
            var snapSettings        = forceVertexSnapping ? SnapSettings.UVGeometryVertices : CurrentSnapSettings;

            // snap to edges of surface that are closest to the intersection point
            SnapSurfaceEdges(snapSettings, surfaceReference, intersectionPoint, 1.25f, ref snappedPoint, ref bestDist, ref snapState);

            // Snap to closest point on grid
            SnapGridIntersection(snapSettings, surfaceReference, intersectionPoint, 1.5f, ref snappedPoint, ref bestDist, ref snapState);

            // snap to vertices of surface that are closest to the intersection point
            SnapSurfaceVertices(snapSettings, surfaceReference, intersectionPoint, 0.5f, ref snappedPoint, ref bestDist, ref snapState);
            SnapSurfaceVertices(snapSettings, surfaceReference, snappedPoint, 0.5f, ref snappedPoint, ref bestDist, ref snapState);

            // TODO: snap to UV space bounds (and more?)


            var gridSnappingenabled = (CurrentSnapSettings & SnapSettings.UVGeometryGrid) != SnapSettings.None;
            var minSnapDistance     = (forceVertexSnapping || gridSnappingenabled) ? float.PositiveInfinity : (handleSize * kMinSnapDistance);

            if (bestDist < minSnapDistance)
            {
                haveWeSnapped = true;
                intersectionPoint = snappedPoint;
            } else
            {
                snapState.SnapCause = SnapSettings.None;
                haveWeSnapped = false;
            }

            return intersectionPoint;
        }

        internal static float SnapAngle(float rotatedAngle)
        {
            if (!Snapping.RotateSnappingEnabled)
                return rotatedAngle;
            return ((int)(rotatedAngle / Snapping.RotateSnappingStep)) * Snapping.RotateSnappingStep;
        }

        #endregion

        
        #region Tool Base
        static bool CanEnableTool(int id)
        {
            // Is another control enabled at the moment?
            if (GUIUtility.hotControl != 0)
                return false;
                        
            var evt = Event.current;
            // Is our tool currently the control nearest to the mouse?
            if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                (GUIUtility.keyboardControl != id || evt.button != 2))
                return false;
            return true;
        }

        internal static bool IsToolEnabled(int id)
        {
            // Is our control enabled at the moment?
            return GUIUtility.hotControl == id;
        }


        static void EnableTool(int id)
        {
            EditorGUIUtility.SetWantsMouseJumping(1);   // enable allowing the user to move the mouse over the bounds of the screen
            jumpedMousePosition = Event.current.mousePosition;  // remember the current mouse position so we can update it using Event.current.delta, 
                                                                // since mousePosition won't make sense any more when mouse jumping
            GUIUtility.hotControl = GUIUtility.keyboardControl = id; // set our tool as the active control
            Event.current.Use(); // make sure no-one else can use our event


            toolSnapOverrides = SnapSettings.All;
            pointHasSnapped = false;
        }

        static void DisableTool()
        {
            EditorGUIUtility.SetWantsMouseJumping(0); // disable allowing the user to move the mouse over the bounds of the screen
            GUIUtility.hotControl = GUIUtility.keyboardControl = 0; // remove the active control so that the user can use another control
            Event.current.Use(); // make sure no-one else can use our event


            toolSnapOverrides = SnapSettings.All;
            pointHasSnapped = false;
        }

        static void CancelToolInProgress()
        {
            DisableTool();
            Undo.RevertAllInCurrentGroup();
        }

        //[Shortcut(kToolShotcutName, ChiselKeyboardDefaults.FreeBuilderModeKey, ChiselKeyboardDefaults.FreeBuilderModeModifiers, displayName = kToolShotcutName)]
        //public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselExtrudedShapeGeneratorMode); }

        const float kMaxControlDistance = 3.0f;
        internal static bool SurfaceToolBase(int id, SelectionType selectionType, Rect dragArea)
        {
            // we only do tools when we do not use a modifier (shift, control etc.)
            if (selectionType != SelectionType.Replace)
                return false;

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    // Unless something else is closer, make sure our tool is selected
                    HandleUtility.AddControl(id, kMaxControlDistance);
                    break;
                }

                case EventType.KeyDown:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (IsToolEnabled(id))
                        {
                            evt.Use();
                            break;
                        } else
                        {
                            if (ChiselSurfaceSelectionManager.HaveSelection)
                            {
                                evt.Use();
                                break;
                            }
                        }
                    }
                    if (!EditorGUIUtility.editingTextField)
                    {
                        if (evt.keyCode == KeyCode.V)
                        {
                            forceVertexSnapping = true;
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
                        if (IsToolEnabled(id))
                        {
                            CancelToolInProgress();
                            evt.Use();
                            GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                            break;
                        } else
                        {
                            // TODO: this works, but something else is deselecting our gameobjects, disabling the editortool?
                            if (ChiselSurfaceSelectionManager.HaveSelection)
                            {
                                ChiselSurfaceSelectionManager.DeselectAll();
                                evt.Use();
                                GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                                break;
                            }
                        }
                    }
                    if (forceVertexSnapping && evt.keyCode == KeyCode.V)
                    {
                        forceVertexSnapping = false;
                        if (!EditorGUIUtility.editingTextField)
                            evt.Use();
                        break;
                    }
                    break;
                }

                case EventType.MouseMove:
                {
                    // In case we somehow missed a MouseUp event, we reset this bool
                    MouseIsDown = false;
                    break;
                }
                case EventType.MouseDown:
                {
                    // We can only use a tool when the mouse cursor is inside the draggable scene area
                    if (!dragArea.Contains(evt.mousePosition))
                        return false;

                    // We can only use a tool when we're hovering over a surfaces
                    if (hoverSurfaces == null || hoverSurfaces.Count == 0)
                        return false;

                    if (!CanEnableTool(id))
                        break;

                    // We want to be able to tell the difference between dragging and clicking,
                    // so we keep track if we dragged or not. In this case we haven't started dragging yet.
                    ToolIsDragging = false;
                    MouseIsDown = true;

                    EnableTool(id); 
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;

                    if (!ToolIsDragging)
                    {
                        // If we haven't dragged the tool yet, check if the surface underneath 
                        // the mouse is selected or not, if it isn't: select it exclusively
                        if (!ChiselSurfaceSelectionManager.IsAnySelected(hoverSurfaces))
                            ClickSelection(dragArea, selectionType);
                    }

                    // In the tool specific code, calling StartToolDragging will set ToolIsDragging to true, 
                    // which will allow us to tell the difference between clicking and dragging.

                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    MouseIsDown = false;

                    // We want to be able to tell the difference between clicking and dragging, 
                    // so we use ToolIsDragging here to determine if we clicked.
                    if (!ToolIsDragging)
                    {
                        // If we clicked on the surface, instead of dragged it, just click select it
                        ClickSelection(dragArea, selectionType);
                    }

                    ToolIsDragging = false;

                    DisableTool();
                    ResetSelection();
                    break;
                }
                case EventType.Repaint:
                {
                    RenderIntersection();
                    break;
                }
            }
            return true;
        }

        static SurfaceReference     startSurfaceReference;
        static Vector2              jumpedMousePosition;

        internal static Plane       worldDragPlane;
        internal static Plane       worldProjectionPlane;
        internal static Vector3		worldStartPosition;
        internal static Vector3		worldIntersection;
        internal static Vector3     worldDragDeltaVector;
        
        internal static ChiselNode[]	            selectedNodes;
        internal static UVMatrix[]			        selectedUVMatrices;
        internal static SurfaceReference[]	        selectedSurfaceReferences;

        internal static bool StartToolDragging()
        {
            jumpedMousePosition += Event.current.delta;
            Event.current.Use();
            if (ToolIsDragging)
            {
                UpdateDragVector();
                return false;
            }

            // We set ToolIsDragging to true to be able to tell the difference between dragging and clicking
            ToolIsDragging = true;

            // Find the intersecting surfaces
            startSurfaceReference           = hoverSurfaceReference;

            selectedSurfaceReferences	    = ChiselSurfaceSelectionManager.Selection.ToArray();

            // We need all the nodes for all the surfaces we're moving, so that we can record them for an undo
            selectedNodes                   = ChiselSurfaceSelectionManager.SelectedNodes.ToArray();

            // We copy all the original surface uvMatrices, so we always apply rotations and transformations relatively to the original
            // This makes it easier to recover from edge cases and makes it more accurate, floating point wise.
            selectedUVMatrices	 = new UVMatrix[selectedSurfaceReferences.Length];
            for (int i = 0; i < selectedSurfaceReferences.Length; i++)
            {
                selectedUVMatrices[i] = selectedSurfaceReferences[i].UV0;
            }
            
            // Find the intersection point/plane in model space
            worldStartPosition	 = hoverIntersection.Value.worldPlaneIntersection;
            worldProjectionPlane = hoverIntersection.Value.worldPlane;
            worldIntersection    = worldStartPosition;

            // TODO: we want to be able to determine delta movement over a plane. Ideally it would match the position of the cursor perfectly.
            //		 unfortunately when moving the cursor towards the horizon of the plane, relative to the camera, the delta movement 
            //		 becomes too large or even infinity. Ideally we'd switch to a camera facing plane for these cases and determine movement in 
            //		 a less perfect way that would still allow the user to move or rotate things in a reasonable way.

            // more accurate for small movements
            worldDragPlane		= worldProjectionPlane;

            // TODO: (unfinished) prevents drag-plane from intersecting near plane (makes movement slow down to a singularity when further away from click position)
            //worldDragPlane	= new Plane(Camera.current.transform.forward, worldStartPosition); 

            // TODO: ideally we'd interpolate the behavior of the worldPlane between near and far behavior
            UpdateDragVector();
            return true;
        }

        private static Vector3 GetCurrentWorldClick(Vector2 mousePosition)
        {
            if (GetCurrentWorldClick(worldDragPlane, mousePosition, out Vector3 currentWorldIntersection))
            {
                snapState.SnapCause = SnapSettings.None;
                currentWorldIntersection = SnapIntersection(currentWorldIntersection, startSurfaceReference, out pointHasSnapped, ref snapState);
                return currentWorldIntersection;
            }
            return worldStartPosition;
        }

        private static bool GetCurrentWorldClick(Plane plane, Vector2 mousePosition, out Vector3 currentWorldIntersection)
        {
            currentWorldIntersection = default;
            var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (!plane.SignedRaycast(mouseRay, out float enter))
                return false;
            currentWorldIntersection = mouseRay.GetPoint(enter);
            return true;
        }

        static void UpdateDragVector()
        {
            worldIntersection = GetCurrentWorldClick(jumpedMousePosition);
            var worldSpaceMovement = worldIntersection - worldStartPosition;

            MathExtensions.CalculateTangents(worldDragPlane.normal, out var tangent, out var binormal);

            var deltaVector = tangent  * Vector3.Dot(tangent,  worldSpaceMovement) +
                              binormal * Vector3.Dot(binormal, worldSpaceMovement);

            if (Snapping.AxisLockX) deltaVector.x = 0;
            if (Snapping.AxisLockY) deltaVector.y = 0;
            if (Snapping.AxisLockZ) deltaVector.z = 0;

            worldDragDeltaVector = deltaVector;
        }
        #endregion        
        
        
        #region Selection
        static bool ClickSelection(Rect dragArea, SelectionType selectionType)
        {
            return ChiselSurfaceSelectionManager.UpdateSelection(selectionType, hoverSurfaces);
        }
        
        internal static bool SurfaceSelection(Rect dragArea, SelectionType selectionType)
        {
            var id = GUIUtility.GetControlID(kSurfaceDragSelectionHash, FocusType.Keyboard, dragArea);
            
            bool repaint = false;

            var evt = Event.current;
            if (evt.type == EventType.MouseMove ||
                evt.type == EventType.MouseDown)
            {
                if (UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, true))
                    repaint = true;
                
                if (!InEditCameraMode)
                    return repaint;
            }

            if (InEditCameraMode && !ToolIsDragging)
            {
                if (MouseIsDown) 
                    ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
                else
                    ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Surface | VisualizationMode.Outline;
            } else
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;


            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    // we only do drag selection when we use a modifier (shift, control etc.)
                    if (selectionType == SelectionType.Replace)
                        break;

                    if (hoverSurfaces != null && hoverSurfaces.Count > 0)
                        HandleUtility.AddControl(id, 3.0f);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl != 0)
                        break;
                    
                    // we only do drag selection when we use a modifier (shift, control etc.)
                    if (selectionType == SelectionType.Replace)
                        break;
                    
                    if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                        (GUIUtility.keyboardControl != id || evt.button != 2))
                        break;
                    
                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    evt.Use();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
                        break;

                    UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, false);
                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id || evt.button != 0)
                        break;
                    
                    GUIUtility.hotControl = 0;
                    GUIUtility.keyboardControl = 0;
                    evt.Use();

                    if (ChiselSurfaceSelectionManager.UpdateSelection(selectionType, hoverSurfaces))
                        repaint = true;

                    if (UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, true))
                        repaint = true;
                    break;
                }
            }

            return repaint;
        }

        static void ResetSelection()
        {
            hoverIntersection = null;
            hoverSurfaceReference = null;
            selectedSurfaceReferences = null;
            selectedNodes = null;
            selectedUVMatrices = null;
        }
        #endregion

        #region Hover Surfaces
        static ChiselIntersection? hoverIntersection;
        static SurfaceReference hoverSurfaceReference;

        static readonly HashSet<SurfaceReference> hoverSurfaces = new HashSet<SurfaceReference>();

        internal static void RenderIntersectionPoint()
        {
            RenderIntersectionPoint(ChiselUVToolCommon.worldIntersection);
        }

        static void RenderIntersectionPoint(Vector3 position)
        {
            SceneHandles.RenderBorderedCircle(position, HandleUtility.GetHandleSize(position) * 0.02f);
        }

        internal static void RenderSnapEvent()
        {
            if (snapState.SnapCause == SnapSettings.None)
                return;

            var prevMatrix = Handles.matrix;
            var prevColor = Handles.color;

            Handles.color = Color.white;
            switch (snapState.SnapCause)
            {
                case SnapSettings.UVGeometryEdges:
                {
                    ChiselOutlineRenderer.DrawLine(snapState.StartEdge, snapState.EndEdge, LineMode.NoZTest, 4, 0);
                    break;
                }
                case SnapSettings.UVGeometryVertices:
                {
                    HandleRendering.RenderVertexBox(snapState.SnapPosition);
                    break;
                }
            }
            // TODO: figure out how to handle this properly
            /*
            if (snapState.SnapGridAxis != SnapAxis.None)
            {
                var grid = UnitySceneExtensions.Grid.defaultGrid;
                if ((snapState.SnapGridAxis & SnapAxis.X) != SnapAxis.None)
                {
                    var direction = grid.Right * 1000.0f;
                    ChiselOutlineRenderer.DrawLine(snapState.SnapPosition - direction, snapState.SnapPosition + direction, LineMode.NoZTest, 1, 0);
                }
                if ((snapState.SnapGridAxis & SnapAxis.Y) != SnapAxis.None)
                {
                    var direction = grid.Up * 1000.0f;
                    ChiselOutlineRenderer.DrawLine(snapState.SnapPosition - direction, snapState.SnapPosition + direction, LineMode.NoZTest, 1, 0);
                }
                if ((snapState.SnapGridAxis & SnapAxis.Z) != SnapAxis.None)
                {
                    var direction = grid.Forward * 1000.0f;
                    ChiselOutlineRenderer.DrawLine(snapState.SnapPosition - direction, snapState.SnapPosition + direction, LineMode.NoZTest, 1, 0);
                }
            }
            */
            Handles.color = prevColor;
            Handles.matrix = prevMatrix;
        }
        
        static void RenderIntersection()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (ToolIsDragging)
                return;

            if (!hoverIntersection.HasValue)
                return;

            var position = hoverIntersection.Value.worldPlaneIntersection;
            RenderIntersectionPoint(position);
            RenderSnapEvent();
        }

        static readonly List<SurfaceReference> s_TempSurfaces = new List<SurfaceReference>();

        static bool UpdateHoverSurfaces(Vector2 mousePosition, Rect dragArea, SelectionType selectionType, bool clearHovering)
        {
            try
            {
                hoverIntersection = null;
                hoverSurfaceReference = null;

                bool modified = false;
                if (clearHovering || !InEditCameraMode)
                {
                    if (hoverSurfaces.Count != 0)
                    {
                        hoverSurfaces.Clear();
                        modified = true;
                    }
                }

                if (!dragArea.Contains(mousePosition))
                    return modified;

                if (!InEditCameraMode)
                    return modified;

                ChiselIntersection intersection;
                SurfaceReference surfaceReference;
                s_TempSurfaces.Clear();
                if (!ChiselClickSelectionManager.FindSurfaceReferences(mousePosition, false, s_TempSurfaces, out intersection, out surfaceReference))
                {
                    modified = (hoverSurfaces != null) || modified;
                    hoverIntersection = null;
                    return modified;
                }

                bool isSurfaceSelected = ChiselSurfaceSelectionManager.IsSelected(surfaceReference);

                if (!float.IsInfinity(intersection.brushIntersection.surfaceIntersection.distance))
                {
                    snapState.SnapCause = SnapSettings.None;
                    if (!isSurfaceSelected && forceVertexSnapping)
                    {
                        if (SelectedSnapIntersection(mousePosition, intersection.worldPlaneIntersection, out surfaceReference, out Vector3 worldIntersectionPoint, ref snapState))
                        {
                            isSurfaceSelected = true;
                            intersection.worldPlane             = surfaceReference.WorldPlane.Value;
                            intersection.worldPlaneIntersection = worldIntersectionPoint;
                            s_TempSurfaces.Clear();
                            s_TempSurfaces.Add(surfaceReference);
                        }
                    } else
                        intersection.worldPlaneIntersection = SnapIntersection(intersection.worldPlaneIntersection, surfaceReference, out pointHasSnapped, ref snapState);
                }

                if (isSurfaceSelected || !forceVertexSnapping)
                {
                    hoverIntersection = intersection;
                    hoverSurfaceReference = surfaceReference;

                    if (s_TempSurfaces.Count == hoverSurfaces.Count)
                        modified = !hoverSurfaces.ContainsAll(s_TempSurfaces) || modified;
                    else
                        modified = true;

                    if (s_TempSurfaces.Count > 0)
                        hoverSurfaces.AddRange(s_TempSurfaces);
                } else
                {
                    modified = true;
                    hoverIntersection = null;
                    hoverSurfaceReference = null;
                }

                return modified;
            }
            finally
            {
                if (forceVertexSnapping)
                    ChiselSurfaceSelectionManager.SetHovering(selectionType, null);
                else
                    ChiselSurfaceSelectionManager.SetHovering(selectionType, hoverSurfaces);
            }
        }
        #endregion
    }
}
