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

    // TODO: make sure this all works well with Polygon CSGSurfaceAssets 
    // TODO: when not unique, on modification make a copy first and modify that (unless it's an asset in the project?)
    [Serializable, PreferBinarySerialization]
    public sealed class CSGBrushMeshAsset : ScriptableObject
    {
        internal void OnEnable()	{ CSGBrushMeshAssetManager.Register(this); }
        internal void OnDisable()	{ CSGBrushMeshAssetManager.Unregister(this); }
        internal void OnValidate()	{ CSGBrushMeshAssetManager.NotifyContentsModified(this); }

        // returns false if it was already dirty
        public new bool SetDirty()	{ return CSGBrushMeshAssetManager.SetDirty(this); }
        public bool Dirty			{ get { return CSGBrushMeshAssetManager.IsDirty(this); } }

        [SerializeField] private CSGBrushSubMesh[]	subMeshes;
        [NonSerialized] private BrushMeshInstance[] instances;
        
        public Vector3[]			Vertices		{ get { if (Empty) return null; return subMeshes[0].Vertices;  } set { if (Empty) { subMeshes = new []{ new CSGBrushSubMesh() }; }; subMeshes[0].Vertices  = value; } }
        public BrushMesh.HalfEdge[]	HalfEdges		{ get { if (Empty) return null; return subMeshes[0].HalfEdges; } set { if (Empty) { subMeshes = new []{ new CSGBrushSubMesh() }; }; subMeshes[0].HalfEdges = value; } }
        public CSGBrushSubMesh.Polygon[]	Polygons		{ get { if (Empty) return null; return subMeshes[0].Polygons;  } set { if (Empty) { subMeshes = new []{ new CSGBrushSubMesh() }; }; subMeshes[0].Polygons  = value; } }
        public bool					Valid			{ get { return subMeshes != null; } }

        public bool					Empty			{ get { if (subMeshes == null) return true; return subMeshes.Length == 0; } }
        public int					SubMeshCount	{ get { if (subMeshes == null) return 0; return subMeshes.Length; } }
        public CSGBrushSubMesh[]	SubMeshes		{ get { return subMeshes; } set { subMeshes = value; OnValidate(); } }

        public BrushMeshInstance[]	Instances		{ get { if (HasInstances) return instances; return null; } }


        public void Clear() { subMeshes = null; OnValidate(); }
        
        internal bool HasInstances { get { return instances != null && instances.Length > 0 && instances[0].Valid; } }

        internal void CreateInstances()
        {
            DestroyInstances();
            if (Empty) return;

            if (instances == null ||
                instances.Length != subMeshes.Length)
                instances = new BrushMeshInstance[subMeshes.Length];

            var userID = GetInstanceID();
            for (int i = 0; i < instances.Length; i++)
                instances[i] = BrushMeshInstance.Create(subMeshes[i].CreateOrUpdateBrushMesh(), userID: userID);
        }

        internal void UpdateInstances()
        {
            if (instances == null) return;						
            if (Empty) { DestroyInstances(); return; }
            if (instances.Length != subMeshes.Length) { CreateInstances(); return; }

            for (int i = 0; i < instances.Length; i++)
                instances[i].Set(subMeshes[i].CreateOrUpdateBrushMesh());
        }

        internal void DestroyInstances()
        {
            if (instances != null)
            {
                for (int i = 0; i < instances.Length; i++)
                    if (instances[i].Valid)
                        instances[i].Destroy();
            }
            instances = null;
        }

        public void	CalculatePlanes()
        {
            for (int i = 0; i < subMeshes.Length; i++)
            {
                if (subMeshes[i] == null)
                    throw new NullReferenceException("SubMeshes[" + i + "] is null");
                subMeshes[i].CalculatePlanes();
            }
        }


        static readonly Vector3 positiveInfinityVector = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        static readonly Vector3 negativeInfinityVector = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        public Bounds CalculateBounds(Matrix4x4 transformation)
        {
            var min = positiveInfinityVector;
            var max = negativeInfinityVector;

            if (subMeshes != null)
            {
                for (int i = 0; i < subMeshes.Length; i++)
                    subMeshes[i].ExtendBounds(transformation, ref min, ref max);
            }
            return new Bounds { min = min, max = max };
        }

        public void Cut(Plane cutPlane, CSGSurfaceAsset asset, UVMatrix uv0)
        {
            // TODO: improve design of surfaceAsset usage
            var surfaceDescription = new SurfaceDescription()
            {
                smoothingGroup  = 0,
                surfaceFlags    = SurfaceFlags.None,
                UV0             = uv0
            };
            var surfaceLayers = new SurfaceLayers()
            {
                layerUsage      = asset.LayerUsage,
                layerParameter1 = (asset.RenderMaterial  == null) ? 0 : asset.RenderMaterial .GetInstanceID(),
                layerParameter2 = (asset.PhysicsMaterial == null) ? 0 : asset.PhysicsMaterial.GetInstanceID(),
            };
            Cut(cutPlane, surfaceDescription, surfaceLayers);
        }
        
        public void Cut(Plane cutPlane, SurfaceDescription surfaceDescription, SurfaceLayers surfaceLayers)
        {
            for (int i = SubMeshes.Length - 1; i >= 0; i--)
            {
                SubMeshes[i].CreateOrUpdateBrushMesh();
                if (!SubMeshes[i].Cut(cutPlane, surfaceDescription, surfaceLayers))
                {
                    if (SubMeshes.Length > 1)
                    {
                        var newSubMeshes = new List<CSGBrushSubMesh>(subMeshes);
                        newSubMeshes.RemoveAt(i);
                        subMeshes = newSubMeshes.ToArray();
                    } else
                        subMeshes = null;
                    continue;
                }
                SubMeshes[i].CreateOrUpdateBrushMeshInverse();
            }
            if (subMeshes == null ||
                subMeshes.Length == 0)
                Clear();
        }
    }
}
