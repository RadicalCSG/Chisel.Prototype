using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public class PlaneIntersection
    {
        public PlaneIntersection(Vector3 point, Plane plane) { this.point = point; this.plane = plane; }
        public PlaneIntersection(Vector3 point, Vector3 normal) { this.point = point; this.plane = new Plane(normal, point); }
        public PlaneIntersection(ChiselIntersection chiselIntersection)
        {
            this.point  = chiselIntersection.worldPlaneIntersection;
            this.plane  = chiselIntersection.worldPlane;
            this.node   = chiselIntersection.node;
            this.model  = chiselIntersection.model;
        }

        public Vector3      point;
        public Plane        plane;
        public Vector3		normal		{ get { return plane.normal; } }
        public Quaternion	orientation { get { return Quaternion.LookRotation(plane.normal); } }
        public ChiselNode   node;
        public ChiselModel  model;
    }

    public sealed class GUIClip
    {
        public delegate Vector2 UnclipDelegate(Vector2 pos);
        public static readonly UnclipDelegate GUIClipUnclip = ReflectionExtensions.CreateDelegate<UnclipDelegate>("UnityEngine.GUIClip", "Unclip");

        public delegate GameObject FindSelectionBaseDelegate(GameObject go);
        public static readonly FindSelectionBaseDelegate FindSelectionBase = typeof(HandleUtility).CreateDelegate<FindSelectionBaseDelegate>("FindSelectionBase");
    }

    // TODO: clean up, rename
    public sealed class ChiselClickSelectionManager : ScriptableObject // TODO: doesn't need to be a scriptableobject?
    {
        #region Instance
        static ChiselClickSelectionManager _instance;
        public static ChiselClickSelectionManager Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                var foundInstances = UnityEngine.Object.FindObjectsOfType<ChiselClickSelectionManager>();
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<ChiselClickSelectionManager>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    return _instance;
                }

                _instance = foundInstances[0];
                return _instance;
            }
        }
        #endregion



        static bool IsValidNodeToBeSelected(GameObject gameObject)
        {
            if (!gameObject || !gameObject.activeInHierarchy)
                return false;

            if (gameObject.TryGetComponent<ChiselModel>(out var model) ||
                // TODO: use a component on the generated MeshRenderer/Container instead
                gameObject.name.StartsWith("‹[generated"))
                return false;

            var sceneVisibilityManager = UnityEditor.SceneVisibilityManager.instance;
            if (sceneVisibilityManager.IsHidden(gameObject) ||
                sceneVisibilityManager.IsPickingDisabled(gameObject))
                return false;
            return true;
        }


        public delegate bool IntersectRayMeshFunc(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
        public static IntersectRayMeshFunc IntersectRayMesh = typeof(HandleUtility).CreateDelegate<IntersectRayMeshFunc>("IntersectRayMesh");
        public delegate GameObject PickClosestGameObjectFunc(Camera camera, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter, out int materialIndex);
        public static PickClosestGameObjectFunc PickClosestGO = typeof(HandleUtility).CreateDelegate<PickClosestGameObjectFunc>("Internal_PickClosestGO");

        public void OnReset()
        {
            UpdateSelection();
        }

        public static bool ignoreSelectionChanged = false;

        public void OnSelectionChanged()
        {
            if (!ignoreSelectionChanged)
                ResetDeepClick();
            ignoreSelectionChanged = false;
            UpdateSelection();
        }

        class SelectedNode
        {
            public SelectedNode(ChiselNode node, Transform transform) { this.node = node; this.transform = transform; }
            public ChiselNode		node;
            public Transform	transform;
        }

        static List<SelectedNode>	selectedNodeList        = new List<SelectedNode>();
        static HashSet<ChiselNode>	selectedNodeHash        = new HashSet<ChiselNode>();

        void UpdateSelection()
        {
            var transforms = Selection.transforms;
            selectedNodeList.Clear();
            if (transforms.Length > 0)
            {
                selectedNodeHash.Clear();
                for (int i = 0; i < transforms.Length; i++)
                {
                    var transform = transforms[i];
                    if (!transform)
                        continue;
                    var nodes = transform.GetComponentsInChildren<ChiselNode>();
                    if (nodes == null || nodes.Length == 0)
                        continue;
                    foreach (var node in nodes)
                        selectedNodeHash.Add(node);
                }
                foreach (var node in selectedNodeHash)
                {
                    var transform = node.transform;
                    selectedNodeList.Add(new SelectedNode(node, transform));
                }
            }
        }

        static HashSet<ChiselNode> modifiedNodes = new HashSet<ChiselNode>();
        public void OnSceneGUI(SceneView sceneView)
        {
            if (selectedNodeList.Count > 0)
            {
                for (int i = 0; i < selectedNodeList.Count; i++)
                {
                    if (!selectedNodeList[i].transform)
                    {
                        UpdateSelection();
                        break;
                    }
                }
                modifiedNodes.Clear();
                for (int i = 0; i < selectedNodeList.Count; i++)
                {
                    var transform	= selectedNodeList[i].transform;
                    var node		= selectedNodeList[i].node;
                    var curLocalToWorldMatrix = transform.localToWorldMatrix;
                    var oldLocalToWorldMatrix = node.hierarchyItem.LocalToWorldMatrix;
                    if (curLocalToWorldMatrix.m00 != oldLocalToWorldMatrix.m00 ||
                        curLocalToWorldMatrix.m01 != oldLocalToWorldMatrix.m01 ||
                        curLocalToWorldMatrix.m02 != oldLocalToWorldMatrix.m02 ||
                        curLocalToWorldMatrix.m03 != oldLocalToWorldMatrix.m03 ||

                        curLocalToWorldMatrix.m10 != oldLocalToWorldMatrix.m10 ||
                        curLocalToWorldMatrix.m11 != oldLocalToWorldMatrix.m11 ||
                        curLocalToWorldMatrix.m12 != oldLocalToWorldMatrix.m12 ||
                        curLocalToWorldMatrix.m13 != oldLocalToWorldMatrix.m13 ||

                        curLocalToWorldMatrix.m20 != oldLocalToWorldMatrix.m20 ||
                        curLocalToWorldMatrix.m21 != oldLocalToWorldMatrix.m21 ||
                        curLocalToWorldMatrix.m22 != oldLocalToWorldMatrix.m22 ||
                        curLocalToWorldMatrix.m23 != oldLocalToWorldMatrix.m23 //||

                        //curLocalToWorldMatrix.m30 != oldLocalToWorldMatrix.m30 ||
                        //curLocalToWorldMatrix.m31 != oldLocalToWorldMatrix.m31 ||
                        //curLocalToWorldMatrix.m32 != oldLocalToWorldMatrix.m32 ||
                        //curLocalToWorldMatrix.m33 != oldLocalToWorldMatrix.m33
                        )
                    {
                        node.hierarchyItem.LocalToWorldMatrix = curLocalToWorldMatrix;
                        node.hierarchyItem.WorldToLocalMatrix = transform.worldToLocalMatrix;
                        modifiedNodes.Add(node);
                    }
                }
                if (modifiedNodes.Count > 0)
                    ChiselNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
            }

            // Handle selection clicks / marquee selection
            ChiselRectSelectionManager.Update(sceneView);
        }
        

        #region DeepSelection (private)
        private static List<GameObject>     deepClickIgnoreGameObjectList   = new List<GameObject>();
        private static Vector2  _prevSceenPos = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        private static Camera   _prevCamera;

        private static void ResetDeepClick(bool resetPosition = true)
        {
            deepClickIgnoreGameObjectList.Clear();
            if (resetPosition)
            {
                _prevSceenPos = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                _prevCamera = null;
            }
        }
        #endregion
        

        public static GameObject PickClosestGameObject(Vector2 screenPos, out ChiselIntersection intersection)
        {
            intersection = ChiselIntersection.None;
            var camera = Camera.current;
            if (!camera)
                return null;

            // If we moved our mouse, reset our ignore list
            if (_prevSceenPos != screenPos ||
                _prevCamera != camera)
                ResetDeepClick();

            _prevSceenPos = screenPos;
            _prevCamera = camera;

            // Get the first click that is not in our ignore list
            GameObject[] ignore = deepClickIgnoreGameObjectList.ToArray();
            GameObject[] filter = null;
            var foundObject = PickClosestGameObjectDelegated(screenPos, ref ignore, ref filter, out intersection);
            
            // If we haven't found anything, try getting the first item in our list that's either a brush or a regular gameobject (loop around)
            if (object.Equals(foundObject, null))
            {
                bool found = false;
                for (int i = 0; i < deepClickIgnoreGameObjectList.Count; i++)
                {
                    foundObject = deepClickIgnoreGameObjectList[i];

                    // We don't want models or mesh containers since they're in this list to skip, and should never be selected
                    if (!IsValidNodeToBeSelected(foundObject))
                        continue;

                    found = true;
                    break;
                }

                if (!found)
                {
                    // We really didn't find anything
                    intersection = ChiselIntersection.None;
                    ResetDeepClick();
                    return null;
                } else
                {
                    // Reset our list so we only skip our current selection on the next click
                    ResetDeepClick(
                        resetPosition: false // But make sure we remember our current mouse position
                        );
                }
            }

            // Remember our gameobject so we don't select it on the next click
            deepClickIgnoreGameObjectList.Add(foundObject);
            return foundObject;
        }


        static bool PickFirstGameObject(Vector2 position, out ChiselIntersection intersection)
        {
            GameObject[] ignore = null;
            GameObject[] filter = null;
            if (!PickClosestGameObjectDelegated(position, ref ignore, ref filter, out intersection))
                return false;

            return intersection.brushIntersection.surfaceID != -1;
        }

        static List<Material> sSharedMaterials = new List<Material>();
        static GameObject PickModelOrGameObject(Camera camera, Vector2 pickposition, int layers, ref GameObject[] ignore, ref GameObject[] filter, out ChiselModel model, out Material material)
        {
            model = null;
            material = null;
            var flagState = ChiselGeneratedComponentManager.BeginPicking();
            GameObject gameObject = null;
            bool foundGameObject = false;
            int materialIndex = -1;
            try
            { 
                if (PickClosestGO == null)
                    gameObject = HandleUtility.PickGameObject(pickposition, ignore, out materialIndex);
                else
                    gameObject = PickClosestGO(camera, layers, pickposition, ignore, filter, out materialIndex);
            }
            finally
            {
                foundGameObject = ChiselGeneratedComponentManager.EndPicking(flagState, gameObject, out model) && model;
            }
            if (object.Equals(gameObject, null))
                return null;

            if (!foundGameObject)
                return gameObject;
            
            if (materialIndex >= 0 &&
                gameObject.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.GetSharedMaterials(sSharedMaterials);
                material = materialIndex < sSharedMaterials.Count ? sSharedMaterials[materialIndex] : null;
                sSharedMaterials.Clear(); // We don't want to keep references to Materials alive
                if (!material) material = null;
            }
            return gameObject;
        }

        static PlaneIntersection GetPlaneIntersection(Vector2 mousePosition)
        {
            ChiselIntersection brushIntersection;
            var intersectionObject = ChiselClickSelectionManager.PickClosestGameObject(mousePosition, out brushIntersection);
            if (intersectionObject &&
                intersectionObject.activeInHierarchy)
            {
                if (brushIntersection.node != null)
                    return new PlaneIntersection(brushIntersection);
                
                if (intersectionObject.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    var mesh = meshFilter.sharedMesh;
                    var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);
                    RaycastHit hit;
                    if (ChiselClickSelectionManager.IntersectRayMesh(mouseRay, mesh, intersectionObject.transform.localToWorldMatrix, out hit))
                    {
                        if (intersectionObject.TryGetComponent<MeshRenderer>(out var meshRenderer) && 
                            meshRenderer.enabled)
                        {
                            return new PlaneIntersection(hit.point, hit.normal);
                        }
                    }
                }
            } else
            {
                var gridPlane = UnitySceneExtensions.Grid.ActiveGrid.PlaneXZ;
                var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);
                var dist = 0.0f;
                if (gridPlane.SignedRaycast(mouseRay, out dist))
                    return new PlaneIntersection(mouseRay.GetPoint(dist), gridPlane);
            }
            return null;
        }

        public static PlaneIntersection GetPlaneIntersection(Vector2 mousePosition, Rect dragArea)
        {
            if (!dragArea.Contains(mousePosition))
                return null;
            ResetDeepClick();
            return GetPlaneIntersection(mousePosition);
        }


        public static bool FindBrushMaterials(Vector2 position, out ChiselBrushMaterial[] brushMaterials, out ChiselBrushContainerAsset[] brushContainerAssets, bool selectAllSurfaces)
        {
            brushMaterials = null;
            brushContainerAssets = null;
            try
            {
                ChiselIntersection intersection;
                if (!PickFirstGameObject(position, out intersection))
                    return false;

                var node = intersection.node;
                if (!node)
                    return false;

                var brush = intersection.brushIntersection.brush;

                if (selectAllSurfaces)
                {
                    brushContainerAssets = node.GetUsedGeneratedBrushes();
                    if (brushContainerAssets == null)
                        return false;
                    brushMaterials = node.GetAllBrushMaterials(brush);
                    return true;
                } else
                {
                    var surface = node.FindBrushMaterial(brush, intersection.brushIntersection.surfaceID);
                    if (surface == null)
                        return false;
                    brushContainerAssets = node.GetUsedGeneratedBrushes();
                    if (brushContainerAssets == null)
                        return false;
                    brushMaterials =  new ChiselBrushMaterial[] { surface };
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        public static SurfaceReference[] FindSurfaceReferences(Vector2 position, bool selectAllSurfaces, out ChiselIntersection intersection, out SurfaceReference surfaceReference)
        {
            intersection = ChiselIntersection.None;
            surfaceReference = null;
            try
            {
                if (!PickFirstGameObject(position, out intersection))
                    return null;
    
                var node = intersection.node;
                if (!node)
                    return null;

                var brush = intersection.brushIntersection.brush;

                surfaceReference = node.FindSurfaceReference(brush, intersection.brushIntersection.surfaceID);
                if (selectAllSurfaces)
                    return node.GetAllSurfaceReferences(brush);

                if (surfaceReference == null)
                    return null;
                return new SurfaceReference[] { surfaceReference };
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }
        
        static GameObject PickNodeOrGameObject(Camera camera, Vector2 pickposition, int layers, ref GameObject[] ignore, ref GameObject[] filter, out ChiselModel model, out ChiselNode node, out ChiselIntersection intersection)
        {
            TryNextSelection:
            intersection = ChiselIntersection.None;

            node = null;
            Material sharedMaterial;
            var gameObject = PickModelOrGameObject(camera, pickposition, layers, ref ignore, ref filter, out model, out sharedMaterial);
            if (object.Equals(gameObject, null))
                return null;

            if (ChiselGeneratedComponentManager.IsValidModelToBeSelected(model))
            { 
                int filterLayerParameter0 = (sharedMaterial) ? sharedMaterial.GetInstanceID() : 0;
                {
                    var worldRay		= camera.ScreenPointToRay(pickposition);
                    var worldRayStart	= worldRay.origin;
                    var worldRayVector	= (worldRay.direction * (camera.farClipPlane - camera.nearClipPlane));
                    var worldRayEnd		= worldRayStart + worldRayVector;

                    if (ChiselSceneQuery.FindFirstWorldIntersection(model, worldRayStart, worldRayEnd, filterLayerParameter0, layers, ignore, filter, out var tempIntersection))
                    {
                        node = tempIntersection.node;
                        if (node)
                        {
                            if (ignore != null &&
                                ignore.Contains(node.gameObject))
                            {
                                node = null;
                                return null;
                            }
                            intersection = tempIntersection;
                            return node.gameObject;
                        } else
                        {
                            node = null;
                        }
                    }
                }

                if (ignore == null)
                {
                    return null;
                }

                ArrayUtility.Add(ref ignore, gameObject);
                goto TryNextSelection;
            }

            if (object.Equals(gameObject, null))
                return null;

            if (ignore != null &&
                ignore.Contains(gameObject))
                return null;

            return gameObject;
        }
        
        static GameObject PickClosestGameObjectDelegated(Vector2 position, ref GameObject[] ignore, ref GameObject[] filter, out ChiselIntersection intersection)
        {
            var camera = Camera.current;
            int layers = camera.cullingMask;
            var pickposition = GUIClip.GUIClipUnclip(position);
            pickposition = EditorGUIUtility.PointsToPixels(pickposition);
            pickposition.y = Screen.height - pickposition.y - camera.pixelRect.yMin;

            var gameObject = PickNodeOrGameObject(camera, pickposition, layers, ref ignore, ref filter, out var model, out var node, out intersection);
            if (!model)
                return gameObject;
                
            if (node)
                return gameObject;

            return null;
        }
    }
}
