using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;

namespace Chisel.Core
{
    public delegate int FinishMeshUpdate(CSGTree tree, ref VertexBufferContents vertexBufferContents,
                                         UnityEngine.Mesh.MeshDataArray meshDataArray,
                                         NativeList<ChiselMeshUpdate> colliderMeshUpdates,
                                         NativeList<ChiselMeshUpdate> debugHelperMeshes,
                                         NativeList<ChiselMeshUpdate> renderMeshes,
                                         JobHandle dependencies);
}
