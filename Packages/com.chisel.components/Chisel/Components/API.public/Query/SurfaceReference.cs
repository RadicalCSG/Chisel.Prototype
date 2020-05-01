using UnityEngine;
using System.Collections;
using System;
using Chisel.Core;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Chisel.Components
{
    [Serializable]
    public sealed class SurfaceReference : IEquatable<SurfaceReference>, IEqualityComparer<SurfaceReference>
    {
        public ChiselNode			       node;
        public ChiselBrushContainerAsset   brushContainerAsset;

        public int  subNodeIndex;
        public int  subMeshIndex;
        public int  surfaceID;
        public int  surfaceIndex;

        public SurfaceReference(ChiselNode node, ChiselBrushContainerAsset brushContainerAsset, int subNodeIndex, int subMeshIndex, int surfaceIndex, int surfaceID)
        {
            this.node                   = node;
            this.brushContainerAsset    = brushContainerAsset;
            this.subNodeIndex           = subNodeIndex;
            this.subMeshIndex           = subMeshIndex;
            this.surfaceIndex           = surfaceIndex;
            this.surfaceID              = surfaceID;
        }

        public void SetDirty()
        {
            brushContainerAsset.SetDirty();
        }

        public ChiselSurface BrushSurface
        {
            get
            {
                if (!brushContainerAsset)
                    return null;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return null;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return null;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return null;
                return brushMesh.polygons[surfaceIndex].surface;
            }
        }

        public ChiselBrushMaterial BrushMaterial
        {
            get
            {
                if (!brushContainerAsset)
                    return null;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return null;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return null;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return null;
                var surface = brushMesh.polygons[surfaceIndex].surface;
                if (surface == null)
                    return null;
                return surface.brushMaterial;
            }
        }

        // A default polygon to return when we actually can't return a polygon
        static BrushMesh.Polygon s_DefaultPolygon = new BrushMesh.Polygon();
        public ref BrushMesh.Polygon Polygon
        {
            get
            {
                if (!brushContainerAsset)
                    return ref s_DefaultPolygon;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return ref s_DefaultPolygon;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return ref s_DefaultPolygon;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return ref s_DefaultPolygon;
                return ref brushMesh.polygons[surfaceIndex];
            }
        }

        public BrushMesh BrushMesh
        {
            get
            {
                if (!brushContainerAsset)
                    return null;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return null;
                var brushMeshes = brushContainerAsset.BrushMeshes;
                if (brushMeshes == null)
                    return null;
                return brushMeshes[subMeshIndex];
            }
        }

        public IEnumerable<Vector3> PolygonVertices
        {
            get
            {
                if (!brushContainerAsset)
                    yield break;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    yield break;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    yield break;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    yield break;
                var polygon		= brushMesh.polygons[surfaceIndex];
                var edges		= brushMesh.halfEdges;
                var vertices	= brushMesh.vertices;
                var firstEdge	= polygon.firstEdge;
                var lastEdge	= firstEdge + polygon.edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                    yield return vertices[edges[e].vertexIndex];
            }
        }

        public Plane? WorldPlane
        {
            get
            {
                if (!brushContainerAsset)
                    return null;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return null;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return null;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.planes.Length)
                    return null;

                var localPlaneVector = brushMesh.planes[surfaceIndex];
                var localPlane       = new Plane(localPlaneVector.xyz, localPlaneVector.w);
                localPlane.Translate(-node.PivotOffset);
                return LocalToWorldSpace.TransformPlane(localPlane);
            }
        }

        public CSGTreeBrush TreeBrush
        {
            get
            {
                if (node == null)
                    return (CSGTreeBrush)CSGTreeNode.InvalidNode;
                return (CSGTreeBrush)node.GetTreeNodeByIndex(subNodeIndex);
            }
        }

        public Matrix4x4 LocalToWorldSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;
                
                return node.hierarchyItem.LocalToWorldMatrix;
            }
        }

        public Matrix4x4 WorldToLocalSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;
                
                return node.hierarchyItem.WorldToLocalMatrix;
            }
        }

        public Matrix4x4 WorldToPlaneSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;

                if (!brushContainerAsset)
                    return Matrix4x4.identity;

                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return Matrix4x4.identity;

                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return Matrix4x4.identity;
                
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.planes.Length)
                    return Matrix4x4.identity;
                
                var localToPlaneSpace   = (Matrix4x4)MathExtensions.GenerateLocalToPlaneSpaceMatrix(brushMesh.planes[surfaceIndex]);
                var worldToLocal        = node.hierarchyItem.WorldToLocalMatrix;
                return localToPlaneSpace * worldToLocal;
            }	
        }

        public Matrix4x4 PlaneToWorldSpace
        {
            get
            {
                return Matrix4x4.Inverse(WorldToPlaneSpace);
            }
        }

        public Matrix4x4 WorldSpaceToPlaneSpace(in Matrix4x4 worldSpaceTransformation)
        {
            var worldToPlaneSpace = WorldToPlaneSpace;
            var planeToWorldSpace = Matrix4x4.Inverse(worldToPlaneSpace);

            return worldToPlaneSpace * worldSpaceTransformation * planeToWorldSpace;
        }

        public void WorldSpaceTransformUV(in Matrix4x4 worldSpaceTransformation, in UVMatrix originalMatrix)
        {
            var planeSpaceTransformation = WorldSpaceToPlaneSpace(in worldSpaceTransformation);
            PlaneSpaceTransformUV(in planeSpaceTransformation, in originalMatrix);
        }

        public void PlaneSpaceTransformUV(in Matrix4x4 planeSpaceTransformation, in UVMatrix originalMatrix)
        {
            if (Polygon.surface == null)
                return;
            // TODO: We're modifying uv coordinates for the generated brush-meshes, 
            //       when we should be changing surfaces descriptions in the generators that generate the brush-meshes ..
            //       Now all UVs are overridden everytime we rebuild the geometry
            Polygon.surface.surfaceDescription.UV0 = (UVMatrix)((Matrix4x4)originalMatrix * planeSpaceTransformation);
            brushContainerAsset.SetDirty();
        }


        #region Equals
        public bool Equals(SurfaceReference other)
        {
            return Equals(this, other);
        }

        public bool Equals(SurfaceReference x, SurfaceReference y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (ReferenceEquals(x, null) ||
                ReferenceEquals(y, null))
                return false;
            return	//x.treeBrush			== y.treeBrush &&
                    x.brushContainerAsset	== y.brushContainerAsset &&
                    x.subNodeIndex		== y.subNodeIndex &&
                    x.subMeshIndex		== y.subMeshIndex &&
                    x.surfaceID			== y.surfaceID &&
                    x.surfaceIndex		== y.surfaceIndex;
        }
        
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            var other = obj as SurfaceReference;
            if (ReferenceEquals(other, null))
                return false;
            return Equals(this, other);
        }
        #endregion

        #region GetHashCode
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public int GetHashCode(SurfaceReference obj)
        {
            // TODO: use a better hash combiner ..
            return  //obj.treeBrush.NodeID.GetHashCode() ^
                    ((obj.brushContainerAsset == null) ? 0 : obj.brushContainerAsset.GetInstanceID()) ^
                    obj.subNodeIndex ^
                    obj.subMeshIndex ^
                    obj.surfaceID ^
                    obj.surfaceIndex;
        }
        #endregion
    }
    
}
