using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace Chisel.Components
{
    internal static class ChiselBoundsUtility
    {
        public static Bounds CalculateBounds(ChiselGeneratorComponent generator)
        {
            if (!generator.TopTreeNode.Valid)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);
            var minMax			= new ChiselAABB { };
            var boundsCount     = 0;

            var s_FoundBrushes = HashSetPool<CSGTreeBrush>.Get();
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
                    childBounds = new ChiselAABB { Min = center - halfSize, Max = center + halfSize };
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
            HashSetPool<CSGTreeBrush>.Release(s_FoundBrushes);
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
            var minMax			= new ChiselAABB { };
            var boundsCount     = 0;

            var s_FoundBrushes = HashSetPool<CSGTreeBrush>.Get();
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
                    childBounds = new ChiselAABB { Min = center - halfSize, Max = center + halfSize };
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
            HashSetPool<CSGTreeBrush>.Release(s_FoundBrushes);
            if (boundsCount == 0)
                return ChiselHierarchyItem.EmptyBounds;
            var bounds = new Bounds();
            bounds.SetMinMax(minMax.Min, minMax.Max);
            return bounds;
        }
    }
}
