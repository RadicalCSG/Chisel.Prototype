using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
    public static class CSGTreeExtensions
    {
        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTree"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static bool SetChildren(in this CSGTree tree, params CSGTreeNode[] array) 
        { 
            if (array == null) 
                throw new ArgumentNullException(nameof(array));

            tree.Clear();
            return tree.AddRange(array);
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTree"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="list">The list whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The list itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static bool SetChildren(in this CSGTree tree, List<CSGTreeNode> list) 
        { 
            if (list == null) 
                throw new ArgumentNullException(nameof(list));

            bool success = true;
            tree.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                success = tree.Add(list[i]) && success;
            }
            return success;
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTreeBranch"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static bool SetChildren(in this CSGTreeBranch branch, params CSGTreeNode[] array)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            branch.Clear();
            return branch.AddRange(array);
        }

        /// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTreeBranch"/> to the give list of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
        /// <param name="list">The list whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The list itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static bool SetChildren(in this CSGTreeBranch branch, List<CSGTreeNode> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            bool success = true;
            branch.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                success = branch.Add(list[i]) && success;
            }
            return success;
        }


        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTree"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static unsafe bool InsertRange(in this Chisel.Core.CSGTree tree, int index, params Chisel.Core.CSGTreeNode[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var length = array.Length;
            if (length == 0) return true;
            var arrayPtr = (Chisel.Core.CSGTreeNode*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.PinGCArrayAndGetDataAddress(array, out var handle);
            try
            {
                return tree.InsertRange(index, arrayPtr, length);
            }
            finally
            {
                Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ReleaseGCObject(handle);
            }
        }


        /// <summary>Inserts the <see cref="Chisel.Core.CSGTreeNode"/>s of an array into the <see cref="Chisel.Core.CSGTreeBranch"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static unsafe bool InsertRange(in this Chisel.Core.CSGTreeBranch branch, int index, params Chisel.Core.CSGTreeNode[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var length = array.Length;
            if (length == 0) return true;
            var arrayPtr = (Chisel.Core.CSGTreeNode*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.PinGCArrayAndGetDataAddress(array, out var handle);
            try
            {
                return branch.InsertRange(index, arrayPtr, length);
            }
            finally
            {
                Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ReleaseGCObject(handle);
            }
        }


        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static unsafe bool AddRange(in this Chisel.Core.CSGTree tree, params Chisel.Core.CSGTreeNode[] array)
        {
            return InsertRange(tree, tree.Count, array);
        }


        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public static unsafe bool AddRange(in this Chisel.Core.CSGTreeBranch branch, params Chisel.Core.CSGTreeNode[] array)
        {
            return InsertRange(branch, branch.Count, array);
        }
    }
}
