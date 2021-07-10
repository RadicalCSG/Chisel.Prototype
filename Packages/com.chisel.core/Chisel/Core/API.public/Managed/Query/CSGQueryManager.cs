using System;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public enum VisibilityState
    {
        Unknown         = 0,
        AllVisible      = 1,
        AllInvisible    = 2,
        Mixed           = 3
    }

    // TODO: clean up
    public static class CSGQueryManager
    {
        // TODO: find a better place
        static bool IsSurfaceVisible(MeshQuery[] meshQueries, ref ChiselSurfaceRenderBuffer surface)
        {
            // Compare surface with 'current' meshquery (is this surface even being rendered???)
            for (int n = 0; n < meshQueries.Length; n++)
            {
                var meshQuery = meshQueries[n];
                var core_surface_flags = surface.surfaceLayers.layerUsage;
                if ((core_surface_flags & meshQuery.LayerQueryMask) == meshQuery.LayerQuery)
                    return true;
            }
            return false;
        }

        // TODO: find a better place
        static bool IsPointInsideSurface(ref ChiselSurfaceRenderBuffer surface, float3 treeSpacePoint, out float3 treeSpaceNormal)
        {
            ref var triangles	= ref surface.indices;
            ref var vertices    = ref surface.colliderVertices;

            for (int i = 0, triangle_count = triangles.Length; i < triangle_count; i += 3)
	        {
                var v0 = vertices[triangles[i + 0]];
                var v1 = vertices[triangles[i + 1]];
                var v2 = vertices[triangles[i + 2]];

                if (GeometryMath.PointInTriangle(treeSpacePoint, v0, v2, v1)) 
                {
                    treeSpaceNormal = math.normalizesafe(math.cross(v2 - v0, v2 - v1));
                    return true;
                }
	        }

            treeSpaceNormal = float3.zero;
            return false;
        }
        
        // Requirement: out values only set when something is found, otherwise are not modified
        static bool BrushRayCast (MeshQuery[]       meshQueries,
                                  CSGTree           tree,
                                  CSGTreeBrush      brush,

                                  ref BlobArray<ChiselSurfaceRenderBuffer> surfaces,

						          Vector3           treeSpaceRayStart,
						          Vector3           treeSpaceRayEnd,

                                  bool              ignoreBackfaced,
                                  bool              ignoreCulled,

                                  List<CSGTreeBrushIntersection> foundIntersections)
        {
            var brushMeshInstanceID = brush.BrushMesh.brushMeshHash;
            var brushMeshBlob       = BrushMeshManager.GetBrushMeshBlob(brushMeshInstanceID);
            if (!brushMeshBlob.IsCreated)
                return false;

            ref var brushMesh = ref brushMeshBlob.Value;
            ref var planes = ref brushMesh.localPlanes;
            ref var planeCount = ref brushMesh.localPlaneCount;
            if (planeCount == 0)
		        return false;

            var treeToNodeSpace = (Matrix4x4)brush.TreeToNodeSpaceMatrix;
            var nodeToTreeSpace = (Matrix4x4)brush.NodeToTreeSpaceMatrix;

            var brushRayStart	= treeToNodeSpace.MultiplyPoint(treeSpaceRayStart);
            var brushRayEnd     = treeToNodeSpace.MultiplyPoint(treeSpaceRayEnd);
	        var brushRayDelta	= brushRayEnd - brushRayStart;

            var found		    = false;

            var brush_ray_start	= brushRayStart;
            var brush_ray_end	= brushRayEnd;
            for (var s = 0; s < planeCount; s++)
            {
                // Compare surface with 'current' meshquery (is this surface even being rendered???)
                if (ignoreCulled)
                {
                    if (surfaces[s].indices.Length == 0)
                        continue;

                    if (!IsSurfaceVisible(meshQueries, ref surfaces[s]))
                        continue;

                    Debug.Assert(surfaces[s].surfaceIndex == s);
                }

                var plane = new Plane(planes[s].xyz, planes[s].w);
                var s_dist = plane.GetDistanceToPoint(brush_ray_start);
                var e_dist = plane.GetDistanceToPoint(brush_ray_end);
                var length = s_dist - e_dist;

                var t = s_dist / length;
                if (!(t >= 0.0f && t <= 1.0f)) // NaN will return false on both comparisons, outer not is intentional
                    continue;

                var delta = brushRayDelta * t;

                var intersection = brush_ray_start + delta;

                // make sure the point is on the brush
                //if (!brushMesh.localBounds.Contains(intersection))
                //    continue;
                bool skipSurface = false;
                for (var s2 = 0; s2 < planeCount; s2++)
                {
                    if (s == s2)
                        continue;

                    var plane2 = new Plane(planes[s2].xyz, planes[s2].w);
                    var pl_dist = plane2.GetDistanceToPoint(intersection);
                    if (pl_dist > MathExtensions.kDistanceEpsilon) { skipSurface = true; break; }
                }

                if (skipSurface)
                    continue;
                
                var treeIntersection = nodeToTreeSpace.MultiplyPoint(intersection);
                if (!IsPointInsideSurface(ref surfaces[s], treeIntersection, out var treeSpaceNormal))
                {
                    if (ignoreCulled)
                        continue;
                }

                var localSpaceNormal = treeToNodeSpace.MultiplyVector(treeSpaceNormal);

                // Ignore backfaced culled triangles
                if (ignoreBackfaced && math.dot(localSpaceNormal, brushRayStart - intersection) < 0)
                    continue;

                found = true;
                var result_isReversed		    = math.dot(localSpaceNormal, plane.normal) < 0;
                var result_localPlane           = plane;
                var result_surfaceIndex         = s;
                var result_localIntersection    = intersection;

                var treePlane               = nodeToTreeSpace.TransformPlane(result_localPlane);

                var result_dist             = delta.sqrMagnitude;
                var result_treeIntersection = nodeToTreeSpace.MultiplyPoint(result_localIntersection);
                var result_treePlane        = result_isReversed ? treePlane.flipped : treePlane;

                foundIntersections.Add(new CSGTreeBrushIntersection()
                { 
                    tree            = tree,
                    brush           = brush,

                    surfaceIndex    = result_surfaceIndex,

                    surfaceIntersection = new ChiselSurfaceIntersection()
                    {
                        treePlane               = result_treePlane,
                        treePlaneIntersection   = result_treeIntersection,
			            distance			    = result_dist,
                    }
                });
            }

	        return found;
        }



        // TODO:	problem with RayCastMulti is that this code is too slow
        //			solution:	1.	replace RayCastMulti with a way to 'simply' changing the in_rayStart 
        //							and only finding the closest brush.
        //						2.	cache the categorization-table per brush (the generation is the slow part)
        //							in a way that we only need to regenerate it if it doesn't touch an 'ignored' brush
        //							(in fact, we could still cache the generated table w/ ignored brushes while moving the mouse)
        //						3.	have a ray-brush acceleration data structure so that we reduce the number of brushes to 'try'
        //							to only the ones that the ray actually intersects with (instead of -all- brushes)
        static readonly HashSet<CSGTreeNode> s_IgnoreNodeIndices = new HashSet<CSGTreeNode>();
        static readonly HashSet<CSGTreeNode> s_FilterNodeIndices = new HashSet<CSGTreeNode>();


        static readonly List<CSGTreeBrushIntersection> s_FoundIntersections = new List<CSGTreeBrushIntersection>();

        public static CSGTreeBrushIntersection[] RayCastMulti(MeshQuery[]       meshQueries, 
                                                              CSGTree           tree,
                                                              Vector3           treeSpaceRayStart,
                                                              Vector3           treeSpaceRayEnd,
                                                              List<CSGTreeNode> ignoreNodes = null,
                                                              List<CSGTreeNode> filterNodes = null,
                                                              bool              ignoreBackfaced = true, 
                                                              bool              ignoreCulled = true)
        {
            if (!tree.Valid)
                return null;

            s_IgnoreNodeIndices.Clear();
            if (ignoreNodes != null)
            {
                for (var i = 0; i < ignoreNodes.Count; i++)
                {
                    if (!ignoreNodes[i].Valid ||
                        ignoreNodes[i].Type != CSGNodeType.Brush)
                        continue;

                    s_IgnoreNodeIndices.Add(ignoreNodes[i]);
                }
            }
            s_FilterNodeIndices.Clear();
            if (filterNodes != null)
            {
                for (var i = 0; i < filterNodes.Count; i++)
                {
                    if (!filterNodes[i].Valid ||
                        filterNodes[i].Type != CSGNodeType.Brush)
                        continue; 

                    s_FilterNodeIndices.Add(filterNodes[i]);
                }
            }

            s_FoundIntersections.Clear();

            using (var treeBrushes = new NativeList<CompactNodeID>(Allocator.Temp))
            {
                // TODO: cache this
                CompactHierarchyManager.GetHierarchy(tree).GetTreeNodes(default, treeBrushes);

                var brushCount = treeBrushes.Length;
                if (brushCount == 0)
                    return null;
            
                var treeSpaceRay            = new Ray(treeSpaceRayStart, treeSpaceRayEnd - treeSpaceRayStart);
                var brushRenderBufferLookup = ChiselTreeLookup.Value[tree].brushRenderBufferLookup;

                // TODO: optimize
                for (int i = 0; i < brushCount; i++)
                {
                    var brush = CSGTreeBrush.Find(treeBrushes[i]);
#if UNITY_EDITOR
                    if (!brush.IsSelectable)
                        continue;
#endif
                    var minMaxAABB = brush.Bounds;
                    if (minMaxAABB.IsEmpty)
                        continue;
                    if (s_IgnoreNodeIndices.Contains(brush) ||
                        (s_FilterNodeIndices.Count > 0 && !s_FilterNodeIndices.Contains(brush)))
                        continue;

                    var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brush);
                    if (!brushRenderBufferLookup.TryGetValue(brushCompactNodeID, out var brushRenderBuffer))
                        continue;

                    var bounds = new Bounds((minMaxAABB.Max + minMaxAABB.Min) / 2, minMaxAABB.Max - minMaxAABB.Min);
                    if (!bounds.IntersectRay(treeSpaceRay))
                        continue;

                    BrushRayCast(meshQueries, tree, brush,

                                    ref brushRenderBuffer.Value.surfaces,

                                    treeSpaceRayStart,
                                    treeSpaceRayEnd,

                                    ignoreBackfaced,
                                    ignoreCulled,

                                    s_FoundIntersections);
                }

                if (s_FoundIntersections.Count == 0)
                    return null;

                return s_FoundIntersections.ToArray();
            }
        }


        static readonly List<CSGTreeNode> s_FoundNodes = new List<CSGTreeNode>();

        public static CSGTreeNode[] GetNodesInFrustum(CSGTree       tree,
                                                      MeshQuery[]   meshQueries, // TODO: add meshquery support here
                                                      Plane[]       planes)
        {
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
                    return null;
            }

            using (var treeBrushes = new NativeList<CompactNodeID>(Allocator.Temp))
            {
                CompactHierarchyManager.GetHierarchy(tree).GetTreeNodes(default, treeBrushes);

                var brushCount = treeBrushes.Length;
                if (brushCount == 0)
                    return null;

                s_FoundNodes.Clear();
                var brushRenderBufferLookup = ChiselTreeLookup.Value[tree].brushRenderBufferLookup;

                for (int i = 0; i < brushCount; i++)
                {
                    var brush = CSGTreeBrush.Find(treeBrushes[i]);
#if UNITY_EDITOR
                    if (!brush.IsSelectable)
                        continue;
#endif

                    var minMaxAABB = brush.Bounds;
                    if (minMaxAABB.IsEmpty)
                        continue;

                    var bounds              = new Bounds((minMaxAABB.Max + minMaxAABB.Min) / 2, minMaxAABB.Max - minMaxAABB.Min);
                    // TODO: take transformations into account? (frustum is already in tree space)

                    bool intersectsFrustum = false;
                    for (int p = 0; p < 6; p++)
                    {
                        if (planes[p].IsOutside(bounds))
                            goto SkipBrush;

                        if (!planes[p].IsInside(bounds))
                            intersectsFrustum = true;
                    }

                    if (intersectsFrustum)
                    {
                        var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brush);
                        if (!brushRenderBufferLookup.TryGetValue(brushCompactNodeID, out var brushRenderBuffers) ||
                            !brushRenderBuffers.IsCreated)
                            continue;

                        ref var surfaceRenderBuffers = ref brushRenderBuffers.Value.surfaces;

                        bool haveVisibleSurfaces = false;

                        // Double check if the vertices of the brush are inside the frustum
                        for (int s = 0; s < surfaceRenderBuffers.Length; s++)
                        {
                            ref var surfaceRenderBuffer = ref surfaceRenderBuffers[s];

                            // Compare surface with 'current' meshquery (is this surface even being rendered???)
                            if (!IsSurfaceVisible(meshQueries, ref surfaceRenderBuffer))
                                goto SkipSurface;

                            ref var vertices = ref surfaceRenderBuffer.colliderVertices;
                            for (int p = 0; p < 6; p++)
                            {
                                var plane = planes[p];
                                for (int v = 0; v < vertices.Length; v++)
                                {
                                    var distance = plane.GetDistanceToPoint(vertices[v]);
                                    if (distance > MathExtensions.kFrustumDistanceEpsilon)
                                    // If we have a visible surface that is outside the frustum, we skip the brush
                                    // (we only want brushes that are completely inside the frustum)
                                        goto SkipBrush;
                                }
                            }
                            // Make sure we find at least one single visible surface inside the frustum
                            haveVisibleSurfaces = true;
SkipSurface:
                            ;
                        }

                        // If we haven't found a single visible surface inside the frustum, skip the brush
                        if (!haveVisibleSurfaces)
                            goto SkipBrush;
                    }

                    // TODO: handle generators, where we only select a generator when ALL of it's brushes are selected

                    s_FoundNodes.Add((CSGTreeNode)brush);
SkipBrush:
                    ;
                }

                if (s_FoundNodes.Count == 0)
                    return null;

                return s_FoundNodes.ToArray();
            }
        }
    }
}
