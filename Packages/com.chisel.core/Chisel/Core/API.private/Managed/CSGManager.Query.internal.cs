using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
    // TODO: clean up
    static partial class CSGManager
    {

        // Requirement: out values only set when something is found, otherwise are not modified
        static bool BrushRayCast (CSGTreeBrush      brush,

						          MeshQuery[]       meshQuery,              // TODO: add meshquery support here
                                  HashSet<Int32>    ignoreBitset,

						          Vector3           treeSpaceRayStart,
						          Vector3           treeSpaceRayEnd,

						          ref float	        smallestDistance,
	
						          out bool	        out_isReversed,
                                  out Int32         out_surfaceIndex,
                                  out Int32         out_surfaceID,

						          out Vector2       out_surfaceIntersection,
						          out Vector3       out_localIntersection,
						          out Vector3       out_treeIntersection,

						          out Plane         out_localPlane,
						          out Plane         out_treePlane)
        {
            out_isReversed          = false;
            out_surfaceIndex        = -1;
            out_surfaceID           = -1;
            out_surfaceIntersection = Vector2.zero;
            out_localIntersection   = Vector3.zero;
            out_treeIntersection    = Vector3.zero;

            out_localPlane          = new Plane();
            out_treePlane           = new Plane();
            
            var brushMeshInstanceID = brush.BrushMesh.brushMeshID;
            var brushMesh           = BrushMeshManager.GetBrushMesh(brushMeshInstanceID);

            if (brushMesh.planes == null ||
                brushMesh.planes.Length == 0)
		        return false;

            var treeToNodeSpace = brush.TreeToNodeSpaceMatrix;
            var nodeToTreeSpace = brush.NodeToTreeSpaceMatrix;

            var brushRayStart	= treeToNodeSpace.MultiplyPoint(treeSpaceRayStart);
            var brushRayEnd     = treeToNodeSpace.MultiplyPoint(treeSpaceRayEnd);
	        var brushRayDelta	= brushRayEnd - brushRayStart;


	        var found						= false;
            var result_surfaceIndex			= -1;
            var result_localIntersection	= Vector3.zero;
            var result_isReversed			= false;
            var smallest_t					= smallestDistance;

            var brush_ray_start	= brushRayStart;
            var brush_ray_end	= brushRayEnd;

	        for (var s = 0; s < brushMesh.planes.Length; s++)
	        {
                var plane       = new Plane(brushMesh.planes[s].xyz, brushMesh.planes[s].w);
                var s_dist      = plane.GetDistanceToPoint(brush_ray_start);
                var e_dist	    = plane.GetDistanceToPoint(brush_ray_end);
                var length	    = s_dist - e_dist;

                var t		= s_dist / length;
		        if (!(t >= 0.0f && t <= 1.0f)) // NaN will return false on both comparisons, outer not is intentional
			        continue;

                var delta	= brushRayDelta * t;

                // only pick the plane that is closest
                var dist = delta.sqrMagnitude;
		        if (dist > smallest_t) // NaN will return false, smallest_t = Positive Infinity will return false
			        continue;

                var intersection = brush_ray_start + delta;

		        // make sure the point is on the brush
		        bool skipSurface = false;
		        for (var s2 = 0; s2 < brushMesh.planes.Length; s2++)
		        {
			        if (s == s2)
				        continue;

                    var plane2  = new Plane(brushMesh.planes[s2].xyz, brushMesh.planes[s2].w);
                    var pl_dist = plane2.GetDistanceToPoint(intersection);
			        if (pl_dist > MathExtensions.kDistanceEpsilon) { skipSurface = true; break; }
		        }
		        if (skipSurface)
			        continue;

                // TODO: fix these issues:
                /*
		        var treeIntersection	= nodeToTreeSpace.MultiplyPoint(intersection);
                var type				= CSGCategorizationStack.CategorizePoint(in_brush, treeIntersection, ignoreBitset);
		        if (ignoreInvisiblePolygons)
		        {
			        if (type == CategoryIndex.Inside || type == CategoryIndex.Outside)
				        continue;
		        }

		        bool is_reversed = (type == CategoryIndex.ReverseAligned);

		        if (is_reversed)
		        {
			        if (s_dist > MathExtensions.kDistanceEpsilon)
				        continue;
		        } else
		        {
			        if (s_dist <= -MathExtensions.kDistanceEpsilon)
				        continue;
                }
                */

                /*
		        result_surfaceIndex		 = s;

                // we can have multiple planes on the same surface
                var mesh_surface_index = FindSurfaceForMeshPoint(brushSurfaces.planes, intersection);
                if (mesh_surface_index > -1)
                    result_surfaceIndex = mesh_surface_index;

                // TODO: do this properly with MeshQueries
                if (filterLayerParameter0 != 0)
                {
                    var layer = brushSurfaces.layers[(int)result_surfaceIndex];
                    if (layer.layerParameters[0] != 0 &&
                        layer.layerParameters[0] != filterLayerParameter0)
                        continue;
                }
                */

                found = true;
		        smallest_t  = dist;

		        result_isReversed		 = false;//is_reversed;
		        result_localIntersection = intersection;
	        }

	        if (found)
	        {
		        smallestDistance	    = smallest_t;
		        out_surfaceIndex		= result_surfaceIndex;
		        out_localIntersection   = result_localIntersection;
		        out_treeIntersection	= nodeToTreeSpace.MultiplyPoint(result_localIntersection);
		        out_isReversed			= result_isReversed;
		        return true;
	        }
	        return false;
        }


        // TODO:	problem with RayCastMulti is that this code is too slow
        //			solution:	1.	replace RayCastMulti with a way to 'simply' changing the in_rayStart 
        //							and only finding the closest brush.
        //						2.	cache the categorization-table per brush (the generation is the slow part)
        //							in a way that we only need to regenerate it if it doesn't touch an 'ignored' brush
        //							(in fact, we could still cache the generated table w/ ignored brushes while moving the mouse)
        //						3.	have a ray-brush acceleration data structure so that we reduce the number of brushes to 'try'
        //							to only the ones that the ray actually intersects with (instead of -all- brushes)
        internal static bool RayCastMulti(CSGTree                           tree,
                                          MeshQuery[]                       meshQuery,
                                          Vector3                           worldRayStart,
                                          Vector3                           worldRayEnd,
                                          Matrix4x4                         treeLocalToWorldMatrix, // TODO: pass along worldToLocal matrix instead
                                          int                               filterLayerParameter0,
                                          out CSGTreeBrushIntersection[]    intersections,
                                          CSGTreeNode[]                     ignoreNodes = null)
        {
            var ignoreNodeIndices = new HashSet<int>();
            if (ignoreNodes != null)
            {
                for (var i = 0; i < ignoreNodes.Length; i++)
                {
                    if (!ignoreNodes[i].Valid)
                        continue;

                    ignoreNodeIndices.Add(ignoreNodes[i].NodeID);
                }
            }

            var worldToTreeLocalSpace               = treeLocalToWorldMatrix.inverse;
            var treeToWorldSpaceInverseTransposed   = worldToTreeLocalSpace.transpose;

            intersections = null;
            var foundIntersections = new List<CSGTreeBrushIntersection>();
            var bounds = new Bounds();
            
            var treeSpaceRayStart   = worldToTreeLocalSpace.MultiplyPoint(worldRayStart);
            var treeSpaceRayEnd     = worldToTreeLocalSpace.MultiplyPoint(worldRayEnd);
            
            var treeNodeID      = tree.NodeID;
            var treeNodeIndex   = treeNodeID - 1;
            var treeInfo        = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;

            var treeSpaceRay    = new Ray(treeSpaceRayStart, treeSpaceRayEnd - treeSpaceRayStart);

            // TODO: optimize
            for (int i = 0; i < treeInfo.treeBrushes.Count; i++)
            {
                var brushNodeID = treeInfo.treeBrushes[i];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                if (ignoreNodeIndices.Contains(brushNodeID))
                    continue;

                //var operation_type_bits = CSGManager.GetNodeOperationByIndex(brushNodeID);
                //if (((int)operation_type_bits & InfiniteBrushBits) == InfiniteBrushBits)
                //    continue;

                if (!CSGManager.GetBrushBounds(brushNodeID, ref bounds))
                    continue;

                if (!bounds.IntersectRay(treeSpaceRay))
                    continue;

                var brush = new CSGTreeBrush() { brushNodeID = brushNodeID };

                var resultDist = float.PositiveInfinity;
			    if (!BrushRayCast(brush,

                                  meshQuery,
                                  ignoreNodeIndices,

								  treeSpaceRayStart,
								  treeSpaceRayEnd,

								  ref resultDist,

								  out bool      result_isReversed,
                                  out Int32     result_surfaceIndex,
                                  out Int32     result_surfaceID,
                                  out Vector2   result_surfaceIntersection,
                                  out Vector3   result_localIntersection,
                                  out Vector3   result_treeIntersection,
                                  out Plane     result_localPlane,
                                  out Plane     result_treePlane))
				    continue;


                var result_worldPlane = treeToWorldSpaceInverseTransposed.Transform(result_treePlane);			    
                foundIntersections.Add(new CSGTreeBrushIntersection()
                { 
                    tree                = tree,
                    brush               = brush,
			        surfaceID			= result_surfaceID,
                    brushUserID         = CSGManager.GetUserIDOfNode(brushNodeID),

                    surfaceIntersection = new ChiselSurfaceIntersection()
                    { 
                        //localPlane            = result_isReversed ? result_localPlane.flipped : result_localPlane,
				        treePlane               = result_isReversed ? result_treePlane.flipped : result_treePlane,
                        //worldPlane            = result_isReversed ? result_worldPlane.flipped : result_worldPlane,

                        treePlaneIntersection   = treeLocalToWorldMatrix.MultiplyPoint(result_treeIntersection),
                        //worldIntersection	    = treeLocalToWorldMatrix.MultiplyPoint(result_treeIntersection),
			            //surfaceIntersection	= result_surfaceIntersection,

			            distance			    = resultDist,
                    }
                });
            }

            if (foundIntersections.Count == 0)
                return false;

            intersections = foundIntersections.ToArray();
            return true;
        }

        internal static bool GetNodesInFrustum(MeshQuery[]          meshQueries, // TODO: add meshquery support here
                                               Plane[]              planes,
                                               out CSGTreeNode[]    nodes)
        {
            nodes = null;

            if (planes == null)
                throw new ArgumentNullException("planes");

            if (planes.Length != 6)
                throw new ArgumentException("planes requires to be an array of length 6", "planes");

            for (int p = 0; p < 6; p++)
            {
                var plane       = planes[p];
                var normal      = plane.normal;
                var distance    = plane.distance;
                var n = normal.x + normal.y + normal.z + distance;
                if (float.IsInfinity(n) || float.IsNaN(n))
                    return false;
            }

            var foundNodes = new List<CSGTreeNode>();
            var bounds = new Bounds();
            for (int i = 0; i < CSGManager.brushes.Count; i++)
            {
                var brushNodeID     = CSGManager.brushes[i];
                var brushNodeIndex  = brushNodeID - 1;
                if (!CSGManager.GetBrushBounds(brushNodeID, ref bounds))
                    continue;

                // TODO: take transformations into account? (frustum is already in tree space)

                bool intersectsFrustum = false;
                for (int p = 0; p < 6; p++)
                {
                    if (planes[p].IsOutside(bounds))
                        goto SkipBrush;

                    if (!planes[p].IsInside(bounds))
                        intersectsFrustum = true;
                }

                var treeNodeID          = nodeHierarchies[brushNodeIndex].treeNodeID;
                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeID - 1];

                if (intersectsFrustum)
                {
                    if (!chiselLookupValues.brushRenderBuffers.TryGetValue(brushNodeIndex, out var brushRenderBuffers) ||
                        !brushRenderBuffers.IsCreated)
                        continue;

                    ref var surfaceRenderBuffers = ref brushRenderBuffers.Value.surfaces;

                    // Double check if the vertices of the brush are inside the frustum
                    for (int s=0;s< surfaceRenderBuffers.Length;s++)
                    {
                        ref var surfaceRenderBuffer = ref surfaceRenderBuffers[s];

                        // Compare surface with 'current' meshquery (is this surface even being rendered???)
                        for (int n = 0; n < meshQueries.Length; n++)
                        {
                            var meshQuery = meshQueries[n];
                            var core_surface_flags = surfaceRenderBuffer.surfaceLayers.layerUsage;
                            if ((core_surface_flags & meshQuery.LayerQueryMask) != meshQuery.LayerQuery)
                                goto SkipSurface;
                        }

                        ref var vertices = ref surfaceRenderBuffer.vertices;
                        for (int p = 0; p < 6; p++)
                        {
                            var plane = planes[p];
                            for (int v = 0; v < vertices.Length; v++)
                            {
                                var distance = plane.GetDistanceToPoint(vertices[v]);
                                if (distance > MathExtensions.kDistanceEpsilon)
                                    goto SkipBrush;
                            }
                        }
SkipSurface:
                        ;
                    }
                }

                foundNodes.Add(CSGTreeNode.Encapsulate(brushNodeID));
SkipBrush:
                ;
            }

            if (foundNodes.Count == 0)
                return false;

            nodes = foundNodes.ToArray();
            return true;
        }
    }
}
