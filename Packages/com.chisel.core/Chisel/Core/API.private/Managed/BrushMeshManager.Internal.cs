using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public partial class BrushMeshManager
    {
        static List<BrushMesh>	brushMeshes		= new List<BrushMesh>();
        static List<int>		userIDs			= new List<int>();
        static List<int>		unusedIDs		= new List<int>();

        internal static bool		IsBrushMeshIDValid		(Int32 brushMeshInstanceID)	{ return brushMeshInstanceID > 0 && brushMeshInstanceID <= brushMeshes.Count; }

        private static bool			AssertBrushMeshIDValid	(Int32 brushMeshInstanceID)
        {
            if (!IsBrushMeshIDValid(brushMeshInstanceID))
            {
                var nodeIndex = brushMeshInstanceID - 1;
                if (nodeIndex >= 0 && nodeIndex < brushMeshes.Count)
                    Debug.LogError($"Invalid ID {brushMeshInstanceID}");
                else
                    Debug.LogError($"Invalid ID {brushMeshInstanceID}, outside of bounds (min 1, max {brushMeshes.Count})");
                return false;
            }
            return true;
        }

        internal static int			GetBrushMeshCount		()					{ return brushMeshes.Count - unusedIDs.Count; }

        public static Int32			GetBrushMeshUserID		(Int32 brushMeshInstanceID)
        {
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return default;
            return userIDs[brushMeshInstanceID - 1];
        }

        public static BrushMesh		GetBrushMesh			(BrushMeshInstance instance)
        {
            return GetBrushMesh(instance.brushMeshID);
        }

        public static BrushMesh		GetBrushMesh			(Int32 brushMeshInstanceID)
        {
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return null;
            var brushMesh = brushMeshes[brushMeshInstanceID - 1];
            if (brushMesh == null)
                return null;
            return brushMesh;
        }

        public static Int32 CreateBrushMesh(Int32				 userID,
                                            float3[]			 vertices,
                                            BrushMesh.HalfEdge[] halfEdges,
                                            BrushMesh.Polygon[]	 polygons)
        {
            int			brushMeshID		= CreateBrushMeshID(userID);
            BrushMesh	brushMesh		= GetBrushMesh(brushMeshID);

            if (brushMesh == null)
            {
                Debug.LogWarning("brushMesh == nullptr");
                DestroyBrushMesh(brushMeshID);
                return BrushMeshInstance.InvalidInstanceID;
            }

            if (!brushMesh.Set(vertices, halfEdges, polygons))
            {
                Debug.LogWarning("GenerateMesh failed");
                DestroyBrushMesh(brushMeshID);
                return BrushMeshInstance.InvalidInstanceID;
            }

            var brushMeshIndex = brushMeshID - 1;
            if (ChiselMeshLookup.Value.brushMeshBlobs.TryGetValue(brushMeshIndex, out BlobAssetReference<BrushMeshBlob> item))
            {
                ChiselMeshLookup.Value.brushMeshBlobs.Remove(brushMeshIndex);
                if (item.IsCreated)
                    item.Dispose();
            }

            ChiselMeshLookup.Value.brushMeshUpdateList.Add(brushMeshIndex);
            /*
            Profiler.BeginSample("BrushMeshBlob.Build");
            ChiselMeshLookup.Value.brushMeshBlobs[brushMeshIndex] = BrushMeshBlob.Build(brushMesh);
            Profiler.EndSample();*/
            return brushMeshID;
        }


        public static bool UpdateBrushMesh(Int32				brushMeshInstanceID,
                                           float3[]			    vertices,
                                           BrushMesh.HalfEdge[] halfEdges,
                                           BrushMesh.Polygon[]	polygons)
        {
            if (vertices == null || halfEdges == null || polygons == null) return false;
            
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return false;

            BrushMesh brushMesh = GetBrushMesh(brushMeshInstanceID);
            if (brushMesh == null)
            {
                Debug.LogWarning("Brush has no BrushMeshInstance set");
                return false;
            }

            if (!brushMesh.Set(vertices, halfEdges, polygons))
            {
                Debug.LogWarning("GenerateMesh failed");
                return false;
            }

            var brushMeshIndex = brushMeshInstanceID - 1;
            if (ChiselMeshLookup.Value.brushMeshBlobs.TryGetValue(brushMeshIndex, out BlobAssetReference<BrushMeshBlob> item))
            {
                ChiselMeshLookup.Value.brushMeshBlobs.Remove(brushMeshIndex);
                if (item.IsCreated)
                    item.Dispose();
            }
            ChiselMeshLookup.Value.brushMeshUpdateList.Add(brushMeshIndex);
            return true;
        }

        private static int CreateBrushMeshID(Int32 userID)
        {
            if (unusedIDs.Count == 0)
            {
                int index = brushMeshes.Count;
                brushMeshes.Add(new BrushMesh());
                userIDs.Add(userID);
                return index + 1;
            }

            unusedIDs.Sort(); // sorry!
            var brushMeshID		= unusedIDs[0];
            var brushMeshIndex	= brushMeshID - 1;
            unusedIDs.RemoveAt(0); // sorry again
            brushMeshes[brushMeshIndex].Reset();
            userIDs[brushMeshIndex] = userID;
            if (ChiselMeshLookup.Value.brushMeshBlobs.TryGetValue(brushMeshIndex, out BlobAssetReference<BrushMeshBlob> item))
            {
                ChiselMeshLookup.Value.brushMeshBlobs.Remove(brushMeshIndex);
                if (item.IsCreated)
                    item.Dispose();
            }
            return brushMeshID;
        }

        public static bool DestroyBrushMesh(Int32 brushMeshInstanceID)
        {
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return false;

            Chisel.Core.CompactHierarchyManager.NotifyBrushMeshRemoved(brushMeshInstanceID);

            var brushMeshIndex = brushMeshInstanceID - 1;
            if (ChiselMeshLookup.Value.brushMeshBlobs.TryGetValue(brushMeshIndex, out BlobAssetReference<BrushMeshBlob> item))
            {
                ChiselMeshLookup.Value.brushMeshBlobs.Remove(brushMeshIndex);
                if (item.IsCreated)
                    item.Dispose();
            }
            brushMeshes[brushMeshIndex].Reset();
            userIDs[brushMeshIndex] = default;
            unusedIDs.Add(brushMeshInstanceID);

            // TODO: remove elements when last values are invalid

            return true;
        }
        
        internal static BrushMeshInstance[] GetAllBrushMeshInstances()
        {
            var instanceCount = GetBrushMeshCount();
            var allInstances = new BrushMeshInstance[instanceCount];
            if (instanceCount == 0)
                return allInstances;
            
            int index = 0;
            for (int i = 0; i < brushMeshes.Count; i++)
            {
                if (IsBrushMeshIDValid(i))
                    continue;
                
                allInstances[index] = new BrushMeshInstance() { brushMeshID = i };
                index++;
            }
            return allInstances;
        }
    }
}
