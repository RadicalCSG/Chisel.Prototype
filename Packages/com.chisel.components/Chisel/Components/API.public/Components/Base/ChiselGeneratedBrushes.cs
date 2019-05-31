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
        internal void OnEnable()	{ ChiselGeneratedBrushesManager.Register(this); }
        internal void OnDisable()	{ ChiselGeneratedBrushesManager.Unregister(this); }
        internal void OnValidate()	{ ChiselGeneratedBrushesManager.NotifyContentsModified(this); }

        // returns false if it was already dirty
        public new bool SetDirty()	{ return ChiselGeneratedBrushesManager.SetDirty(this); }
        public bool Dirty			{ get { return ChiselGeneratedBrushesManager.IsDirty(this); } }

        [SerializeField] private BrushMesh[]	    brushMeshes;
        [SerializeField] private CSGOperationType[]	operations;
        [NonSerialized] private BrushMeshInstance[] instances;

        public bool					Valid			{ get { return brushMeshes != null; } }

        public bool					Empty			{ get { if (brushMeshes == null) return true; return brushMeshes.Length == 0; } }
        public int					SubMeshCount	{ get { if (brushMeshes == null) return 0; return brushMeshes.Length; } }
        public BrushMesh[]	        BrushMeshes		{ get { return brushMeshes; } }
        public CSGOperationType[]	Operations		{ get { return operations; } }
        public BrushMeshInstance[]	Instances		{ get { if (HasInstances) return instances; return null; } }

        public bool SetSubMeshes(BrushMesh[] brushMeshes)
        {
            if (brushMeshes == null)
            {
                Clear();
                return false;
            }
            this.brushMeshes = brushMeshes;
            this.operations = new CSGOperationType[brushMeshes.Length]; // default is Additive
            OnValidate();
            return true;
        }

        public bool SetSubMeshes(BrushMesh[] brushMeshes, CSGOperationType[] operations)
        {
            if (brushMeshes == null || operations == null ||
                brushMeshes.Length != operations.Length)
            {
                Debug.Assert(brushMeshes == null && operations == null);
                Clear();
                return false;
            }
            this.brushMeshes = brushMeshes;
            this.operations = operations;
            OnValidate();
            return true;
        }

        public void Clear() { brushMeshes = null; operations = null; OnValidate(); }
        
        internal bool HasInstances { get { return instances != null && instances.Length > 0 && instances[0].Valid; } }

        internal void CreateInstances()
        {
            DestroyInstances();
            if (Empty) return;

            if (instances == null ||
                instances.Length != brushMeshes.Length)
                instances = new BrushMeshInstance[brushMeshes.Length];

            var userID = GetInstanceID();
            for (int i = 0; i < instances.Length; i++)
            {
                ref var brushMesh = ref brushMeshes[i];
                if (!brushMesh.Validate(logErrors: true))
                    brushMesh.Clear();
                instances[i] = BrushMeshInstance.Create(brushMesh, userID: userID);
            }
        }

        internal void UpdateInstances()
        {
            if (instances == null) return;						
            if (Empty) { DestroyInstances(); return; }
            if (instances.Length != brushMeshes.Length) { CreateInstances(); return; }

            for (int i = 0; i < instances.Length; i++)
            {
                ref var brushMesh = ref brushMeshes[i];
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
            for (int i = 0; i < brushMeshes.Length; i++)
            {
                if (brushMeshes[i] == null)
                    throw new NullReferenceException("SubMeshes[" + i + "] is null");
                ref var brushMesh = ref brushMeshes[i];
                brushMesh.CalculatePlanes();
                brushMesh.UpdateHalfEdgePolygonIndices();
            }
        }


        static readonly Vector3 positiveInfinityVector = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        static readonly Vector3 negativeInfinityVector = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        public Bounds CalculateBounds(Matrix4x4 transformation)
        {
            if (brushMeshes == null)
                return new Bounds();

            var min = positiveInfinityVector;
            var max = negativeInfinityVector;
            
            for (int m = 0; m < brushMeshes.Length; m++)
            {
                var vertices = brushMeshes[m].vertices;
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
            return new Bounds { min = min, max = max };
        }

        public void Cut(Plane cutPlane, in ChiselSurface chiselSurface)
        {
            if (brushMeshes == null)
                return;

            for (int i = brushMeshes.Length - 1; i >= 0; i--)
            {
                if (!brushMeshes[i].Cut(cutPlane, in chiselSurface))
                {
                    if (brushMeshes.Length > 1)
                    {
                        var newBrushMeshes = new List<BrushMesh>(brushMeshes);
                        var newOperations = new List<CSGOperationType>(operations);
                        newBrushMeshes.RemoveAt(i);
                        newOperations.RemoveAt(i);
                        brushMeshes = newBrushMeshes.ToArray();
                        operations = newOperations.ToArray();
                    } else
                    {
                        brushMeshes = null;
                        operations = null;
                    }
                    continue;
                }
            }
            if (brushMeshes == null ||
                brushMeshes.Length == 0)
                Clear();
        }
    }
}
