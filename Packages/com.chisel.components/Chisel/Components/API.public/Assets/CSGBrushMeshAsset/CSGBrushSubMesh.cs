using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Assets
{
    // This is an asset so that when brushes share the same brush 
    // (when, for example, they're repeated or mirrored) 
    // they will all automatically update when one is modified.

    [Serializable]
    public class CSGBrushSubMesh
    {
        public CSGBrushSubMesh() { }
        public CSGBrushSubMesh(CSGBrushSubMesh other)
        {
            if (other.polygons != null)
            {
                this.polygons = new Polygon[other.polygons.Length];
                for (int p = 0; p < other.polygons.Length; p++)
                    this.polygons[p] = new Polygon(other.polygons[p]);
            } else
                this.polygons = null;
            this.brushMesh = new BrushMesh(other.brushMesh);
        }

        [Serializable]
        public sealed class Polygon
        {
            public Polygon() { }
            public Polygon(Polygon other)
            {
                firstEdge = other.firstEdge;
                edgeCount = other.edgeCount;
                surfaceID = other.surfaceID;
                description = other.description;
                surfaceAsset = other.surfaceAsset;
            }
            [HideInInspector] [SerializeField] public Int32 firstEdge;
            [HideInInspector] [SerializeField] public Int32 edgeCount;
            [HideInInspector] [SerializeField] public Int32 surfaceID;
            public SurfaceDescription description;
            public CSGSurfaceAsset surfaceAsset;    // this is an unfortunate consequence of the native dll, since we can't pass this along to 
                                                    // the native side, it was "translated" to an integer using it's uniqueID, this created a 
                                                    // lot of boiler plate/management code that we can probably do away with once the CSG algorithm 
                                                    // is moved to managed code ..
        }

        private ref BrushMesh BrushMesh { get { if (brushMesh == null) Clear(); return ref brushMesh; } }

        public Vector3[]					Vertices		       { get { return BrushMesh.vertices;  } set { BrushMesh.vertices  = value; } }
        public BrushMesh.HalfEdge[]			HalfEdges		       { get { return BrushMesh.halfEdges; } set { BrushMesh.halfEdges = value; } }
        public BrushMesh.Surface[]          Surfaces               { get { return BrushMesh.surfaces; } set { BrushMesh.surfaces = value; } }
        public int[]                        HalfEdgePolygonIndices { get { return BrushMesh.halfEdgePolygonIndices; } set { BrushMesh.halfEdgePolygonIndices = value; } }
        public Polygon[]					Polygons		       { get { return polygons; } set { polygons = value; } }
        public CSGOperationType				Operation		       { get { return operation; } set { operation = value; } }
        
        [SerializeField] private Polygon[]				polygons;
        [SerializeField] private BrushMesh				brushMesh;
        [SerializeField] private CSGOperationType		operation = CSGOperationType.Additive;

        public void Clear()
        {
            if (brushMesh == null)
                brushMesh = new BrushMesh() { version = BrushMesh.CurrentVersion };
            polygons = null;
        }

        public bool Validate()
        {
            if (brushMesh == null)
                return false;

            return brushMesh.Validate(logErrors: true);
        }
        
        internal void CalculatePlanes()
        {
            if (brushMesh == null)
                return;

            if (brushMesh.polygons == null ||
                brushMesh.polygons.Length != polygons.Length)
                CreateOrUpdateBrushMesh();

            brushMesh.CalculatePlanes();
            brushMesh.UpdateHalfEdgePolygonIndices();
        }

        internal BrushMesh CreateOrUpdateBrushMesh()
        {
            if (brushMesh == null)
                brushMesh = new BrushMesh() { version = BrushMesh.CurrentVersion };

            // In case a user sets "polygons" to null, for consistency
            if (polygons == null ||
                polygons.Length == 0)
            {
                Clear();
                return brushMesh;
            }

            if (brushMesh.polygons == null ||
                brushMesh.polygons.Length != polygons.Length)
                brushMesh.polygons = new BrushMesh.Polygon[polygons.Length];

            var dstPolygons = brushMesh.polygons;
            for (int i = 0; i < polygons.Length; i++)
            {
                if (polygons[i] == null)
                {
                    Debug.LogError("Polygons[" + i + "] is not initialized.");
                    Clear();
                    return brushMesh;
                }
                dstPolygons[i].firstEdge    = polygons[i].firstEdge;
                dstPolygons[i].edgeCount    = polygons[i].edgeCount;
                dstPolygons[i].surfaceID    = polygons[i].surfaceID;
                dstPolygons[i].description  = polygons[i].description;

                var surfaceAsset = polygons[i].surfaceAsset;
                if (surfaceAsset == null)
                {
                    dstPolygons[i].layers.layerUsage = LayerUsageFlags.None;
                    dstPolygons[i].layers.layerParameter1 = 0;
                    dstPolygons[i].layers.layerParameter2 = 0;
                    continue;
                } 
                dstPolygons[i].layers.layerUsage      = surfaceAsset.LayerUsage;
                dstPolygons[i].layers.layerParameter1 = surfaceAsset.RenderMaterialInstanceID;
                dstPolygons[i].layers.layerParameter2 = surfaceAsset.PhysicsMaterialInstanceID;
            }

            if (!Validate())
                Clear(); 

            return brushMesh;
        }

        internal void CreateOrUpdateBrushMeshInverse()
        {
            if (brushMesh == null)
                return;

            // In case a user sets "polygons" to null, for consistency
            if (brushMesh.polygons == null ||
                brushMesh.polygons.Length == 0)
            {
                Clear();
                return;
            }

            if (polygons == null)
            {
                polygons = new Polygon[brushMesh.polygons.Length];
                for (int i = 0; i < polygons.Length; i++)
                    polygons[i] = new Polygon();
            } else
            if (polygons.Length != brushMesh.polygons.Length)
            {
                var newPolygons = new Polygon[brushMesh.polygons.Length];
                var minLength = Mathf.Min(brushMesh.polygons.Length, polygons.Length);
                Array.Copy(polygons, newPolygons, minLength);
                for (int i = minLength; i < newPolygons.Length; i++)
                    newPolygons[i] = new Polygon();
                polygons = newPolygons;
            }

            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                polygons[i].firstEdge    = brushMesh.polygons[i].firstEdge;
                polygons[i].edgeCount    = brushMesh.polygons[i].edgeCount;
                polygons[i].surfaceID    = brushMesh.polygons[i].surfaceID;
                polygons[i].description  = brushMesh.polygons[i].description;

                var renderMaterial  = (brushMesh.polygons[i].layers.layerParameter1 == 0) ? null : CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(brushMesh.polygons[i].layers.layerParameter1);
                var physicsMaterial = (brushMesh.polygons[i].layers.layerParameter2 == 0) ? null : CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(brushMesh.polygons[i].layers.layerParameter2);
                polygons[i].surfaceAsset = CSGSurfaceAsset.CreateInstance(renderMaterial, physicsMaterial, brushMesh.polygons[i].layers.layerUsage);
            }
        }
        
        public void ExtendBounds(Matrix4x4 transformation, ref Vector3 min, ref Vector3 max)
        {
            if (brushMesh == null)
                return;
            
            var vertices = brushMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = transformation.MultiplyPoint(vertices[i]);

                min.x = Mathf.Min(min.x, point.x);
                min.y = Mathf.Min(min.y, point.y);
                min.z = Mathf.Min(min.z, point.z);

                max.x = Mathf.Max(max.x, point.x);
                max.y = Mathf.Max(max.y, point.y);
                max.z = Mathf.Max(max.z, point.z);
            }
        }

        public bool Cut(Plane cuttingPlane, SurfaceDescription description, SurfaceLayers layers)
        {
            if (brushMesh == null)
                return false;

            return brushMesh.Cut(cuttingPlane, description, layers);
        }
    }
}
