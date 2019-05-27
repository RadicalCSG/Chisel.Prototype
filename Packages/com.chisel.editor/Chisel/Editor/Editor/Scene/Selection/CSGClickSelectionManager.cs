using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public class PlaneIntersection
    {
        public PlaneIntersection(Vector3 point, Plane plane) { this.point = point; this.plane = plane; }
        public PlaneIntersection(Vector3 point, Vector3 normal) { this.point = point; this.plane = new Plane(normal, point); }
        public PlaneIntersection(CSGTreeBrushIntersection brushIntersection, CSGNode node, CSGModel model)
        {
            this.point = brushIntersection.surfaceIntersection.worldIntersection;
            this.plane = brushIntersection.surfaceIntersection.worldPlane;
            this.node = node;
            this.model = model;
        }

        public Vector3      point;
        public Plane        plane;
        public Vector3		normal		{ get { return plane.normal; } }
        public Quaternion	orientation { get { return Quaternion.LookRotation(plane.normal); } }
        public CSGNode      node;
        public CSGModel     model;
    }

    public sealed class GUIClip
    {
        delegate Vector2 UnclipDelegate(Vector2 pos);
        static UnclipDelegate GUIClipUnclipPtr;

        delegate GameObject FindSelectionBaseDelegate(GameObject go);
        static FindSelectionBaseDelegate FindSelectionBasePtr;

        static GUIClip()
        {
            var HandleUtilityType = typeof(HandleUtility);
            var UnityEngineTypes = typeof(UnityEngine.GUIUtility).Assembly.GetTypes();
            var GUIClipType = UnityEngineTypes.FirstOrDefault(t => t.FullName == "UnityEngine.GUIClip");

            if (FindSelectionBasePtr == null)
            {
                var findSelectionBaseMethod = HandleUtilityType.GetMethod("FindSelectionBase", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(GameObject) }, null);
                if (findSelectionBaseMethod != null)
                    FindSelectionBasePtr = (FindSelectionBaseDelegate)Delegate.CreateDelegate(typeof(FindSelectionBaseDelegate), null, findSelectionBaseMethod, true);
            }
            if (GUIClipUnclipPtr == null && GUIClipType != null)
            {
                var unclipMethod = GUIClipType.GetMethod("Unclip", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector2) }, null);
                if (unclipMethod != null)
                    GUIClipUnclipPtr = (UnclipDelegate)Delegate.CreateDelegate(typeof(UnclipDelegate), null, unclipMethod, true);
            }
        }

        public static Vector2 GUIClipUnclip(Vector2 pos)
        {
            return GUIClipUnclipPtr(pos);
        }

        public static GameObject FindSelectionBase(GameObject go)
        {
            return FindSelectionBasePtr(go);
        }
    }

    // TODO: clean up, rename
    public sealed class CSGClickSelectionManager : ScriptableObject // TODO: doesn't need to be a scriptableobject?
    {
        #region Instance
        static CSGClickSelectionManager _instance;
        public static CSGClickSelectionManager Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                var foundInstances = UnityEngine.Object.FindObjectsOfType<CSGClickSelectionManager>();
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    _instance = ScriptableObject.CreateInstance<CSGClickSelectionManager>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                    return _instance;
                }

                _instance = foundInstances[0];
                return _instance;
            }
        }
        #endregion


        public static bool IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit)
        {
            return IntersectRayMeshImpl(ray, mesh, matrix, out hit);
        }


        delegate bool IntersectRayMeshFunc(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
        static IntersectRayMeshFunc IntersectRayMeshImpl;
        delegate GameObject PickClosestGameObjectFunc(Camera camera, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter, out int materialIndex);
        static PickClosestGameObjectFunc pickClosestGO;
        static FieldInfo pickClosestGameObjectDelegate;
        
        static CSGClickSelectionManager()
        {
            var HandleUtilityType	= typeof(HandleUtility);

            if (pickClosestGO == null ||
                pickClosestGameObjectDelegate == null ||
                IntersectRayMeshImpl == null)
            { 
                var IntersectRayMeshMethod = HandleUtilityType.GetMethod("IntersectRayMesh", BindingFlags.NonPublic | BindingFlags.Static);
                if (IntersectRayMeshMethod != null)
                    IntersectRayMeshImpl = (IntersectRayMeshFunc)Delegate.CreateDelegate(typeof(IntersectRayMeshFunc), null, IntersectRayMeshMethod, true);

                var pickClosestGOMethod = HandleUtilityType.GetMethod("Internal_PickClosestGO", BindingFlags.NonPublic | BindingFlags.Static);
                if (pickClosestGOMethod != null)
                    pickClosestGO = (PickClosestGameObjectFunc)Delegate.CreateDelegate(typeof(PickClosestGameObjectFunc), null, pickClosestGOMethod, true);
                
                pickClosestGameObjectDelegate = HandleUtilityType.GetField("pickClosestGameObjectDelegate", BindingFlags.NonPublic | BindingFlags.Static);
                //var delegateType			= pickClosestGameObjectDelegate.FieldType;
                //var pickClosestGameObject	= typeof(CSGSelectionManager).GetMethod("PickClosestGameObject");
                //var methodDelegate		= Delegate.CreateDelegate(delegateType, pickClosestGameObject);
                //pickClosestGameObjectDelegate.SetValue(null, methodDelegate);
            }

            Selection.selectionChanged += ResetHashes;
        }

        public void OnReset()
        {
            UpdateSelection();
        }

        public void OnSelectionChanged()
        {
            UpdateSelection();
        }

        class SelectedNode
        {
            public SelectedNode(CSGNode node, Transform transform) { this.node = node; this.transform = transform; }
            public CSGNode		node;
            public Transform	transform;
        }
        static List<SelectedNode>	selectedNode = new List<SelectedNode>();
        static HashSet<CSGNode>		foundNodes = new HashSet<CSGNode>();

        void UpdateSelection()
        {
            var transforms = Selection.transforms;
            selectedNode.Clear();
            if (transforms.Length > 0)
            {
                foundNodes.Clear();
                for (int i = 0; i < transforms.Length; i++)
                {
                    var transform = transforms[i];
                    if (!transform)
                        continue;
                    var nodes = transform.GetComponentsInChildren<CSGNode>();
                    if (nodes == null || nodes.Length == 0)
                        continue;
                    foreach (var node in nodes)
                        foundNodes.Add(node);
                }
                foreach (var node in foundNodes)
                {
                    var transform = node.transform;
                    selectedNode.Add(new SelectedNode(node, transform));
                }
            }
        }

        static HashSet<CSGNode> modifiedNodes = new HashSet<CSGNode>();
        public void OnSceneGUI(SceneView sceneView)
        {
            if (selectedNode.Count > 0)
            {
                for (int i = 0; i < selectedNode.Count; i++)
                {
                    if (!selectedNode[i].transform)
                    {
                        UpdateSelection();
                        break;
                    }
                }
                modifiedNodes.Clear();
                for (int i = 0; i < selectedNode.Count; i++)
                {
                    var transform	= selectedNode[i].transform;
                    var node		= selectedNode[i].node;
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
                    CSGNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
            }

            // Handle selection clicks / marquee selection
            CSGRectSelectionManager.Update(sceneView);
        }
        

        private static bool s_RetainHashes = false;
        private static int s_PreviousTopmostHash = 0;
        private static int s_PreviousPrefixHash = 0;
        private static IEnumerator<KeyValuePair<GameObject, CSGTreeBrushIntersection>> enumerator;
        private static Vector2 prevMousePosition = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        private static void ResetHashes()
        {
            if (!s_RetainHashes)
            {
                s_PreviousTopmostHash = 0;
                s_PreviousPrefixHash = 0;
            }

            enumerator = null;
            s_RetainHashes = false;
        }


        // TODO: rewrite this, why do we need hashes?
        public static GameObject PickClosestGameObject(Vector2 mousePosition, out CSGTreeBrushIntersection intersection)
        {
            intersection = new CSGTreeBrushIntersection
            {
                surfaceID = -1,
                brushUserID = -1
            };

            s_RetainHashes = true;

            if (enumerator == null ||
                (prevMousePosition - mousePosition).sqrMagnitude > 2)
            {
                enumerator = GetAllOverlapping(mousePosition).GetEnumerator();
                prevMousePosition = mousePosition;
            }
            if (!enumerator.MoveNext())
            {
                enumerator = GetAllOverlapping(mousePosition).GetEnumerator();
                if (!enumerator.MoveNext())
                    return null;
            }

            var topmost			= enumerator.Current;

            var selectionBase	= GUIClip.FindSelectionBase(topmost.Key);
            var first			= (selectionBase == null ? topmost.Key : selectionBase);
            int topmostHash		= topmost.GetHashCode();
            int prefixHash		= topmostHash;

            if (Selection.activeGameObject == null)
            {
                // Nothing selected
                // Return selection base if it exists, otherwise topmost game object
                s_PreviousTopmostHash = topmostHash;
                s_PreviousPrefixHash = prefixHash;
                intersection = topmost.Value;
                return first;
            }

            if (topmostHash != s_PreviousTopmostHash)
            {
                // Topmost game object changed
                // Return selection base if exists and is not already selected, otherwise topmost game object
                s_PreviousTopmostHash = topmostHash;
                s_PreviousPrefixHash = prefixHash;
                intersection = topmost.Value;
                return (Selection.activeGameObject == selectionBase ? topmost.Key : first);
            }

            s_PreviousTopmostHash = topmostHash;

            // Pick potential selection base before topmost game object
            if (Selection.activeGameObject == selectionBase)
            {
                intersection = topmost.Value;
                if (prefixHash != s_PreviousPrefixHash)
                {
                    s_PreviousPrefixHash = prefixHash;
                    return selectionBase;
                }
                return topmost.Key;
            }

            // Check if active game object will appear in selection stack
            GameObject[] ignore = null;
            GameObject[] filter = new GameObject[] { Selection.activeGameObject };
            var picked = PickClosestGameObjectDelegated(mousePosition, ref ignore, ref filter, out intersection);
            if (picked == Selection.activeGameObject)
            {
                // Advance enumerator to active game object
                while (enumerator.Current.Key != Selection.activeGameObject)
                {
                    if (!enumerator.MoveNext())
                    {
                        s_PreviousPrefixHash = topmostHash;
                        intersection = topmost.Value;
                        return first; // Should not occur
                    }

                    UpdateHash(ref prefixHash, enumerator.Current);
                }
            }
            
            if (prefixHash != s_PreviousPrefixHash)
            {
                // Prefix hash changed, start over
                s_PreviousPrefixHash = topmostHash;
                intersection = topmost.Value;
                return first;
            }

            // Move on to next game object
            if (!enumerator.MoveNext())
            {
                s_PreviousPrefixHash = topmostHash;
                intersection = topmost.Value;
                return first; // End reached, start over
            }

            UpdateHash(ref prefixHash, enumerator.Current);

            if (enumerator.Current.Key == selectionBase)
            {
                // Skip selection base
                if (!enumerator.MoveNext())
                {
                    s_PreviousPrefixHash = topmostHash;
                    intersection = topmost.Value;
                    return first; // End reached, start over
                }

                UpdateHash(ref prefixHash, enumerator.Current);
            }

            s_PreviousPrefixHash = prefixHash;
            
            return enumerator.Current.Key;
        }

        public static bool PickFirstGameObject(Vector2 position, out CSGTreeBrushIntersection intersection)
        {
            GameObject[] ignore = null;
            GameObject[] filter = null;
            if (!PickClosestGameObjectDelegated(position, ref ignore, ref filter, out intersection))
                return false;

            return intersection.surfaceID != -1;
        }

        internal static GameObject PickModel(Camera camera, Vector2 pickposition, int layers, ref GameObject[] ignore, ref GameObject[] filter, out CSGModel model, out Material material)
        {
            model = null;
            material = null;
            var flagState = CSGGeneratedComponentManager.BeginPicking();
            GameObject gameObject = null;
            bool foundGameObject = false;
            try
            {
                int materialIndex = -1;
                if (pickClosestGO == null ||
                    pickClosestGameObjectDelegate == null)
                    gameObject = HandleUtility.PickGameObject(pickposition, ignore, out materialIndex);
                else
                    gameObject = pickClosestGO(camera, layers, pickposition, ignore, filter, out materialIndex);

            }
            finally
            {
                foundGameObject = CSGGeneratedComponentManager.EndPicking(flagState, gameObject, out model) && model;
            }
            if (object.Equals(gameObject, null))
                return null;

            if (!foundGameObject)
                return gameObject;
            
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer)
            {
                material = renderer.sharedMaterial;
                if (!material) material = null;
            }
            return gameObject;
        }

        public static PlaneIntersection GetPlaneIntersection(Vector2 mousePosition)
        {
            CSGTreeBrushIntersection brushIntersection;
            var intersectionObject = CSGClickSelectionManager.PickClosestGameObject(mousePosition, out brushIntersection);
            if (intersectionObject &&
                intersectionObject.activeInHierarchy)
            {
                if (brushIntersection.brushUserID != -1)
                {
                    var	brush	= CSGNodeHierarchyManager.FindCSGNodeByInstanceID(brushIntersection.brush.UserID);
                    var model	= CSGNodeHierarchyManager.FindCSGNodeByInstanceID(brushIntersection.tree.UserID) as CSGModel;
                    return new PlaneIntersection(brushIntersection, brush, model);
                }
                
                var meshFilter = intersectionObject.GetComponent<MeshFilter>();
                if (meshFilter)
                {
                    var mesh = meshFilter.sharedMesh;
                    var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);
                    RaycastHit hit;
                    if (CSGClickSelectionManager.IntersectRayMesh(mouseRay, mesh, intersectionObject.transform.localToWorldMatrix, out hit))
                    {
                        var meshRenderer = intersectionObject.GetComponent<MeshRenderer>();
                        if (meshRenderer.enabled)
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
                if (gridPlane.UnsignedRaycast(mouseRay, out dist))
                    return new PlaneIntersection(mouseRay.GetPoint(dist), gridPlane);
            }
            return null;
        }

        public static PlaneIntersection GetPlaneIntersection(Vector2 mousePosition, Rect dragArea)
        {
            if (!dragArea.Contains(mousePosition))
                return null;
            ResetHashes();
            return GetPlaneIntersection(mousePosition);
        }


        public static bool FindBrushMaterials(Vector2 position, out ChiselBrushMaterial[] brushMaterials, out ChiselGeneratedBrushes[] brushMeshAssets, bool selectAllSurfaces)
        {
            brushMaterials = null;
            brushMeshAssets = null;
            try
            {
                CSGTreeBrushIntersection intersection;
                if (!PickFirstGameObject(Event.current.mousePosition, out intersection))
                    return false;

                var brush = intersection.brush;
    
                var node = CSGNodeHierarchyManager.FindCSGNodeByInstanceID(brush.UserID);
                if (!node)
                    return false;

                if (selectAllSurfaces)
                {
                    brushMeshAssets = node.GetUsedBrushMeshAssets();
                    if (brushMeshAssets == null)
                        return false;
                    brushMaterials = node.GetAllBrushMaterials(brush);
                    return true;
                } else
                {
                    var surface = node.FindBrushMaterial(brush, intersection.surfaceID);
                    if (surface == null)
                        return false;
                    brushMeshAssets = node.GetUsedBrushMeshAssets();
                    if (brushMeshAssets == null)
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

        public static SurfaceIntersection FindSurfaceIntersection(Vector2 position)
        {
            try
            {
                CSGTreeBrushIntersection brushIntersection;
                if (!PickFirstGameObject(position, out brushIntersection))
                    return null;

                var brush = brushIntersection.brush;
    
                var node = CSGNodeHierarchyManager.FindCSGNodeByInstanceID(brush.UserID);
                if (!node)
                    return null;
                
                var surface = node.FindSurfaceReference(brush, brushIntersection.surfaceID);
                if (surface == null)
                    return null;
                return new SurfaceIntersection { surface = surface, intersection = brushIntersection.surfaceIntersection };
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }
        
        public static SurfaceReference[] FindSurfaceReference(Vector2 position, bool selectAllSurfaces, out CSGTreeBrushIntersection intersection, out SurfaceReference surfaceReference)
        {
            intersection = CSGTreeBrushIntersection.None;
            surfaceReference = null;
            try
            {
                if (!PickFirstGameObject(position, out intersection))
                    return null;

                var brush = intersection.brush;
    
                var node = CSGNodeHierarchyManager.FindCSGNodeByInstanceID(brush.UserID);
                if (!node)
                    return null;

                surfaceReference = node.FindSurfaceReference(brush, intersection.surfaceID);
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

        internal static GameObject PickNodeOrGameObject(Camera camera, Vector2 pickposition, int layers, ref GameObject[] ignore, ref GameObject[] filter, out CSGModel model, out CSGNode node, out CSGTreeBrushIntersection intersection)
        {
            TryNextSelection:
            intersection = new CSGTreeBrushIntersection { surfaceID = -1, brushUserID = -1 };

            model = null;
            node = null;
            Material sharedMaterial;
            var gameObject = PickModel(camera, pickposition, layers, ref ignore, ref filter, out model, out sharedMaterial);
            if (object.Equals(gameObject, null))
                return null;

            if (model)
            { 
                int filterLayerParameter0 = (sharedMaterial) ? sharedMaterial.GetInstanceID() : 0;
                {
                    var worldRay		= camera.ScreenPointToRay(pickposition);
                    var worldRayStart	= worldRay.origin;
                    var worldRayVector	= (worldRay.direction * (camera.farClipPlane - camera.nearClipPlane));
                    var worldRayEnd		= worldRayStart + worldRayVector;

                    CSGTreeBrushIntersection tempIntersection;
                    if (CSGSceneQuery.FindFirstWorldIntersection(model, worldRayStart, worldRayEnd, filterLayerParameter0, layers, ignore, filter, out tempIntersection))
                    {
                        var clickedBrush		= tempIntersection.brush;
                        node = CSGNodeHierarchyManager.FindCSGNodeByInstanceID(clickedBrush.UserID);
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
                            node = null;
                    }
                }
                
                if (ignore == null)
                    return null;

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

        internal static CSGNode PickNode(Camera camera, Vector2 pickposition, int layers, ref GameObject[] ignore, ref GameObject[] filter, out CSGTreeBrushIntersection intersection)
        {
            TryNextNode:
            CSGModel model;
            CSGNode node;
            var gameObject = PickNodeOrGameObject(camera, pickposition, layers, ref ignore, ref filter, out model, out node, out intersection);
            if (object.Equals(gameObject, null))
                return null;
                
            if (model)
                return node;
            
            ArrayUtility.Add(ref ignore, gameObject);
            goto TryNextNode;
        }
        
        public static CSGNode PickClosestNode(Vector2 position, out CSGTreeBrushIntersection intersection)
        {
            var camera = Camera.current;
            int layers = camera.cullingMask;
            var pickposition = GUIClip.GUIClipUnclip(position);
            pickposition = EditorGUIUtility.PointsToPixels(pickposition);
            pickposition.y = Screen.height - pickposition.y - camera.pixelRect.yMin;
            
            GameObject[] ignore = new GameObject[0];
            GameObject[] filter = null;
            return PickNode(camera, pickposition, layers, ref ignore, ref filter, out intersection);
        }

        internal static GameObject PickClosestGameObjectDelegated(Vector2 position, ref GameObject[] ignore, ref GameObject[] filter, out CSGTreeBrushIntersection intersection)
        {
            var camera = Camera.current;
            int layers = camera.cullingMask;
            var pickposition = GUIClip.GUIClipUnclip(position);
            pickposition = EditorGUIUtility.PointsToPixels(pickposition);
            pickposition.y = Screen.height - pickposition.y - camera.pixelRect.yMin;
            
            /*
            GameObject picked = null;
            if (pickClosestGameObjectDelegate != null)
            {
                // TODO: figure out how to call a delegate through reflection with an out parameter ...
            }*/

            intersection = new CSGTreeBrushIntersection
            {
                surfaceID = -1,
                brushUserID = -1
            };

            //if (picked == null)
            {
                CSGNode node;
                CSGModel model;
                var gameObject = PickNodeOrGameObject(camera, pickposition, layers, ref ignore, ref filter, out model, out node, out intersection);
                if (!model)
                    return gameObject;
                
                if (node)
                    return gameObject;
                
                return null;
            }
            /*
            if (CSGGeneratedComponentManager.IsObjectGenerated(picked))
            {
                if (ignore == null)
                    return null;
                ArrayUtility.Add(ref ignore, picked);
                return PickClosestGameObjectDelegated(position, ref ignore, ref filter, out intersection);
            }
            return picked;*/
        }
        
        public static IEnumerable<KeyValuePair<GameObject, CSGTreeBrushIntersection>> GetAllOverlapping(Vector2 position)
        {
            var allOverlapping = new List<GameObject>();

            while (true)
            {
                GameObject[] ignore = allOverlapping.ToArray();
                GameObject[] filter = null;
                CSGTreeBrushIntersection intersection;
                var go = PickClosestGameObjectDelegated(position, ref ignore, ref filter, out intersection);
                if (go == null)
                    break;

                if (allOverlapping.Count > 0 && allOverlapping.Contains(go))
                    break;

                if (!CSGGeneratedComponentManager.IsObjectGenerated(go))
                    yield return new KeyValuePair<GameObject, CSGTreeBrushIntersection>(go, intersection);

                allOverlapping.Add(go);
            }
        }

        private static void UpdateHash(ref int hash, object obj)
        {
            hash = unchecked(hash * 33 + obj.GetHashCode());
        }
    }
}
