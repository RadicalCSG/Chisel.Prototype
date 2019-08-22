using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    public sealed partial class SceneHandles
    {
        internal static int s_BoundsHash = "BoundsHash".GetHashCode();
        
        static readonly Vector3[]   s_BoundsVertices	= new Vector3[8];

        // slideDir1, slideDir2, facing direction
        static readonly int[,]		s_EdgeDirectionOffsets = new int[,]
        {
            {1,2,0}, {3,2,1}, {4,2,0}, {0,2,1},

            {1,5,0}, {3,5,1}, {4,5,0}, {0,5,1},
        
            {0,1,2}, {3,1,2}, {3,4,2}, {0,4,2}
        };
        
        
        static readonly Axes[]		s_EdgeAxes = new Axes[]
        {
            Axes.YZ, Axes.XZ, Axes.YZ, Axes.XZ,

            Axes.YZ, Axes.XZ, Axes.YZ, Axes.XZ,

            Axes.XY, Axes.XY, Axes.XY, Axes.XY
        };
        
        static readonly Axes[]		s_BoundsAxes = new Axes[]
        {
            Axes.X, Axes.Y, Axes.Z, Axes.X, Axes.Y, Axes.Z
        };

        // from-to vertex indices
        static readonly int[,]		s_BoundsEdgeIndices	= new int[,]
        {
            {0, 1}, {1, 2}, {2, 3}, {3, 0}, 
                
            {4, 5}, {5, 6}, {6, 7}, {7, 4}, 

            {0, 4}, {1, 5}, {2, 6}, {3, 7}  
        };
        
        // min - max values of bounds, put in an array to be indexed by number
        static readonly float[]     s_BoundsValues			= new float[6];

        // slide directions, put in array to be indexed by number
        static readonly Vector3[]   s_BoundsSlideDirs		= new Vector3[3];

        static readonly Vector3[]   s_BoundsSidePoint		= new Vector3[6];
        static readonly bool[]		s_BoundsBackfaced		= new bool[6];
        static readonly bool[]		s_BoundsAxisDisabled	= new bool[6];
        static readonly bool[]      s_BoundsAxisHot			= new bool[6];
        static readonly int[]		s_BoundsControlIds		= new int[6];

        public const float kPointScale			= 0.05f;
        
        const float kShowPointThreshold = 0.00001f;

        public static Bounds BoundsHandle(Bounds bounds, Quaternion rotation, Vector3? snappingSteps = null)
        {
            return BoundsHandle(bounds, rotation, UnitySceneExtensions.SceneHandles.NormalHandleCap, UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap, snappingSteps);
        }
        
        public static Bounds BoundsHandle(Bounds bounds, Quaternion rotation, CapFunction sideCapFunction, CapFunction pointCapFunction, Vector3? snappingSteps = null)
        {
            var hotControl = GUIUtility.hotControl;
            bool isControlHot = false;
            for (int i = 0; i < s_BoundsControlIds.Length; i++)
            {
                s_BoundsControlIds[i] = GUIUtility.GetControlID(s_BoundsHash, FocusType.Keyboard);
                s_BoundsAxisHot[i] = s_BoundsControlIds[i] == hotControl;
                isControlHot = isControlHot || s_BoundsAxisHot[i];
            }

            s_BoundsSlideDirs[0] = rotation * Vector3.right;
            s_BoundsSlideDirs[1] = rotation * Vector3.up;
            s_BoundsSlideDirs[2] = rotation * Vector3.forward;
            
            var min		= bounds.min;
            var max		= bounds.max;
            var center	= bounds.center; 

            s_BoundsValues[0] = min.x;
            s_BoundsValues[1] = min.y;
            s_BoundsValues[2] = min.z;

            s_BoundsValues[3] = max.x;
            s_BoundsValues[4] = max.y;
            s_BoundsValues[5] = max.z;


            s_BoundsVertices[0] = rotation * new Vector3(s_BoundsValues[0], s_BoundsValues[1], s_BoundsValues[2]);
            s_BoundsVertices[1] = rotation * new Vector3(s_BoundsValues[3], s_BoundsValues[1], s_BoundsValues[2]);
            s_BoundsVertices[2] = rotation * new Vector3(s_BoundsValues[3], s_BoundsValues[4], s_BoundsValues[2]);
            s_BoundsVertices[3] = rotation * new Vector3(s_BoundsValues[0], s_BoundsValues[4], s_BoundsValues[2]);
            
            s_BoundsVertices[4] = rotation * new Vector3(s_BoundsValues[0], s_BoundsValues[1], s_BoundsValues[5]);
            s_BoundsVertices[5] = rotation * new Vector3(s_BoundsValues[3], s_BoundsValues[1], s_BoundsValues[5]);
            s_BoundsVertices[6] = rotation * new Vector3(s_BoundsValues[3], s_BoundsValues[4], s_BoundsValues[5]);
            s_BoundsVertices[7] = rotation * new Vector3(s_BoundsValues[0], s_BoundsValues[4], s_BoundsValues[5]);

            
            s_BoundsSidePoint[0] = rotation * new Vector3(s_BoundsValues[0], center.y, center.z);
            s_BoundsSidePoint[1] = rotation * new Vector3(center.x, s_BoundsValues[1], center.z);
            s_BoundsSidePoint[2] = rotation * new Vector3(center.x, center.y, s_BoundsValues[2]);
            s_BoundsSidePoint[3] = rotation * new Vector3(s_BoundsValues[3], center.y, center.z);
            s_BoundsSidePoint[4] = rotation * new Vector3(center.x, s_BoundsValues[4], center.z);
            s_BoundsSidePoint[5] = rotation * new Vector3(center.x, center.y, s_BoundsValues[5]);

            // TODO: add handles in the corners of each quad on the bounds, with an offset from the vertex, to drag from there

            
            using (new SceneHandles.DrawingScope())
            {
                var prevDisabled	= SceneHandles.disabled;

                var isStatic = (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
                

                for (int i = 0; i < s_BoundsAxisDisabled.Length; i++)
                {
                    s_BoundsAxisDisabled[i] = isStatic || prevDisabled || Snapping.AxisLocking[i % 3] || (isControlHot && !s_BoundsAxisHot[i]);
                }

                var camera					= Camera.current;
                var cameraLocalPos			= SceneHandles.inverseMatrix.MultiplyPoint(camera.transform.position);
                var cameraLocalForward		= SceneHandles.inverseMatrix.MultiplyVector(camera.transform.forward);
                var isCameraInsideBox		= bounds.Contains(cameraLocalPos);
                var isCameraOrthographic	= camera.orthographic;
            

                var boundsColor			= SceneHandles.yAxisColor;
                var backfacedColor		= new Color(boundsColor.r, boundsColor.g, boundsColor.b, boundsColor.a * SceneHandles.backfaceAlphaMultiplier);
            

                var prevGUIchanged = GUI.changed;
            
                bool haveChanged = false;

                var selectedAxes = Axes.None;

                // all sides of bounds
                int currentFocusControl = SceneHandleUtility.focusControl;
                for (int i = 0; i < s_BoundsValues.Length; i++)
                {
                    var id = s_BoundsControlIds[i];
                

                    GUI.changed = false;
                    var localPoint	= s_BoundsSidePoint[i];
                    var handleSize	= UnityEditor.HandleUtility.GetHandleSize(localPoint);
                    var pointSize	= handleSize * kPointScale;
                    var direction	= s_BoundsSlideDirs[i % 3];
                    var normal		= (i < 3) ? -direction : direction;
                    normal.x *= (bounds.size.x < 0) ? -1 : 1;
                    normal.y *= (bounds.size.y < 0) ? -1 : 1;
                    normal.z *= (bounds.size.z < 0) ? -1 : 1;

                    if (Event.current.type == EventType.Repaint)
                    {
                        s_BoundsBackfaced[i] = false;
                        if (!isCameraInsideBox)
                        {
                            var cosV = isCameraOrthographic ? Vector3.Dot(normal, -cameraLocalForward) :
                                                              Vector3.Dot(normal, (cameraLocalPos - localPoint));
                            if (cosV < -0.0001f)
                                // TODO: do not set backfaced to true when side is infinitely thin
                                s_BoundsBackfaced[i] = !(isControlHot && !s_BoundsAxisHot[i % 3]);
                        }

                        var sideColor = (s_BoundsBackfaced[i] ? backfacedColor: boundsColor);
                        SceneHandles.color = SceneHandles.StateColor(sideColor, s_BoundsAxisDisabled[i], (currentFocusControl == id));
                    
                        if (currentFocusControl == id) 
                        {
                            var sceneView = SceneView.currentDrawingSceneView;
                            if (sceneView) 
                            {
                                var rect = sceneView.position;
                                rect.min = Vector2.zero;
                                EditorGUIUtility.AddCursorRect(rect, SceneHandleUtility.GetCursorForDirection(localPoint, normal));
                            }
                            selectedAxes = s_BoundsAxes[i];
                        }
                    
                        if (s_BoundsBackfaced[i]) pointSize *= backfaceSizeMultiplier;
                    }
                
                    var steps		= snappingSteps ?? Snapping.MoveSnappingSteps;
                    var newPoint	= Slider1DHandle(id, (Axis)(i % 3), localPoint, normal, steps[i % 3], pointSize, sideCapFunction);
                    if (GUI.changed)
                    {
                        s_BoundsValues[i] += Vector3.Dot(direction, newPoint - localPoint);
                        haveChanged = true;
                    }
                }
            
                // all edges of bounds
                for (int i = 0; i < s_BoundsEdgeIndices.GetLength(0); i++)
                {
                    var id = GUIUtility.GetControlID (s_BoundsHash, FocusType.Keyboard);

                    GUI.changed = false;
                    var index1		= s_BoundsEdgeIndices[i,0];
                    var index2		= s_BoundsEdgeIndices[i,1];
                    var point1		= s_BoundsVertices[index1];
                    var point2		= s_BoundsVertices[index2];

                    var midPoint	= (point1 + point2) * 0.5f;
                
                    var offset1		= s_EdgeDirectionOffsets[i, 0];
                    var offset2		= s_EdgeDirectionOffsets[i, 1];
                    var offset3		= s_EdgeDirectionOffsets[i, 2];
                    var offset1_dir	= offset1 % 3;
                    var offset2_dir	= offset2 % 3;

                    if (Event.current.type == EventType.Repaint)
                    {
                        var highlight	 = (currentFocusControl == id) || (currentFocusControl == s_BoundsControlIds[offset1]) || (currentFocusControl == s_BoundsControlIds[offset2]);					
                        var edgeColor	 = (s_BoundsBackfaced[offset1] && s_BoundsBackfaced[offset2]) ? backfacedColor : boundsColor;
                        var edgeDisabled = (s_BoundsAxisDisabled[offset1] && s_BoundsAxisDisabled[offset2]);
                        SceneHandles.color = SceneHandles.StateColor(edgeColor, edgeDisabled, highlight);

                        if (currentFocusControl == id)
                            selectedAxes = s_EdgeAxes[i];
                    }

                    // only use capFunction (render point) when in ortho mode & aligned with box or when side size is 0
                    bool isSideAlignedWithCamera = false; // TODO: determine if aligned with camera direction & in ortho mode
                    bool showSidePoint = !isSideAlignedWithCamera && ((point2 - point1).sqrMagnitude < kShowPointThreshold);

                    float pointSize;
                    Vector3 offset;
                    if (showSidePoint)
                    { 
                        pointSize	= UnityEditor.HandleUtility.GetHandleSize(midPoint) * kPointScale;
                        offset		= Edge2DHandleOffset(id, point1, point2, midPoint, s_BoundsSlideDirs[offset3], 
                                                                                       s_BoundsSlideDirs[offset1_dir], 
                                                                                       s_BoundsSlideDirs[offset2_dir], pointSize, pointCapFunction, s_EdgeAxes[i], snappingSteps: snappingSteps);
                    } else
                    {
                        offset		= Edge2DHandleOffset(id, point1, point2, midPoint, s_BoundsSlideDirs[offset3], 
                                                                                       s_BoundsSlideDirs[offset1_dir], 
                                                                                       s_BoundsSlideDirs[offset2_dir], 0, null, s_EdgeAxes[i], snappingSteps: snappingSteps);
                    }

                    if (GUI.changed)
                    {
                        offset = Quaternion.Inverse(rotation) * offset;
                        if (Mathf.Abs(offset[offset1_dir]) > 0.000001f ||
                            Mathf.Abs(offset[offset2_dir]) > 0.000001f)
                        {
                            s_BoundsValues[offset1] += offset[offset1_dir];
                            s_BoundsValues[offset2] += offset[offset2_dir];
                            haveChanged = true;
                        } else
                            GUI.changed = false;
                    }
                }
            
                GUI.changed = prevGUIchanged || haveChanged;

                if (haveChanged)
                { 
                    var size   = bounds.size;

                    center.x = (s_BoundsValues[3] + s_BoundsValues[0]) * 0.5f;
                    size.x   = (s_BoundsValues[3] - s_BoundsValues[0]);
            
                    center.y = (s_BoundsValues[4] + s_BoundsValues[1]) * 0.5f;
                    size.y   = (s_BoundsValues[4] - s_BoundsValues[1]);
            
                    center.z = (s_BoundsValues[5] + s_BoundsValues[2]) * 0.5f;
                    size.z   = (s_BoundsValues[5] - s_BoundsValues[2]);
            
                    bounds.center = center;
                    bounds.size = size;
                }

                // TODO: paint XZ intersection with grid plane + 'shadow' 
            
                SceneHandles.disabled = prevDisabled;
            }
            
            return bounds;
        }
    }
}
