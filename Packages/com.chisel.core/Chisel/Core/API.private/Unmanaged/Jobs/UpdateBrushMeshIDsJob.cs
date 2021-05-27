using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile]
    unsafe struct UpdateBrushMeshIDsJob : IJob
    {
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        // Read
        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy*                            compactHierarchyPtr;
        [NoAlias, ReadOnly] public NativeHashMap<int, RefCountedBrushMeshBlob>  brushMeshBlobs;
        [NoAlias, ReadOnly] public int                                          brushCount;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID>                    brushes;

        // Read/Write
        [NoAlias] public NativeHashSet<int>                                     allKnownBrushMeshIndices;
        [NoAlias] public NativeArray<ChiselLayerParameters>                     parameters;
        [NoAlias] public NativeArray<int>                                       parameterCounts;

        // Write
        [NoAlias, WriteOnly] public NativeArray<int>                            allBrushMeshIDs;

        public void Execute()
        {
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            Debug.Assert(parameters.Length == SurfaceLayers.ParameterCount);
            Debug.Assert(parameterCounts.Length == SurfaceLayers.ParameterCount);
            Debug.Assert(SurfaceLayers.kLayerUsageFlags.Length == SurfaceLayers.ParameterCount);

            var capacity = math.max(1, math.max(allKnownBrushMeshIndices.Count(), brushCount));
            var removeBrushMeshIndices = new NativeHashSet<int>(capacity, Allocator.Temp);
            {
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    var brushCompactNodeID = brushes[nodeOrder];
                    int brushMeshHash = 0;
                    if (!compactHierarchy.IsValidCompactNodeID(brushCompactNodeID) ||
                        // NOTE: Assignment is intended, this is not supposed to be a comparison
                        (brushMeshHash = compactHierarchy.GetBrushMeshID(brushCompactNodeID)) == 0)
                    {
                        // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                        Debug.LogError($"Brush with ID ({brushCompactNodeID}) has its brushMeshID set to ({brushMeshHash}), which is invalid.");
                        allBrushMeshIDs[nodeOrder] = 0;
                    } else
                    {
                        allBrushMeshIDs[nodeOrder] = brushMeshHash;

                        if (!allKnownBrushMeshIndices.Contains(brushMeshHash))
                            allKnownBrushMeshIndices.Add(brushMeshHash);

                        if (removeBrushMeshIndices.Add(brushMeshHash))
                            allKnownBrushMeshIndices.Remove(brushMeshHash);
                    } 
                }

                // TODO: optimize all of this, especially slow for complete update

                // Regular index operator will return a copy instead of a reference *sigh* 
                var parameterPtr = (ChiselLayerParameters*)parameters.GetUnsafePtr();
                var brushMeshIndicesArray = allKnownBrushMeshIndices.ToNativeArray(Allocator.Temp); // NativeHashSet iterator is broken, so need to copy it to an array *sigh*
                foreach (int brushMeshHash in brushMeshIndicesArray)
                {
                    if (removeBrushMeshIndices.Contains(brushMeshHash)) 
                        continue;
                    
                    if (!brushMeshBlobs.ContainsKey(brushMeshHash))
                        continue;

                    ref var polygons = ref brushMeshBlobs[brushMeshHash].brushMeshBlob.Value.polygons;
                    for (int p = 0; p < polygons.Length; p++)
                    {
                        ref var polygon = ref polygons[p];
                        var layerUsage = polygon.surface.layerDefinition.layerUsage;
                        for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                        {
                            var layerUsageFlags = SurfaceLayers.kLayerUsageFlags[l];
                            if ((layerUsage & layerUsageFlags) != 0) parameterPtr[l].RegisterParameter(polygon.surface.layerDefinition.layerParameters[l]);
                        }
                    }
                }
                brushMeshIndicesArray.Dispose();

                foreach (int brushMeshHash in removeBrushMeshIndices)
                    allKnownBrushMeshIndices.Remove(brushMeshHash);

                for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                    parameterCounts[l] = parameterPtr[l].uniqueParameterCount;
            }
            removeBrushMeshIndices.Dispose();
        }
    }
}
