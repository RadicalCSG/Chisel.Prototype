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

        void Start()
        {
            CreateTree();
        }

        void CreateTree()
        {
            tree = CSGTree.Create(GetInstanceID());
            var brush = new CSGTreeBrush();
            var brushContainer = new ChiselBrushContainer();
            var box = new ChiselBoxDefinition();

            BrushMeshFactory.GenerateBox(ref brushContainer, ref box);

            Debug.Log(brushContainer.brushMeshes.Length);

            brush.BrushMesh = BrushMeshInstance.Create(brushContainer.brushMeshes[0]);
            tree.Add(brush);
        }

        void UpdateMesh()
        {
            CSGManager.Flush(finishMeshUpdates);
        }

        List<Mesh> foundMeshes = new List<Mesh>();

        int finishMeshUpdates(CSGTree tree,
            ref VertexBufferContents vertexBufferContents,
            Mesh.MeshDataArray meshDataArray,
            NativeList<ChiselMeshUpdate> colliderMeshUpdates,
            NativeList<ChiselMeshUpdate> debugHelperMeshes,
            NativeList<ChiselMeshUpdate> renderMeshes,
            JobHandle dependencies)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();



            var foundMeshCount = foundMeshes.Count;
            foundMeshes.Clear();
            return foundMeshCount;
        }
    }
}