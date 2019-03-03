//	Poly2Mesh
//
//	This is a static class that wraps up all the details of creating a Mesh
//	(or even an entire GameObject) out of a polygon.  The polygon must be
//	planar, and should be well-behaved (no duplicate points, etc.), but it
//	can have any number of non-overlapping holes.  In addition, the polygon
//	can be in ANY plane -- it doesn't have to be the XY plane.  Huzzah!
//
//	To use:
//		1. Create a Poly2Mesh.Polygon.
//		2. Set its .outside to a list of Vector3's describing the outside of the shape.
//		3. [Optional] Add to its .holes list as desired.
//		4. [Optional] Call CalcPlaneNormal on it, passing in a hint as to which way you
//			want the polygon to face.
//		5. Pass it to Poly2Mesh.CreateMesh, or Poly2Mesh.CreateGameObject.

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Poly2Tri;
using System.Linq;

public static class Poly2Mesh {

	// Polygon: defines the input to the triangulation algorithm.
	public class Polygon {
		// outside: a set of points defining the outside of the polygon
		public List<Vector3> outside;

		// holes: a (possibly empty) list of non-overlapping holes within the polygon
		public List<List<Vector3>> holes;

		// normal to the plane containing the polygon (normally calculated by CalcPlaneNormal)
		public Vector3 planeNormal;

		// rotation into the XY plane (normally calculated by CalcRotation)
		public Quaternion rotation = Quaternion.identity;

        public bool inverse = false;

		// constructor (just initializes the lists)
		public Polygon() {
			outside = new List<Vector3>();
			holes = new List<List<Vector3>>();
		}

		/// <summary>
		/// Calculates the rotation needed to get this polygon into the XY plane.
		/// </summary>
		public void CalcRotation() {
			if (planeNormal == Vector3.forward) {
				// Special case: our polygon is already in the XY plane, no rotation needed.
				rotation = Quaternion.identity;
			} else {
				rotation = Quaternion.FromToRotation(planeNormal, Vector3.forward);
			}
		}
	}

	/// <summary>
	/// Helper method to convert a set of 3D points into the 2D polygon points
	/// needed by the Poly2Tri code.
	/// </summary>
	/// <returns>List of 2D PolygonPoints.</returns>
	/// <param name="points">3D points.</param>
	/// <param name="rotation">Rotation needed to convert 3D points into the XY plane.</param>
	/// <param name="name="codeToPosition">Map (which we'll update) of PolygonPoint vertex codes to original 3D position.</param> 
	internal static List<PolygonPoint> ConvertPoints(List<Vector3> points, Quaternion rotation, Dictionary<uint, Vector3> codeToPosition) {
		int count = points.Count;
		List<PolygonPoint> result = new List<PolygonPoint>(count);
		for (int i=0; i<count; i++) {
			Vector3 originalPos = points[i];
			Vector3 p = rotation * originalPos;
			PolygonPoint pp = new PolygonPoint(p.x, p.y);
//			Debug.Log("Rotated " + originalPos.ToString("F4") + " to " + p.ToString("F4"));
			codeToPosition[pp.VertexCode] = originalPos;
			result.Add(pp);
		}
		return result;
	}
    
    /// <summary>
    /// Create a Mesh from a given Polygon.
    /// </summary>
    /// <returns>The freshly minted mesh.</returns>
    /// <param name="polygon">Polygon you want to triangulate.</param>
    public static bool CreateMesh(List<Poly2Mesh.Polygon> polygons, ref Mesh dstMesh)
	{
		// TODO: use vertex indices instead of actual vertices to find original vertices

		var poly2TriPolygons = new List<Poly2Tri.Polygon>();
		var codeToPositions = new List<Dictionary<uint, Vector3>>();
		var zs = new List<float>();

		// Ensure we have the rotation properly calculated, and have a valid normal
		for (int p = 0; p < polygons.Count; p++)
		{
			if (polygons[p].rotation == Quaternion.identity) polygons[p].CalcRotation();
			if (polygons[p].planeNormal == Vector3.zero)
			{
				Debug.Log("polygons[p].planeNormal == Vector3.zero");
				return false;       // bad data
			}
			
			// Rotate 1 point and note where it ends up in Z
			float z = (polygons[p].rotation * polygons[p].outside[0]).z;

			// Prepare a map from vertex codes to 3D positions.
			Dictionary<uint, Vector3> codeToPosition = new Dictionary<uint, Vector3>();

			// Convert the outside points (throwing out Z at this point)
			Poly2Tri.Polygon poly = new Poly2Tri.Polygon(ConvertPoints(polygons[p].outside, polygons[p].rotation, codeToPosition));


			// Convert each of the holes
			if (polygons[p].holes != null)
			{
				foreach (List<Vector3> hole in polygons[p].holes)
				{
					poly.AddHole(new Poly2Tri.Polygon(ConvertPoints(hole, polygons[p].rotation, codeToPosition)));
				}
			}

			codeToPositions.Add(codeToPosition);
			poly2TriPolygons.Add(poly);
			zs.Add(z);
		}

		// Triangulate it!  Note that this may throw an exception if the data is bogus.
		for (int p = 0; p < poly2TriPolygons.Count; p++)
		{
			try
			{
				DTSweepContext tcx = new DTSweepContext();
				tcx.PrepareTriangulation(poly2TriPolygons[p]);
				DTSweep.Triangulate(tcx);
				tcx = null;
			} catch (System.Exception e) {
				//Profiler.Exit(profileID);
				Debug.LogException(e);
				//throw e;
			}
		}

		// Now, to get back to our original positions, use our code-to-position map.  We do
		// this instead of un-rotating to be a little more robust about noncoplanar polygons.

		// Create the Vector3 vertices (undoing the rotation),
		// and also build a map of vertex codes to indices
		Quaternion? invRot = null;
		Dictionary<uint, int> codeToIndex = new Dictionary<uint, int>();
		List<Vector3> vertexList = new List<Vector3>();
		List<int> indexList = new List<int>();
		int triangleCount = 0;
		for (int p = 0; p < polygons.Count; p++)
		{
			var poly = poly2TriPolygons[p];
			var polygon = polygons[p];
			var z = zs[p];
			var codeToPosition = codeToPositions[p];
			triangleCount += poly.Triangles.Count;
			codeToIndex.Clear();
			foreach (DelaunayTriangle t in poly.Triangles)
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
				}
			}
            if (polygon.inverse)
            {
                foreach (DelaunayTriangle t in poly.Triangles)
                {
                    indexList.Add(codeToIndex[t.Points[2].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[1].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[0].VertexCode]);
                }
            } else
            {
                foreach (DelaunayTriangle t in poly.Triangles)
                {
                    indexList.Add(codeToIndex[t.Points[0].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[1].VertexCode]);
                    indexList.Add(codeToIndex[t.Points[2].VertexCode]);
                }
            }
		}

		// Create the indices array
		var indices = indexList.ToArray();

		// Create the mesh
		dstMesh.vertices = vertexList.ToArray();
		dstMesh.triangles = indices;
		dstMesh.RecalculateNormals();
		return true;
	}
}
