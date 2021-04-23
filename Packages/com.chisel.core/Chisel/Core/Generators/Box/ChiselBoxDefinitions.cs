using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;

namespace Chisel.Core
{
    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBoxDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Box";

        public static readonly MinMaxAABB kDefaultBounds = new MinMaxAABB { Min = new float3(-0.5f), Max = new float3(0.5f) };

        public MinMaxAABB               bounds;

        #region Properties
        public float3   Min     { get { return bounds.Min;    } set { bounds.Min    = value; } }
        public float3   Max	    { get { return bounds.Max;    } set { bounds.Max    = value; } }
        public float3   Size    
        { 
            get { return bounds.Max - bounds.Min; } 
            set 
            {
                var newSize  = math.abs(value);
                var halfSize = newSize * 0.5f;
                var center   = this.Center;
                bounds.Min = center - halfSize;
                bounds.Max = center + halfSize;
            } 
        }

        public float3   Center  
        { 
            get { return (bounds.Max + bounds.Min) * 0.5f; } 
            set 
            { 
                var newSize  = math.abs(Size);
                var halfSize = newSize * 0.5f;
                bounds.Min = value - halfSize;
                bounds.Max = value + halfSize;
            } 
        }
        #endregion

        //[NamedItems("Top", "Bottom", "Right", "Left", "Back", "Front", fixedSize = 6)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            bounds = kDefaultBounds;
        }

        public int RequiredSurfaceCount { get { return 6; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            var originalBox = bounds;
            
            bounds.Min = math.min(originalBox.Min, originalBox.Max);
            bounds.Max = math.max(originalBox.Min, originalBox.Max);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct CreateBoxJob : IJob
        {
            public MinMaxAABB                                           bounds;
            [NoAlias, ReadOnly]
            public BlobAssetReference<NativeChiselSurfaceDefinition>    surfaceDefinitionBlob;
            
            [NoAlias]
            public NativeReference<BlobAssetReference<BrushMeshBlob>>   brushMesh;

            public void Execute()
            {
                if (!BrushMeshFactory.CreateBox(bounds.Min, bounds.Max,
                                                in surfaceDefinitionBlob,
                                                out var newBrushMesh,
                                                Allocator.Persistent))
                    brushMesh.Value = default;
                else
                    brushMesh.Value = newBrushMesh;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public JobHandle Generate(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, ref CSGTreeNode node, int userID, CSGOperationType operation)
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

            using (var brushMeshRef = new NativeReference<BlobAssetReference<BrushMeshBlob>>(Allocator.TempJob))
            {
                var createBoxJob = new CreateBoxJob
                {
                    bounds                  = bounds,
                    surfaceDefinitionBlob   = surfaceDefinitionBlob,
                    brushMesh               = brushMeshRef
                };
                var handle = createBoxJob.Schedule();
                handle.Complete();

                if (!brushMeshRef.Value.IsCreated)
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                else
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshRef.Value) };
            }
            return default;
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoBoundsHandle(ref bounds);
            handles.RenderBoxMeasurements(bounds);
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        public bool HasValidState()
        {
            var size = this.Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                return false;
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
            var size = this.Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
    }
}