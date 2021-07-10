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
{
    public struct CSGHierarchy
    {
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
}
