using System;
using System.Collections.Generic;
using System.ComponentModel;
using Chisel.Core;

using Unity.Entities;

using UnityEngine;

namespace Chisel.Components
{
    // Reference to a surface. Used for things like queries and selection.
    [Serializable]
    public sealed class SurfaceReference : IEquatable<SurfaceReference>, IEqualityComparer<SurfaceReference>
    {
        public ChiselGeneratorComponent node;
        public CSGTreeBrush             brush;
        public int                      descriptionIndex;
        public int                      surfaceIndex;

        public SurfaceReference(ChiselNode node, int descriptionIndex, CSGTreeBrush brush, int surfaceIndex)
        {
            this.node                   = node as ChiselGeneratorComponent;
            this.brush                  = brush;
            this.descriptionIndex       = descriptionIndex;
            this.surfaceIndex           = surfaceIndex;
        }


        public ChiselSurface Surface
        {
            get
            {
                if (!node)
                    return null;

                return node.GetSurface(descriptionIndex);
            }
        }


        public Material RenderMaterial
        {
            get
            {
                if (!node)
                    return null;

                var surface = Surface;
				if (surface == null)
					return null;

                return surface.RenderMaterial;
            }
            set
            {
                if (!node)
                    return;

				var surface = Surface;
                if (surface == null)
                    return;

                if (surface.RenderMaterial == value)
                    return;

				surface.RenderMaterial = value;
                node.SetDirty();
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
                node.SetDirty();
            }
        }

        public SurfaceDetails SurfaceDetails
		{
            get
            {
                if (!node)
                    return SurfaceDetails.Default;

                return node.GetSurfaceDetails(descriptionIndex);
            }
            set
            {
                if (!node)
                    return;
                node.SetSurfaceDetails(descriptionIndex, value);
                node.SetDirty();
            }
        }

        public BlobAssetReference<BrushMeshBlob> BrushMeshBlob
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

        // brush becomes temporarily invalid DURING uv operation
        Matrix4x4 lastValidWorldToPlaneSpaceRotation = Matrix4x4.identity;
        public Matrix4x4 WorldToPlaneSpaceRotation
        {
            get
            {
                if (!brush.Valid)
                {
                    return lastValidWorldToPlaneSpaceRotation;
                }

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                {
                    return lastValidWorldToPlaneSpaceRotation;
                }

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                {
                    return lastValidWorldToPlaneSpaceRotation;
                }

                var localPlaneVector    = brushMesh.localPlanes[surfaceIndex];
                var localToPlaneSpace   = MathExtensions.GenerateLocalToPlaneSpaceMatrix(localPlaneVector);
                var worldToLocal        = WorldToLocalSpace;
                lastValidWorldToPlaneSpaceRotation = (Matrix4x4)localToPlaneSpace * worldToLocal;
                return lastValidWorldToPlaneSpaceRotation;
            }	
        }

        public Matrix4x4 WorldSpaceToPlaneSpace(Matrix4x4 worldSpaceTransformation)
        {
            var worldToPlaneSpace = WorldToPlaneSpaceRotation;
            var planeToWorldSpace = Matrix4x4.Inverse(worldToPlaneSpace);            
            var planeSpaceTransformation = worldToPlaneSpace * worldSpaceTransformation * planeToWorldSpace;
            return planeSpaceTransformation;
        }

        public void WorldSpaceTransformUV(Matrix4x4 worldSpaceTransformation, UVMatrix originalMatrix)
        {
            var planeSpaceTransformation = WorldSpaceToPlaneSpace(worldSpaceTransformation);
            PlaneSpaceTransformUV(planeSpaceTransformation, originalMatrix);
        }

        public void PlaneSpaceTransformUV(Matrix4x4 planeSpaceTransformation, UVMatrix originalMatrix)
        {
            // TODO: We're modifying uv coordinates for the generated brush-meshes, 
            //       when we should be changing surfaces descriptions in the generators that generate the brush-meshes ..
            //       Now all UVs are overridden everytime we rebuild the geometry

            Matrix4x4 rotation = originalMatrix;
            Matrix4x4 input = rotation * planeSpaceTransformation;
            UV0 = input;
            //brushContainerAsset.SetDirty();
        }


        #region Equals
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(SurfaceReference left, SurfaceReference right) { return left.Equals(right); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(SurfaceReference left, SurfaceReference right) { return !left.Equals(right); }

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
