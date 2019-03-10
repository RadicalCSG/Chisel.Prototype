using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
#if USE_MANAGED_CSG_IMPLEMENTATION
    public enum Category : byte
    {
        None,

        Inside,
        Aligned,
        ReverseAligned,
        Outside
    }

    // TODO: create methods to subtract/join loops, create tests for all edge cases
    // TODO: rename
    public sealed class Loop
    {
        public Plane	            localPlane;
        public Plane	            worldPlane;
        public int		            basePlaneIndex;							// always on owning brush
        public Category             interiorCategory = Category.Aligned;    // inside or on other brush
        public SurfaceLayers		layers;
        public List<Loop>           holes;
        public List<Intersection>   intersections = new List<Intersection>();


        public class Intersection
        {
            public Vector3      vertex;                             // TODO: can we get away with using vertexIndices instead?
            public HashSet<int> planeIndices = new HashSet<int>();  // TODO: try to get rid of HashSet

            public void Merge(Intersection otherIntersection)
            {
                foreach (var planeIndex in otherIntersection.planeIndices)
                    planeIndices.Add(planeIndex);
            }
        }

        public int FindIndexForVertex(Vector3 vertex)
        {
            for (int vAi = 0; vAi < intersections.Count; vAi++)
            {
                if (intersections[vAi].vertex == vertex)
                    return vAi;
            }
            return -1;
        }

        public Loop() { }
        public Loop(Loop original, List<Intersection> intersections)
        {
            this.localPlane         = original.localPlane;
            this.worldPlane         = original.worldPlane;
            this.basePlaneIndex     = original.basePlaneIndex;
            this.interiorCategory   = original.interiorCategory;
            this.layers				= original.layers;
            this.intersections      = intersections;
        }

        public static Vector3 FindPolygonCentroid(List<Intersection> vertices)
        {
            var centroid = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
                centroid += vertices[i].vertex;
            return centroid / vertices.Count;
        }

        // TODO: sort by using plane information instead of unreliable floating point math ..
        // TODO: make this work on non-convex polygons
        public void SortVertices()
        {
            // there's no point in trying to sort a point or a line
            if (intersections.Count < 3)
                return;

            var centroid	= FindPolygonCentroid(intersections);
            var normal		= worldPlane.normal;

            Vector3 tangentX, tangentY;
            if (normal.x > normal.y)
            {
                if (normal.x > normal.z)
                {
                    tangentX = Vector3.Cross(normal, Vector3.up);
                    tangentY = Vector3.Cross(normal, tangentX);
                    //Debug.Log("A " + normal + " " + tangentX + " " + tangentY);
                }
                else
                {
                    tangentX = Vector3.Cross(normal, Vector3.forward);
                    tangentY = Vector3.Cross(normal, tangentX);
                    //Debug.Log("B " + normal + " " + tangentX + " " + tangentY);
                }
            }
            else
            {
                if (normal.y > normal.z)
                {
                    tangentX = Vector3.Cross(normal, Vector3.right);
                    tangentY = Vector3.Cross(normal, tangentX);
                    //Debug.Log("C " + normal + " " + tangentX + " " + tangentY);
                }
                else
                {
                    tangentX = Vector3.Cross(normal, Vector3.up);
                    tangentY = Vector3.Cross(normal, tangentX);
                    //Debug.Log("D " + normal + " " + tangentX + " " + tangentY);
                }
            }

            var cx = Vector3.Dot(tangentX, centroid); // distance in direction of tangentX
            var cy = Vector3.Dot(tangentY, centroid); // distance in direction of tangentY

            //Debug.Log(centroid + " " + cx + " " + cy + " " + plane + " " + vertices[0].vertex + " " + vertices[1].vertex + " " + vertices[2].vertex + " " + vertices[3].vertex);

            // sort vertices according to their angle relative to the centroid on plane defined by tangentX/tangentY
            intersections.Sort(delegate (Intersection a, Intersection b)
            {
                var ax = Vector3.Dot(tangentX, a.vertex);  // distance in direction of tangentX
                var ay = Vector3.Dot(tangentY, a.vertex);  // distance in direction of tangentY
                var bx = Vector3.Dot(tangentX, b.vertex);  // distance in direction of tangentX
                var by = Vector3.Dot(tangentY, b.vertex);  // distance in direction of tangentY

                var a1 = Mathf.Atan2(ax - cx, ay - cy); // angle between ax/ay and cx/cy
                var a2 = Mathf.Atan2(bx - cx, by - cy); // angle between bx/by and cx/cy

                //Debug.Log(a.vertex + " " + ax + " " + ay + " : " + a1 + " | " + a.vertex + " " + bx + " " + by + " : " + a2 + " | ");
                return (int)Mathf.Sign(a2 - a1);
            });
        }

        internal bool Triangulate(Matrix4x4 uv0Matrix, ref CSGSurfaceRenderBuffer dstSurface)
        {
            if (interiorCategory == Category.Outside ||
                interiorCategory == Category.Inside)
                return false;

            if (intersections.Count < 3)
                return false;

            // TODO: replace with other triangulation solution

            var polygon = new Poly2Mesh.Polygon
            {
                inverse = (interiorCategory == Category.ReverseAligned),
                planeNormal = worldPlane.normal
            };
            for (int v = 0; v < intersections.Count; v++)
            {
                polygon.outside.Add(intersections[v].vertex);
            }
            if (holes != null && holes.Count > 0)
            {
                polygon.holes = new List<List<Vector3>>(holes.Count);
                for (int h = 0; h < holes.Count; h++)
                {
                    var holeLoop = holes[h];
                    if (holeLoop.intersections.Count < 3)
                        continue;
                    var hole = new List<Vector3>(holeLoop.intersections.Count);
                    for (int v = 0; v < holeLoop.intersections.Count; v++)
                        hole.Add(holeLoop.intersections[v].vertex);
                    polygon.holes.Add(hole);
                }
            } else
                polygon.holes = null;

            return CreateMesh(polygon, uv0Matrix, ref dstSurface);
        }
            
        static bool CreateMesh(Poly2Mesh.Polygon polygon, Matrix4x4 uv0Matrix, ref CSGSurfaceRenderBuffer dstMesh)
        {
            // TODO: replace with other triangulation solution

            // Ensure we have the rotation properly calculated, and have a valid normal
            if (polygon.rotation == Quaternion.identity) polygon.CalcRotation();
            if (polygon.planeNormal == Vector3.zero)
            {
                Debug.Log("polygon.planeNormal == Vector3.zero");
                return false;       // bad data
            }
            
            // Rotate 1 point and note where it ends up in Z
            float z = (polygon.rotation * polygon.outside[0]).z;

            // TODO: use vertex indices instead of actual vertices to find original vertices

            // Prepare a map from vertex codes to 3D positions.
            Dictionary<uint, Vector3> codeToPosition = new Dictionary<uint, Vector3>();

            // Convert the outside points (throwing out Z at this point)
            Poly2Tri.Polygon poly = new Poly2Tri.Polygon(Poly2Mesh.ConvertPoints(polygon.outside, polygon.rotation, codeToPosition));


            // Convert each of the holes
            if (polygon.holes != null)
            {
                foreach (List<Vector3> hole in polygon.holes)
                    poly.AddHole(new Poly2Tri.Polygon(Poly2Mesh.ConvertPoints(hole, polygon.rotation, codeToPosition)));
            }

            // Triangulate it!  Note that this may throw an exception if the data is bogus.
            try
            {
                var tcx = new Poly2Tri.DTSweepContext();
                tcx.PrepareTriangulation(poly);
                Poly2Tri.DTSweep.Triangulate(tcx);
                tcx = null;
            } catch (System.Exception e) {
                //Profiler.Exit(profileID);
                Debug.LogException(e);
                //throw e;
            }

            // Now, to get back to our original positions, use our code-to-position map.  We do
            // this instead of un-rotating to be a little more robust about noncoplanar polygons.

            // Create the Vector3 vertices (undoing the rotation),
            // and also build a map of vertex codes to indices
            Quaternion? invRot = null;
            var codeToIndex = new Dictionary<uint, int>();
            var vertexList	= new List<Vector3>();
            var normals		= new List<Vector3>();
            var indexList	= new List<int>();
            int triangleCount = 0;
            triangleCount += poly.Triangles.Count;
            codeToIndex.Clear();
            foreach (var t in poly.Triangles)
            {
                foreach (var point in t.Points)
                {
                    if (codeToIndex.ContainsKey(point.VertexCode)) continue;
                    codeToIndex[point.VertexCode] = vertexList.Count;
                    Vector3 pos;
                    if (!codeToPosition.TryGetValue(point.VertexCode, out pos))
                    {
                        // This can happen in rare cases when we're hitting limits of floating-point precision.
                        // Rather than fail, let's just do the inverse rotation.
                        Debug.LogWarning("Vertex code lookup failed; using inverse rotation.");
                        if (!invRot.HasValue) invRot = Quaternion.Inverse(polygon.rotation);
                        pos = invRot.Value * new Vector3(point.Xf, point.Yf, z);
                    }
                    vertexList.Add(pos);
                    normals.Add(polygon.planeNormal);
                }
            }
            if (polygon.inverse)
            {
                foreach (var t in poly.Triangles)
                {
                    indexList.Add(codeToIndex[t.Points[2].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[1].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[0].VertexCode]);
                }
            } else
            {
                foreach (var t in poly.Triangles)
                {
                    indexList.Add(codeToIndex[t.Points[0].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[1].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[2].VertexCode]);
                }
            }

            // Create the indices array
            var indices = indexList.ToArray();

            // Create the mesh
            dstMesh.vertices	= vertexList.ToArray();
            dstMesh.normals		= normals.ToArray();

            var uv0 = new Vector2[vertexList.Count];
            for (int v=0; v < vertexList.Count; v++)
                uv0[v] = uv0Matrix.MultiplyPoint3x4(vertexList[v]);
            dstMesh.uv0 = uv0;

            dstMesh.indices		= indices;
            return true;
        }
    }

    public sealed class SurfaceLoops
    {
        public SurfaceLoops(int length)
        {
            surfaces = new List<Loop>[length];
            for (int i = 0; i < length; i++)
                surfaces[i] = new List<Loop>();
        }
        public readonly List<Loop>[] surfaces;
        
        const float kDistanceEpsilon = 0.0006f;
        const float kSqrMergeEpsilon = (kDistanceEpsilon * kDistanceEpsilon * 4);


        static Loop GetLoopForPlaneIndex(int planeIndex, List<Loop> holeLoops)
        {
            for (int i = 0; i < holeLoops.Count; i++)
            {
                if (holeLoops[i].basePlaneIndex == planeIndex)
                    return holeLoops[i];
            }
            holeLoops.Add(new Loop() { basePlaneIndex = planeIndex });
            return holeLoops[holeLoops.Count - 1];
        }

        static List<int> GetIntersectingPlanes(BrushMesh mesh, BrushMesh.Surface[] otherSurfaces, Matrix4x4 treeToNodeSpaceMatrixTransform)
        {
            // TODO: use convex polytope stuff to be more precise than using bounds
            var inverseTransform = treeToNodeSpaceMatrixTransform.inverse;
            var intersectingPlanes = new List<int>();
            for (int i = 0; i < otherSurfaces.Length; i++)
            {
                // bring plane into local space of mesh, the same space as the bounds of the mesh
                var otherLocalPlane = inverseTransform.InverseTransform(otherSurfaces[i].plane);
                var intersection = otherLocalPlane.Intersection(mesh.localBounds);
                if (intersection == IntersectionResult.Outside)
                    return null;

                if (intersection != IntersectionResult.Intersecting)
                    continue;

                intersectingPlanes.Add(i);
            }
            if (intersectingPlanes.Count == 0)
                return null;

            return intersectingPlanes;
        }

        // TODO: create loops for both brushes TOGETHER, taking advantage of overlapping edges/vertices (necessary to avoid gaps!)
        //       right now loops are generated separately for each brush
        // TODO: make this work on non-convex polygons
        internal static SurfaceLoops GetIntersectionLoops(CSGTreeBrush leaf1, CSGTreeBrush leaf2)
        {
            var mesh1 = BrushMeshManager.GetBrushMesh(leaf1.BrushMesh.BrushMeshID);
            var mesh2 = BrushMeshManager.GetBrushMesh(leaf2.BrushMesh.BrushMeshID);

            var intersectingPlanes2 = GetIntersectingPlanes(mesh1, mesh2.surfaces, leaf2.NodeToTreeSpaceMatrix * leaf1.TreeToNodeSpaceMatrix);
            if (intersectingPlanes2 == null)
                return null;

            var intersectingPlanes1 = GetIntersectingPlanes(mesh2, mesh1.surfaces, leaf1.NodeToTreeSpaceMatrix * leaf2.TreeToNodeSpaceMatrix);
            if (intersectingPlanes1 == null)
                return null;

            var usedPlanePairs2 = new List<int>(mesh2.surfaces.Length * 2);
            var usedVertices2 = new HashSet<int>();
            for (int e = 0; e < mesh2.halfEdges.Length; e++)
            {
                var twinIndex = mesh2.halfEdges[e].twinIndex;
                if (twinIndex < e)
                    continue;

                var sI0 = mesh2.polygons[mesh2.halfEdges[e].polygonIndex].surfaceID;
                var sI1 = mesh2.polygons[mesh2.halfEdges[twinIndex].polygonIndex].surfaceID;
                if (!intersectingPlanes2.Contains(sI0) &&
                    !intersectingPlanes2.Contains(sI1))
                    continue;

                var vI0 = mesh2.halfEdges[e].vertexIndex;
                var vI1 = mesh2.halfEdges[twinIndex].vertexIndex;

                usedVertices2.Add(vI0);
                usedVertices2.Add(vI1);
                usedPlanePairs2.Add(sI0);
                usedPlanePairs2.Add(sI1);
            }

            var usedPlanePairs1 = new List<int>(mesh1.surfaces.Length * 2);
            var usedVertices1 = new HashSet<int>();
            for (int e = 0; e < mesh1.halfEdges.Length; e++)
            {
                var twinIndex = mesh1.halfEdges[e].twinIndex;
                if (twinIndex < e)
                    continue;

                var sI0 = mesh2.polygons[mesh1.halfEdges[e].polygonIndex].surfaceID;
                var sI1 = mesh2.polygons[mesh1.halfEdges[twinIndex].polygonIndex].surfaceID;
                if (!intersectingPlanes1.Contains(sI0) &&
                    !intersectingPlanes1.Contains(sI1))
                    continue;

                var vI0 = mesh1.halfEdges[e].vertexIndex;
                var vI1 = mesh1.halfEdges[twinIndex].vertexIndex;

                usedVertices1.Add(vI0);
                usedVertices1.Add(vI1);
                usedPlanePairs1.Add(sI0);
                usedPlanePairs1.Add(sI1);
            }



            var inverseNodeToTreeSpaceMatrix1 = leaf1.TreeToNodeSpaceMatrix;
            var inverseNodeToTreeSpaceMatrix2 = leaf2.TreeToNodeSpaceMatrix;

            // TODO: we don't actually use ALL of these planes .. Optimize this
            var worldSpacePlanes1 = new Plane[mesh1.surfaces.Length];
            for (int p = 0; p < worldSpacePlanes1.Length; p++)
                worldSpacePlanes1[p] = inverseNodeToTreeSpaceMatrix1.InverseTransform(mesh1.surfaces[p].plane);

            // TODO: we don't actually use ALL of these planes .. Optimize this
            var worldSpacePlanes2 = new Plane[mesh2.surfaces.Length];
            for (int p = 0; p < worldSpacePlanes2.Length; p++)
                worldSpacePlanes2[p] = inverseNodeToTreeSpaceMatrix2.InverseTransform(mesh2.surfaces[p].plane);

            var holeLoops = new List<Loop>();

#if true
            for (int a = 0; a < intersectingPlanes1.Count; a++)
            {
                var pI2		= intersectingPlanes1[a];
                var p2		= worldSpacePlanes1[pI2];
                var layers	= mesh1.polygons[pI2].layers; // FIXME: this assumes polygonIndex == surfaceIndex!!

                Loop loop = null;
                for (int i = 0; i < usedPlanePairs2.Count; i += 2)
                {
                    var pI0 = usedPlanePairs2[i + 0];
                    var pI1 = usedPlanePairs2[i + 1];

                    var p0 = worldSpacePlanes2[pI0];
                    var p1 = worldSpacePlanes2[pI1];

                    var vertex = PlaneExtensions.Intersection(p0, p1, p2);
                    if (float.IsNaN(vertex.x))
                        continue;

                    bool inside;

                    // TODO: since we're using a pair in the outer loop, we could also determine which 
                    //       2 planes it intersects at both ends and just check those two planes ..
                    inside = true;
                    for (int n = 0; n < intersectingPlanes2.Count; n++)
                    {
                        var pI = intersectingPlanes2[n];
                        if (pI == pI0 ||
                            pI == pI1)
                            continue;
                        var plane = worldSpacePlanes2[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance <= kDistanceEpsilon) continue;
                        inside = false;
                        break;
                    }
                    if (!inside)
                        continue;

                    inside = true;
                    for (int n = 0; n < intersectingPlanes1.Count; n++)
                    {
                        var pI = intersectingPlanes1[n];
                        if (pI == pI2)
                            continue;
                        var plane = worldSpacePlanes1[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance <= kDistanceEpsilon) continue;
                        inside = false;
                        break;
                    }
                    if (!inside)
                        continue;

                    var intersection = new Loop.Intersection() { vertex = vertex };

                    // NOTE: for brush2, the intersection will always be only on two planes
                    //       UNLESS it's a corner vertex along that edge (we can compare to the two vertices)
                    //       in which case we could use a pre-calculated list of planes ..
                    //       OR when the intersection is outside of the edge ..

                    // TODO: put in above code
                    var planes = intersection.planeIndices;
                    for (int n = 0; n < intersectingPlanes1.Count; n++)
                    {
                        var pI = intersectingPlanes1[n];
                        var plane = worldSpacePlanes1[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance < -kDistanceEpsilon ||
                            distance > kDistanceEpsilon)
                            continue;
                        planes.Add(pI);
                    }
                    for (int n = 0; n < intersectingPlanes2.Count; n++)
                    {
                        var pI = intersectingPlanes2[n];
                        var plane = worldSpacePlanes2[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance < -kDistanceEpsilon ||
                            distance > kDistanceEpsilon)
                            continue;
                        planes.Add(-(1 + pI));
                    }
                    planes.Add(pI2);

                    if (loop == null)
                    {
                        loop = new Loop
                        {
                            layers = layers,
                            localPlane = mesh1.surfaces[pI2].plane,
                            worldPlane = p2,
                            basePlaneIndex = pI2
                        };
                        //if (loop.worldPlane.normal == Vector3.zero) Debug.LogError("!");
                    }
                    // TODO: should be having a Loop for each plane that intersects this vertex, and add that vertex
                    loop.intersections.Add(intersection);
                }
                if (loop != null)
                    holeLoops.Add(loop);
            }
#endif

#if true
            for (int a = 0; a < intersectingPlanes2.Count; a++)
            {
                var pI2 = intersectingPlanes2[a];
                var p2 = worldSpacePlanes2[pI2];

                for (int i = 0; i < usedPlanePairs1.Count; i += 2)
                {
                    var pI0 = usedPlanePairs1[i + 0];
                    var pI1 = usedPlanePairs1[i + 1];

                    var p0 = worldSpacePlanes1[pI0];
                    var p1 = worldSpacePlanes1[pI1];

                    var vertex = PlaneExtensions.Intersection(p0, p1, p2);
                    if (float.IsNaN(vertex.x))
                        continue;

                    bool inside;

                    // FIXME: sometimes we have two planes of a brush2 instersecting one plane on brush1, 
                    //		  and even though it's outside of brush1, it's still *just* within kDistanceEpsilon
                    //		  and can cause issues .. we need to find a better way of doing this

                    // TODO: since we're using a pair in the outer loop, we could also determine which 
                    //       2 planes it intersects at both ends and just check those two planes ..
                    inside = true;
                    for (int n = 0; n < intersectingPlanes1.Count; n++)
                    {
                        var pI = intersectingPlanes1[n];
                        if (pI == pI0 ||
                            pI == pI1)
                            continue;
                        var plane = worldSpacePlanes1[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance <= kDistanceEpsilon) continue;
                        inside = false;
                        break;
                    }
                    if (!inside)
                        continue;

                    inside = true;
                    for (int n = 0; n < intersectingPlanes2.Count; n++)
                    {
                        var pI = intersectingPlanes2[n];
                        if (pI == pI2)
                            continue;
                        var plane = worldSpacePlanes2[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance <= kDistanceEpsilon) continue;
                        inside = false;
                        break;
                    }
                    if (!inside)
                        continue;

                    Loop loop0 = null;
                    Loop loop1 = null;
                    for (int l = 0; l < holeLoops.Count; l++)
                    {
                        if (holeLoops[l].basePlaneIndex == pI0) { loop0 = holeLoops[l]; if (loop1 != null) break; }
                        if (holeLoops[l].basePlaneIndex == pI1) { loop1 = holeLoops[l]; if (loop0 != null) break; }
                    }

                    if (loop0 == null) { loop0 = new Loop { localPlane = mesh1.surfaces[pI0].plane, layers = mesh1.polygons[pI0].layers, worldPlane = p0, basePlaneIndex = pI0 }; holeLoops.Add(loop0); }
                    if (loop1 == null) { loop1 = new Loop { localPlane = mesh1.surfaces[pI1].plane, layers = mesh1.polygons[pI1].layers, worldPlane = p1, basePlaneIndex = pI1 }; holeLoops.Add(loop1); }
                    //if (loop0 != null && loop0.worldPlane.normal == Vector3.zero) Debug.LogError("!");
                    //if (loop1 != null && loop1.worldPlane.normal == Vector3.zero) Debug.LogError("!");

                    // TODO: snap to other vertices we've already found ...
                    var intersection = new Loop.Intersection() { vertex = vertex };

                    // TODO: put in above code
                    var planes = intersection.planeIndices;
                    for (int n = 0; n < intersectingPlanes1.Count; n++)
                    {
                        var pI = intersectingPlanes1[n];
                        var plane = worldSpacePlanes1[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance < -kDistanceEpsilon ||
                            distance > kDistanceEpsilon)
                            continue;
                        planes.Add(pI);
                    }
                    for (int n = 0; n < intersectingPlanes2.Count; n++)
                    {
                        var pI = intersectingPlanes2[n];
                        var plane = worldSpacePlanes2[pI];
                        var distance = plane.GetDistanceToPoint(vertex);
                        if (distance < -kDistanceEpsilon ||
                            distance > kDistanceEpsilon)
                            continue;
                        planes.Add(-(1 + pI));
                    }
                    planes.Add(loop0.basePlaneIndex);
                    planes.Add(loop1.basePlaneIndex);

                    loop0.intersections.Add(intersection);
                    loop1.intersections.Add(intersection);
                }
            }
#endif

            // TODO: figure out the default category for the interior of each loop
            //interiorCategory = Category.Inside;
            if (holeLoops.Count == 0)
                return null;

#if true
            // TODO: when all vertices of a polygon are inside the other brush, don't bother intersecting it
            //       same when two planes overlap each other ...
            foreach (var vertexIndex in usedVertices1)
            {
                // TODO: perform all above math inside space of mesh1, which gives better floating point accuracy, and won't need to do this then either
                var nodeToTreeSpace1 = leaf1.NodeToTreeSpaceMatrix;
                var vertex = nodeToTreeSpace1.MultiplyPoint(mesh1.vertices[vertexIndex]);
                bool inside = true;
                for (int n = 0; n < intersectingPlanes2.Count; n++)
                {
                    var pI = intersectingPlanes2[n];
                    var plane = worldSpacePlanes2[pI];
                    var distance = plane.GetDistanceToPoint(vertex);
                    if (distance <= kDistanceEpsilon) continue;
                    inside = false;
                    break;
                }
                if (!inside)
                    continue;

                var intersection = new Loop.Intersection() { vertex = vertex };

                // TODO: put in above code
                var planes = intersection.planeIndices;
                for (int n = 0; n < intersectingPlanes1.Count; n++)
                {
                    var pI = intersectingPlanes1[n];
                    var plane = worldSpacePlanes1[pI];
                    var distance = plane.GetDistanceToPoint(vertex);
                    if (distance < -kDistanceEpsilon ||
                        distance > kDistanceEpsilon)
                        continue;
                    planes.Add(pI);
                }
                for (int n = 0; n < intersectingPlanes2.Count; n++)
                {
                    var pI = intersectingPlanes2[n];
                    var plane = worldSpacePlanes2[pI];
                    var distance = plane.GetDistanceToPoint(vertex);
                    if (distance < -kDistanceEpsilon ||
                        distance > kDistanceEpsilon)
                        continue;
                    planes.Add(-pI);
                }

                foreach (var planeIndex in planes)
                {
                    if (planeIndex < 0)
                        continue;

                    // TODO: optimize this
                    var loop = GetLoopForPlaneIndex(planeIndex, holeLoops);
                    loop.localPlane = mesh1.surfaces[loop.basePlaneIndex].plane;
                    loop.worldPlane = nodeToTreeSpace1.TransformPlane(loop.localPlane);
                    //if (loop != null && loop.worldPlane.normal == Vector3.zero) Debug.LogError("!");

                    int found_index = -1;
                    float distance = float.PositiveInfinity;
                    for (int v = 0; v < loop.intersections.Count; v++)
                    {
                        var sqrDistance = (loop.intersections[v].vertex - vertex).sqrMagnitude;
                        if (sqrDistance > kSqrMergeEpsilon)
                            continue;

                        if (sqrDistance > distance)
                            continue;

                        found_index = v;
                        sqrDistance = distance;
                    }

                    if (found_index == -1)
                    {
                        found_index = loop.intersections.Count;
                        loop.intersections.Add(intersection);
                    }
                    else
                    {
                        // TODO: add planes in this intersection to the current intersection
                        //       and REPLACE this intersection (AND ALL OTHER PLACES THIS INTERSECTION IS USED) with it
                        loop.intersections[found_index].vertex = vertex;
                    }

                    var found_planes = loop.intersections[found_index].planeIndices;
                    foreach (var plane in planes)
                        found_planes.Add(plane);
                }
            }
#endif
            // TODO: rewrite, use plane information to sort instead of slow and unsafe floating point math
            for (int l = 0; l < holeLoops.Count; l++)
            {
                if (holeLoops[l].intersections.Count == 0)
                    continue;
                var basePlaneIndex = holeLoops[l].basePlaneIndex;
                //if (basePlaneIndex < 0)
                //	holeLoops[l].worldPlane = worldSpacePlanes2[(-basePlaneIndex) - 1];
                //else
                holeLoops[l].worldPlane = worldSpacePlanes1[basePlaneIndex];
                //if (holeLoops[l] != null && holeLoops[l].worldPlane.normal == Vector3.zero) Debug.LogError("!");
                holeLoops[l].SortVertices();
                holeLoops[l].intersections.Reverse();// TODO: fix the need for holes to be reversed in order
            }

            // Merge duplicate intersections
            // TODO: prevent this from happening in the first place
            for (int l = 0; l < holeLoops.Count; l++)
            {
                var loop = holeLoops[l];
                var intersections = loop.intersections;
                var i0 = 0;
                var i1 = intersections.Count - 1;

                while (i1 >= 0 && (intersections[i0].vertex - intersections[i1].vertex).sqrMagnitude <= kSqrMergeEpsilon)
                {
                    intersections[i0].Merge(intersections[i1]);
                    intersections.RemoveAt(i1);
                    i1--;
                }

                if (i1 > 0)
                {
                    do
                    {
                        i0 = i1;
                        i1--;
                        if (i1 >= 0 && (intersections[i0].vertex - intersections[i1].vertex).sqrMagnitude <= kSqrMergeEpsilon)
                        {
                            intersections[i1].Merge(intersections[i0]);
                            intersections.RemoveAt(i0);
                        }
                    } while (i1 >= 0);
                }
            }

            var surfaceLoops = new SurfaceLoops(mesh1.surfaces.Length);
            for (int l = 0; l < holeLoops.Count; l++)
            {
                var hole = holeLoops[l];
                var basePlaneIndex = hole.basePlaneIndex;
                var localPlane = mesh1.surfaces[basePlaneIndex].plane;
                var worldPlane = leaf1.NodeToTreeSpaceMatrix.TransformPlane(localPlane);

                hole.localPlane = localPlane;
                hole.worldPlane = worldPlane;
                //if (hole != null && hole.worldPlane.normal == Vector3.zero) Debug.LogError("!");

                surfaceLoops.surfaces[basePlaneIndex].Add(hole);
            }

            return surfaceLoops;
        }
    }


    internal class OutputLoops
    {
        public CSGTreeBrush                    brush;
        public List<Loop>                      basePolygons        = new List<Loop>();
        public Dictionary<int, SurfaceLoops>   intersectionLoops   = new Dictionary<int, SurfaceLoops>();

        public void Reset()
        {
            intersectionLoops.Clear();
        }

        public void GenerateBasePolygons()
        {
            var mesh                    = BrushMeshManager.GetBrushMesh(brush.BrushMesh.BrushMeshID);
            var halfEdges               = mesh.halfEdges;
            var vertices                = mesh.vertices;
            var surfaces                = mesh.surfaces;
            var polygons                = mesh.polygons;
            var surfacesAroundVertex    = mesh.surfacesAroundVertex;
            var nodeToTreeSpaceMatrix   = brush.NodeToTreeSpaceMatrix;
            basePolygons.Clear();

            for (int p = 0; p < polygons.Length; p++)
            {
                var polygon      = polygons[p];
                var surfaceIndex = polygon.surfaceID;
                var firstEdge    = polygon.firstEdge;
                var lastEdge     = firstEdge + polygon.edgeCount;
                var localPlane   = surfaces[surfaceIndex].plane;
                var worldPlane   = brush.NodeToTreeSpaceMatrix.TransformPlane(localPlane);

                var loop = new Loop()
                {
                    layers = polygon.layers,
                    localPlane = localPlane,
                    worldPlane = worldPlane,
                    basePlaneIndex = surfaceIndex,
                    holes = new List<Loop>()
                };
                //if (loop != null && loop.worldPlane.normal == Vector3.zero) Debug.LogError("!");
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var vertexIndex = halfEdges[e].vertexIndex;
                    var vertex      = vertices[vertexIndex];
                    loop.intersections.Add(new Loop.Intersection()
                    {
                        vertex = nodeToTreeSpaceMatrix.MultiplyPoint(vertex),
                        planeIndices = new HashSet<int>(surfacesAroundVertex[vertexIndex])
                    });
                }
                basePolygons.Add(loop);
            }
        }

        internal void GenerateIntersectionLoops(CSGTreeBrush otherBrush)
        {
            intersectionLoops[otherBrush.NodeID] = SurfaceLoops.GetIntersectionLoops(brush, otherBrush);
        }
    }
    
    public sealed class LoopList
    {
        public readonly List<Loop> loops = new List<Loop>();

        public bool RemoveFrom(Loop loopA, Loop loopB)
        {
            // TODO: optimize


            // We actually know that intersections will have the EXACT positions, so we CAN actually use a hashSet for this
            var knownVerticesA = new HashSet<Vector3>();
            for (int vA1 = 0; vA1 < loopA.intersections.Count; vA1++)
                knownVerticesA.Add(loopA.intersections[vA1].vertex);

            // Dirty little trick. 
            // We know that in the current loop all the vertices are the corners of a polygon (it's convex).
            // We also know that all the vertices in the otherLoop, which share planes with the current loop, 
            // are either these exact corners or something between them, and that they're in order.
            // So we can add vertices in the order of otherLoop to the current Loop when they share planes.
            // Update: doesn't actually work, that's why we need to sort it later.
            bool found = false;
            for (int vA1 = loopA.intersections.Count - 1, vA2 = 0; vA1 >= 0; vA2 = vA1, vA1--)
            {
                foreach (var planeIndex in loopA.intersections[vA1].planeIndices)
                {
                    if (planeIndex < 0)
                        continue;

                    if (planeIndex == loopA.basePlaneIndex)
                        continue;

                    if (!loopA.intersections[vA2].planeIndices.Contains(planeIndex))
                        continue;

                    for (int vB1 = loopB.intersections.Count - 1, vB2 = 0; vB1 >= 0; vB2 = vB1, vB1--)
                    {
                        var intersectionB = loopB.intersections[vB2];
                        if (!intersectionB.planeIndices.Contains(planeIndex))
                            continue;

                        found = true;
                        if (//loopA.FindIndexForVertex(loopB.intersections[vB2].vertex) == -1)
                            knownVerticesA.Contains(intersectionB.vertex))
                            continue;

                        // TODO: store per shared plane, sort based on distance to an intersecting plane
                        knownVerticesA.Add(intersectionB.vertex);
                        loopA.intersections.Insert(vA1 + 1, intersectionB);
                    }
                }
            }
            if (!found)
                return false;

            // TODO: find a way around needing to do this, or at least do this more efficiently
            loopA.SortVertices();

            var knownVerticesB = new HashSet<Vector3>();
            for (int vBi = 0; vBi < loopB.intersections.Count; vBi++)
                knownVerticesB.Add(loopB.intersections[vBi].vertex);

            // Find first vertex on current Loop that is not known on the other Loop
            var vAf = -1;
            for (int vAi = 0; vAi < loopA.intersections.Count; vAi++)
            {
                var vertexA = loopA.intersections[vAi].vertex;
                if (!knownVerticesB.Contains(vertexA)) // TODO: maybe mark intersections instead? no need for hashsets
                {
                    vAf = vAi;
                    break;
                }
            }

            if (vAf == -1) // Check if the two loops are completely overlapping
            {
                // TODO: handle this properly
                loopA.intersections.Clear();
                return true;
            }
             
            // Iterate over current Loop, find first vertex that DOES overlap otherLoop
            {
                var loop = 0;
                var countA = loopA.intersections.Count;
                var countB = loopB.intersections.Count;
                var vAi = vAf;
                do
                {
                    loop++;
                    if (loop > 1000)
                    {
                        Debug.Log("loop > 1000");
                        break;
                    }

                    var vertexA = loopA.intersections[vAi].vertex;
                    if (knownVerticesB.Contains(vertexA))
                    {
                        var vA1 = vAi;

                        // Find index of shared vertex on current Loop
                        var vB1 = loopB.FindIndexForVertex(vertexA);
                        if (vB1 == -1) { Debug.Assert(false); return false; }
                        var vBi = vB1;
                        var vB2 = -1;
                        var vA2 = -1;
                        do
                        {
                            vBi = (vBi + 1) % countB;
                            //vBi = (vBi - 1 + countB) % countB;
                            var vertexB = loopB.intersections[vBi].vertex;
                            if (knownVerticesA.Contains(vertexB))
                            {
                                vB2 = vBi;
                                vA2 = loopA.FindIndexForVertex(vertexB);
                                //vB2 = (vBi - 1 + otherCount) % otherCount;
                                //vB2 = (vBi + 1) % otherCount;
                                break;
                            }
                        } while (vBi != vB1);
                        if (vB2 == -1 ||
                            vA2 == -1) { Debug.Assert(false); return false; }

                        var newIntersections = new List<Loop.Intersection>();

                        if (vB1 < vB2)
                        {
                            for (int i = vB1; i <= vB2; i++)
                                newIntersections.Add(loopB.intersections[i]);
                        }
                        else
                        {
                            for (int i = vB1; i < countB; i++)
                                newIntersections.Add(loopB.intersections[i]);
                            for (int i = 0; i <= vB2; i++)
                                newIntersections.Add(loopB.intersections[i]);

                        }

                        if (vA2 < vA1)
                        {
                            for (int i = vA2; i <= vA1; i++)
                                newIntersections.Add(loopA.intersections[i]);
                        }
                        else
                        {
                            for (int i = vA2; i < countA; i++)
                                newIntersections.Add(loopA.intersections[i]);
                            for (int i = 0; i <= vA1; i++)
                                newIntersections.Add(loopA.intersections[i]);
                        }

                        // TODO: figure out how this is happening and avoid this
                        bool foundThinEdge;
                        do
                        {
                            foundThinEdge = false;
                            if (newIntersections.Count < 3)
                            {
                                newIntersections.Clear();
                                break;
                            }

                            for (int i1 = 0; i1 < newIntersections.Count; i1++)
                            {
                                var i0 = (i1 + newIntersections.Count + -1) % newIntersections.Count;
                                var i2 = (i1 + 1) % newIntersections.Count;
                                if (newIntersections[i1].vertex == newIntersections[i2].vertex)//(newIntersections[i1].vertex - newIntersections[i2].vertex).sqrMagnitude < kSqrMergeEpsilon)
                                {
                                    if (i2 > i1)
                                        newIntersections.RemoveAt(i2);
                                    else
                                        newIntersections.RemoveAt(i1);
                                    foundThinEdge = true;
                                    break;
                                }
                                else
                                if (newIntersections.Count > 2 &&
                                    newIntersections[i0].vertex == newIntersections[i2].vertex)
                                {
                                    if (i2 > i1)
                                    {
                                        newIntersections.RemoveAt(i2);
                                        newIntersections.RemoveAt(i1);
                                    }
                                    else
                                    {
                                        newIntersections.RemoveAt(i1);
                                        newIntersections.RemoveAt(i2);
                                    }
                                    foundThinEdge = true;
                                    break;
                                }
                            }
                        } while (foundThinEdge);

                        if (newIntersections.Count >= 3)
                        {
                            // Create new loop
                            loops.Add(new Loop(loopA, newIntersections));
                        }

                        if (vA2 < vA1)
                        {
                            for (int i = vA1 - 1; i > vA2; i--)
                                loopA.intersections.RemoveAt(i);
                        }
                        else
                        {
                            for (int i = countA - 1; i > vA2; i--)
                                loopA.intersections.RemoveAt(i);
                            for (int i = vA1 - 1; i >= 0; i--)
                                loopA.intersections.RemoveAt(i);
                        }

                        countA = loopA.intersections.Count;

                        //Debug.Log(vA1 + " -> " + vA2 + " | " + vB1 + " -> " + vB2 + " | " + newIntersections.Count);
                        if (countA < 3) break; // check if we still potentially have more loops

                        // Find first vertex on current Loop that is not known on the other Loop
                        vAf = -1;
                        for (int vAn = 0; vAn < countA; vAn++)
                        {
                            if (!knownVerticesB.Contains(loopA.intersections[vAn].vertex))
                            {
                                vAf = vAn;
                                break;
                            }
                        }
                        if (vAf == -1) break; // if we don't have a cross-over, then there's no more loops
                        vAi = (vAf + 1) % countA; // reset our index to the beginning
                    }
                    else
                        vAi = (vAi + 1) % countA;
                } while (vAi != vAf);
            }

            loops.Remove(loopA);
            return true;
        }
    }
#endif
}
