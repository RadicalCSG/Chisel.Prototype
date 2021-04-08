using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBoxDefinition : IChiselGenerator, IBrushGenerator
    {
        public const string kNodeTypeName = "Box";

        public static readonly Bounds   kDefaultBounds = new Bounds(Vector3.zero, Vector3.one);

        public UnityEngine.Bounds       bounds;

        [NamedItems("Top", "Bottom", "Right", "Left", "Back", "Front", fixedSize = 6)]
        public ChiselSurfaceDefinition  surfaceDefinition;
                
        public Vector3      min		{ get { return bounds.min; } set { bounds.min = value; } }
        public Vector3		max	    { get { return bounds.max; } set { bounds.max = value; } }
        public Vector3		size    { get { return bounds.size; } set { bounds.size = value; } }
        public Vector3		center  { get { return bounds.center; } set { bounds.center = value; } }
        
        public void Reset()
        {
            bounds = kDefaultBounds;
            surfaceDefinition?.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();
            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateBox(ref brushContainer, ref this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)math.hash(new uint3(math.hash(bounds.min), 
                                                math.hash(bounds.max),
                                                (uint)surfaceDefinition.GetHashCode()));
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public bool Generate(ref CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var brush = (CSGTreeBrush)node;
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
                if (!BrushMeshFactory.CreateBox(bounds.min, bounds.max,
                                                in surfaceDefinitionBlob,
                                                out var brushMesh,
                                                Allocator.Persistent))
                {
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                    return false;
                }

                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
            }
            return true;
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoBoundsHandle(ref bounds);
            handles.RenderBoxMeasurements(bounds);
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        public void OnMessages(IChiselMessages messages)
        {
            if (bounds.size.x == 0 || bounds.size.y == 0 || bounds.size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
    }
}