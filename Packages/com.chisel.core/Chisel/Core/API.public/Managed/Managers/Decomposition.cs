using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Vector2 = UnityEngine.Vector2;

namespace Chisel.Core
{
    // TODO: replace with complete managed solution, clean up
    public partial class Decomposition
    {
        static Dictionary<Vector2, int> s_PointToIndex = new Dictionary<Vector2, int>();
        public static bool ConvexPartition(List<Vector2>	inputVertices2D,
                                           List<int>		segmentIndices,
                                           out Vector2[][]	outputPolygonVertices2D,
                                           out int[][]		outputPolygonIndices)
        {
            // TODO: Optimize all these useless allocations away (Note: ConvexPartition can modify the given list under some circumstances)
            var points = inputVertices2D.ToArray();
            outputPolygonVertices2D = BayazitDecomposer.ConvexPartition(points.ToList());
            if (outputPolygonVertices2D == null)
            {
                outputPolygonIndices = null;
                return false;
            }

            s_PointToIndex.Clear();
            for (int i = 0; i < points.Length; i++)
                s_PointToIndex[points[i]] = i;

            outputPolygonIndices = new int[outputPolygonVertices2D.Length][];
            for (int i = 0; i < outputPolygonVertices2D.Length; i++)
            {
                var polygonVertices = outputPolygonVertices2D[i];
                var polygonIndices	= new int[polygonVertices.Length];

                for (int p = 0; p < polygonVertices.Length; p++)
                {
                    var point = polygonVertices[p];
                    if (!s_PointToIndex.TryGetValue(point, out var index))
                        polygonIndices[p] = segmentIndices[0];
                    else
                        polygonIndices[p] = segmentIndices[index];
                }

                outputPolygonIndices[i] = polygonIndices;
            }
            return true;
        }
    }
}
