using System;
using System.Collections.Generic;
using Unity.Jobs;

namespace Chisel.Core
{
    public delegate bool UpdateMeshEvent(CSGTree tree, int index, ref VertexBufferContents vertexBufferContents);

    /// <summary>This class is manager class for all <see cref="Chisel.Core.CSGTreeNode"/>s.</summary>	
    public static partial class CSGManager
    {
        /// <summary>Destroys all <see cref="Chisel.Core.CSGTreeNode"/>s and all <see cref="Chisel.Core.BrushMesh"/>es.</summary>
        public static void	Clear	()	{ ClearAllNodes(); }

        /// <summary>Updates all pending changes to all <see cref="Chisel.Core.CSGTree"/>s.</summary>
        /// <returns>True if any <see cref="Chisel.Core.CSGTree"/>s have been updated, false if no changes have been found.</returns>
        public static bool	Flush	(UpdateMeshEvent updateMeshEvent)	{ if (!UpdateAllTreeMeshes(updateMeshEvent, out JobHandle handle)) return false; handle.Complete(); return true; }


        /// <summary>Destroy all <see cref="Chisel.Core.CSGTreeNode"/>s contained in <paramref name="nodes"/>.</summary>
        /// <param name="nodes">The <see cref="Chisel.Core.CSGTreeNode"/>s to destroy</param>
        /// <returns>True on success, false if there was a problem with destroying the <see cref="Chisel.Core.CSGTreeNode"/>s. See the log for more information.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="nodes"/> is null.</exception>  
        public static bool	Destroy	(CSGTreeNode[] nodes) { if (nodes == null) throw new ArgumentNullException("nodes"); return DestroyNodes(nodes); }

        /// <summary>Destroy all <see cref="Chisel.Core.CSGTreeNode"/>s contained in <paramref name="nodes"/>.</summary>
        /// <param name="nodes">The <see cref="Chisel.Core.CSGTreeNode"/>s to destroy</param>
        /// <returns>True on success, false if there was a problem with destroying the <see cref="Chisel.Core.CSGTreeNode"/>s. See the log for more information.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="nodes"/> is null.</exception>  
        public static bool Destroy(HashSet<CSGTreeNode> nodes) { if (nodes == null) throw new ArgumentNullException("nodes"); return DestroyNodes(nodes); }

        // TODO: add description
        public static bool	Destroy(int userID) { return DestroyAllNodesWithUserID(userID); }

        /// <summary>Destroy all <see cref="Chisel.Core.CSGTreeNode"/>s contained in <paramref name="nodes"/> and its children.</summary>
        /// <param name="nodes">The top level <see cref="Chisel.Core.CSGTreeNode"/>s of all <see cref="Chisel.Core.CSGTreeNode"/>s to destroy</param>
        /// <returns>True on success, false if there was a problem with destroying the <see cref="Chisel.Core.CSGTreeNode"/>s. See the log for more information.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="nodes"/> is null.</exception>  
        public static bool	DeepDestroy(CSGTreeNode[] nodes) { if (nodes == null) throw new ArgumentNullException("nodes"); return DeepDestroyNodes(nodes); }

        /// <summary>Destroy a <see cref="Chisel.Core.CSGTreeNode"/>s and its children.</summary>
        /// <param name="node">The top level <see cref="Chisel.Core.CSGTreeNode"/> to destroy</param>
        /// <returns>True on success, false if there was a problem with destroying the <see cref="Chisel.Core.CSGTreeNode"/>. See the log for more information.</returns>
        public static bool	DeepDestroy(CSGTreeNode node) { return DeepDestroyNode(node); }

        /// <value>The number of <see cref="Chisel.Core.CSGTreeNode"/>s.</value>
        public static int	TreeNodeCount			
        {
            get { return GetNodeCount(); } 
        }

        /// <value>The number of <see cref="Chisel.Core.CSGTreeBrush"/>es.</value>
        public static int	TreeBrushCount			
        {
            get { return GetBrushCount(); } 
        }
        
        /// <value>The number of <see cref="Chisel.Core.CSGTreeBranch"/>es.</value>
        public static int	TreeBranchCount			
        {
            get { return GetBranchCount(); } 
        }

        /// <value>The number of <see cref="Chisel.Core.CSGTree"/>s.</value>
        public static int	TreeCount				
        {
            get { return GetTreeCount(); } 
        }

        /// <value>The number of <see cref="Chisel.Core.BrushMesh"/>es.</value>
        public static int BrushMeshCount
        {
            get { return GetBrushMeshCount(); }
        }

        /// <value>All the <see cref="Chisel.Core.CSGTreeNode"/>s.</value>
        public static CSGTreeNode[] AllTreeNodes
        {
            get { return GetAllTreeNodes(); }
        }

        /// <value>All the <see cref="Chisel.Core.CSGTree"/>s.</value>
        public static CSGTree[] AllTrees
        {
            get { return GetAllTrees(); }
        }
        
        /// <value>All the <see cref="Chisel.Core.BrushMeshInstance"/>s.</value>
        public static BrushMeshInstance[] AllBrushMeshInstances
        {
            get { return GetAllBrushMeshInstances(); }
        }


        // TODO: add description
        public static CSGTreeNode	Duplicate(CSGTreeNode node) { return DuplicateInternal(node); }

        // TODO: add description
        public static CSGTreeNode[]	Duplicate(CSGTreeNode[] nodes) { return DuplicateInternal(nodes); }


        /// <value>Version number.</value>
        public static string Version
        {
            get
            {
                return
                    string.Format("v {0}{1}{2}",
                        Version,
#if !USE_NATIVE_IMPLEMENTATION
                        string.Empty,
#if DEBUG
                        " (DEBUG)"
#else
                        string.Empty
#endif
#else
                        HasBeenCompiledInDebugMode() ? " (C++ DEBUG)" : " (C++ RELEASE)",
#if DEBUG
                        " (C# DEBUG)"
#else
                        string.Empty
#endif
#endif
                        );
            }
        }
    }
}