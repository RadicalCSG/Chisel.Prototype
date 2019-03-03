using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Core
{
    internal partial class BrushMeshManager
    {
#if USE_INTERNAL_IMPLEMENTATION
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
                    Debug.LogError("Invalid ID " + brushMeshInstanceID);
                else
                    Debug.LogError("Invalid ID " + brushMeshInstanceID + ", outside of bounds");
                return false;
            }
            return true;
        }
        
        internal static int			GetBrushMeshCount		()					{ return brushMeshes.Count - unusedIDs.Count; }

        public static Int32			GetBrushMeshUserID		(Int32 brushMeshInstanceID)
        {
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return CSGManager.kDefaultUserID;
            return userIDs[brushMeshInstanceID - 1];
        }

        public static BrushMesh		GetBrushMesh			(Int32 brushMeshInstanceID)
        {
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return null;
            return brushMeshes[brushMeshInstanceID - 1];
        }

        public static Int32 CreateBrushMesh(Int32				 userID,
                                            Vector3[]			 vertices,
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
            return brushMeshID;
        }


        public static bool UpdateBrushMesh(Int32				brushMeshInstanceID,
                                           Vector3[]			vertices,
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

            CSGManager.NotifyBrushMeshModified(brushMeshInstanceID);
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
            return brushMeshID;
        }

        public static bool DestroyBrushMesh(Int32 brushMeshInstanceID)
        {
            if (!AssertBrushMeshIDValid(brushMeshInstanceID))
                return false;

            CSGManager.NotifyBrushMeshRemoved(brushMeshInstanceID);

            var brushMeshIndex = brushMeshInstanceID - 1;
            brushMeshes[brushMeshIndex].Reset();
            userIDs[brushMeshIndex] = CSGManager.kDefaultUserID;
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
#endif
    }
}
