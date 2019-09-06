using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    [Serializable]
    public class BrushDefinition : IChiselGenerator
    {
        public BrushMesh                brushOutline;

        [NamedItems(overflow = "Surface {0}")]
        public ChiselSurfaceDefinition  surfaceDefinition;
        
        [HideInInspector]
        [SerializeField] bool           isInsideOut = false;
        [HideInInspector]
        [SerializeField] bool           validState = true;

        public bool ValidState { get { return validState; } set { validState = value; } }
        public bool IsInsideOut { get { return isInsideOut; } }

        public bool IsValid
        {
            get
            {
                return brushOutline != null &&
                       brushOutline.vertices != null &&
                       brushOutline.polygons != null &&
                       brushOutline.halfEdges != null &&
                       brushOutline.vertices.Length > 0 &&
                       brushOutline.polygons.Length > 0 &&
                       brushOutline.halfEdges.Length > 0 &&
                       ValidState;
            }
        }

        public void Reset()
        {
            brushOutline = null;
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (!IsValid)
                return;

            if (brushOutline.polygons == null)
                return;

            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();

            surfaceDefinition.EnsureSize(brushOutline.polygons.Length);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            if (!IsValid)
                return false;

            brushContainer.EnsureSize(1);

            brushContainer.brushMeshes[0] = new BrushMesh(brushOutline);

            brushContainer.brushMeshes[0].CalculatePlanes();

            // Detect if outline is inside-out and if so, just invert all polygons.
            isInsideOut = brushContainer.brushMeshes[0].IsInsideOut();
            if (isInsideOut)
                brushContainer.brushMeshes[0].Invert();

            // Split non planar polygons into convex pieces
            brushContainer.brushMeshes[0].SplitNonPlanarPolygons();

            return brushContainer.brushMeshes[0].Validate();
        }
    }
} 
