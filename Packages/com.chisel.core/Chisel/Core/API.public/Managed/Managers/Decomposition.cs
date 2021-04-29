using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Chisel.Core.External;
using Unity.Collections;
using Unity.Mathematics;
using UnitySceneExtensions;

namespace Chisel.Core
{
    // TODO: replace with complete managed solution, clean up
    public partial class Decomposition
    {
        public static bool ConvexPartition(List<SegmentVertex> 	    inputVertices2D,
                                           out SegmentVertex[][]    outputPolygonVertices2D)
        {
            var outputVertices = new List<SegmentVertex>();
            var outputRanges = new List<int>();

            // TODO: Optimize all these useless allocations away (Note: ConvexPartition can modify the given list under some circumstances)
            if (!BayazitDecomposer.ConvexPartition(inputVertices2D, outputVertices, outputRanges))
            {
                outputPolygonVertices2D = null;
                return false;
            }

            outputPolygonVertices2D = new SegmentVertex[outputRanges.Count][];
            for (int i = 0; i < outputRanges.Count; i++)
            {
                var start           = (i == 0) ? 0 : outputRanges[i - 1];
                var end             = outputRanges[i];
                var count           = end - start;
                var polygonVertices = new SegmentVertex[count];
                for (int d = 0, p = start; p < end; p++, d++)
                    polygonVertices[d] = outputVertices[p];                
                outputPolygonVertices2D[i] = polygonVertices;
            }
            return true;
        }
    }
}
