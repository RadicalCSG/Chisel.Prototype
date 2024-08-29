using System;
using UnityEngine;

namespace Chisel.Core
{
    [Serializable]
    public class ChiselBrushDefinition : IChiselNodeGenerator
    {
        public const string kNodeTypeName = "Brush";

        const int kLatestVersion = 1;
        [HideInInspector]
        [SerializeField] int version = 0;
        
        // TODO: avoid storing surfaceDefinition and surfaces in brushOutline twice, which is wasteful and causes potential conflicts
        [HideInInspector]
        public BrushMesh        brushOutline;


        [HideInInspector]
        [SerializeField] bool   isInsideOut = false;
        [HideInInspector]
        [SerializeField] bool   validState = true;

        // TODO: clean this mess up
        public void ResetValidState() 
        {
			validState = true;
			isInsideOut = false; 
        } 
		public BrushMesh BrushOutline
        {
            get { return brushOutline; }
            set
            {
                if (brushOutline == value)
                    return;
				ResetValidState();
				brushOutline = value;
			}
        }

		public bool ValidState  { get { return validState; } }
        public bool IsInsideOut { get { return isInsideOut; } }

		string errorMessage = null;

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
			ResetValidState();
            brushOutline = null;
		}

        public bool EnsurePlanarPolygons()
        {
            if (!IsValid)
                return false;

            // Split non planar polygons into convex pieces
            return brushOutline.SplitNonPlanarPolygons();
        }

        public int RequiredSurfaceCount { get { return brushOutline?.polygons?.Length ?? 0; } }

        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            if (surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length == 0)
                return;

            for (int p = 0; p < brushOutline.polygons.Length; p++)
                brushOutline.polygons[p].descriptionIndex = p;
        }

		public bool Validate()
		{
			try
			{
				if (!IsValid)
                    return false;

			    errorMessage = string.Empty;
                if (version != kLatestVersion)
                    version = kLatestVersion;

				if (!brushOutline.ValidateData(out errorMessage))
                {
                    Debug.LogError(errorMessage);
                    validState = false;
                    return false;
				}

				brushOutline.CalculatePlanes();

				// If the brush is concave, we set the generator to not be valid, so that when we commit, it will be reverted
				if (!brushOutline.ValidateShape(out errorMessage))
				{
					Debug.LogError(errorMessage);
					validState = false;
					return false;
				}

				// TODO: shouldn't do this all the time:
				{
                    // Detect if outline is inside-out and if so, just invert all polygons.
                    isInsideOut = brushOutline.IsInsideOut();
                    if (isInsideOut)
                    {
                        brushOutline.Invert();
                        isInsideOut = false;
                    }
                     
                }
                return true;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = ex.ToString();
				}
                throw ex;
            }
        }

        /*
        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            Profiler.BeginSample("GenerateBrush");
            try
            {
                if (!IsValid)
                    return false;

                Profiler.BeginSample("EnsureSize");
                brushContainer.EnsureSize(1);
                Profiler.EndSample();

                Profiler.BeginSample("new_BrushMesh");
                BrushMesh brushMesh;
                if (brushContainer.brushMeshes[0] == null)
                {
                    brushMesh = new BrushMesh(brushOutline);
                    brushContainer.brushMeshes[0] = brushMesh;
                } else
                {
                    brushContainer.brushMeshes[0].CopyFrom(brushOutline);
                    brushMesh = brushContainer.brushMeshes[0];
                }
                Profiler.EndSample();

                Profiler.BeginSample("Definition.Validate");
                Validate();
                Profiler.EndSample();

                Profiler.BeginSample("Assign Materials");
                for (int p = 0; p < brushMesh.polygons.Length; p++)
                    brushMesh.polygons[p].surface = surfaceDefinition.surfaces[p];
                Profiler.EndSample();

                Profiler.BeginSample("BrushMesh.Validate");
                var valid = brushMesh.Validate();
                Profiler.EndSample();
                return valid;
            }
            finally
            {
                Profiler.EndSample();
            }
        }*/

        public void OnEdit(IChiselHandles handles)
        {
        }

		public void GetMessages(IChiselMessageHandler messages)
        {
            if (!IsValid || !ValidState)
			{
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    if (messages.Destination == MessageDestination.Hierarchy)
                        messages.Warning("The brush is in an invalid state. View brush in inspector for more details.");
                    else
						messages.Warning(errorMessage);
                }
			}
		}
    }
} 
