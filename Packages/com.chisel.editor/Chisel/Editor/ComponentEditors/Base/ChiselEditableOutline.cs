using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using Unity.Mathematics;

namespace Chisel.Editors
{
    // TODO: lots of different responsibilities in here, we should try and separate responsibilities 
    //       into multiple classes to make it easier to understand 
    //       (but at the same time be careful to not over-engineer and make it too abstract)
    public class ChiselEditableOutline
    {
        public ChiselEditableOutline(ChiselBrush brush)
        {
            this.brush = brush;
            this.brushMesh = new BrushMesh(this.brush.definition.brushOutline);
            this.selection = ChiselTopologySelectionManager.Selection[this.brush][0];
            Rebuild();
        }

        const float kHighlightedEdgeThickness   = 3.5f * 0.25f;
        const float kEdgeThickness              = 2.0f * 0.25f;
        const float kSoftEdgeDashSize           = 2.0f;


        public ChiselBrush              brush;
        public BrushMesh                brushMesh;
        public ChiselTopologySelection  selection;

        // TODO: figure out a better place for these
        // These are temporary helpers to allow us to remap selection when modifying a brushMesh 
        // (so that selected vertices,edges, polygons still point to the 'correct' items after we removed something)
        public int[] vertexRemap;
        public int[] edgeRemap;
        public int[] polygonRemap;


        #region Selection Methods
        public void SelectVertex(int vertexIndex, SelectionType selectionType)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            SelectVertex(brushMesh, selection, vertexIndex, selectionType);
            UpdateSelectionCenter();
        }

        public static void SelectVertex(BrushMesh brushMesh, ChiselTopologySelection selection, int vertexIndex, SelectionType selectionType)
        {
            if (vertexIndex < 0 ||
                brushMesh.vertices == null ||
                vertexIndex >= brushMesh.vertices.Length)
                return;

            bool additive = true;
            switch (selectionType)
            {
                case SelectionType.Replace: { additive = true; selection.Clear(); break; }
                case SelectionType.Additive: { additive = true; break; }
                case SelectionType.Subtractive: { additive = false; break; }
            }

            if (additive)
            {
                selection.selectedVertices.Add(vertexIndex);

                var halfEdges = brushMesh.halfEdges;
                for (int edgeIndex = 0; edgeIndex < halfEdges.Length; edgeIndex++)
                {
                    var twinIndex = halfEdges[edgeIndex].twinIndex;
                    if (halfEdges[edgeIndex].vertexIndex == vertexIndex)
                    {
                        if (selection.selectedVertices.Contains(halfEdges[twinIndex].vertexIndex))
                            selection.selectedEdges.Add(edgeIndex);
                    }
                    if (halfEdges[twinIndex].vertexIndex == vertexIndex)
                    {
                        if (selection.selectedVertices.Contains(halfEdges[edgeIndex].vertexIndex))
                            selection.selectedEdges.Add(edgeIndex);
                    }
                }

                var polygons = brushMesh.polygons;
                for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
                {
                    if (!brushMesh.IsVertexIndexPartOfPolygon(polygonIndex, vertexIndex))
                        continue;

                    ref var polygon = ref polygons[polygonIndex];
                    var firstEdge = polygon.firstEdge;
                    var edgeCount = polygon.edgeCount;
                    var lastEdge = firstEdge + edgeCount;

                    bool allSelected = true;
                    for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                    {
                        var twinIndex = halfEdges[edgeIndex].twinIndex;
                        if (!selection.selectedEdges.Contains(edgeIndex) &&
                            !selection.selectedEdges.Contains(twinIndex))
                        {
                            allSelected = false;
                            break;
                        }
                    }
                    if (allSelected)
                        selection.selectedPolygons.Add(polygonIndex);
                }
            } else
            {
                selection.selectedVertices.Remove(vertexIndex);
                var allSelectedEdges    = selection.selectedEdges.ToArray();
                var allSelectedPolygons = selection.selectedPolygons.ToArray();
                var halfEdges           = brushMesh.halfEdges;
                foreach (var edgeIndex in allSelectedEdges)
                {
                    var twinIndex = halfEdges[edgeIndex].twinIndex;
                    if (halfEdges[edgeIndex].vertexIndex == vertexIndex) { selection.selectedEdges.Remove(edgeIndex); }
                    if (halfEdges[twinIndex].vertexIndex == vertexIndex) { selection.selectedEdges.Remove(edgeIndex); }
                }
                foreach (var polygonIndex in allSelectedPolygons)
                {
                    if (brushMesh.IsVertexIndexPartOfPolygon(polygonIndex, vertexIndex))
                    {
                        selection.selectedPolygons.Remove(polygonIndex);
                    }
                }
            }
        }

