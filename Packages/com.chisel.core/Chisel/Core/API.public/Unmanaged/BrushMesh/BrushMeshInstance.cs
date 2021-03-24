using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    /// <summary>Represents the instance of a <see cref="Chisel.Core.BrushMesh"/>. This can be used to assign a <see cref="Chisel.Core.BrushMesh"/> to one or multiple <see cref="Chisel.Core.CSGTreeBrush"/>es.</summary>
    /// <remarks>See the [Brush Meshes](~/documentation/brushMesh.md) article for more information.
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.BrushMeshInstance"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note></remarks>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct BrushMeshInstance
    {
        internal Int32			brushMeshHash;

        /// <value>Is the current <see cref="Chisel.Core.BrushMeshInstance"/> in a correct state</value>
        public bool				Valid				{ get { return brushMeshHash != BrushMeshInstance.InvalidInstanceID && BrushMeshManager.IsBrushMeshIDValid(brushMeshHash); } }
        
        /// <value>Returns the unique id of this <see cref="Chisel.Core.BrushMesh"/></value>
        public Int32			BrushMeshID			{ get { return brushMeshHash; } }

        /// <summary>Create a <see cref="Chisel.Core.BrushMeshInstance"/> from a given <see cref="Chisel.Core.BrushMesh"/></summary>
        /// <param name="brushMesh">The <see cref="Chisel.Core.BrushMesh"/> to create an instance with</param>
        /// <returns>A newly created <see cref="Chisel.Core.BrushMeshInstance"/> on success, or an invalid <see cref="Chisel.Core.BrushMeshInstance"/> on failure.</returns>
        public static BrushMeshInstance Create(BrushMesh brushMesh) { return new BrushMeshInstance { brushMeshHash = BrushMeshManager.CreateBrushMesh(brushMesh) }; }

        /// <summary>Destroy the <see cref="Chisel.Core.BrushMeshInstance"/> and release the memory used by this instance.</summary>
        public void	Destroy		()					{ var prevBrushMeshID = brushMeshHash; brushMeshHash = BrushMeshInstance.InvalidInstanceID; BrushMeshManager.DecreaseRefCount(prevBrushMeshID); }

        /// <summary>Update this <see cref="Chisel.Core.BrushMeshInstance"/> with the given <see cref="Chisel.Core.BrushMesh"/>.</summary>
        /// <param name="brushMesh">The <see cref="Chisel.Core.BrushMesh"/> to update the <see cref="Chisel.Core.BrushMeshInstance"/> with</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure. In case of failure the brush will keep using the previously set <see cref="Chisel.Core.BrushMesh"/>.</returns>
        public bool Set			(BrushMesh brushMesh)	
        {
            brushMeshHash = BrushMeshManager.CreateBrushMesh(brushMesh, oldBrushMeshHash: brushMeshHash);
            return Valid;
        }
        
        /// <value>An invalid instance</value>
        public static readonly BrushMeshInstance InvalidInstance = new BrushMeshInstance { brushMeshHash = BrushMeshInstance.InvalidInstanceID };
        internal const Int32 InvalidInstanceID = 0;
        
        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator == (BrushMeshInstance left, BrushMeshInstance right) { return left.brushMeshHash == right.brushMeshHash; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (BrushMeshInstance left, BrushMeshInstance right) { return left.brushMeshHash != right.brushMeshHash; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) { if (!(obj is BrushMeshInstance)) return false; var type = (BrushMeshInstance)obj; return brushMeshHash == type.brushMeshHash; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return brushMeshHash.GetHashCode(); }
        #endregion
    }
}