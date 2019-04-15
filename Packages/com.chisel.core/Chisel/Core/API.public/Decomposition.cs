using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vector2 = UnityEngine.Vector2;

namespace Chisel.Core
{
    // TODO: replace with complete managed solution, clean up
    public partial class Decomposition
    {
        public static Vector2[][] ConvexPartition(Vector2[] inputVertices2D)
        {
            var polygonCount = DecomposeStart(inputVertices2D);
            if (polygonCount == 0)
                return null;

            var polygonSizes = new Int32[polygonCount];
            if (!DecomposeGetSizes(polygonSizes))
                return null;

            var polygons = new List<Vector2[]>();
            for (int i = 0; i < polygonCount; i++)
            {
                var vertexCount = polygonSizes[i];
                var vertices	= DecomposeGetPolygon(i, vertexCount);
                if (vertices == null)
                    return null;
                polygons.Add(vertices);
            }
            return polygons.ToArray();
        }
        
        public static bool ConvexPartition(List<Vector2>	inputVertices2D,
                                           List<int>		segmentIndices,
                                           out Vector2[][]	outputPolygonVertices2D,
                                           out int[][]		outputPolygonIndices)
        {
            var points = inputVertices2D.ToArray();
            outputPolygonVertices2D = ConvexPartition(points);
            if (outputPolygonVertices2D == null)
            {
                outputPolygonIndices = null;
                return false;
            }

            var pointToIndex = new Dictionary<Vector2, int>();
            for (int i = 0; i < points.Length; i++)
                pointToIndex[points[i]] = i;

            outputPolygonIndices = new int[outputPolygonVertices2D.Length][];
            for (int i = 0; i < outputPolygonVertices2D.Length; i++)
            {
                var polygonVertices = outputPolygonVertices2D[i];
                var polygonIndices	= new int[polygonVertices.Length];

                for (int p = 0; p < polygonVertices.Length; p++)
                {
                    var point = polygonVertices[p];
                    int index;
                    if (!pointToIndex.TryGetValue(point, out index))
                    {
                        polygonIndices[p] = segmentIndices[0];
                    } else
                        polygonIndices[p] = segmentIndices[index];
                }

                outputPolygonIndices[i] = polygonIndices;
            }			
            return true;
        }
    }
}
