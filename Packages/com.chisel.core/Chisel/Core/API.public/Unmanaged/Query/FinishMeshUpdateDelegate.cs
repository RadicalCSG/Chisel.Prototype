using Unity.Jobs;
using Unity.Collections;

namespace Chisel.Core
{
    public delegate int FinishMeshUpdate(CSGTree tree, 
                                         ref VertexBufferContents vertexBufferContents,
                                         ref UnityEngine.Mesh.MeshDataArray meshDataArray,
                                         NativeList<ChiselMeshUpdate> colliderMeshUpdates,
                                         NativeList<ChiselMeshUpdate> debugHelperMeshes,
                                         NativeList<ChiselMeshUpdate> renderMeshes,
                                         JobHandle dependencies);
}
