using Chisel.Core;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Chisel.Nodes
{
    [ExecuteInEditMode]
    public class ChiselGraphInstance : MonoBehaviour
    {
        public ChiselGraph graph;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public bool isDirty;

        CSGTree tree;
        List<Mesh> meshes;

        void Start()
        {
            meshes = new List<Mesh>();
            UpdateCSG();
        }

        void Update()
        {
            if (isDirty)
                UpdateCSG();
        }

        public void UpdateCSG()
        {
            isDirty = false;
            if (!tree.Valid)
                tree = CSGTree.Create(GetInstanceID());
            else
                tree.Clear();
            graph.CollectTreeNode(tree);
            CSGManager.Flush(finishMeshUpdates);
        }

        int finishMeshUpdates(CSGTree tree,
            ref VertexBufferContents vertexBufferContents,
            Mesh.MeshDataArray meshDataArray,
            NativeList<ChiselMeshUpdate> colliderMeshUpdates,
            NativeList<ChiselMeshUpdate> debugHelperMeshes,
            NativeList<ChiselMeshUpdate> renderMeshes,
            JobHandle dependencies)
        {
            dependencies.Complete();

            if (meshes == null || meshes.Count != meshDataArray.Length)
            {
                Debug.Log("new mesh");
                meshes = new List<Mesh>();
                for (int i = 0; i < meshDataArray.Length; i++)
                    meshes.Add(new Mesh());
            }

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

            meshes[0].RecalculateNormals();
            meshes[1].RecalculateBounds();
            meshFilter.mesh = meshes[1];

            return 1;
        }

        public void Rebuild()
        {
            CSGManager.Clear();
            ChiselBrushMaterialManager.Reset();
        }
    }
}