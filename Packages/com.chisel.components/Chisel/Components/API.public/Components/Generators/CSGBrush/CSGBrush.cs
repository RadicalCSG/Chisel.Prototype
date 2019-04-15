using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;

namespace Chisel.Components
{
    // TODO: is RemoveUnusedAssets safe? (relative to monobehaviour / managers?)
    // TODO: how to handle duplication (ctrl-D)? (brushMeshAsset reference no longer unique)
    // TODO: have some sort of bounds that we can use to "focus" on brush
    // TODO: add reset method, use default box
    [ExecuteInEditMode]
    [HelpURL("http://example.com/docs/MyComponent.html")] // TODO: put these on every asset / component
    public sealed class CSGBrush : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Brush"; } }

        public CSGBrush() : base() {  }

        public override void UpdateGenerator()
        {
            // This class doesn't generate anything, so we keep the original brush
        }

        protected override void UpdateGeneratorInternal() { }
        
#if UNITY_EDITOR
        // The icon used in the hierarchy
        public override GUIContent Icon
        {
            get
            {
                switch (this.operation)
                {
                    default:
                    case CSGOperationType.Additive:		return CSGDefaults.Style.AdditiveIcon;
                    case CSGOperationType.Subtractive:	return CSGDefaults.Style.SubtractiveIcon;
                    case CSGOperationType.Intersecting:	return CSGDefaults.Style.IntersectingIcon;
                }
            }
        }
#endif
    }
}