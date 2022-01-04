using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    internal struct NodeOrderNodeID { public int nodeOrder; public CompactNodeID compactNodeID; }

    [BurstCompile(CompileSynchronously = true)]
    unsafe struct UpdateTransformationsJob: IJobParallelForDefer
    {                
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        // Read
        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy*                    compactHierarchyPtr;
        [NoAlias, ReadOnly] public NativeArray<NodeOrderNodeID>         transformTreeBrushIndicesList;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<NodeTransformations>    transformationCache;

        public void Execute(int index)
        {
            // TODO: optimize, only do this when necessary
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            var lookup = transformTreeBrushIndicesList[index];
            transformationCache[lookup.nodeOrder] = CompactHierarchyManager.GetNodeTransformation(in compactHierarchy, lookup.compactNodeID);
        }
    }
}
