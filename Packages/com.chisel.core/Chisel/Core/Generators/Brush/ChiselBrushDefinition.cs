using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Chisel.Core
{
    [Serializable]
    public class ChiselBrushDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Brush";

        const int kLatestVersion = 1;
        [HideInInspector]
        [SerializeField] int version = 0;
        
        // TODO: avoid storing surfaceDefinition and surfaces in brushOutline twice, which is wasteful and causes potential conflicts
        [HideInInspector]
        public BrushMesh                brushOutline;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;


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

        public void Reset(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            brushOutline = null;
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public bool EnsurePlanarPolygons()
        {
            if (!IsValid)
                return false;

            // Split non planar polygons into convex pieces
            return brushOutline.SplitNonPlanarPolygons();
        }

        public void Validate(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            if (!IsValid)
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

        [BurstCompile(CompileSynchronously = true)]
        public JobHandle Generate(ref ChiselSurfaceDefinition surfaceDefinition, ref CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var brush = (CSGTreeBrush)node;
            if (!IsValid)
            {
                if (brush.Valid)
                    brush.Destroy();
                node = default;
                return default;
            }
            
            Validate(ref surfaceDefinition);

            if (!brush.Valid)
            {
                node = brush = CSGTreeBrush.Create(userID: userID, operation: operation);
            } else
            {
                if (brush.Operation != operation)
                    brush.Operation = operation;
            }

            using (var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.Temp))
            {
                var brushMesh = BrushMeshFactory.CreateBrushBlob(brushOutline);
                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
            }
            return default;
        }

        public void OnEdit(ref ChiselSurfaceDefinition surfaceDefinition, IChiselHandles handles)
        {
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
} 
