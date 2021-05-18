﻿using UnityEngine;
using System.Collections;
using System;
using Chisel.Core;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Components
{
    [Serializable]
    public sealed class SurfaceReference : IEquatable<SurfaceReference>, IEqualityComparer<SurfaceReference>
    {
        public ChiselGeneratorComponent     node;
        public CSGTreeBrush                 brush;

        public int      descriptionIndex;
        public int      surfaceIndex;

        public SurfaceReference(ChiselNode node, int descriptionIndex, CSGTreeBrush brush, int surfaceIndex)
        {
            this.node                   = node as ChiselGeneratorComponent;
            this.brush                  = brush;
            this.descriptionIndex       = descriptionIndex;
            this.surfaceIndex           = surfaceIndex;
        }

        public void SetDirty()
        {
            //brushContainerAsset.SetDirty();
        }

        public ChiselBrushMaterial BrushMaterial
        {
            get
            {
                if (!node)
                    return null;

                return node.GetBrushMaterial(descriptionIndex);
            }
        }

        // A default polygon to return when we actually can't return a polygon
        //static BrushMeshBlob.Polygon s_DefaultPolygon = default;
        public UVMatrix UV0
        {
            get
            {
                if (!node)
                    return UVMatrix.identity;

                return node.GetSurfaceUV0(descriptionIndex);
            }
            set
            {
                if (!node)
                    return;
                node.SetSurfaceUV0(descriptionIndex, value);
            }
        }
        public SurfaceDescription SurfaceDescription
        {
            get
            {
                if (!node)
                    return SurfaceDescription.Default;

                return node.GetSurfaceDescription(descriptionIndex);
            }
            set
            {
                if (!node)
                    return;
                node.SetSurfaceDescription(descriptionIndex, value);
            }
        }

        public BlobAssetReference<BrushMeshBlob> BrushMesh
        {
            get
            {
                if (!brush.Valid)
                    return default;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return default;

                return brushMeshBlob;
            }
        }

        public IEnumerable<Vector3> PolygonVertices
        {
            get
            {
                if (!brush.Valid)
                    yield break;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    yield break;

                if (surfaceIndex < 0 || surfaceIndex >= brushMeshBlob.Value.polygons.Length)
                    yield break;

                var polygon = brushMeshBlob.Value.polygons[surfaceIndex];                
                var firstEdge = polygon.firstEdge;
                var lastEdge  = firstEdge + polygon.edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                    yield return brushMeshBlob.Value.localVertices[brushMeshBlob.Value.halfEdges[e].vertexIndex];
            }
        }

        public Plane? WorldPlane
        {
            get
            {
                if (!brush.Valid)
                    return null;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return null;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return null;

                var localPlaneVector = brushMesh.localPlanes[surfaceIndex];
                var localPlane = new Plane(localPlaneVector.xyz, localPlaneVector.w);
                return LocalToWorldSpace.TransformPlane(localPlane);
            }
        }

        public CSGTreeBrush TreeBrush
        {
            get
            {
                if (node == null)
                    return (CSGTreeBrush)CSGTreeNode.Invalid;
                return brush;
            }
        }

        public Matrix4x4 LocalToWorldSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;

                return node.hierarchyItem.LocalToWorldMatrix * node.PivotTransformation;
            }
        }

        public Matrix4x4 WorldToLocalSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;

                return node.InversePivotTransformation * node.hierarchyItem.WorldToLocalMatrix;
            }
        }

        public Matrix4x4 WorldToPlaneSpace
        {
            get
            {
                if (!brush.Valid)
                    return Matrix4x4.identity;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return Matrix4x4.identity;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return Matrix4x4.identity;

                var localPlaneVector = brushMesh.localPlanes[surfaceIndex];
                var localToPlaneSpace   = (Matrix4x4)MathExtensions.GenerateLocalToPlaneSpaceMatrix(localPlaneVector);
                var worldToLocal        = WorldToLocalSpace;
                return localToPlaneSpace * worldToLocal;
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
            // TODO: We're modifying uv coordinates for the generated brush-meshes, 
            //       when we should be changing surfaces descriptions in the generators that generate the brush-meshes ..
            //       Now all UVs are overridden everytime we rebuild the geometry
            UV0 = (UVMatrix)((Matrix4x4)originalMatrix * planeSpaceTransformation);
            //brushContainerAsset.SetDirty();
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
            return	x.brush             == y.brush &&
                    x.descriptionIndex  == y.descriptionIndex &&
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
            return  obj.brush.GetHashCode() ^
                    obj.descriptionIndex ^
                    obj.surfaceIndex;
        }
        #endregion
    }
    
}
