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
        // TODO: find a better name
        public sealed class Orientation
        {
            public Orientation() { }
            public Orientation(Orientation other)
            {
                localPlane = other.localPlane;
                localToPlaneSpace = other.localToPlaneSpace;
                planeToLocalSpace = other.planeToLocalSpace;
            }
            [HideInInspector] [SerializeField] public Plane		localPlane;
            [HideInInspector] [SerializeField] public Matrix4x4 localToPlaneSpace;
            [HideInInspector] [SerializeField] public Matrix4x4 planeToLocalSpace;
            public Vector3 Tangent { get { return localToPlaneSpace.GetRow(0); } }
            public Vector3 BiNormal { get { return localToPlaneSpace.GetRow(1); } }
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


        public Vector3[]					Vertices		{ get { if (brushMesh == null) brushMesh = new BrushMesh(); return brushMesh.vertices;  } set { if (brushMesh == null) brushMesh = new BrushMesh(); brushMesh.vertices  = value; } }
        public BrushMesh.HalfEdge[]			HalfEdges		{ get { if (brushMesh == null) brushMesh = new BrushMesh(); return brushMesh.halfEdges; } set { if (brushMesh == null) brushMesh = new BrushMesh(); brushMesh.halfEdges = value; } }
        public Polygon[]					Polygons		{ get { return polygons; } set { polygons = value; } }
        public Orientation[]				Orientations	{ get { return orientations; } set { orientations = value; } }
        public CSGOperationType				Operation		{ get { return operation; } set { operation = value; } }
        
        [SerializeField] private Orientation[]			orientations;
        [SerializeField] private Polygon[]				polygons;
        [SerializeField] private BrushMesh				brushMesh;
        [SerializeField] private CSGOperationType		operation = CSGOperationType.Additive;

        public void Clear()
        {
            if (brushMesh == null)
                brushMesh = new BrushMesh();
            polygons = null;
        }

        public bool Validate()
        {
            if (brushMesh == null)
                return false;
            return brushMesh.Validate(logErrors: true);
        }
        
        internal void	CalculatePlanes()
        {
            if (orientations == null ||
                orientations.Length != polygons.Length)
                orientations = new Orientation[polygons.Length];

            var vertices  = brushMesh.vertices;
            var halfEdges = brushMesh.halfEdges;
            for (int i = 0; i < polygons.Length; i++)
            {
                var firstEdge = polygons[i].firstEdge;
                var edgeCount = polygons[i].edgeCount;
                if (edgeCount <= 0)
                {
                    if (orientations[i] == null) orientations[i] = new Orientation();
                    orientations[i].localPlane = new Plane(Vector3.up, 0);
                    orientations[i].localToPlaneSpace = Matrix4x4.identity;
                    orientations[i].planeToLocalSpace = Matrix4x4.identity;
                    continue;
                }
                var lastEdge	= firstEdge + edgeCount;
                var normal		= Vector3.zero;
                var prevVertex	= vertices[halfEdges[lastEdge - 1].vertexIndex];
                for (int n = firstEdge; n < lastEdge; n++)
                {
                    var currVertex = vertices[halfEdges[n].vertexIndex];
                    normal.x = normal.x + ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                    normal.y = normal.y + ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                    normal.z = normal.z + ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
                    prevVertex = currVertex;
                }
                normal = normal.normalized;

                var d = 0.0f;
                for (int n = firstEdge; n < lastEdge; n++)
                    d -= Vector3.Dot(normal, vertices[halfEdges[n].vertexIndex]);
                d /= edgeCount;
                
                if (orientations[i] == null) orientations[i] = new Orientation();
                orientations[i].localPlane = new Plane(normal, d);
                orientations[i].localToPlaneSpace = MathExtensions.GenerateLocalToPlaneSpaceMatrix(orientations[i].localPlane);
                orientations[i].planeToLocalSpace = Matrix4x4.Inverse(orientations[i].localToPlaneSpace);
            }
        }

        internal BrushMesh CreateOrUpdateBrushMesh()
        {
            if (brushMesh == null)
                brushMesh = new BrushMesh();

            // In case a user sets "polygons" to null, for consistency
            if (polygons == null ||
                polygons.Length == 0)
            {
                brushMesh.polygons = null;
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
                polygons = null;
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
            if (brushMesh != null)
            {
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
        }

        public bool Cut(Plane cuttingPlane, SurfaceDescription description, SurfaceLayers layers)
        {
            return brushMesh.Cut(cuttingPlane, description, layers);
        }
    }
}
