using UnityEngine;
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
        public ChiselBrushGeneratorComponent    node;
        public CSGTreeBrush                     brush;
        //public ChiselBrushContainerAsset      brushContainerAsset;

        //public int    subNodeIndex;
        //public int    subMeshIndex;
        public int      descriptionIndex;
        //public int      surfaceID;
        public int      surfaceIndex;

        public SurfaceReference(ChiselNode node, //ChiselBrushContainerAsset brushContainerAsset, 
                                                int descriptionIndex,
                                                CSGTreeBrush brush, 
                                                //int subNodeIndex, int subMeshIndex, 
                                                int surfaceIndex//, int surfaceID
            )
        {
            this.node                   = node as ChiselBrushGeneratorComponent;
            this.brush                  = brush;
            //this.brushContainerAsset  = brushContainerAsset;
            //this.subNodeIndex         = subNodeIndex;
            //this.subMeshIndex         = subMeshIndex;
            this.descriptionIndex       = descriptionIndex;
            this.surfaceIndex           = surfaceIndex;
            //this.surfaceID              = surfaceID;
        }

        public void SetDirty()
        {
            //brushContainerAsset.SetDirty();
        }
        /*
        public ChiselSurface BrushSurface
        {
            get
            {
                return null;/*
                if (!brush.Valid)
                    return null;
                if (!brushContainerAsset)
                    return null;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return null;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return null;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return null;
                return brushMesh.polygons[surfaceIndex].surface;*
            }
        }*/
        
        public ChiselBrushMaterial BrushMaterial
        {
            get
            {
                if (!node)
                    return null;

                return node.GetBrushMaterial(descriptionIndex);/*

                if (!brush.Valid)
                    return null;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return null;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return null;
                var surface = brushMesh.polygons[surfaceIndex].surface;
                var brushMaterial = new ChiselBrushMaterial
                {
                    LayerUsage      = surface.layerDefinition.layerUsage,
                    RenderMaterial  = surface.layerDefinition.layerParameter1 == 0 ? default : ChiselMaterialManager.Instance.GetMaterial(surface.layerDefinition.layerParameter1),
                    PhysicsMaterial = surface.layerDefinition.layerParameter2 == 0 ? default : ChiselMaterialManager.Instance.GetPhysicMaterial(surface.layerDefinition.layerParameter2)
                };
                return brushMaterial;/*

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
                return surface.brushMaterial;*/
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

                return node.GetSurfaceUV0(descriptionIndex);/*
                if (!brush.Valid)
                    return UVMatrix.identity;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return UVMatrix.identity;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return UVMatrix.identity;

                return brushMesh.polygons[surfaceIndex].surface.UV0;
                /*

                if (!brushContainerAsset)
                    return ref s_DefaultPolygon;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return ref s_DefaultPolygon;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return ref s_DefaultPolygon;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return ref s_DefaultPolygon;
                return ref brushMesh.polygons[surfaceIndex];*/
            }
            set
            {
                if (!node)
                    return;
                node.SetSurfaceUV0(descriptionIndex, value);/*
                if (!brush.Valid)
                    return;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return;

                var originalUV0 = brushMesh.polygons[surfaceIndex].surface.UV0;
                if (originalUV0 == value)
                    return;

                var copy = BrushMeshManager.Copy(brushMeshBlob, Allocator.Persistent);
                copy.Value.polygons[surfaceIndex].surface.UV0 = value;
                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(copy) };*/
            }
        }
        public SurfaceDescription SurfaceDescription
        {
            get
            {
                if (!node)
                    return SurfaceDescription.Default;

                return node.GetSurfaceDescription(descriptionIndex);/*
                if (!brush.Valid)
                    return SurfaceDescription.Default;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return SurfaceDescription.Default;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return SurfaceDescription.Default;

                ref var surface = ref brushMesh.polygons[surfaceIndex].surface;
                return new SurfaceDescription
                { 
                    surfaceFlags    = surface.surfaceFlags,
                    smoothingGroup  = surface.smoothingGroup,
                    UV0             = surface.UV0
                };
                return surface;
                /*

                if (!brushContainerAsset)
                    return ref s_DefaultPolygon;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return ref s_DefaultPolygon;
                var brushMesh = brushContainerAsset.BrushMeshes[subMeshIndex];
                if (brushMesh == null)
                    return ref s_DefaultPolygon;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return ref s_DefaultPolygon;
                return ref brushMesh.polygons[surfaceIndex];*/
            }
            set
            {
                if (!node)
                    return;
                node.SetSurfaceDescription(descriptionIndex, value);/*
                if (!brush.Valid)
                    return;

                var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
                if (!brushMeshBlob.IsCreated)
                    return;

                ref var brushMesh = ref brushMeshBlob.Value;
                if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                    return;

                var originalUV0 = brushMesh.polygons[surfaceIndex].surface.UV0;
                if (originalUV0 == value)
                    return;

                var copy = BrushMeshManager.Copy(brushMeshBlob, Allocator.Persistent);
                copy.Value.polygons[surfaceIndex].surface.UV0 = value;
                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(copy) };*/
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

                return brushMeshBlob;/*

                if (!brushContainerAsset)
                    return null;
                if (subMeshIndex < 0 || subMeshIndex >= brushContainerAsset.SubMeshCount)
                    return null;
                var brushMeshes = brushContainerAsset.BrushMeshes;
                if (brushMeshes == null)
                    return null;
                return brushMeshes[subMeshIndex];*/
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
                /*

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
                    yield return vertices[edges[e].vertexIndex];*/
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
                //localPlane.Translate(node.PivotOffset);
                return LocalToWorldSpace.TransformPlane(localPlane);
                /*


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
                //localPlane.Translate(node.PivotOffset);
                return LocalToWorldSpace.TransformPlane(localPlane);*/
            }
        }

        public CSGTreeBrush TreeBrush
        {
            get
            {
                if (node == null)
                    return (CSGTreeBrush)CSGTreeNode.InvalidNode;
                return brush;
                //return (CSGTreeBrush)node.GetTreeNodeByIndex(subNodeIndex);
            }
        }

        public Matrix4x4 LocalToWorldSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;

                {
                    var generator = node as ChiselBrushGeneratorComponent;
                    if (generator != null)
                        return node.hierarchyItem.LocalToWorldMatrix * generator.PivotTransformation;
                }
                /*
                {
                    var generator = node as ChiselGeneratorComponent;
                    if (generator != null)
                        return node.hierarchyItem.LocalToWorldMatrix * generator.PivotTransformation;
                }
                */
                return node.hierarchyItem.LocalToWorldMatrix;
            }
        }

        public Matrix4x4 WorldToLocalSpace
        {
            get
            {
                if (node == null)
                    return Matrix4x4.identity;

                {
                    var generator = node as ChiselBrushGeneratorComponent;
                    if (generator != null)
                        return generator.InversePivotTransformation * node.hierarchyItem.WorldToLocalMatrix;
                }
                /*
                {
                    var generator = node as ChiselGeneratorComponent;
                    if (generator != null)
                        return generator.InversePivotTransformation * node.hierarchyItem.WorldToLocalMatrix;
                }
                */
                return node.hierarchyItem.WorldToLocalMatrix;
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
                /*
                
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
                */
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
                    //x.treeBrush			== y.treeBrush &&
                    //x.brushContainerAsset	== y.brushContainerAsset &&
                    //x.subNodeIndex		== y.subNodeIndex &&
                    //x.subMeshIndex		== y.subMeshIndex &&
                    //x.surfaceID			== y.surfaceID &&
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
            return  //obj.treeBrush.NodeID.GetHashCode() ^
                    //((obj.brushContainerAsset == null) ? 0 : obj.brushContainerAsset.GetInstanceID()) ^
                    obj.brush.NodeID.GetHashCode() ^
                    //obj.subNodeIndex ^
                    //obj.subMeshIndex ^
                    //obj.surfaceID ^
                    obj.descriptionIndex ^
                    obj.surfaceIndex;
        }
        #endregion
    }
    
}
