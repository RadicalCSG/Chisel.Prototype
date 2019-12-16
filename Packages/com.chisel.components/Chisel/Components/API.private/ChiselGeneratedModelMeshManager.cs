﻿using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Components
{
    [Serializable]
    public sealed class ChiselGeneratedModelMesh
    {
        public GeneratedMeshDescription meshDescription;
        public GeneratedMeshKey         meshKey;

        /// <note>users should never directly use this mesh since it might be destroyed by Chisel</note>
        internal UnityEngine.Mesh       sharedMesh;

        public ChiselRenderComponents   renderComponents;
        public ChiselColliderComponents colliderComponents;
        public bool                     needsUpdate = true;
    }

    [Serializable]
    public sealed class ChiselRenderComponents
    {
        public MeshFilter   meshFilter;
        public MeshRenderer meshRenderer;
        public GameObject   gameObject;
        public Transform    transform;
        public float        uvLightmapUpdateTime;
    }

    [Serializable]
    public sealed class ChiselColliderComponents
    {
        public MeshCollider meshCollider;
        public GameObject   gameObject;
        public Transform    transform;
    }

    public static class ChiselGeneratedModelMeshManager
    {
        public static event Action              PreReset;
        public static event Action              PostReset;
        public static event Action<ChiselModel> PreUpdateModel;
        public static event Action<ChiselModel> PostUpdateModel;
        public static event Action              PostUpdateModels;
        
        const int MaxVertexCount = 65000;

        static HashSet<ChiselNode>  registeredNodeLookup    = new HashSet<ChiselNode>();
        static List<ChiselModel>    registeredModels        = new List<ChiselModel>();

        static ChiselSharedUnityMeshManager	    sharedUnityMeshes   = new ChiselSharedUnityMeshManager();
        static ChiselGeneratedComponentManager  componentGenerator  = new ChiselGeneratedComponentManager();
        
        static List<ChiselModel> updateList = new List<ChiselModel>();

        internal static void Reset()
        {
            if (PreReset != null) PreReset();

            foreach (var model in registeredModels) 
            {
                componentGenerator.RemoveAllGeneratedComponents(model);
            }

            registeredNodeLookup.Clear();
            registeredModels.Clear();
            sharedUnityMeshes.Clear();
            updateList.Clear();
            
            
            if (PostReset != null) PostReset();
        }

        internal static void Unregister(ChiselNode node)
        {
            if (!registeredNodeLookup.Remove(node))
                return;

            var model = node as ChiselModel;
            if (!ReferenceEquals(model, null))
            {
                componentGenerator.Unregister(model);
                sharedUnityMeshes.Unregister(model);
                registeredModels.Remove(model);
            }
        }

        internal static void Register(ChiselNode node)
        {
            if (!registeredNodeLookup.Add(node))
                return;

            var model = node as ChiselModel;
            if (!ReferenceEquals(model, null))
            {
                registeredModels.Add(model);
                sharedUnityMeshes.Register(model);
                componentGenerator.Register(model);
            }
        }

        public static void UpdateModels()
        {
            // Update the tree meshes
            if (!CSGManager.Flush())
            {
                ChiselGeneratedComponentManager.DelayedUVGeneration();
                if (sharedUnityMeshes.FindAllUnusedUnityMeshes())
                    sharedUnityMeshes.DestroyNonRecycledUnusedUnityMeshes();
                return; // Nothing to update ..
            }

            for (int m = 0; m < registeredModels.Count; m++)
            {
                var model = registeredModels[m];
                if (!model)
                    continue;

                var tree = model.Node;

                // See if the tree has been modified
                if (!tree.Dirty)
                    continue;

                try
                {
                    if (PreUpdateModel != null)
                        PreUpdateModel(model);
                }
                catch (Exception ex) // if there's a bug in user-code we don't want to end up in a bad state
                {
                    Debug.LogException(ex);
                }

                UpdateModelMeshDescriptions(model);

                // Re-use existing UnityEngine.Mesh if they exist
                sharedUnityMeshes.ReuseExistingMeshes(model);

                updateList.Add(model);
            }
            
            bool modifications = false;
            try
            {
                // Find all meshes whose refCounts are 0
                sharedUnityMeshes.FindAllUnusedUnityMeshes();

                // Separate loop so we can re-use garbage collected UnityEngine.Meshes to avoid allocation costs

                for (int m = 0; m < updateList.Count; m++)
                {
                    var model = updateList[m];

                    // Generate new UnityEngine.Mesh instances and fill them with data from the CSG algorithm (if necessary)
                    //	note: reuses garbage collected meshes when possible
                    sharedUnityMeshes.CreateNewMeshes(model);

                    // Generate (or re-use) components and set them up properly
                    componentGenerator.Rebuild(model);
                }
            }
            finally
            {
                for (int m = 0; m < updateList.Count; m++)
                {
                    var model = updateList[m];
                    try
                    {
                        modifications = true;
                        if (PostUpdateModel != null)
                            PostUpdateModel(model);
                    }
                    catch (Exception ex) // if there's a bug in user-code we don't want to end up in a bad state
                    {
                        Debug.LogException(ex);
                    }
                }
                updateList.Clear();
            }

            // Destroy all meshes whose refCounts are 0
            sharedUnityMeshes.DestroyNonRecycledUnusedUnityMeshes();

            if (modifications)
            {
                if (PostUpdateModels != null)
                    PostUpdateModels();
            }
        }

        public static void UpdateModel(ChiselModel model)
        {
            if (PreUpdateModel != null)
                PreUpdateModel(model);

            UpdateModelMeshDescriptions(model);

            // Re-use existing UnityEngine.Mesh if they exist
            sharedUnityMeshes.ReuseExistingMeshes(model);

            // Find all meshes whose refCounts are 0
            sharedUnityMeshes.FindAllUnusedUnityMeshes();

            // Generate new UnityEngine.Mesh instances and fill them with data from the CSG algorithm (if necessary)
            //	note: reuses garbage collected meshes when possible
            sharedUnityMeshes.CreateNewMeshes(model);

            // Generate (or re-use) components and set them up properly
            componentGenerator.Rebuild(model);

            if (PostUpdateModel != null)
                PostUpdateModel(model);

            // Destroy all meshes whose refCounts are 0
            sharedUnityMeshes.DestroyNonRecycledUnusedUnityMeshes();
        }

        static ChiselGeneratedModelMesh[]		__emptyGeneratedMeshesTable		= new ChiselGeneratedModelMesh[0];		// static to avoid allocations
        static List<ChiselGeneratedModelMesh>	__allocateGeneratedMeshesTable	= new List<ChiselGeneratedModelMesh>();	// static to avoid allocations
        internal static void UpdateModelMeshDescriptions(ChiselModel model)
        {
            var tree				= model.Node;
            if (!tree.Valid)
                return;
            
            var meshTypes			= ChiselMeshQueryManager.GetMeshQuery(model);
            var meshDescriptions	= tree.GetMeshDescriptions(meshTypes, model.VertexChannelMask);

            // Make sure we remove all old generated meshes
            sharedUnityMeshes.DecreaseMeshRefCount(model);

            // Check if the tree creates *any* meshes
            if (meshDescriptions == null)
            {
                model.generatedMeshes = __emptyGeneratedMeshesTable; 
                componentGenerator.RemoveAllGeneratedComponents(model);
                if (PostUpdateModel != null)
                    PostUpdateModel(model);
                return;
            }
            
            __allocateGeneratedMeshesTable.Clear();
            for (int d = 0; d < meshDescriptions.Length; d++)
            {
                var meshDescription = meshDescriptions[d];

                // Make sure the meshDescription actually holds a mesh
                if (meshDescription.vertexCount == 0 || 
                    meshDescription.indexCount == 0)
                    continue;
                
                // Make sure the mesh is valid
                if (meshDescription.vertexCount >= MaxVertexCount)
                {
                    Debug.LogError("Mesh has too many vertices (" + meshDescription.vertexCount + " > " + MaxVertexCount + ")");
                    continue;
                }

                // Add the generated mesh to the list
                __allocateGeneratedMeshesTable.Add(new ChiselGeneratedModelMesh { meshDescription = meshDescription, meshKey = new GeneratedMeshKey(meshDescription) });
            }

            // TODO: compare with existing generated meshes, only rebuild stuff for things that have actually changed

            model.generatedMeshes = __allocateGeneratedMeshesTable.ToArray();
        }
    }
}
