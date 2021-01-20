using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        public bool IsDirty { get; set; }

        public List<GraphProperty> properties;
        public Dictionary<string, GraphProperty> overriddenProperties;

        CSGTree tree;
        List<Mesh> meshes;

        void Start()
        {
            meshes = new List<Mesh>();
            UpdateCSG();
        }

        void Update()
        {
            UpdateCSG();
        }

        void OnValidate()
        {
            graph.instance = this;

            if (properties == null || graph.properties.Count != properties.Count)
                InitProperties();

            for (int i = 0; i < graph.properties.Count; i++)
            {
                if (graph.properties[i].Name != properties[i].Name)
                    InitProperties();
            }
        }

        void InitProperties()
        {
            properties = new List<GraphProperty>();
            foreach (var property in graph.properties)
                properties.Add(Clone(property));
            UpdateProperties();
        }

        GraphProperty Clone(GraphProperty source)
        {
            var newProperty = Activator.CreateInstance(source.GetType()) as GraphProperty;
            var fields = source.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var info in fields)
                info.SetValue(newProperty, info.GetValue(source));

            return newProperty;
        }

        public void UpdateProperties()
        {
            if (overriddenProperties == null)
                overriddenProperties = new Dictionary<string, GraphProperty>();
            overriddenProperties.Clear();

            foreach (var property in properties)
                if (property.overrideValue)
                    overriddenProperties[property.Name] = property;
        }

        public void UpdateCSG()
        {
            if (!IsDirty) return;
            IsDirty = false;

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

            meshes[1].RecalculateNormals();
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