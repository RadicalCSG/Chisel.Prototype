using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    [Serializable]
    public class ChiselBrushDefinition : IChiselGenerator
    {
        const int kLatestVersion = 1;
        [SerializeField] int version = kLatestVersion;  // Serialization will overwrite the version number 
                                                        // new instances will have the latest version

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

            if (version != kLatestVersion)
            {
                version = kLatestVersion;
                surfaceDefinition = null;
            }

            if (surfaceDefinition == null)
            {
                surfaceDefinition = new ChiselSurfaceDefinition();
                surfaceDefinition.EnsureSize(brushOutline.polygons.Length);
                if (brushOutline.polygons.Length > 0)
                {
                    for (int p = 0; p < brushOutline.polygons.Length; p++)
                    {
                        surfaceDefinition.surfaces[p].surfaceDescription = brushOutline.polygons[p].surface.surfaceDescription;
                        surfaceDefinition.surfaces[p].brushMaterial = brushOutline.polygons[p].surface.brushMaterial;
                    }
                }
            } else
                surfaceDefinition.EnsureSize(brushOutline.polygons.Length);

            // Temporary fix for misformed brushes
            for (int i = 0; i < brushOutline.polygons.Length; i++)
                brushOutline.polygons[i].surfaceID = i;
            brushOutline.CalculatePlanes();

            // TODO: shouldn't do this all the time:
            {
                // Detect if outline is inside-out and if so, just invert all polygons.
                isInsideOut = brushOutline.IsInsideOut();
                if (isInsideOut)
                    brushOutline.Invert();

                // Split non planar polygons into convex pieces
                brushOutline.SplitNonPlanarPolygons();
            }
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            if (!IsValid)
                return false;

            brushContainer.EnsureSize(1);
            var brushMesh = new BrushMesh(brushOutline); 
            brushContainer.brushMeshes[0] = brushMesh;

            Validate();

            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                brushMesh.polygons[p].surface = surfaceDefinition.surfaces[p];
            }
            return brushMesh.Validate();
        }
    }
} 
