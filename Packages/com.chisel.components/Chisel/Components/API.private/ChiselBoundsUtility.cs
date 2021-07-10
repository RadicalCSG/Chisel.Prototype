using UnityEngine;
using System.Collections;
using System;
using Chisel.Core;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Components
{
    internal static class ChiselBoundsUtility
    {
        static readonly HashSet<CSGTreeBrush> s_FoundBrushes = new HashSet<CSGTreeBrush>();
        public static Bounds CalculateBounds(ChiselGeneratorComponent generator)
        {
            if (!generator.TopTreeNode.Valid)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);
            var minMax			= new MinMaxAABB { };
            var boundsCount     = 0;

            s_FoundBrushes.Clear();
            ChiselGeneratedComponentManager.GetAllTreeBrushes(generator, s_FoundBrushes);
            foreach (var brush in s_FoundBrushes)
            {
                if (!brush.Valid)
                    continue;

                var transformation  = modelMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                var childBounds     = brush.Bounds;
                var size            = childBounds.Max - childBounds.Min;
                var magnitude       = math.lengthsq(size);
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = ((float4)transformation.GetColumn(3)).xyz;
                    var halfSize = size * 0.5f;
                    childBounds = new MinMaxAABB { Min = center - halfSize, Max = center + halfSize };
                }
                if (magnitude != 0)
                {
                    if (boundsCount == 0)
                        minMax = childBounds;
                    else
                        minMax.Encapsulate(childBounds);
                    boundsCount++;
                }
            }
            if (boundsCount == 0)
                return ChiselHierarchyItem.EmptyBounds;
            var bounds = new Bounds();
            bounds.SetMinMax(minMax.Min, minMax.Max);
            return bounds;
        }
        
        public static Bounds CalculateBounds(ChiselGeneratorComponent generator, Matrix4x4 boundsTransformation)
        {
            if (!generator.TopTreeNode.Valid)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);
            var minMax			= new MinMaxAABB { };
            var boundsCount     = 0;

            s_FoundBrushes.Clear();
            ChiselGeneratedComponentManager.GetAllTreeBrushes(generator, s_FoundBrushes);
            foreach (var brush in s_FoundBrushes)
            {
                if (!brush.Valid)
                    continue;
                var transformation  = modelMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix * boundsTransformation;
                var childBounds     = brush.GetBounds(transformation);
                var size            = childBounds.Max - childBounds.Min;
                var magnitude       = math.lengthsq(size);
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = ((float4)transformation.GetColumn(3)).xyz;
                    var halfSize = size * 0.5f;
                    childBounds = new MinMaxAABB { Min = center - halfSize, Max = center + halfSize };
                }
                if (magnitude != 0)
                {
                    if (boundsCount == 0)
                        minMax = childBounds;
                    else
                        minMax.Encapsulate(childBounds);
                    boundsCount++;
                }
            }
            if (boundsCount == 0)
                return ChiselHierarchyItem.EmptyBounds;
            var bounds = new Bounds();
            bounds.SetMinMax(minMax.Min, minMax.Max);
            return bounds;
        }
    }
}