        public void SelectEdge(int currEdge, SelectionType selectionType)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            SelectEdge(brushMesh, selection, currEdge, selectionType);
            UpdateSelectionCenter();
        }

        public static void SelectEdge(BrushMesh brushMesh, ChiselTopologySelection selection, int currEdge, SelectionType selectionType)
        {
            if (currEdge < 0 ||
                brushMesh.halfEdges == null ||
                currEdge >= brushMesh.halfEdges.Length)
                return;

            var twinEdge        = brushMesh.halfEdges[currEdge].twinIndex;
            var currVertexIndex = brushMesh.halfEdges[currEdge].vertexIndex;
            var prevVertexIndex = brushMesh.halfEdges[twinEdge].vertexIndex;

            bool additive = true;
            switch (selectionType)
            {
                case SelectionType.Replace: { additive = true; selection.Clear(); break; }
                case SelectionType.Additive: { additive = true; break; }
                case SelectionType.Subtractive: { additive = false; break; }
            }

            if (additive)
            {
                selection.selectedVertices.Add(currVertexIndex);
                selection.selectedVertices.Add(prevVertexIndex);
                selection.selectedEdges.Add(currEdge);
                selection.selectedEdges.Add(twinEdge);

                var polygons    = brushMesh.polygons;
                var halfEdges   = brushMesh.halfEdges;
                for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
                {
                    if (!brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, currEdge) &&
                        !brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, twinEdge))
                        continue;

                    ref var polygon = ref polygons[polygonIndex];
                    var firstEdge = polygon.firstEdge;
                    var edgeCount = polygon.edgeCount;
                    var lastEdge = firstEdge + edgeCount;

                    bool allSelected = true;
                    for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                    {
                        var twinIndex = halfEdges[edgeIndex].twinIndex;
                        if (!selection.selectedEdges.Contains(edgeIndex) &&
                            !selection.selectedEdges.Contains(twinIndex))
                        {
                            allSelected = false;
                            break;
                        }
                    }
                    if (allSelected)
                        selection.selectedPolygons.Add(polygonIndex);
                }
            } else
            {
                selection.selectedEdges.Remove(currEdge);
                selection.selectedEdges.Remove(twinEdge);

                // Deselect all polygons that have this edge
                var allSelectedPolygons = selection.selectedPolygons.ToArray();
                foreach (var polygonIndex in allSelectedPolygons)
                {
                    if (brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, currEdge) ||
                        brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, twinEdge))
                        selection.selectedPolygons.Remove(polygonIndex);
                }

                // Only deselect vertices if they're not selected by another selected edge or selected polygon
                if (!IsVertexIndirectlySelected(brushMesh, selection, currVertexIndex)) selection.selectedVertices.Remove(currVertexIndex);
                if (!IsVertexIndirectlySelected(brushMesh, selection, prevVertexIndex)) selection.selectedVertices.Remove(prevVertexIndex);
            }
        }

        public void SelectPolygon(int polygonIndex, SelectionType selectionType)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            SelectPolygon(brushMesh, selection, polygonIndex, selectionType);
            UpdateSelectionCenter();
        }

        public static void SelectPolygon(BrushMesh brushMesh, ChiselTopologySelection selection, int polygonIndex, SelectionType selectionType)
        {
            if (polygonIndex < 0 ||
                brushMesh.polygons == null ||
                polygonIndex >= brushMesh.polygons.Length)
                return;

            ref var polygon = ref brushMesh.polygons[polygonIndex];
            var firstEdge   = polygon.firstEdge;
            var edgeCount   = polygon.edgeCount;
            var lastEdge    = firstEdge + edgeCount;

            bool additive = true;
            switch (selectionType)
            {
                case SelectionType.Replace: { additive = true; selection.Clear(); break; }
                case SelectionType.Additive: { additive = true; break; }
                case SelectionType.Subtractive: { additive = false; break; }
            }

            if (additive)
            {
                selection.selectedPolygons.Add(polygonIndex);
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    selection.selectedEdges.Add(e);
                    selection.selectedEdges.Add(brushMesh.halfEdges[e].twinIndex);
                    selection.selectedVertices.Add(brushMesh.halfEdges[e].vertexIndex);
                }
            } else
            {
                var halfEdges               = brushMesh.halfEdges;
                var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;

                selection.selectedPolygons.Remove(polygonIndex);
                for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                {
                    var twinIndex = halfEdges[edgeIndex].twinIndex;
                    var twinPolygonIndex = halfEdgePolygonIndices[twinIndex];

                    selection.selectedEdges.Remove(edgeIndex);
                    selection.selectedEdges.Remove(twinIndex);
                    selection.selectedPolygons.Remove(twinPolygonIndex);
                }

                for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                {
                    var vertexIndex = halfEdges[edgeIndex].vertexIndex;
                    if (!IsVertexIndirectlySelected(brushMesh, selection, vertexIndex)) selection.selectedVertices.Remove(vertexIndex);
                }
            }
        }

        public static bool IsVertexIndirectlySelected(BrushMesh brushMesh, ChiselTopologySelection selection, int vertexIndex)
        {
            var halfEdges = brushMesh.halfEdges;
            // Check if vertex is part of edge
            foreach (var edgeIndex in selection.selectedEdges)
            {
                var twinIndex = halfEdges[edgeIndex].twinIndex;
                if (halfEdges[edgeIndex].vertexIndex == vertexIndex ||
                    halfEdges[twinIndex].vertexIndex == vertexIndex)
                    return true;
            }

            // Check if vertex is part of polygon
            foreach (var polygonIndex in selection.selectedPolygons)
            {
                if (brushMesh.IsVertexIndexPartOfPolygon(polygonIndex, vertexIndex))
                    return true;
            }
            return false;
        }

        void ClearSelection()
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Deselect all");
            selection.Clear();
        }

        void SelectAll()
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Select all");
            selection.Clear();

            for (int p = 0; p < brushMesh.polygons.Length; p++)
                selection.selectedPolygons.Add(p);

            for (int e = 0; e < brushMesh.halfEdges.Length; e++)
                selection.selectedEdges.Add(e);

            for (int v = 0; v < brushMesh.vertices.Length; v++)
                selection.selectedVertices.Add(v);
        }


        static public void RemapSelection(BrushMesh brushMesh, ChiselTopologySelection selection, int[] vertexRemap, int[] edgeRemap, int[] polygonRemap)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Remapping selection");

            if (vertexRemap  == null &&
                edgeRemap    == null &&
                polygonRemap == null)
            {
                // since we don't have anything to remap, assume we didn't actually remove anything (in which case our selection *should* still be valid?
                return;
            }

            var halfEdges = brushMesh.halfEdges;

            var prevVertexSelection     = selection.selectedVertices.ToArray();
            var prevEdgeSelection       = selection.selectedEdges.ToArray();
            var prevPolygonSelection    = selection.selectedPolygons.ToArray();
            selection.Clear();


            if (vertexRemap != null)
            {
                for (int v = 0; v < prevVertexSelection.Length; v++)
                {
                    var vertexIndex = vertexRemap[prevVertexSelection[v]];
                    if (vertexIndex == -1)
                        continue;
                    SelectVertex(brushMesh, selection, vertexIndex, SelectionType.Additive);
                }
            }

            if (edgeRemap != null)
            {
                for (int e = 0; e < prevEdgeSelection.Length; e++)
                {
                    var edgeIndex = edgeRemap[prevEdgeSelection[e]];
                    if (edgeIndex == -1)
                        continue;
                    SelectEdge(brushMesh, selection, edgeIndex, SelectionType.Additive);
                }
            }

            if (polygonRemap != null)
            {
                for (int p = 0; p < prevPolygonSelection.Length; p++)
                {
                    var polygonIndex = polygonRemap[prevPolygonSelection[p]];
                    if (polygonIndex == -1)
                        continue;
                    SelectPolygon(brushMesh, selection, polygonIndex, SelectionType.Additive);
                }
            }
        }
        #endregion


        #region Modification Methods
        struct EdgeVertices { public int vertexIndex1; public int vertexIndex2; }

        void DeleteSelectedEdges()
        {
            // TODO: handle actual deletion of vertices

            var halfEdges = brushMesh.halfEdges;

            // since edge indices will change every time we remove one edge, we need to
            // find our edge indirectly every time after we delete one, we do this
            // using the vertex indices which will not change
            var foundEdges = new List<EdgeVertices>();
            var skipEdges = new HashSet<int>();
            foreach (var e in selection.selectedEdges)
            {
                if (skipEdges.Contains(e))
                    continue;
                var twin = halfEdges[e].twinIndex;
                skipEdges.Add(twin);
                skipEdges.Add(e);
                foundEdges.Add(new EdgeVertices()
                {
                    vertexIndex1 = halfEdges[twin].vertexIndex,
                    vertexIndex2 = halfEdges[e].vertexIndex
                });
            }

            Undo.RecordObject(brush, "Delete selected edges");
            for (int i = 0; i < foundEdges.Count; i++)
            {
                var realEdge = brushMesh.FindEdgeByVertexIndices(foundEdges[i].vertexIndex1, foundEdges[i].vertexIndex2);
                if (realEdge == -1)
                    continue;
                brushMesh.RemoveEdge(realEdge);
            }
            ClearSelection();
        }

        void MoveSelectedVertices(Vector3 offset)
        {
            if (offset.sqrMagnitude == 0)
                return;

            var vertices = brushMesh.vertices;
            Undo.RecordObject(brush, "Changed shape of Brush");
            foreach (var v in selection.selectedVertices)
                vertices[v] += (float3)offset;
            brushMesh.CalculatePlanes();
        }

        void SplitPolygonBetweenTwoVertices(int startEdge, int endEdge, Vector3 prevNewVertex, Vector3 firstNewVertex)
        {
            var twin = brushMesh.halfEdges[startEdge].twinIndex;
            var vertexIndex1    = brushMesh.halfEdges[startEdge].vertexIndex;
            var vertexIndex2    = brushMesh.halfEdges[twin].vertexIndex;

            var polygonIndexA1  = brushMesh.halfEdgePolygonIndices[startEdge];
            var polygonIndexA2  = brushMesh.halfEdgePolygonIndices[twin];

            var polygonIndexB1  = brushMesh.halfEdgePolygonIndices[endEdge];
            var polygonIndexB2  = brushMesh.halfEdgePolygonIndices[brushMesh.halfEdges[endEdge].twinIndex];

            int sharedPolygonIndex = (polygonIndexA1 == polygonIndexB1 || polygonIndexA1 == polygonIndexB2) ? polygonIndexA1 :
                                     (polygonIndexA2 == polygonIndexB1 || polygonIndexA2 == polygonIndexB2) ? polygonIndexA2 : 
                                     -1;

            if (sharedPolygonIndex == -1)
            {
                Debug.Assert(false, "Invalid input, both edges need to be on same polygon");
                return;
            }

            Undo.RecordObjects(new UnityEngine.Object[] { brush, ChiselTopologySelectionManager.Selection }, "Split polygon between two vertices");
            var prevNewVertexIndex = brushMesh.FindVertexIndexOfVertex(prevNewVertex);
            if (prevNewVertexIndex == -1)
            {
                prevNewVertexIndex = brushMesh.vertices.Length;
                ArrayUtility.Add(ref brushMesh.vertices, prevNewVertex);
                brushMesh.SplitHalfEdge(endEdge, prevNewVertexIndex, out int newEdgeIndex2);
                startEdge = brushMesh.FindEdgeByVertexIndices(vertexIndex1, vertexIndex2);
            }

            var firstNewVertexIndex = brushMesh.FindVertexIndexOfVertex(firstNewVertex);
            if (firstNewVertexIndex == -1)
            {
                firstNewVertexIndex = brushMesh.vertices.Length;
                ArrayUtility.Add(ref brushMesh.vertices, firstNewVertex);
                brushMesh.SplitHalfEdge(startEdge, firstNewVertexIndex, out int newEdgeIndex1);
            }

            SplitPolygonBetweenTwoVertices(sharedPolygonIndex, prevNewVertexIndex, firstNewVertexIndex, registerUndo: false);
        }

        void SplitPolygonBetweenTwoVertices(int polygonIndex, int vertexIndex1, int vertexIndex2, bool registerUndo = true)
        {
            if (registerUndo)
                Undo.RecordObjects(new UnityEngine.Object[] { brush, ChiselTopologySelectionManager.Selection }, "Split polygon between two vertices");
            var indexIn = brushMesh.FindPolygonEdgeByVertexIndex(polygonIndex, vertexIndex1);
            var indexOut = brushMesh.FindPolygonEdgeByVertexIndex(polygonIndex, vertexIndex2);

            brushMesh.SplitPolygon(polygonIndex, indexOut, indexIn);
            var edgeIndex = brushMesh.FindEdgeByVertexIndices(vertexIndex1, vertexIndex2);
            if (edgeIndex != -1)
            {
                SelectEdge(edgeIndex, SelectionType.Replace);
            } else
                ClearSelection();
        }

        void CombinePolygonsOnEdge(int edgeIndex)
        {
            ClearSelection();
            Undo.RecordObject(brush, "Combine polygons by removing edge");
            brushMesh.RemoveEdge(edgeIndex);
        }
        #endregion


        // Rebuild all mesh specific computations
        public void Rebuild()
        {
            FindSoftEdges();

            if (!IsValidBrush())
                return;

            EnsureCorrectSizes();

            for (int p = 0; p < polygonCenters.Length; p++)
                polygonCenters[p] = brushMesh.GetPolygonCentroid(p);

            UpdateSelectionCenter();
        }

        bool IsValidBrush()
        {
            if (brushMesh == null ||
                brushMesh.vertices == null || brushMesh.vertices.Length == 0 ||
                brushMesh.halfEdges == null || brushMesh.halfEdges.Length == 0 ||
                brushMesh.polygons == null || brushMesh.polygons.Length == 0)
                return false;
            return true;
        }


        // TODO: improve naming
        struct SoftEdge
        {
            public int vertexIndex1;
            public int vertexIndex2;
            public int afterPolygonIndexA;
            public int afterPolygonIndexB;

            // TODO: might want to put this somewhere else (order of struct is exactly wrong too)
            public Vector3 afterPolygonCenterA;
            public Vector3 afterPolygonCenterB;
            public Vector4 afterLocalPlaneA;
            public Vector4 afterLocalPlaneB;
        }

        public Color[]      vertexColors;
        public Color[]      edgeColors;
        public Color[]      polygonColors;
        public Color[]      softEdgeColors;

        public Vector3[]    polygonCenters;
        public Vector3      vertexSelectionCenter = Vector3.zero;

        static SoftEdge[]   softEdges;

        enum ItemState : byte
        {
            None,
            Frontfaced  = 1,

            Hovering    = 2,
            Active      = 4,
            Selected    = 8
        };

        // We don't want to allocate memory every frame, so we keep some 
        // temporary arrays and only resize them when necessary
        #region Allocations
        static ItemState[]  s_TempPolygonsState = null;
        static int[]        s_TempPolygonsIDs = null;
        static int          s_TempPolygonsIDCount = 0;
        static ItemState[]  s_TempVerticesState = null;
        static int[]        s_TempVerticesIDs = null;
        static int          s_TempVerticesIDCount = 0;
        static ItemState[]  s_TempEdgesState = null;
        static int[]        s_TempEdgesIDs = null;
        static int          s_TempEdgesIDCount = 0;
        static ItemState[]  s_TempSoftEdgesState = null;
        static int[]        s_TempSoftEdgesIDs = null;
        static int          s_TempSoftEdgesIDCount = 0;

        void EnsureCorrectSizes()
        {
            if (vertexColors == null ||
                vertexColors.Length != brushMesh.vertices.Length)
                vertexColors = new Color[brushMesh.vertices.Length];

            if (edgeColors == null ||
                edgeColors.Length != brushMesh.halfEdges.Length)
                edgeColors = new Color[brushMesh.halfEdges.Length];

            if (polygonColors == null ||
                polygonColors.Length != brushMesh.polygons.Length)
                polygonColors = new Color[brushMesh.polygons.Length];

            if (polygonCenters == null ||
                polygonCenters.Length != brushMesh.polygons.Length)
                polygonCenters = new Vector3[brushMesh.polygons.Length];

            if (softEdgeColors == null ||
                softEdgeColors.Length != softEdges.Length)
                softEdgeColors = new Color[softEdges.Length];
        }

        void EnsureTemporaryArraySize()
        {
            var polygons = brushMesh.polygons;
            var vertices = brushMesh.vertices;
            var halfEdges = brushMesh.halfEdges;

            if (s_TempPolygonsState == null ||
                s_TempPolygonsState.Length < polygons.Length)
            {
                s_TempPolygonsState = new ItemState[polygons.Length];
                s_TempPolygonsIDs = new int[polygons.Length];
            }

            if (s_TempVerticesState == null ||
                s_TempVerticesState.Length < vertices.Length)
            {
                s_TempVerticesState = new ItemState[vertices.Length];
                s_TempVerticesIDs = new int[vertices.Length];
            }

            if (s_TempEdgesState == null ||
                s_TempEdgesState.Length < halfEdges.Length)
            {
                s_TempEdgesState = new ItemState[halfEdges.Length];
                s_TempEdgesIDs = new int[halfEdges.Length];
            }

            if (s_TempSoftEdgesState == null ||
                s_TempSoftEdgesState.Length < softEdges.Length)
            {
                s_TempSoftEdgesState = new ItemState[softEdges.Length];
                s_TempSoftEdgesIDs = new int[softEdges.Length];
            }

            s_TempPolygonsIDCount = 0;
            s_TempVerticesIDCount = 0;
            s_TempEdgesIDCount = 0;
            s_TempSoftEdgesIDCount = 0;
        }
        #endregion

        #region UpdateControlIDs
        static SceneHandles.PositionHandleIDs s_TempPositionHandleIDs;

        internal static int s_PolygonHash = "Polygon".GetHashCode();
        internal static int s_VertexHash = "Vertex".GetHashCode();
        internal static int s_EdgeHash = "Edge".GetHashCode();
        internal static int s_SoftEdgeHash = "SoftEdge".GetHashCode();

        void UpdateControlIDs()
        {
            var polygons = brushMesh.polygons;
            var vertices = brushMesh.vertices;
            var halfEdges = brushMesh.halfEdges;

            SceneHandles.Initialize(ref s_TempPositionHandleIDs);

            s_TempPolygonsIDCount   = polygons.Length;
            s_TempVerticesIDCount   = vertices.Length;
            s_TempEdgesIDCount      = halfEdges.Length;
            s_TempSoftEdgesIDCount  = softEdges.Length;

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
                s_TempPolygonsIDs[p] = GUIUtility.GetControlID(s_PolygonHash, FocusType.Keyboard);

            for (int v = 0; v < s_TempVerticesIDCount; v++)
                s_TempVerticesIDs[v] = GUIUtility.GetControlID(s_VertexHash, FocusType.Keyboard);

            for (int e = 0; e < s_TempEdgesIDCount; e++)
                s_TempEdgesIDs[e] = (halfEdges[e].twinIndex > e) ? 0 : GUIUtility.GetControlID(s_EdgeHash, FocusType.Keyboard);

            for (int e = 0; e < s_TempSoftEdgesIDCount; e++)
                s_TempSoftEdgesIDs[e] = GUIUtility.GetControlID(s_SoftEdgeHash, FocusType.Keyboard);
        }
        #endregion


        public void UpdateSelectionCenter()
        {
            vertexSelectionCenter = Vector3.zero;
            if (selection.selectedVertices.Count == 0)
                return;

            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;

            var vertices = brushMesh.vertices;
            foreach (var v in selection.selectedVertices)
            {
                min.x = Mathf.Min(min.x, vertices[v].x);
                min.y = Mathf.Min(min.y, vertices[v].y);
                min.z = Mathf.Min(min.z, vertices[v].z);

                max.x = Mathf.Max(max.x, vertices[v].x);
                max.y = Mathf.Max(max.y, vertices[v].y);
                max.z = Mathf.Max(max.z, vertices[v].z);
            }

            vertexSelectionCenter = (min + max) * 0.5f;
        }

        // Finds all edges that have been created when optimizing the outline mesh.
        // This is possible when not all vertices on a polygon actually lie on the same plane and we're forced to split that polygon
        #region FindSoftEdges
        static List<int> s_TempPolygon1ToPolygon2 = new List<int>();
        static readonly List<ChiselBrushContainerAsset> brushContainers = new List<ChiselBrushContainerAsset>();
        void FindSoftEdges()
        {
            softEdges = new SoftEdge[0];
            brushContainers.Clear();
            if (!brush.GetUsedGeneratedBrushes(brushContainers))
                return;

            // TODO: for now, just assume we have one submesh
            var brushMeshes = (brushContainers == null || brushContainers.Count != 1) ? null : brushContainers[0].BrushMeshes;
            var afterBrushMesh = (brushMeshes == null || brushMeshes.Length == 0) ? null : brushMeshes[0];
            if (afterBrushMesh == null)
                return;

            var beforBrushMesh = brushMesh;

            var afterHalfEdges = afterBrushMesh.halfEdges;
            var beforeHalfEdges = beforBrushMesh.halfEdges;

            // If before and after have the same number of edges, then we don't have any soft edges
            if (afterHalfEdges.Length == beforeHalfEdges.Length)
                return;


            var afterPolygons = afterBrushMesh.polygons;
            var beforePolygons = beforBrushMesh.polygons;
            var afterHalfEdgePolygonIndices = afterBrushMesh.halfEdgePolygonIndices;
            var beforeHalfEdgePolygonIndices = beforBrushMesh.halfEdgePolygonIndices;

            var afterPlanes     = afterBrushMesh.planes;
            var afterVertices   = afterBrushMesh.vertices;

            s_TempPolygon1ToPolygon2.Clear();
            for (int i = 0; i < afterPolygons.Length; i++)
                s_TempPolygon1ToPolygon2.Add(-1);

            // TODO: optimize this
            // NOTE: assumes vertices are the same on both the original brushMesh and the optimized one
            var softEdgeList = new List<SoftEdge>();
            for (int afterEdgeA = 0; afterEdgeA < afterHalfEdges.Length; afterEdgeA++)
            {
                var afterPolygonIndexA = afterHalfEdgePolygonIndices[afterEdgeA];
                var afterPolygon = afterPolygons[afterPolygonIndexA];
                var afterEdgeB = afterPolygon.firstEdge + ((afterEdgeA + 1 - afterPolygon.firstEdge) % afterPolygon.edgeCount);
                var afterPolygonIndexB = afterHalfEdgePolygonIndices[afterHalfEdges[afterEdgeB].twinIndex];
                var afterVertexIndexA = afterHalfEdges[afterEdgeA].vertexIndex;
                var afterVertexIndexB = afterHalfEdges[afterEdgeB].vertexIndex;

                bool found = false;
                for (int beforeEdgeA = 0; beforeEdgeA < beforeHalfEdges.Length; beforeEdgeA++)
                {
                    var beforePolygonIndexA = beforeHalfEdgePolygonIndices[beforeEdgeA];
                    var beforePolygon = beforePolygons[beforePolygonIndexA];
                    var beforeEdgeB = beforePolygon.firstEdge + ((beforeEdgeA + 1 - beforePolygon.firstEdge) % beforePolygon.edgeCount);
                    var beforePolygonIndexB = beforeHalfEdgePolygonIndices[beforeHalfEdges[beforeEdgeB].twinIndex];
                    var beforeVertexIndexA = beforeHalfEdges[beforeEdgeA].vertexIndex;
                    var beforeVertexIndexB = beforeHalfEdges[beforeEdgeB].vertexIndex;

                    if (afterVertexIndexA != beforeVertexIndexA ||
                        afterVertexIndexB != beforeVertexIndexB)
                        continue;

                    s_TempPolygon1ToPolygon2[afterPolygonIndexA] = beforePolygonIndexA;
                    s_TempPolygon1ToPolygon2[afterPolygonIndexB] = beforePolygonIndexB;
                    found = true;
                    break;
                }

                if (found)
                    continue;

                // Skips half of the edges since they are identical
                if (afterVertexIndexA < afterVertexIndexB)
                    continue;

                softEdgeList.Add(new SoftEdge()
                {
                    vertexIndex1 = afterVertexIndexA,
                    vertexIndex2 = afterVertexIndexB,
                    afterPolygonIndexA = afterPolygonIndexA,
                    afterPolygonIndexB = afterPolygonIndexB,
                    afterPolygonCenterA = afterBrushMesh.GetPolygonCentroid(afterPolygonIndexA),
                    afterPolygonCenterB = afterBrushMesh.GetPolygonCentroid(afterPolygonIndexB),
                    afterLocalPlaneA = afterPlanes[afterPolygonIndexA],
                    afterLocalPlaneB = afterPlanes[afterPolygonIndexB]
                });
            }
            softEdges = softEdgeList.ToArray();
        }
        #endregion

        enum ClickState
        {
            None,
            Dragged,

            MouseDown,
            Clicked,
            DoubleClicked,
            MouseUp,

            Hover,


            DeselectAll,
            SelectAll,
            Delete
        }

        #region HandleClick

        void SetControl(Event evt, int id)
        {
            GUIUtility.hotControl = id;
            GUIUtility.keyboardControl = id;
            evt.Use();
            EditorGUIUtility.SetWantsMouseJumping((id == 0) ? 0 : 1);
        }

        bool CanUseMouseDown(Event evt, int id)
        {
            if (GUIUtility.hotControl != 0)
                return false;

            if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                (GUIUtility.keyboardControl != id || evt.button != 2))
                return false;
            return true;
        }

        bool CanUseMouseUp(Event evt, int id)
        {
            if (GUIUtility.hotControl != id || (evt.button != 0 && evt.button != 2))
                return false;
            return true;
        }

        const KeyCode           kCancelKey          = KeyCode.Escape;
        const EventModifiers    kKeyModifiers       = EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command;
        
        const string    kSelectAllCommand       = "SelectAll";
        const string    kSoftDeleteCommand      = "SoftDelete";

        const float     kEqualitySqrDistance    = 0.0001f;
        const float     kSnapSqrDistance        = 0.01f;
        
        int         clickCount          = 0;

        int         prevEdgeIndex       = 0;
        bool?       prevAlternateMode   = null;

        bool        secondVertex        = false;
        int         highlightEdgeIndex  = -1;
        Vector3     firstNewVertex;
        Vector3     prevNewVertex       = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);



        ClickState HandleClick(int id, bool captureControl = false)
        {
            if (id == 0)
                return ClickState.None;

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseMove:
                {
                    if (SceneHandleUtility.focusControl != id)
                        break;

                    prevAlternateMode = AlternateEditMode;
                    return ClickState.Hover;
                }

                case EventType.MouseDown:
                {
                    if (!CanUseMouseDown(evt, id))
                        break;

                    if (captureControl)
                        SetControl(evt, id);

                    // Unfortunately the clickCount is always 1 at mouseUp, so we need to remember it
                    clickCount = evt.clickCount;

                    prevAlternateMode = AlternateEditMode;
                    return ClickState.MouseDown;
                }

                case EventType.MouseDrag:
                {
                    // Once we start dragging, we're no longer clicking, so set it to 0
                    clickCount = 0;
                    if (GUIUtility.hotControl != id)
                        break;

                    return ClickState.Dragged;
                }

                case EventType.MouseUp:
                {
                    if (!CanUseMouseUp(evt, id))
                        break;

                    prevAlternateMode = null;
                    if (captureControl)
                    {
                        SetControl(evt, 0);
                        Snapping.ActiveAxes = Axes.XYZ;
                        SceneView.RepaintAll();
                    }

                    switch (clickCount)
                    {
                        default: return ClickState.MouseUp;
                        case 1:  return ClickState.Clicked;
                        case 2:  return ClickState.DoubleClicked;
                    }
                }

                case EventType.KeyDown:
                {
                    if (evt.keyCode == kCancelKey && ((evt.modifiers & kKeyModifiers) == EventModifiers.None))
                        evt.Use();
                    break;
                }

                case EventType.KeyUp:
                {
                    if (evt.keyCode == kCancelKey && ((evt.modifiers & kKeyModifiers) == EventModifiers.None))
                    {
                        evt.Use();
                        return ClickState.DeselectAll;
                    }
                    break;
                }

                case EventType.ValidateCommand:
                {
                    // TODO: shouldn't just hijack these commands, should only use them in the right toolmode
                    if (evt.commandName == kSelectAllCommand ||
                        evt.commandName == kSoftDeleteCommand)
                        evt.Use();
                    break;
                }

                case EventType.ExecuteCommand:
                {
                    // TODO: shouldn't just hijack these commands, should only use them in the right toolmode
                    if (evt.commandName == kSelectAllCommand)
                    {
                        evt.Use();
                        return ClickState.SelectAll;
                    }
                    if (evt.commandName == kSoftDeleteCommand)
                    {
                        evt.Use();
                        return ClickState.Delete;
                    }
                    break;
                }
            }

            var alternateEditMode = AlternateEditMode;
            if (prevAlternateMode.HasValue &&
                prevAlternateMode.Value == alternateEditMode)
                return ClickState.None;

            if (SceneHandleUtility.focusControl != id)
                return ClickState.None;

            prevAlternateMode = alternateEditMode;
            return ClickState.Hover;
        }
        #endregion

        #region Update Colors
        ItemState CalculateState(bool isHovering, bool isActive, bool isSelected)
        {
            return (isHovering ? ItemState.Hovering : ItemState.None) |
                   (isActive ? ItemState.Active : ItemState.None) |
                   (isSelected ? ItemState.Selected : ItemState.None);
        }

        ItemState CalculateState(bool isHovering, bool isActive, bool isSelected, bool isFrontFacing)
        {
            return (isHovering ? ItemState.Hovering : ItemState.None) |
                   (isActive ? ItemState.Active : ItemState.None) |
                   (isSelected ? ItemState.Selected : ItemState.None) |
                   (isFrontFacing ? ItemState.Frontfaced : ItemState.None);
        }

        bool IsPolygonFrontfacing(Vector3 polygonCenter, Vector4 localPlane, Vector3 cameraLocalPos, Vector3 cameraLocalForward, bool isCameraInsideOutline, bool isCameraOrthographic)
        {
            var localNormal = (Vector3)localPlane;
            if (isCameraInsideOutline ||
                localNormal.sqrMagnitude == 0)
                return false;

            var cosV = isCameraOrthographic ? Vector3.Dot(localNormal, cameraLocalForward) :
                                              Vector3.Dot(localNormal, (polygonCenter - cameraLocalPos));
            return (cosV < -0.0001f);
        }

        bool IsPointInsideOutline(Vector3 point)
        {
            brushContainers.Clear();
            if (!brush.GetUsedGeneratedBrushes(brushContainers))
                return false;

            // TODO: for now, just assume we have one submesh
            var brushMeshes = (brushContainers.Count != 1) ? null : brushContainers[0].BrushMeshes;
            var afterBrushMesh = (brushMeshes == null) ? null : brushMeshes[0];
            if (afterBrushMesh == null)
                return false;

            return afterBrushMesh.IsInsideOrOn(point);
        }

        void UpdateStateColors(Color[] colors, ItemState[] states, bool validState, Color backfacedColor, Color frontfacedColor, Color invalidColor)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var state = states[i];
                var isSelected = (state & ItemState.Selected) == ItemState.Selected;
                var isHovering = (state & ItemState.Hovering) == ItemState.Hovering;
                var isActive = (state & ItemState.Active) == ItemState.Active;
                var isBackfaced = (state & ItemState.Frontfaced) != ItemState.Frontfaced;
                var baseColor = (isBackfaced && !isActive && !isHovering) ? backfacedColor : frontfacedColor;
                colors[i] = !validState ? invalidColor : SceneHandles.StateColor(baseColor, SceneHandles.disabled, isSelected: (isActive || isSelected), isPreSelected: isHovering);
            }
        }

        public bool UpdateColors(Camera camera)
        {
            if (!IsValidBrush())
                return false;

            EnsureTemporaryArraySize();
            UpdateControlIDs();

            if (Event.current.type != EventType.Repaint)
                return true;

            var validState = brush.definition.ValidState;

            // TODO: improve polygon color and make it handle transparency (polygon rendering does not work with alpha?)
            // TODO: make it possible to modify these colors from a settings page
            #region Default Colors
            var FrontfacedPolygonColor = SceneHandles.yAxisColor;
            var BackfacedPolygonColor = new Color(FrontfacedPolygonColor.r, FrontfacedPolygonColor.g, FrontfacedPolygonColor.b, FrontfacedPolygonColor.a * SceneHandles.backfaceAlphaMultiplier);

            var FrontfacedColor = SceneHandles.zAxisColor;
            var BackfacedColor = new Color(FrontfacedColor.r, FrontfacedColor.g, FrontfacedColor.b, FrontfacedColor.a * SceneHandles.backfaceAlphaMultiplier);
            var InvalidColor = Color.red;
            #endregion

            var currentHotControl       = GUIUtility.hotControl;
            var currentFocusControl     = SceneHandleUtility.focusControl;
            var currDisabled            = SceneHandles.disabled;

            var cameraTransform         = camera.transform;
            var cameraLocalPos          = SceneHandles.inverseMatrix.MultiplyPoint(cameraTransform.position);
            var cameraLocalForward      = SceneHandles.inverseMatrix.MultiplyVector(cameraTransform.forward);
            var isCameraOrthographic    = camera.orthographic;
            var isCameraInsideOutline   = IsPointInsideOutline(cameraLocalPos);

            var polygons                = brushMesh.polygons;
            var planes                  = brushMesh.planes;
            var vertices                = brushMesh.vertices;
            var halfEdges               = brushMesh.halfEdges;
            var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;

            Array.Clear(s_TempPolygonsState, 0, polygons.Length);
            Array.Clear(s_TempVerticesState, 0, vertices.Length);
            Array.Clear(s_TempEdgesState, 0, halfEdges.Length);
            Array.Clear(s_TempSoftEdgesState, 0, softEdges.Length);

            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                var id = s_TempVerticesIDs[v];

                var isActive    = currentHotControl == id;
                var isHovering  = currentFocusControl == id;
                var isSelected  = selection.selectedVertices.Contains(v);

                var vertexState = CalculateState(isHovering, isActive, isSelected);

                s_TempVerticesState[v] = vertexState;
            }

            var currentHighlightEdge = (secondVertex && highlightEdgeIndex != -1) ? s_TempEdgesIDs[highlightEdgeIndex] : 0;
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0)
                    continue;

                var isActive    = (currentHotControl == id);
                var isHovering  = (currentFocusControl == id) || (currentHighlightEdge == id);
                var isSelected  = selection.selectedEdges.Contains(e);

                var edgeState   = CalculateState(isHovering, isActive, isSelected);

                var twin        = halfEdges[e].twinIndex;
                var currVertex  = halfEdges[e].vertexIndex;
                var prevVertex  = halfEdges[twin].vertexIndex;

                s_TempVerticesState[currVertex] |= edgeState;
                s_TempVerticesState[prevVertex] |= edgeState;

                s_TempEdgesState[e] |= edgeState;
                s_TempEdgesState[twin] |= edgeState;
            }

            for (int p = 0; p < polygons.Length; p++)
            {
                var id              = s_TempPolygonsIDs[p];

                var isHovering      = currentFocusControl == id;
                var isActive        = currentHotControl == id;
                var isSelected      = selection.selectedPolygons.Contains(p);
                var isFrontFacing   = IsPolygonFrontfacing(polygonCenters[p], planes[p], cameraLocalPos, cameraLocalForward, isCameraInsideOutline, isCameraOrthographic);

                var firstEdge   = polygons[p].firstEdge;
                var edgeCount   = polygons[p].edgeCount;
                var lastEdge    = firstEdge + edgeCount;

                var polygonState = CalculateState(isHovering, isActive, isSelected, isFrontFacing);

                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var v = halfEdges[e].vertexIndex;
                    var twin = halfEdges[e].twinIndex;
                    s_TempVerticesState[v] |= polygonState;
                    s_TempEdgesState[e]    |= polygonState;
                    s_TempEdgesState[twin] |= polygonState;
                }

                s_TempPolygonsState[p] = polygonState;
            }

            for (int s = 0; s < softEdges.Length; s++)
            {
                var id = s_TempSoftEdgesIDs[s];

                var afterPolygonIndexA  = softEdges[s].afterPolygonIndexA;
                var afterPolygonIndexB  = softEdges[s].afterPolygonIndexB;
                var beforePolygonIndexA = s_TempPolygon1ToPolygon2[afterPolygonIndexA];
                var beforePolygonIndexB = s_TempPolygon1ToPolygon2[afterPolygonIndexB];

                // We cannot use the original polygon for backfacing since it's probably pointing in another direction
                var beforePolygonAState = (beforePolygonIndexA < 0 || beforePolygonIndexA >= polygons.Length) ? ItemState.None : (s_TempPolygonsState[beforePolygonIndexA] & ~ItemState.Frontfaced);
                var beforePolygonBState = (beforePolygonIndexB < 0 || beforePolygonIndexB >= polygons.Length) ? ItemState.None : (s_TempPolygonsState[beforePolygonIndexB] & ~ItemState.Frontfaced);

                var afterPolygonAFrontFacing = IsPolygonFrontfacing(softEdges[s].afterPolygonCenterA, softEdges[s].afterLocalPlaneA, cameraLocalPos, cameraLocalForward, isCameraInsideOutline, isCameraOrthographic);
                var afterPolygonBFrontFacing = IsPolygonFrontfacing(softEdges[s].afterPolygonCenterB, softEdges[s].afterLocalPlaneB, cameraLocalPos, cameraLocalForward, isCameraInsideOutline, isCameraOrthographic);

                var isHovering = currentFocusControl == id;
                var isActive   = currentHotControl == id;
                var isSelected = false; // impossible

                var softEdgeState = CalculateState(isHovering, isActive, isSelected, afterPolygonAFrontFacing || afterPolygonBFrontFacing);

                s_TempSoftEdgesState[s] = beforePolygonAState | beforePolygonBState | softEdgeState;
            }

            UpdateStateColors(polygonColors, s_TempPolygonsState, validState, BackfacedColor, FrontfacedColor, InvalidColor);
            UpdateStateColors(edgeColors, s_TempEdgesState, validState, BackfacedColor, FrontfacedColor, InvalidColor);
            UpdateStateColors(vertexColors, s_TempVerticesState, validState, BackfacedColor, FrontfacedColor, InvalidColor);
            UpdateStateColors(softEdgeColors, s_TempSoftEdgesState, validState, BackfacedColor, FrontfacedColor, InvalidColor);
            return true;
        }
        #endregion


        #region Snapping
        void SnapVertex(Vector3 point, Vector3 vertex, ref Vector3 snappedPoint, ref float bestDist)
        {
            var sqrMagnitude = (vertex - point).sqrMagnitude;
            if (sqrMagnitude <= kSnapSqrDistance)
            {
                var magnitude = Mathf.Sqrt(sqrMagnitude);
                if (magnitude < bestDist)
                {
                    bestDist = magnitude;
                    snappedPoint = vertex;
                }
            }
        }

        // Snap a point to one of the 2 vertices of the given edge
        void SnapVertices(Vector3 point, Vector3 vertex1, Vector3 vertex2, ref Vector3 snappedPoint, ref float bestDist)
        {
            SnapVertex(point, vertex1, ref snappedPoint, ref bestDist);
            SnapVertex(point, vertex2, ref snappedPoint, ref bestDist);
        }

        // Snap a point on an edge where it intersects with the grid. 
        // Note that we're never allowed to snap to a point outside the edge
        void SnapToGridOnEdge(float3 point, int edgeIndex, ref Vector3 snappedPoint, ref float bestDist)
        {
            var vertices                = brushMesh.vertices;
            var halfEdges               = brushMesh.halfEdges;
            var planes                  = brushMesh.planes;
            var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;
            var twinIndex           = halfEdges[edgeIndex].twinIndex;
            var edgePolygonIndex    = halfEdgePolygonIndices[edgeIndex];
            var twinPolygonIndex    = halfEdgePolygonIndices[twinIndex];
            var edgeLocalPlane      = new Plane(planes[edgePolygonIndex].xyz, planes[edgePolygonIndex].w);
            var twinLocalPlane      = new Plane(planes[twinPolygonIndex].xyz, planes[twinPolygonIndex].w);
            var localToWorldMatrix  = brush.hierarchyItem.LocalToWorldMatrix;
            var worldToLocalMatrix  = brush.hierarchyItem.WorldToLocalMatrix;

            var vertex1 = vertices[halfEdges[edgeIndex].vertexIndex];
            var vertex2 = vertices[halfEdges[twinIndex].vertexIndex];

            var grid    = UnitySceneExtensions.Grid.defaultGrid;
            var xAxis   = (float3)worldToLocalMatrix.MultiplyVector(grid.Right);
            var yAxis   = (float3)worldToLocalMatrix.MultiplyVector(grid.Up);
            var zAxis   = (float3)worldToLocalMatrix.MultiplyVector(grid.Forward);
            var center  = (float3)worldToLocalMatrix.MultiplyPoint(grid.Center);
            var spacing = grid.Spacing;

            var edgeRay = new Ray(vertex1, math.normalize(vertex2 - vertex1));

            var snapAxis = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, edgeLocalPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, edgeLocalPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, edgeLocalPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;

            if (Mathf.Abs(Vector3.Dot(xAxis, twinLocalPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, twinLocalPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, twinLocalPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;

            var gridPoint = point - center;
            var posX = Vector3.Dot(xAxis, gridPoint);
            var posY = Vector3.Dot(yAxis, gridPoint);
            var posZ = Vector3.Dot(zAxis, gridPoint);

            if ((snapAxis & Axis.X) != Axis.None)
            {
                var axis = xAxis;
                var axisPos = posX;
                var offset = (posY * yAxis) + (posZ * zAxis) + center;
                var axisSpacing = spacing.x;

                var min = Vector3.Dot(axis, vertex1 - center);
                var max = Vector3.Dot(axis, vertex2 - center);
                if (min > max) { var t = min; min = max; max = t; }

                var floor = Mathf.Floor(axisPos / axisSpacing) * axisSpacing;
                if (floor > min && floor < max)
                {
                    var axisPlane = new Plane(axis, (floor * axis) + offset);
                    if (axisPlane.Raycast(edgeRay, out float rayDistance))
                    {
                        var snapPoint = (float3)edgeRay.GetPoint(rayDistance);
                        var magnitude = math.length(snapPoint - point);
                        if (magnitude < bestDist)
                        {
                            bestDist = magnitude;
                            snappedPoint = snapPoint;
                        }
                    }
                }

                var ceil = Mathf.Ceil(axisPos / axisSpacing) * axisSpacing;
                if (ceil > min && ceil < max)
                {
                    var axisPlane = new Plane(axis, (ceil * axis) + offset);
                    if (axisPlane.Raycast(edgeRay, out float rayDistance))
                    {
                        var snapPoint = (float3)edgeRay.GetPoint(rayDistance);
                        var magnitude = math.length(snapPoint - point);
                        if (magnitude < bestDist)
                        {
                            bestDist = magnitude;
                            snappedPoint = snapPoint;
                        }
                    }
                }
            }

            if ((snapAxis & Axis.Y) != Axis.None)
            {
                var axis = yAxis;
                var axisPos = posY;
                var offset = (posX * xAxis) + (posZ * zAxis) + center;
                var axisSpacing = spacing.y;

                var min = Vector3.Dot(axis, vertex1 - center);
                var max = Vector3.Dot(axis, vertex2 - center);
                if (min > max) { var t = min; min = max; max = t; }

                var floor = Mathf.Floor(axisPos / axisSpacing) * axisSpacing;
                if (floor > min && floor < max)
                {
                    var axisPlane = new Plane(axis, (floor * axis) + offset);
                    if (axisPlane.Raycast(edgeRay, out float rayDistance))
                    {
                        var snapPoint = (float3)edgeRay.GetPoint(rayDistance);
                        var magnitude = math.length(snapPoint - point);
                        if (magnitude < bestDist)
                        {
                            bestDist = magnitude;
                            snappedPoint = snapPoint;
                        }
                    }
                }

                var ceil = Mathf.Ceil(axisPos / axisSpacing) * axisSpacing;
                if (ceil > min && ceil < max)
                {
                    var axisPlane = new Plane(axis, (ceil * axis) + offset);
                    if (axisPlane.Raycast(edgeRay, out float rayDistance))
                    {
                        var snapPoint = (float3)edgeRay.GetPoint(rayDistance);
                        var magnitude = math.length(snapPoint - point);
                        if (magnitude < bestDist)
                        {
                            bestDist = magnitude;
                            snappedPoint = snapPoint;
                        }
                    }
                }
            }

            if ((snapAxis & Axis.Z) != Axis.None)
            {
                var axis = zAxis;
                var axisPos = posZ;
                var offset = (posX * xAxis) + (posY * yAxis) + center;
                var axisSpacing = spacing.z;

                var min = Vector3.Dot(axis, vertex1 - center);
                var max = Vector3.Dot(axis, vertex2 - center);
                if (min > max) { var t = min; min = max; max = t; }

                var floor = Mathf.Floor(axisPos / axisSpacing) * axisSpacing;
                if (floor > min && floor < max)
                {
                    var axisPlane = new Plane(axis, (floor * axis) + offset);
                    if (axisPlane.Raycast(edgeRay, out float rayDistance))
                    {
                        var snapPoint = (float3)edgeRay.GetPoint(rayDistance);
                        var magnitude = math.length(snapPoint - point);
                        if (magnitude < bestDist)
                        {
                            bestDist = magnitude;
                            snappedPoint = snapPoint;
                        }
                    }
                }

                var ceil = Mathf.Ceil(axisPos / axisSpacing) * axisSpacing;
                if (ceil > min && ceil < max)
                {
                    var axisPlane = new Plane(axis, (ceil * axis) + offset);
                    if (axisPlane.Raycast(edgeRay, out float rayDistance))
                    {
                        var snapPoint = (float3)edgeRay.GetPoint(rayDistance);
                        var magnitude = math.length(snapPoint - point);
                        if (magnitude < bestDist)
                        {
                            bestDist = magnitude;
                            snappedPoint = snapPoint;
                        }
                    }
                }
            }
        }
        #endregion

        #region Snapped intersection point on edges
        void FindClosestEdgeFromEdge(int polygonIndex, int edgeIndex, int twinIndex, ref int closestEdgeIndex, ref float closestEdgeDistance)
        {
            var vertices    = brushMesh.vertices;
            var polygons    = brushMesh.polygons;
            var halfEdges   = brushMesh.halfEdges;
            var firstEdge   = polygons[polygonIndex].firstEdge;
            var edgeCount   = polygons[polygonIndex].edgeCount;
            var lastEdge    = firstEdge + edgeCount;
            for (int e0 = lastEdge - 1, e1 = firstEdge; e1 < lastEdge; e0 = e1, e1++)
            {
                var vertex0 = vertices[halfEdges[e0].vertexIndex];
                var vertex1 = vertices[halfEdges[e1].vertexIndex];

                if (e1 == edgeIndex ||
                    e1 == twinIndex)
                    continue;

                var distance = HandleUtility.DistanceToLine(vertex0, vertex1);
                if (distance >= closestEdgeDistance)
                    continue;

                closestEdgeDistance = distance;
                var twin = halfEdges[e1].twinIndex;
                if (twin > e1)
                    closestEdgeIndex = twin;
                else
                    closestEdgeIndex = e1;
            }
        }

        void FindClosestEdgeFromVertex(int polygonIndex, int vertexIndex, ref int closestEdgeIndex, ref float closestEdgeDistance)
        {
            var vertices    = brushMesh.vertices;
            var polygons    = brushMesh.polygons;
            var halfEdges   = brushMesh.halfEdges;
            var firstEdge   = polygons[polygonIndex].firstEdge;
            var edgeCount   = polygons[polygonIndex].edgeCount;
            var lastEdge    = firstEdge + edgeCount;
            for (int e0 = lastEdge - 1, e1 = firstEdge; e1 < lastEdge; e0 = e1, e1++)
            {
                var vertexIndex0 = halfEdges[e0].vertexIndex;
                var vertexIndex1 = halfEdges[e1].vertexIndex;

                if (vertexIndex == vertexIndex0 ||
                    vertexIndex == vertexIndex1)
                    continue;

                var vertex0 = vertices[vertexIndex0];
                var vertex1 = vertices[vertexIndex1];

                var distance = HandleUtility.DistanceToLine(vertex0, vertex1);
                if (distance >= closestEdgeDistance)
                    continue;

                closestEdgeDistance = distance;
                var twin = halfEdges[e1].twinIndex;
                if (twin > e1)
                    closestEdgeIndex = twin;
                else
                    closestEdgeIndex = e1;
            }
        }

        void FindSnappedPointOnEdge(int edgeIndex)
        {
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;
            var twinIndex   = halfEdges[edgeIndex].twinIndex;
            var vertex1     = vertices[halfEdges[edgeIndex].vertexIndex];
            var vertex2     = vertices[halfEdges[twinIndex].vertexIndex];

            float bestDist = float.PositiveInfinity;
            var newVertex = HandleUtility.ClosestPointToPolyLine(vertex1, vertex2);
            Vector3 snapPoint = newVertex;
            SnapToGridOnEdge(newVertex, edgeIndex, ref snapPoint, ref bestDist);
            SnapVertices(newVertex, vertex1, vertex2, ref snapPoint, ref bestDist);
            newVertex = snapPoint;

            if (prevEdgeIndex == edgeIndex &&
                (prevNewVertex - newVertex).sqrMagnitude <= kEqualitySqrDistance)
                return;

            prevEdgeIndex = edgeIndex;
            prevNewVertex = newVertex;
            if (Event.current.type != EventType.Repaint)
                SceneView.RepaintAll();
        }

        void FindSnappedPointOnVertex(int vertexIndex)
        {
            var vertices = brushMesh.vertices;
            var newVertex = vertices[vertexIndex];
            if (math.lengthsq((float3)prevNewVertex - newVertex) <= kEqualitySqrDistance)
                return;

            prevEdgeIndex = brushMesh.FindAnyHalfEdgeWithVertexIndex(vertexIndex);
            prevNewVertex = newVertex;
            if (Event.current.type != EventType.Repaint)
                SceneView.RepaintAll();
        }

        void UpdateEdgeHoverPointOnEdge(int edgeIndex)
        {
            highlightEdgeIndex = -1;
            var halfEdges               = brushMesh.halfEdges;
            var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;
            if (!secondVertex)
            {
                FindSnappedPointOnEdge(edgeIndex);
                return;
            }

            var twinIndex        = halfEdges[edgeIndex].twinIndex;
            var polygonIndex     = halfEdgePolygonIndices[edgeIndex];
            var twinPolygonIndex = halfEdgePolygonIndices[twinIndex];
            int closestEdgeIndex = -1;

            float closestEdgeDistance = float.PositiveInfinity;
            FindClosestEdgeFromEdge(polygonIndex,     edgeIndex, twinIndex, ref closestEdgeIndex, ref closestEdgeDistance);
            FindClosestEdgeFromEdge(twinPolygonIndex, edgeIndex, twinIndex, ref closestEdgeIndex, ref closestEdgeDistance);

            if (closestEdgeIndex == -1)
                return;

            highlightEdgeIndex = closestEdgeIndex;
            FindSnappedPointOnEdge(highlightEdgeIndex);
        }

        void UpdateEdgeHoverPointOnVertex(int vertexIndex)
        {
            highlightEdgeIndex = -1;
            var polygons = brushMesh.polygons;

            if (!secondVertex)
            {
                FindSnappedPointOnVertex(vertexIndex);
                return;
            }

            int     closestEdgeIndex    = -1;
            float   closestEdgeDistance = float.PositiveInfinity;
            for (int p = 0; p < polygons.Length; p++)
            {
                if (!brushMesh.IsVertexIndexPartOfPolygon(p, vertexIndex))
                    continue;
                FindClosestEdgeFromVertex(p, vertexIndex, ref closestEdgeIndex, ref closestEdgeDistance);
            }

            if (closestEdgeIndex == -1)
                return;

            highlightEdgeIndex = closestEdgeIndex;
            FindSnappedPointOnEdge(highlightEdgeIndex);
        }

        void RenderHoverPoint()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // TODO: highlight what we're snapping with (grid, vertex, edges)

            SceneHandles.RenderBorderedCircle(prevNewVertex, HandleUtility.GetHandleSize(prevNewVertex) * SceneHandles.kPointScale);
            if (secondVertex)
            {
                SceneHandles.RenderBorderedCircle(firstNewVertex, HandleUtility.GetHandleSize(firstNewVertex) * SceneHandles.kPointScale);
                SceneHandles.DrawLine(firstNewVertex, prevNewVertex);
            }
        }
        #endregion


        bool AlternateEditMode { get { return Event.current.shift; } }

        const float kAlignmentEpsilon = 0.001f;

        bool HaveFocusOnHalfEdgeOrVertex
        {
            get
            {
                // TODO: handle this in a more efficient way
                var currentFocusControl = SceneHandleUtility.focusControl;
                for (int e = 0; e < s_TempEdgesIDCount; e++)
                {
                    var id = s_TempEdgesIDs[e];
                    if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges

                    if (currentFocusControl != id)
                        continue;

                    return true;
                }
                for (int e = 0; e < s_TempVerticesIDCount; e++)
                {
                    var id = s_TempVerticesIDs[e];

                    if (currentFocusControl != id)
                        continue;

                    return true;
                }
                return false;
            }
        }

        bool HaveFocusOnPolygon
        {
            get
            {
                // TODO: handle this in a more efficient way
                var currentFocusControl = SceneHandleUtility.focusControl;
                for (int p = 0; p < s_TempPolygonsIDCount; p++)
                {
                    if (currentFocusControl != s_TempPolygonsIDs[p])
                        continue;

                    return true;
                }
                return false;
            }
        }

        // Do not use polygons when they are camera aligned, or when their normal is zero (zero area)
        bool IsPolygonCameraAligned(int polygonIndex, Vector3 cameraDirection, bool isOutlineInsideOut)
        {
            var normal = isOutlineInsideOut ? -brushMesh.planes[polygonIndex].xyz
                                            :  brushMesh.planes[polygonIndex].xyz;
            return math.lengthsq(normal) == 0 ||
                   math.abs(math.dot(cameraDirection, normal)) > 1 - kAlignmentEpsilon;
        }

        public void RenderOutline(Vector3 cameraDirection, bool isCameraOrtho, bool isOutlineInsideOut)
        {
            var evt = Event.current;
            if (evt.type != EventType.Repaint &&
                evt.type != EventType.Layout)
                return;

            var currentFocusControl     = SceneHandleUtility.focusControl;
            var inCreateEdgeEditMode    = InCreateEdgeEditMode;

            var polygons    = brushMesh.polygons;
            var planes      = brushMesh.planes;
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;

            // Render the vertices 
            for (int v = 0; v < vertices.Length; v++)
            {
                var id = s_TempVerticesIDs[v];

                // TODO: instead of hardcoding this to this edit-mode, have some way to turn rendering off for 
                //       a vertex or multiple vertices and use that from the editmode
                if (inCreateEdgeEditMode &&
                    currentFocusControl == id)
                    continue;

                var position    = vertices[v];
                var handleSize  = UnityEditor.HandleUtility.GetHandleSize(position) * SceneHandles.kPointScale;

                SceneHandles.color = vertexColors[v];
                SceneHandles.OutlinedDotHandleCap(id, position, Quaternion.identity, handleSize, evt.type);
            }

            // Render half-edges, set cursor & register control
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges

                var twin        = halfEdges[e].twinIndex;
                var vertex1     = vertices[halfEdges[e].vertexIndex];
                var vertex2     = vertices[halfEdges[twin].vertexIndex];

                SceneHandles.color = edgeColors[e];
                if (currentFocusControl == id)
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kHighlightedEdgeThickness);
                else
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kEdgeThickness);

                // Under certain circumstances, like when an edge is camera aligned in ortho mode, 
                // we want the edge to be treated as a polygon instead. We handle that here
                if (HandleEdgeAsPolygon(id, e, cameraDirection, isCameraOrtho))
                    continue;

                SceneHandles.DrawEdgeHandle(id, vertex1, vertex2, setCursor: true, renderEdge: false);
            }

            // Render polygon handles
            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                if (IsPolygonCameraAligned(p, cameraDirection, isOutlineInsideOut))
                    continue;

                var id = s_TempPolygonsIDs[p];
                var polygonCenter   = polygonCenters[p];
                var handleSize      = UnityEditor.HandleUtility.GetHandleSize(polygonCenter);
                var pointSize       = handleSize * SceneHandles.kPointScale;
                var normal          = isOutlineInsideOut ? -planes[p].xyz : planes[p].xyz;

                SceneHandles.color = polygonColors[p];
                var rotation = Quaternion.LookRotation(normal);
                SceneHandles.NormalHandleCap(id, polygonCenter, rotation, pointSize, evt.type);
            }
        }


        // Soft edges are edges that do not exist in our source mesh, but have been created during the optimization process (due to non planar polygons)
        // By double clicking on the soft edges we can turn them into 'real edges', we can reverse the process by clicking on a real edge
        // This method returns true when the brushMesh has changed
        public bool HandleSoftEdges()
        {
            var halfEdges = brushMesh.halfEdges;

            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: false))
                {
                    case ClickState.DoubleClicked:
                    {
                        // We double clicked on an existing edge and we turn it into a soft edge by 
                        // combining the polygons on both sides of the edge
                        CombinePolygonsOnEdge(e);
                        return true;
                    }
                }
            }

            var vertices = brushMesh.vertices;
            var currentFocusControl = SceneHandleUtility.focusControl;

            for (int s = 0; s < softEdges.Length; s++)
            {
                var id          = s_TempSoftEdgesIDs[s];
                var vertex1     = vertices[softEdges[s].vertexIndex1];
                var vertex2     = vertices[softEdges[s].vertexIndex2];
                SceneHandles.color = softEdgeColors[s];
                if (currentFocusControl == id)
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kHighlightedEdgeThickness, dashSize: kSoftEdgeDashSize);
                else
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kEdgeThickness, dashSize: kSoftEdgeDashSize);
                switch (HandleClick(id, captureControl: true))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.DoubleClicked:
                    {
                        // We double clicked on a soft edge and turn it into a real edge by splitting the polygon along this edge
                        var afterPolygonIndexA = softEdges[s].afterPolygonIndexA;
                        var beforePolygonIndexA = s_TempPolygon1ToPolygon2[afterPolygonIndexA];
                        SplitPolygonBetweenTwoVertices(beforePolygonIndexA, softEdges[s].vertexIndex1, softEdges[s].vertexIndex2);
                        return true;
                    }
                }
                SceneHandles.DrawEdgeHandle(id, vertex1, vertex2, setCursor: true, renderEdge: false, cursor: MouseCursor.ArrowPlus);
            }
            return false;
        }

        // This method returns true when the brushMesh has changed
        public bool HandleDeletion()
        {
            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                switch (HandleClick(s_TempVerticesIDs[v], captureControl: false))
                {
                    case ClickState.Delete:
                    {
                        DeleteSelectedEdges();
                        return true;
                    }
                }
            }

            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: false))
                {
                    case ClickState.Delete:
                    {
                        DeleteSelectedEdges();
                        return true;
                    }
                }
            }

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                switch (HandleClick(s_TempPolygonsIDs[p], captureControl: false))
                {
                    case ClickState.Delete:
                    {
                        DeleteSelectedEdges();
                        return true;
                    }
                }
            }
            return false;
        }

        void SelectVertexViewAligned(int v, Vector3 cameraDirection, SelectionType currentSelectionType)
        {
            SelectVertex(v, currentSelectionType);
            if (currentSelectionType == SelectionType.Replace)
                currentSelectionType = SelectionType.Additive;
            var halfEdges               = brushMesh.halfEdges;
            var planes                  = brushMesh.planes;
            var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;
            for (int edgeIndex = 0; edgeIndex < halfEdges.Length; edgeIndex++)
            {
                var vertexIndex = halfEdges[edgeIndex].vertexIndex;
                if (vertexIndex != v)
                    continue;

                var twinIndex = halfEdges[edgeIndex].twinIndex;

                var edgePolygonIndex = halfEdgePolygonIndices[edgeIndex];
                var edgeNormal = planes[edgePolygonIndex].xyz;
                var twinPolygonIndex = halfEdgePolygonIndices[twinIndex];
                var twinNormal = planes[twinPolygonIndex].xyz;

                // Check if both polygons on either side of edge are aligned with the view direction 
                // (which means the edge is as well)
                if (Mathf.Abs(Vector3.Dot(cameraDirection, edgeNormal)) > kAlignmentEpsilon ||
                    Mathf.Abs(Vector3.Dot(cameraDirection, twinNormal)) > kAlignmentEpsilon)
                    continue;

                // Select the other vertex on the edge as well since it's right behind our vertex
                SelectVertex(halfEdges[twinIndex].vertexIndex, currentSelectionType);
            }
        }

        public void HandleSelection(bool in2DMode, Vector3 cameraDirection)
        {
            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                switch (HandleClick(s_TempVerticesIDs[v], captureControl: false))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.Dragged:
                    {
                        // Select the vertex if we start dragging on an unselected vertex
                        if (!selection.selectedVertices.Contains(v))
                        {
                            if (in2DMode)
                            {
                                SelectVertexViewAligned(v, cameraDirection, SelectionType.Replace);
                            } else
                                SelectVertex(v, SelectionType.Replace);
                        }
                        break;
                    }
                    case ClickState.Clicked:
                    {
                        // Select the vertex if we clicked on it
                        var currentSelectionType = ChiselRectSelectionManager.GetCurrentSelectionType();
                        if (in2DMode)
                        {
                            SelectVertexViewAligned(v, cameraDirection, currentSelectionType);
                        } else
                            SelectVertex(v, currentSelectionType);
                        break;
                    }
                }
            }

            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: false))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.Dragged:
                    {
                        // Select the edge if we start dragging on an unselected edge
                        if (!selection.selectedEdges.Contains(e))
                            SelectEdge(e, SelectionType.Replace);
                        break;
                    }
                    case ClickState.Clicked:
                    {
                        // Select the edge if we clicked on it
                        SelectEdge(e, ChiselRectSelectionManager.GetCurrentSelectionType());
                        break;
                    }
                }
            }

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                switch (HandleClick(s_TempPolygonsIDs[p], captureControl: false))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.Dragged:
                    {
                        // Select the polygon if we start dragging on an unselected polygon
                        if (!selection.selectedPolygons.Contains(p))
                            SelectPolygon(p, SelectionType.Replace);
                        break;
                    }
                    case ClickState.Clicked:
                    {
                        // Select the polygon if we clicked on it
                        SelectPolygon(p, ChiselRectSelectionManager.GetCurrentSelectionType());
                        break;
                    }
                }
            }
        }

        // When a polygon is aligned with the camera, and we're in ortho mode, pretend that the edges of that polygon are the entire polygon, 
        // so that when you move the edge, you move the entire polygon instead.
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeAsPolygon(int id, int edgeIndex, Vector3 cameraDirection, bool isCameraOrtho)
        {
            if (!isCameraOrtho)
                return false;

            var evt = Event.current;

            var planes      = brushMesh.planes;
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;
            var halfEdgePolygonIndices = brushMesh.halfEdgePolygonIndices;


            var twinIndex = halfEdges[edgeIndex].twinIndex;

            var edgePolygonIndex    = halfEdgePolygonIndices[edgeIndex];
            var edgeNormal          = planes[edgePolygonIndex].xyz;
            var twinPolygonIndex    = halfEdgePolygonIndices[twinIndex];
            var twinNormal          = planes[twinPolygonIndex].xyz;
            var alignedIndex = -1;

            // Check if the polygon is aligned with the view direction
            if      (Mathf.Abs(Vector3.Dot(cameraDirection, edgeNormal)) < kAlignmentEpsilon) alignedIndex = edgePolygonIndex;
            // Otherwise try the twin side of the edge instead
            else if (Mathf.Abs(Vector3.Dot(cameraDirection, twinNormal)) < kAlignmentEpsilon) alignedIndex = twinPolygonIndex;
            else return false;

            var vertex1 = vertices[halfEdges[edgeIndex].vertexIndex];
            var vertex2 = vertices[halfEdges[twinIndex].vertexIndex];

            // Handle edge cursor before we change the id
            if (evt.type == EventType.Repaint)
                SceneHandles.SetCursor(id, vertex1, vertex2);

            // Register this edge with the id for the polygon, so that when we interact with it, it'll use the handle code for the polygon instead.
            UnityEditor.HandleUtility.AddControl(s_TempPolygonsIDs[alignedIndex], UnityEditor.HandleUtility.DistanceToLine(vertex1, vertex2) * 0.5f);
            return true;
        }

        // Handle splitting a polygon by starting on a point on an edge, 
        // and creating a new edge by splitting a polygon that is on one of the sides of the starting edge.
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeCreationFromEdge()
        {
            // TODO: handle polygons that are camera aligned -> pick edge that is closest to camera
            // TODO: make this make sense in ortho mode (we can't see which polygon we're splitting)
            //          do a split instead?? or properly handle distance (see previous TODO) and 
            //          just let user make sure their sceneview is set to, for instance, bottom or top??

            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: true))
                {
                    case ClickState.Hover:      { UpdateEdgeHoverPointOnEdge(e); break; }
                    case ClickState.Dragged:    { if (secondVertex) UpdateEdgeHoverPointOnEdge(e); break; }
                    case ClickState.MouseDown:
                    {
                        UpdateEdgeHoverPointOnEdge(e);
                        secondVertex = true;
                        firstNewVertex = prevNewVertex;
                        break;
                    }
                    case ClickState.Clicked:
                    case ClickState.DoubleClicked:
                    {
                        secondVertex = false;
                        break;
                    }
                    case ClickState.MouseUp:
                    {
                        if (!secondVertex)
                            break;

                        secondVertex = false;
                        if (e == highlightEdgeIndex ||
                            highlightEdgeIndex == -1)
                            break;

                        SplitPolygonBetweenTwoVertices(e, highlightEdgeIndex, prevNewVertex, firstNewVertex);
                        return true;
                    }
                }
            }
            return false;
        }

        // Handle splitting a polygon by starting on a point on an vertex, 
        // and creating a new edge by splitting one of the polygons that is around the starting vertex.
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeCreationFromVertex()
        {
            // TODO: handle edges that are camera aligned -> pick vertex that is closest to camera
            // TODO: make this make sense in ortho mode (we can't see which polygon we're splitting)
            //          do a split instead?? or properly handle distance (see previous TODO) and 
            //          just let user make sure their sceneview is set to, for instance, bottom or top??

            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                switch (HandleClick(s_TempVerticesIDs[v], captureControl: true))
                {
                    case ClickState.Hover:      { UpdateEdgeHoverPointOnVertex(v); break; }
                    case ClickState.Dragged:    { if (secondVertex) UpdateEdgeHoverPointOnVertex(v); break; }
                    case ClickState.MouseDown:
                    {
                        UpdateEdgeHoverPointOnVertex(v);
                        secondVertex = true;
                        firstNewVertex = prevNewVertex;
                        break;
                    }
                    case ClickState.Clicked:
                    case ClickState.DoubleClicked:
                    {
                        secondVertex = false;
                        break;
                    }
                    case ClickState.MouseUp:
                    {
                        if (!secondVertex)
                            break;

                        secondVertex = false;
                        if (highlightEdgeIndex == -1)
                            break;

                        // Find the polygon that belongs to the half-edge we're hovering over
                        var polygonIndex    = brushMesh.halfEdgePolygonIndices[highlightEdgeIndex];
                        var startEdgeIndex  = brushMesh.FindPolygonEdgeByVertexIndex(polygonIndex, v);

                        // We might be looking on the wrong side of the half-edge, so if we fail, 
                        // try the polygon on the other side
                        if (startEdgeIndex == -1)
                        {
                            highlightEdgeIndex  = brushMesh.halfEdges[highlightEdgeIndex].twinIndex;
                            polygonIndex        = brushMesh.halfEdgePolygonIndices[highlightEdgeIndex];
                            startEdgeIndex      = brushMesh.FindPolygonEdgeByVertexIndex(polygonIndex, v);
                        }

                        SplitPolygonBetweenTwoVertices(startEdgeIndex, highlightEdgeIndex, prevNewVertex, firstNewVertex);
                        return true;
                    }
                }
            }
            return false;
        }

        // Handle moving an edge along the grid aligned plane which the edge intersects with
        // This method returns true when the brushMesh has changed
        public bool HandleVertexMovementCameraAligned(Vector3 cameraDirection)
        {
            var vertices = brushMesh.vertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                var id          = s_TempVerticesIDs[v];
                var position    = vertices[v];
                var handleSize  = UnityEditor.HandleUtility.GetHandleSize(position) * SceneHandles.kPointScale;

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider2DHandleOffset(id, position, cameraDirection, capFunction: null); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }


        // Handle moving a vertex in 3D
        // Note: We can't actually move the vertex in 3D since we don't know how to map 3D to 2D for a point
        //       so the name of this method is a bit of a lie, but maybe we'll find a solution for it
        // This method returns true when the brushMesh has changed
        public bool HandleVertexMovement3D()
        {
            // Capture the click so we don't actually pass through to another control
            for (int v = 0; v < s_TempVerticesIDCount; v++)
                HandleClick(s_TempVerticesIDs[v], captureControl: true); // TODO: do this in a different way
            return false;
        }

        // Handle moving the polygon along the grid plane that's closest to the polygon plane
        // This method returns true when the brushMesh has changed
        public bool HandlePolygonMovementCameraAligned(Vector3 cameraDirection, bool isOutlineInsideOut)
        {
            var planes = brushMesh.planes;

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                if (IsPolygonCameraAligned(p, cameraDirection, isOutlineInsideOut))
                    continue;
                
                var id      = s_TempPolygonsIDs[p];
                var normal  = isOutlineInsideOut ? -planes[p].xyz : planes[p].xyz;
                var offset  = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider2DHandleOffset(id, polygonCenters[p], normal, handleSize: -1); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // Handle moving the polygon center in the direction of the normal
        // This method returns true when the brushMesh has changed
        public bool HandlePolygonMovement3D(Vector3 cameraDirection, bool isOutlineInsideOut)
        {
            // TODO: handle moving in axis aligned direction instead of along normal

            var planes = brushMesh.planes;

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                if (IsPolygonCameraAligned(p, cameraDirection, isOutlineInsideOut))
                    continue;

                var id      = s_TempPolygonsIDs[p];
                var normal  = isOutlineInsideOut ? -planes[p].xyz : planes[p].xyz;

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider1DHandleOffset(id, polygonCenters[p], normal, handleSize: -1); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // Handle moving an edge along the direction of itself
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeMovementEdgeAligned(Vector3 cameraDirection, bool isCameraOrtho)
        {
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges

                var twin    = halfEdges[e].twinIndex;
                var vertex1 = vertices[halfEdges[e].vertexIndex];
                var vertex2 = vertices[halfEdges[twin].vertexIndex];

                // Render a dotted line along the current edge (but do not overlap it)
                if ((s_TempEdgesState[e] & (ItemState.Hovering | ItemState.Active)) != ItemState.None)
                {
                    var direction = math.length(vertex1 - vertex2) * 1000.0f;
                    // TODO: make this line go into infinity
                    SceneHandles.DrawDottedLine(vertex1 + direction, vertex1, 2.0f);
                    SceneHandles.DrawDottedLine(vertex2, vertex2 - direction, 2.0f);
                }

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider1DHandleAlignedOffset(id, vertex1, vertex2); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // Handle moving an edge along a grid aligned 2D plane that the edge intersects with
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeMovement3D()
        {
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges

                var twin    = halfEdges[e].twinIndex;
                var vertex1 = vertices[halfEdges[e].vertexIndex];
                var vertex2 = vertices[halfEdges[twin].vertexIndex];

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider2DHandleTangentOffset(id, vertex1, vertex2); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // This method returns true when the brushMesh has changed
        public bool HandleVertexMovementWithPositionHandle()
        {
            // If we're focusing on a position handle we don't want the position Handle to be enabled, so that when they overlap, 
            // you're not accidentally grabbing the wrong thing
            bool enablePositionHandle = !HaveFocusOnPolygon;

            // TODO: somehow handle snapping against all other non-selected vertices
            //          this way, when we move an edge of a polygon on another edge, *which is not grid aligned*, it would still snap to it.

            using (new EditorGUI.DisabledScope(!enablePositionHandle))
            {
                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.PositionHandleOffset(ref s_TempPositionHandleIDs, vertexSelectionCenter); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        bool InAlternateEditMode    { get { return Event.current.shift; } }

        bool IsSceneViewIn2DMode(SceneView sceneView)
        {
            return  sceneView.isRotationLocked &&
                    sceneView.camera.orthographic;
        } 

        // TODO: have a more scalable way to handle brush "edit modes"
        bool InCreateEdgeEditMode   { get { return InAlternateEditMode; } }

        bool InDefaultEditMode      { get { return !InAlternateEditMode; } }


        // This method returns true when the brushMesh has changed
        public bool DoHandles(SceneView sceneView, bool isOutlineInsideOut)
        {
            var camera = sceneView.camera;
            if (!UpdateColors(camera))
                return false;

            var isCameraOrtho       = camera.orthographic;
            var cameraDirection     = camera.transform.forward;
            var in2DMode            = IsSceneViewIn2DMode(sceneView);

            // Ortho mode
            //      TODO: handle being able to move brushes when dragging from the selected brush itself 
            //              (might be better to handle this outside the generator so we can do this for all types of generators)
            //      TODO: move polygon -> needs to always be grid axis aligned movement

            // TODO: add ability to delete vertices
            // TODO: add scaling/rotating of vertices

            // TODO: need support for multiple edit modes (maybe use EditorTool?)
            //       TODO: add marquee selection edit mode
            //       TODO: add create split brush edit mode

            // TODO: have ability to click on each XY/YZ/XZ square of the position handle to select a working plane, 
            //          then dragging objects/vertices directly works on that selected plane (3ds-max style)

            if (HandleDeletion())
                return true;

            HandleSelection(in2DMode, cameraDirection);

            if (HandleSoftEdges())
                return true;

            if (InCreateEdgeEditMode)
            {
                if (HaveFocusOnHalfEdgeOrVertex)
                    RenderHoverPoint();

                if (HandleEdgeCreationFromEdge())
                    return true;

                if (HandleEdgeCreationFromVertex())
                    return true;

                // Edit mode that is disabled until we figure out how to fit it in, UX wise
                /*
                if (HandleEdgeMovementEdgeAligned(cameraDirection, isCameraOrtho))
                  return true;

                if (HandlePolygonMovementCameraAligned(cameraDirection, isOutlineInsideOut))
                    return true;
                */
            } else
            {
                if (HandleEdgeMovement3D())
                    return true;

                if (HandlePolygonMovement3D(cameraDirection, isOutlineInsideOut))
                    return true;

                if (in2DMode)
                {
                    if (HandleVertexMovementCameraAligned(cameraDirection))
                        return true;
                } else
                {
                    if (HandleVertexMovement3D())
                        return true;
                }
            }

            if (InDefaultEditMode && 
                !in2DMode &&
                selection.selectedVertices.Count > 0)
            {
                if (HandleVertexMovementWithPositionHandle())
                    return true;
            }

            RenderOutline(cameraDirection, isCameraOrtho, isOutlineInsideOut);
            return false;
        }
    }
}