using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{/*
    public struct CSGHierarchy
    {
        #region GetHash
        static unsafe uint GetHash(NativeList<uint> list)
        {
            return math.hash(list.GetUnsafePtr(), sizeof(uint) * list.Length);
        }

        static unsafe uint GetHash(ref CompactHierarchy hierarchy, NativeList<CSGTreeBrush> list)
        {
            using (var hashes = new NativeList<uint>(Allocator.Temp))
            {
                for (int i = 0; i < list.Length; i++)
                {
                    var compactNodeID = CompactHierarchyManager.GetCompactNodeID(list[i]);
                    if (!hierarchy.IsValidCompactNodeID(compactNodeID))
                        continue;
                    ref var node = ref hierarchy.GetNodeRef(compactNodeID);
                    hashes.Add(hierarchy.GetHash(in node));
                }
                return GetHash(hashes);
            }
        }
        #endregion
        public bool Valid { get { return compactHierarchyID != CompactHierarchyID.Invalid && CompactHierarchyManager.IsValidHierarchyID(compactHierarchyID); } }

        public CSGTreeBrush CreateBrush(Int32 userID = 0, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive) 
        {
            return CSGTreeBrush.Invalid;
        }

        public CSGTreeBrush CreateBrush(float4x4 localTransformation, Int32 userID = 0, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            return CSGTreeBrush.Invalid;
        }

        public CSGTreeBranch CreateBranch(Int32 userID = 0, CSGOperationType operation = CSGOperationType.Additive)
        {
            return CSGTreeBranch.Invalid;
        }

        public CSGTreeBranch CreateBranch(float4x4 localTransformation, Int32 userID = 0, CSGOperationType operation = CSGOperationType.Additive)
        {
            return CSGTreeBranch.Invalid;
        }


        //public bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        //public void Dispose()
        //internal unsafe uint GetHash(in CompactChildNode node)

        //public bool Destroy()  {  var prevID = compactHierarchyID; this = default; return CompactHierarchyManager.DestroyNode(prevID);  }

        [SerializeField] internal CompactHierarchyID compactHierarchyID;
    }
    */
}
