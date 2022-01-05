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
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct BuildCompactTreeJob : IJob
    {
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        // Read
        public CompactNodeID treeCompactNodeID;
        [NoAlias, ReadOnly] public NativeArray<CompactNodeID> brushes;
        [NoAlias, ReadOnly] public NativeArray<CompactNodeID> nodes;
        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy* compactHierarchyPtr;


        // Write
        [NoAlias, WriteOnly] public NativeReference<ChiselBlobAssetReference<CompactTree>> compactTreeRef;

        public void Execute()
        {
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            compactTreeRef.Value = CompactTreeBuilder.Create(ref compactHierarchy, nodes, brushes, treeCompactNodeID);
        }
    }
}
