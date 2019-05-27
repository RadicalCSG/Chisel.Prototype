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

    // TODO: make sure this all works well with Polygon ChiselBrushMaterial 
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

        public void Cut(Plane cutPlane, ChiselBrushMaterial asset, UVMatrix uv0)
        {
            // TODO: improve design of brushMaterial usage
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
