using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using System.Reflection;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public interface IChiselNodeDetails
    {
        GUIContent GetHierarchyIconForGenericNode(CSGNode node);
    }

    public abstract class ChiselNodeDetails<T> : IChiselNodeDetails
        where T : CSGNode
    {
        GUIContent IChiselNodeDetails.GetHierarchyIconForGenericNode(CSGNode node) { return GetHierarchyIcon((T)node); }

        public abstract GUIContent GetHierarchyIcon(T node);
    }

    public abstract class ChiselGeneratorDetails<T> : ChiselNodeDetails<T>
        where T : CSGGeneratorComponent
    {
        const string AdditiveIconName		= "csg_addition";
        const string SubtractiveIconName	= "csg_subtraction";
        const string IntersectingIconName	= "csg_intersection";

        public override GUIContent GetHierarchyIcon(T node)
        {
            switch (node.Operation)
            {
                default:
                case CSGOperationType.Additive:     return ChiselEditorResources.GetIconContent(AdditiveIconName,     $"Additive {node.NodeTypeName}")[0];
                case CSGOperationType.Subtractive:  return ChiselEditorResources.GetIconContent(SubtractiveIconName,  $"Subtractive {node.NodeTypeName}")[0];
                case CSGOperationType.Intersecting: return ChiselEditorResources.GetIconContent(IntersectingIconName, $"Intersecting {node.NodeTypeName}")[0];
            }
        }
    }
}
