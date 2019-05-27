using System;
using System.Collections.Generic;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    // This is an asset so that when generators share the same output
    // (when, for example, they're repeated or mirrored) 
    // they will all automatically update when one is modified.

    // TODO: when not unique, on modification make a copy first and modify that (unless it's an asset in the project?)
    [Serializable, PreferBinarySerialization]
    public sealed class ChiselGeneratedBrushes : ScriptableObject
    {
        [Serializable]
        public sealed class ChiselGeneratedBrush
        {
            public ChiselGeneratedBrush() { }

            public ChiselGeneratedBrush(ChiselGeneratedBrush other)
            {
                this.brushMesh = new BrushMesh(other.brushMesh);
                this.operation = other.operation;
            }

            [SerializeField] public BrushMesh brushMesh = new BrushMesh() { version = BrushMesh.CurrentVersion };
            [SerializeField] public CSGOperationType operation = CSGOperationType.Additive;
        }

        internal void OnEnable()	{ ChiselGeneratedBrushesManager.Register(this); }
        internal void OnDisable()	{ ChiselGeneratedBrushesManager.Unregister(this); }
        internal void OnValidate()	{ ChiselGeneratedBrushesManager.NotifyContentsModified(this); }

        // returns false if it was already dirty
        public new bool SetDirty()	{ return ChiselGeneratedBrushesManager.SetDirty(this); }
        public bool Dirty			{ get { return ChiselGeneratedBrushesManager.IsDirty(this); } }

        [SerializeField] private ChiselGeneratedBrush[]	subMeshes;
        [NonSerialized] private BrushMeshInstance[] instances;

        public bool					Valid			{ get { return subMeshes != null; } }

        public bool					Empty			{ get { if (subMeshes == null) return true; return subMeshes.Length == 0; } }
        public int					SubMeshCount	{ get { if (subMeshes == null) return 0; return subMeshes.Length; } }
        public ChiselGeneratedBrush[]	SubMeshes		{ get { return subMeshes; } set { subMeshes = value; OnValidate(); } }
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
            {
                ref var brushMesh = ref subMeshes[i].brushMesh;
                if (!brushMesh.Validate(logErrors: true))
                    brushMesh.Clear();
                instances[i] = BrushMeshInstance.Create(brushMesh, userID: userID);
            }
        }

        internal void UpdateInstances()
        {
            if (instances == null) return;						
            if (Empty) { DestroyInstances(); return; }
            if (instances.Length != subMeshes.Length) { CreateInstances(); return; }

            for (int i = 0; i < instances.Length; i++)
            {
                ref var brushMesh = ref subMeshes[i].brushMesh;
                if (!brushMesh.Validate(logErrors: true))
                    brushMesh.Clear();
                instances[i].Set(brushMesh);
            }
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
                var brushMesh = subMeshes[i].brushMesh;
                brushMesh.CalculatePlanes();
                brushMesh.UpdateHalfEdgePolygonIndices();
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
                for (int m = 0; m < subMeshes.Length; m++)
                {
                    var vertices = subMeshes[m].brushMesh.vertices;
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
            return new Bounds { min = min, max = max };
        }

        public void Cut(Plane cutPlane, ChiselBrushMaterial brushMaterial, UVMatrix uv0)
        {
            // TODO: improve design of brushMaterial usage
            var surfaceDescription = new SurfaceDescription()
            {
                smoothingGroup  = 0,
                surfaceFlags    = SurfaceFlags.None,
                UV0             = uv0
            };
            Cut(cutPlane, brushMaterial, surfaceDescription);
        }
        
        public void Cut(Plane cutPlane, ChiselBrushMaterial brushMaterial, SurfaceDescription surfaceDescription)
        {
            if (SubMeshes == null)
                return;

            for (int i = SubMeshes.Length - 1; i >= 0; i--)
            {
                if (!SubMeshes[i].brushMesh.Cut(cutPlane, surfaceDescription, brushMaterial))
                {
                    if (SubMeshes.Length > 1)
                    {
                        var newSubMeshes = new List<ChiselGeneratedBrush>(subMeshes);
                        newSubMeshes.RemoveAt(i);
                        subMeshes = newSubMeshes.ToArray();
                    } else
                        subMeshes = null;
                    continue;
                }
            }
            if (subMeshes == null ||
                subMeshes.Length == 0)
                Clear();
        }
    }
}
