using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using HideInInspector = UnityEngine.HideInInspector;
using SerializeField = UnityEngine.SerializeField;

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
        public BrushMesh                brushOutline;


        [HideInInspector]
        [SerializeField] bool           isInsideOut = false;
        [HideInInspector]
        [SerializeField] bool           validState = true;

        public bool ValidState      { get { return validState; } set { validState = value; } }
        public bool IsInsideOut     { get { return isInsideOut; } }

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
        }

        public bool EnsurePlanarPolygons()
        {
            if (!IsValid)
                return false;

            // Split non planar polygons into convex pieces
            return brushOutline.SplitNonPlanarPolygons();
        }

        public int RequiredSurfaceCount { get { return brushOutline?.polygons?.Length ?? 0; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            if (surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length == 0)
                return;

            for (int p = 0; p < brushOutline.polygons.Length; p++)
                brushOutline.polygons[p].descriptionIndex = p;
        }

        public void Validate()
        {
            if (!IsValid)
                return;

            if (version != kLatestVersion)
                version = kLatestVersion;

            brushOutline.CalculatePlanes();
            
            // If the brush is concave, we set the generator to not be valid, so that when we commit, it will be reverted
            validState = brushOutline.HasVolume() &&            // TODO: implement this, so we know if a brush is a 0D/1D/2D shape
                         !brushOutline.IsConcave() &&           // TODO: eventually allow concave shapes
                         !brushOutline.IsSelfIntersecting();    // TODO: in which case this needs to be implemented

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

        public void GetWarningMessages(IChiselMessageHandler messages)
        {
            if (!IsValid)
            {
                // TODO: show message that internal brush is invalid
            }
            if (ValidState)
            {
                // TODO: show message that brush is not in valid state
            }
        }
    }
} 
