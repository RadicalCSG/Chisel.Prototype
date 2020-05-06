using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    [Serializable]
    public sealed class ChiselGeneratedRenderMesh
    {
        public MeshQuery                meshQuery;
        public int                      meshKey;

        /// <note>users should never directly use this mesh since it might be destroyed by Chisel</note>
        internal UnityEngine.Mesh       sharedMesh;
        internal Material[]             renderMaterials;

        public ChiselRenderComponents   renderComponents;

        static List<Material> sSharedMaterials = new List<Material>();

        public bool HasIdenticalMaterials(MeshRenderer meshRenderer)
        {
            meshRenderer.GetSharedMaterials(sSharedMaterials);
            if (sSharedMaterials.Count != renderMaterials.Length)
                return false;
            for (int i = 0; i < renderMaterials.Length; i++)
            {
                if (!sSharedMaterials.Contains(renderMaterials[i]))
                    return false;
            }
            return true;
        }

        public void SetMaterialsTo(MeshRenderer meshRenderer)
        {
            meshRenderer.sharedMaterials = renderMaterials;
        }

        [NonSerialized]
        public bool                     needsUpdate = true;
    }
    
    [Serializable]
    public sealed class ChiselGeneratedColliderMesh
    {
        public int                      meshKey;

        /// <note>users should never directly use this mesh since it might be destroyed by Chisel</note>
        internal UnityEngine.Mesh       sharedMesh;
        internal PhysicMaterial         physicsMaterial;

        public ChiselColliderComponents colliderComponents;

        public bool HasIdenticalMaterials(MeshCollider meshCollider)
        {
            return (meshCollider.sharedMaterial == physicsMaterial);
        }

        public void SetMaterialsTo(MeshCollider meshCollider)
        {
            meshCollider.sharedMaterial = physicsMaterial;
        }

        [NonSerialized]
        public bool                     needsUpdate = true;
    }


    [Serializable]
    public sealed class ChiselRenderComponents
    {
        public MeshFilter   meshFilter;
        public MeshRenderer meshRenderer;
        public GameObject   gameObject;
        public Transform    transform;
        [NonSerialized]
        public float        uvLightmapUpdateTime;

        public bool IsValid()
        {
            return (gameObject && meshFilter && meshRenderer);
        }
    }

    [Serializable]
    public sealed class ChiselColliderComponents
    {
        public MeshCollider meshCollider;
        public GameObject   gameObject;
        public Transform    transform;

        public bool IsValid()
        {
            return (gameObject && meshCollider);
        }
    }

    public static class ChiselGeneratedModelMeshManager
    {
        public static event Action              PreReset;
        public static event Action              PostReset;
        public static event Action<ChiselModel> PostUpdateModel;
        public static event Action              PostUpdateModels;
        
        const int kMaxVertexCount = HashedVertices.kMaxVertexCount;

        internal static HashSet<ChiselNode>     registeredNodeLookup    = new HashSet<ChiselNode>();
        internal static List<ChiselModel>       registeredModels        = new List<ChiselModel>();

        static ChiselSharedUnityMeshManager	    sharedUnityMeshes       = new ChiselSharedUnityMeshManager();
        static ChiselGeneratedComponentManager  componentGenerator      = new ChiselGeneratedComponentManager();
        
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

        public static void UpdatePartialVisibilityMeshes(ChiselModel node)
        {
            if (!node || !node.generated.needVisibilityMeshUpdate)
                return;

            // TODO: figure out how to reuse mesh
            //sharedUnityMeshes.UpdatePartialVisibilityMeshes(node);
        }

        public static void UpdateModels()
        {
            // Update the tree meshes
            Profiler.BeginSample("Flush");
            try
            {
                if (!CSGManager.Flush())
                {
                    ChiselGeneratedComponentManager.DelayedUVGeneration();
                    if (sharedUnityMeshes.FindAllUnusedUnityMeshes())
                        sharedUnityMeshes.DestroyNonRecycledUnusedUnityMeshes();
                    return; // Nothing to update ..
                }
            }
            finally
            {
                Profiler.EndSample();
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

                Profiler.BeginSample("UpdateModelMeshDescriptions");
                UpdateModelMeshDescriptions(model);
                Profiler.EndSample();

                updateList.Add(model);
            }
            
            bool modifications = false;
            try
            {
                // Find all meshes whose refCounts are 0
                Profiler.BeginSample("sharedUnityMeshes.FindAllUnusedUnityMeshes");
                sharedUnityMeshes.FindAllUnusedUnityMeshes();
                Profiler.EndSample();

                // Separate loop so we can re-use garbage collected UnityEngine.Meshes to avoid allocation costs

                for (int m = 0; m < updateList.Count; m++)
                {
                    var model = updateList[m];

                    sharedUnityMeshes.UpdatePartialVisibilityMeshes(model);


                    // Generate new UnityEngine.Mesh instances and fill them with data from the CSG algorithm (if necessary)
                    //	note: reuses garbage collected meshes when possible

                    // Generate (or re-use) components and set them up properly
                    Profiler.BeginSample("componentGenerator.Rebuild");
                    componentGenerator.Rebuild(model);
                    Profiler.EndSample();
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
                        Profiler.BeginSample("PostUpdateModel");
                        if (PostUpdateModel != null)
                            PostUpdateModel(model);
                        Profiler.EndSample();
                    }
                    catch (Exception ex) // if there's a bug in user-code we don't want to end up in a bad state
                    {
                        Debug.LogException(ex);
                    }
                }
                updateList.Clear();
            }

            // Destroy all meshes whose refCounts are 0
            Profiler.BeginSample("DestroyNonRecycledUnusedUnityMeshes");
            sharedUnityMeshes.DestroyNonRecycledUnusedUnityMeshes();
            Profiler.EndSample();

            if (modifications)
            {
                Profiler.BeginSample("PostUpdateModels");
                PostUpdateModels?.Invoke();
                Profiler.EndSample();
            }
        }

        static List<Material> __foundMaterials = new List<Material>(); // static to avoid allocations
        static void AddGeneratedRenderMesh(ChiselModel model, GeneratedMeshDescription[] meshDescriptions, int startIndex, int endIndex, List<ChiselGeneratedRenderMesh> output)
        {
            __foundMaterials.Clear();
            for (int n = startIndex; n < endIndex; n++)
            {
                ref var meshDescription = ref meshDescriptions[n];
                var renderMaterial = ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(meshDescription.surfaceParameter);
                __foundMaterials.Add(renderMaterial);
            }

            var meshKey     = MeshKey.Calculate(meshDescriptions);
            // Find an existing mesh ...
            var sharedMesh  = sharedUnityMeshes.ReturnUnityMeshAndIncreaseRefCountIfExists(meshKey);

            // If not, create a new mesh ...
            if (!sharedMesh)
            {
                sharedMesh = sharedUnityMeshes.CreateNewUnityMesh(meshKey);
                sharedUnityMeshes.RetrieveUnityMesh(model, meshDescriptions, startIndex, endIndex, sharedMesh);
            }

            output.Add(new ChiselGeneratedRenderMesh
            {
                meshQuery       = meshDescriptions[0].meshQuery,
                meshKey         = meshKey,
                renderMaterials = __foundMaterials.ToArray(),
                sharedMesh      = sharedMesh
            });
        }

        static void AddGeneratedColliderMesh(ChiselModel model, GeneratedMeshDescription meshDescription, List<ChiselGeneratedColliderMesh> output)
        {
            // Find an existing mesh ...
            var meshKey     = MeshKey.Calculate(meshDescription);
            var sharedMesh  = sharedUnityMeshes.ReturnUnityMeshAndIncreaseRefCountIfExists(meshKey);

            // If not, create a new mesh ...
            if (!sharedMesh)
            {
                sharedMesh = sharedUnityMeshes.CreateNewUnityMesh(meshKey);
                sharedUnityMeshes.RetrieveUnityMeshPositionOnly(model, meshDescription, sharedMesh);
            }

            var physicsMaterial = ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(meshDescription.surfaceParameter);

            output.Add(new ChiselGeneratedColliderMesh
            {
                meshKey         = meshKey,
                physicsMaterial = physicsMaterial,
                sharedMesh      = sharedMesh
            });
        }

        static int MeshDescriptionSorter(GeneratedMeshDescription x, GeneratedMeshDescription y)
        {
            if (x.meshQuery.LayerParameterIndex != y.meshQuery.LayerParameterIndex) return ((int)x.meshQuery.LayerParameterIndex) - ((int)y.meshQuery.LayerParameterIndex);
            if (x.meshQuery.LayerQuery          != y.meshQuery.LayerQuery) return ((int)x.meshQuery.LayerQuery) - ((int)y.meshQuery.LayerQuery);
            if (x.surfaceParameter  != y.surfaceParameter) return ((int)x.surfaceParameter) - ((int)y.surfaceParameter);
            if (x.geometryHashValue != y.geometryHashValue) return ((int)x.geometryHashValue) - ((int)y.geometryHashValue);
            return 0;
        }

        static Comparison<GeneratedMeshDescription> kMeshDescriptionSorterDelegate = MeshDescriptionSorter;

        static ChiselGeneratedRenderMesh[]		    __emptyGeneratedRenderMeshesTable		= new ChiselGeneratedRenderMesh[0];		// static to avoid allocations
        static ChiselGeneratedColliderMesh[]		__emptyGeneratedColliderMeshesTable		= new ChiselGeneratedColliderMesh[0];		// static to avoid allocations
        static List<ChiselGeneratedRenderMesh>	    __allocateGeneratedRenderMeshesTable	= new List<ChiselGeneratedRenderMesh>();	// static to avoid allocations
        static List<ChiselGeneratedColliderMesh>	__allocateGeneratedColliderMeshesTable	= new List<ChiselGeneratedColliderMesh>();	// static to avoid allocations
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
            if (meshDescriptions == null || meshDescriptions.Length == 0)
            {
                model.generatedRenderMeshes     = __emptyGeneratedRenderMeshesTable;
                model.generatedColliderMeshes   = __emptyGeneratedColliderMeshesTable;
                componentGenerator.RemoveAllGeneratedComponents(model);
                PostUpdateModel?.Invoke(model);
                return;
            }

            // Sort all meshDescriptions so that meshes that can be merged are next to each other
            Array.Sort(meshDescriptions, kMeshDescriptionSorterDelegate);


            Debug.Assert(LayerParameterIndex.LayerParameter1 < LayerParameterIndex.LayerParameter2);
            Debug.Assert((LayerParameterIndex.LayerParameter1 + 1) == LayerParameterIndex.LayerParameter2);
            Debug.Assert(meshDescriptions[0].meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1);

            int descriptionIndex = 0;

            // Loop through all meshDescriptions with LayerParameter1, and create renderable meshes from them
            __allocateGeneratedRenderMeshesTable.Clear();
            if (meshDescriptions[0].meshQuery.LayerParameterIndex == LayerParameterIndex.LayerParameter1)
            {
                var prevQuery   = meshDescriptions[0].meshQuery;
                var startIndex  = 0;
                for (; descriptionIndex < meshDescriptions.Length; descriptionIndex++)
                {
                    ref var meshDescriptionIterator = ref meshDescriptions[descriptionIndex];
                    // Exit when layerParameterIndex is no longer LayerParameter1
                    if (meshDescriptionIterator.meshQuery.LayerParameterIndex != LayerParameterIndex.LayerParameter1)
                        break;

                    var currQuery = meshDescriptionIterator.meshQuery;
                    if (prevQuery == currQuery)
                        continue;

                    // Group by all meshDescriptions with same query
                    AddGeneratedRenderMesh(model, meshDescriptions, startIndex, descriptionIndex, __allocateGeneratedRenderMeshesTable);

                    startIndex = descriptionIndex;
                    prevQuery = currQuery;
                }

                // Group by all meshDescriptions with same query
                AddGeneratedRenderMesh(model, meshDescriptions, startIndex, descriptionIndex, __allocateGeneratedRenderMeshesTable);
            }

            __allocateGeneratedColliderMeshesTable.Clear();
            if (descriptionIndex < meshDescriptions.Length &&
                meshDescriptions[descriptionIndex].meshQuery.LayerParameterIndex == LayerParameterIndex.LayerParameter2)
            {
                // Loop through all meshDescriptions with LayerParameter2, and create collider meshes from them
                for (; descriptionIndex < meshDescriptions.Length; descriptionIndex++)
                {
                    ref var meshDescription = ref meshDescriptions[descriptionIndex];
                    // Exit when layerParameterIndex is no longer LayerParameter2
                    if (meshDescription.meshQuery.LayerParameterIndex != LayerParameterIndex.LayerParameter2)
                        break;

                    AddGeneratedColliderMesh(model, meshDescription, __allocateGeneratedColliderMeshesTable);
                }
            }

            Debug.Assert(descriptionIndex == meshDescriptions.Length);

            // TODO: compare with existing generated meshes, only rebuild stuff for things that have actually changed

            model.generatedRenderMeshes     = __allocateGeneratedRenderMeshesTable.ToArray();
            model.generatedColliderMeshes   = __allocateGeneratedColliderMeshesTable.ToArray();
        }
    }
}
