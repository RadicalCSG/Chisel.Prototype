using Chisel.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Chisel.Nodes
{
    public class ChiselGraphInstance : MonoBehaviour
    {
        public ChiselGraph graph;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        CSGTree tree;
        List<Mesh> meshes;

        void Start()
        {
            meshes = new List<Mesh>();
            CreateTree();
        }

        public void CreateTree()
        {
            tree = CSGTree.Create(GetInstanceID());
            var box = new ChiselBoxDefinition();
            box.min = -Vector3.one;
            box.max = Vector3.one;

            var brushContainer = new ChiselBrushContainer();
            BrushMeshFactory.GenerateBox(ref brushContainer, ref box);

            var instance = BrushMeshInstance.Create(brushContainer.brushMeshes[0]);
            var brush = CSGTreeBrush.Create(0, instance);
            tree.Add(brush);

            UpdateMesh();
        }

        void UpdateMesh()
        {
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

            print(meshDataArray.Length);

            meshes = new List<Mesh>();
            for (int i = 0; i < meshDataArray.Length; i++)
            {
                var mesh = new Mesh();
                meshes.Add(mesh);
            }
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

            for (int i = 0; i < meshDataArray.Length; i++)
            {
                var mesh = meshes[i];
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

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