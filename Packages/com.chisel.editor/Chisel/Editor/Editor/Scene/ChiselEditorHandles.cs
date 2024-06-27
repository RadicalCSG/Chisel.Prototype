using System;
using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // TODO: TEST EVERYTHING WHILE ROTATED
    // TODO: make radius dragging snap better, no weird values
    // TODO: add polygon support (hover over surface, but not necessarily render the surface itself)
    // TODO: rewrite all "editors" to use new handle solution
    // TODO: move brush "editor" to handles
    // TODO: add support for "editors" to have an init/shutdown/update part so we can cache handles
    public sealed class ChiselEditorHandles : IChiselHandles
    {
        public void Start(ChiselNode generator, SceneView sceneView = null)
        {
            this.focusControl = UnitySceneExtensions.SceneHandleUtility.focusControl;
            this.disabled = UnitySceneExtensions.SceneHandles.disabled;
            this.generator = generator;
            this.modified = false;
            this.lastHandleHadFocus = false;
            if (generator)
                this.generatorTransform = this.generator.transform;
            else
                this.generatorTransform = null;
            this.sceneView = sceneView;
            if (sceneView)
                this.camera = sceneView.camera;
            else
                this.camera = null;
            this.mouseCursor = null;
            highlightHandles.Clear();
            editorHandlesToDraw.Clear();
            if (Event.current != null)
                internalMouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        }

        public void End()
        {
            if (editorHandlesToDraw.Count > 0)
            {
                foreach (var handle in editorHandlesToDraw)
                {
                    var focus = highlightHandles.Contains(handle);
                    handle.Draw(this, focus);
                }
            }
            if (this.mouseCursor.HasValue)
            {
                var rect = sceneView.position;
                rect.min = Vector2.zero;
                EditorGUIUtility.AddCursorRect(rect, this.mouseCursor.Value);
            }
            highlightHandles.Clear();
            editorHandlesToDraw.Clear();
            this.focusControl = 0;
            this.disabled = true;
            this.generator = null;
            this.modified = false;
            this.lastHandleHadFocus = false;
            this.generatorTransform = null;
            this.sceneView = null;
            this.camera = null;
            this.mouseCursor = null;

        }

        static Ray internalMouseRay;

        static Ray MouseRay
        {
            get
            {
                var matrix = Handles.inverseMatrix;
                var mouseRay = internalMouseRay;
                mouseRay.origin = matrix.MultiplyPoint(mouseRay.origin);
                mouseRay.direction = matrix.MultiplyVector(mouseRay.direction);
                return mouseRay;
            }
        }

        static HashSet<IChiselEditorHandle> editorHandlesToDraw = new HashSet<IChiselEditorHandle>();
        static HashSet<IChiselEditorHandle> highlightHandles = new HashSet<IChiselEditorHandle>();

        public bool IsIn2DMode
        {
            get
            {
                return sceneView != null && sceneView.isRotationLocked && camera.orthographic;
            }
        }

        int focusControl;

        ChiselNode generator;
        Transform generatorTransform;
        public bool modified { get; private set; }

        public MouseCursor? mouseCursor { get; set; }
        public Matrix4x4 matrix { get { return Handles.matrix; } set { Handles.matrix = value; } }
        public Matrix4x4 inverseMatrix { get { return Handles.inverseMatrix; } }
        public SceneView sceneView { get; private set; }
        public Camera camera { get; private set; }

        public bool lastHandleHadFocus { get; private set; }
        public bool disabled { get; private set; }

        // TODO: get rid of this
        public bool backfaced { get; set; }
        public Color color
        {
            get { return Handles.color; }
            set { Handles.color = value; }
        }

        public Vector3 moveSnappingSteps
        {
            get
            {
                return UnitySceneExtensions.Snapping.MoveSnappingSteps;
            }
        }

        public readonly Dictionary<ChiselNode, object> generatorStateLookup = new Dictionary<ChiselNode, object>();

        public object generatorState 
        { 
            get
            {
                if (!generator)
                    return null;
                if (generatorStateLookup.TryGetValue(generator, out object val))
                    return val;
                return null;
            }
            set
            {
                if (generator)
                    generatorStateLookup[generator] = value;
            } 
        }



        static float3[] s_CircleVertices;
        static void GenerateCircleVertices()
        {
            if (s_CircleVertices == null)
                BrushMeshFactory.GetCircleVertices(1.0f, 1.0f, 0, 72, ref s_CircleVertices);
        }


        // TODO: get rid of this
        public bool IsSufaceBackFaced(Vector3 point, Vector3 normal)
        {
            // TODO: make matrix and Handles.matrix/Handles.inverseMatrix work together in a reasonable way
            var inverseMatrix = UnityEditor.Handles.inverseMatrix;
            var cameraLocalPos = inverseMatrix.MultiplyPoint(camera.transform.position);
            var cameraLocalForward = inverseMatrix.MultiplyVector(camera.transform.forward);
            var isCameraOrthographic = camera.orthographic;

            var cosV = isCameraOrthographic ? Vector3.Dot(normal, -cameraLocalForward) :
                                              Vector3.Dot(normal, (cameraLocalPos - point));

            return (cosV < -0.0001f);
        }

        const float kVertexHandleSize = 0.005f;

        interface IChiselEditorHandle
        {
            float MouseDistance();
            void Draw(IChiselHandles handles, bool focus);
        }

        struct LineHandle : IChiselEditorHandle, IChiselLineHandle
        {
            public Vector3 From { get; set; }
            public Vector3 To { get; set; }
            public Color Color { get; set; }
            public float DashSize { get; set; }
            public bool HighlightOnly { get; set; }

            const float kDefaultThickness = 1.0f;

            public void Draw(IChiselHandles handles, bool focus)
            {
                handles.color = handles.GetStateColor(Color, focus, false);
                ChiselOutlineRenderer.DrawLine(From, To, LineMode.ZTest, kDefaultThickness, DashSize);
                handles.color = handles.GetStateColor(Color, focus, true);
                ChiselOutlineRenderer.DrawLine(From, To, LineMode.NoZTest, kDefaultThickness, DashSize);
            }

            public float MouseDistance()
            {
                if (HighlightOnly)
                    return float.PositiveInfinity;
                return HandleUtility.DistanceToLine(From, To);
            }

            public bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true)
            {
                if (interpolate)
                {
                    closestPoint = HandleUtility.ClosestPointToPolyLine(From, To);
                    return true;
                }
                var dist1 = HandleUtility.DistanceToCircle(From, HandleUtility.GetHandleSize(From) * kVertexHandleSize);
                var dist2 = HandleUtility.DistanceToCircle(To, HandleUtility.GetHandleSize(To) * kVertexHandleSize);
                if (dist1 < dist2)
                    closestPoint = From;
                else
                    closestPoint = To;
                return true;
            }
        }

        struct PolygonHandle : IChiselEditorHandle, IChiselPolygonHandle
        {
            Vector3[] vertices3D;
            Vector2[] vertices2D;
            Plane plane;
            Vector3 tangent;
            Vector3 binormal;

            public Vector3[] Vertices
            {
                get { return vertices3D; }
                set { SetVertices(value); }
            }
            public Color Color { get; set; }
            public float DashSize { get; set; }

            const float kDefaultThickness = 1.0f;

            void SetVertices(Vector3[] input)
            {
                vertices3D = input;
                if (input == null ||
                    input.Length < 3)
                {
                    vertices2D = null;
                    return;
                }

                if (vertices2D == null ||
                    vertices2D.Length != vertices3D.Length)
                    vertices2D = new Vector2[vertices3D.Length];

                plane = GeometryMath.CalculatePlane(input);
                UnitySceneExtensions.GeometryUtility.CalculateTangents(plane.normal, out tangent, out binormal);

                for (int i = 0; i < vertices2D.Length; i++)
                {
                    var vertex = vertices3D[i];
                    var x = Vector3.Dot(tangent, vertex);
                    var y = Vector3.Dot(binormal, vertex);
                    vertices2D[i] = new Vector2(x, y);
                }
            }

            public void Draw(IChiselHandles handles, bool focus)
            {
                if (Vertices == null || Vertices.Length < 3)
                    return;

                handles.color = handles.GetStateColor(Color, focus, false);
                ChiselOutlineRenderer.DrawLineLoop(Vertices, 0, Vertices.Length, LineMode.ZTest, kDefaultThickness, DashSize);

                handles.color = handles.GetStateColor(Color, focus, true);
                ChiselOutlineRenderer.DrawLineLoop(Vertices, 0, Vertices.Length, LineMode.NoZTest, kDefaultThickness, DashSize);
            }

            public float MouseDistance()
            {
                if (Vertices == null || Vertices.Length < 3)
                    return float.PositiveInfinity;

                var mouseRay = MouseRay;
                if (plane.Raycast(mouseRay, out var intersectionDist))
                {
                    var intersectionPoint = mouseRay.GetPoint(intersectionDist);

                    var x = Vector3.Dot(tangent, intersectionPoint);
                    var y = Vector3.Dot(binormal, intersectionPoint);
                    var planePoint = new Vector2(x, y);

                    if (GeometryMath.ContainsPoint(vertices2D, planePoint))
                        return 0;
                }

                var minDistance = HandleUtility.DistanceToLine(Vertices[Vertices.Length - 1], Vertices[0]);
                for (int i = 1; i < Vertices.Length; i++)
                    minDistance = Mathf.Min(minDistance, HandleUtility.DistanceToLine(Vertices[i - 1], Vertices[i]));
                return minDistance;
            }

            public bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true)
            {
                closestPoint = Vector3.zero;
                if (Vertices == null || Vertices.Length < 3)
                    return false;

                if (interpolate)
                {
                    // TODO: polyline isn't closed ..
                    closestPoint = HandleUtility.ClosestPointToPolyLine(Vertices);
                    return true;
                }

                var mouseRay = MouseRay;
                if (plane.Raycast(mouseRay, out var intersectionDist))
                {
                    var intersectionPoint = mouseRay.GetPoint(intersectionDist);

                    var x = Vector3.Dot(tangent, intersectionPoint);
                    var y = Vector3.Dot(binormal, intersectionPoint);
                    var planePoint = new Vector2(x, y);

                    if (GeometryMath.ContainsPoint(vertices2D, planePoint))
                    {
                        closestPoint = intersectionPoint;
                        return true;
                    }
                }

                var vertex = Vertices[0];
                var dist = HandleUtility.DistanceToCircle(vertex, HandleUtility.GetHandleSize(vertex) * kVertexHandleSize);
                closestPoint = vertex;
                for (int i = 1; i < Vertices.Length; i++)
                {
                    vertex = Vertices[i];
                    var newDist = HandleUtility.DistanceToCircle(vertex, HandleUtility.GetHandleSize(vertex) * kVertexHandleSize);
                    if (newDist < dist)
                        closestPoint = vertex;
                }
                return true;
            }
        }

        struct LineLoopHandle : IChiselEditorHandle, IChiselLineLoopHandle
        {
            public Vector3[] Vertices { get; set; }
            public int Offset { get; set; }
            public int Count { get; set; }
            public Color Color { get; set; }
            public float DashSize { get; set; }

            const float kDefaultThickness = 1.0f;

            public void Draw(IChiselHandles handles, bool focus)
            {
                if (Vertices == null || Vertices.Length == 0)
                    return;
                handles.color = handles.GetStateColor(Color, focus, false);
                ChiselOutlineRenderer.DrawLineLoop(Vertices, Offset, Count, LineMode.ZTest, kDefaultThickness, DashSize);
                handles.color = handles.GetStateColor(Color, focus, true);
                ChiselOutlineRenderer.DrawLineLoop(Vertices, Offset, Count, LineMode.NoZTest, kDefaultThickness, DashSize);
            }

            public float MouseDistance()
            {
                if (Vertices == null || Vertices.Length == 0)
                    return float.PositiveInfinity;
                if (Count < 2)
                    return float.PositiveInfinity;
                var minDistance = HandleUtility.DistanceToLine(Vertices[Offset + Count - 1], Vertices[Offset + 0]);
                for (int i = 1; i < Count; i++)
                    minDistance = Mathf.Min(minDistance, HandleUtility.DistanceToLine(Vertices[Offset + i - 1], Vertices[Offset + i]));
                return minDistance;
            }

            public bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true)
            {
                closestPoint = Vector3.zero;
                if (Vertices == null || Vertices.Length == 0)
                    return false;

                if (interpolate)
                {
                    if (Count < 2)
                        return false;
                    // TODO: polyline isn't closed ..
                    closestPoint = HandleUtility.ClosestPointToPolyLine(Vertices);
                    return true;
                }

                if (Count < 1)
                    return false;

                var vertex = Vertices[Offset];
                var dist = HandleUtility.DistanceToCircle(vertex, HandleUtility.GetHandleSize(vertex) * kVertexHandleSize);
                closestPoint = vertex;
                for (int i = 1; i < Count; i++)
                {
                    vertex = Vertices[Offset + i];
                    var newDist = HandleUtility.DistanceToCircle(vertex, HandleUtility.GetHandleSize(vertex) * kVertexHandleSize);
                    if (newDist < dist)
                        closestPoint = vertex;
                }
                return true;
            }
        }

        struct NormalHandle : IChiselEditorHandle, IChiselNormalHandle
        {
            public Vector3 Origin { get; set; }
            public Vector3 Normal { get; set; }
            public Color Color { get; set; }
            public float DashSize { get; set; }


            float HandleSize { get { return UnityEditor.HandleUtility.GetHandleSize(Origin) * 0.05f; } }

            const float kDefaultThickness = 1.0f;

            public void Draw(IChiselHandles handles, bool focus)
            {
                var size = HandleSize;
                if (focus)
                {
                    handles.color = handles.GetStateColor(Color, focus, false);
                    UnityEditor.Handles.ArrowHandleCap(0, Origin, Quaternion.LookRotation(Normal), size * 20, EventType.Repaint);
                    SceneHandles.RenderBorderedCircle(Origin, size);
                } else
                {
                    var dstPoint = Origin + (Normal.normalized * size * 10);
                    handles.color = handles.GetStateColor(Color, focus, false);
                    ChiselOutlineRenderer.DrawLine(Origin, dstPoint, LineMode.ZTest, kDefaultThickness, DashSize);
                    handles.color = handles.GetStateColor(Color, focus, true);
                    ChiselOutlineRenderer.DrawLine(Origin, dstPoint, LineMode.NoZTest, kDefaultThickness, DashSize);
                }
                // TODO: be able to render dots "backfaced"
                SceneHandles.RenderBorderedCircle(Origin, size);
            }

            public float MouseDistance()
            {
                var size = HandleSize;
                return Math.Min(UnityEditor.HandleUtility.DistanceToCircle(Origin, size),
                                UnityEditor.HandleUtility.DistanceToLine(Origin, Origin + (Normal.normalized * size * 10)));
            }

            public bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true)
            {
                closestPoint = Origin;
                return true;
            }
        }

        struct EllipsoidHandle : IChiselEditorHandle, IChiselCircleHandle, IChiselEllipsoidHandle
        {
            float diameterX;
            float diameterZ;
            float rotation;
            float startAngle;
            float totalAngles;

            public Vector3 Center { get; set; }
            public Vector3 Normal { get; set; }
            public float Diameter { get => Mathf.Max(diameterX, diameterZ); set => diameterX = diameterZ = Mathf.Abs(value); }
            public float DiameterX { get => diameterX; set => diameterX = Mathf.Abs(value); }
            public float DiameterZ { get => diameterZ; set => diameterZ = Mathf.Abs(value); }
            public float Rotation { get => rotation; set => rotation = value % 360.0f; }
            public float StartAngle { get => startAngle; set => startAngle = value % 360.0f; }
            public float TotalAngles { get => totalAngles; set => totalAngles = Mathf.Clamp(value, 1, 360.0f); }
            public Color Color { get; set; }
            public float DashSize { get; set; }

            const float kDefaultThickness = 1.0f;

            Matrix4x4 StartAngleTransform
            {
                get
                {
                    if (startAngle == 0 || totalAngles == 360.0f)
                        return Matrix4x4.identity;
                    return Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(startAngle, Vector3.up), Vector3.one);
                }
            }

            Matrix4x4 WorldTransform
            {
                get
                {
                    return Matrix4x4.TRS(Center, Quaternion.AngleAxis(rotation, Normal), new Vector3(diameterX * 0.5f, 1.0f, diameterZ * 0.5f));
                }
            }

            Matrix4x4 EllipsoidTransform
            {
                get
                {
                    return WorldTransform *
                            StartAngleTransform;
                }
            }

            public Vector3 GetPointAtDegree(float degree)
            {
                GenerateCircleVertices();
                degree = degree % 360;
                degree = (360 + degree) % 360;
                var index = Mathf.RoundToInt((degree / 360) * s_CircleVertices.Length);

                var transform = EllipsoidTransform;
                return transform.MultiplyPoint(s_CircleVertices[index]);
            }

            public void Draw(IChiselHandles handles, bool focus)
            {
                GenerateCircleVertices();

                var maxLines = s_CircleVertices.Length;
                var count = Mathf.Clamp(Mathf.CeilToInt((maxLines / 360.0f) * totalAngles) + 1, 1, maxLines);

                var prevMatrix = Handles.matrix;
                var transform = this.EllipsoidTransform;
                Handles.matrix = prevMatrix * transform;

                handles.color = handles.GetStateColor(Color, focus, false);
                if (count < maxLines)
                {
                    ChiselOutlineRenderer.DrawContinuousLines(s_CircleVertices, 0, count, LineMode.ZTest, kDefaultThickness, DashSize);
                } else
                    ChiselOutlineRenderer.DrawLineLoop(s_CircleVertices, 0, count, LineMode.ZTest, kDefaultThickness, DashSize);

                handles.color = handles.GetStateColor(Color, focus, true);
                if (count < maxLines)
                {
                    ChiselOutlineRenderer.DrawContinuousLines(s_CircleVertices, 0, count, LineMode.NoZTest, kDefaultThickness, DashSize);
                } else
                    ChiselOutlineRenderer.DrawLineLoop(s_CircleVertices, 0, count, LineMode.NoZTest, kDefaultThickness, DashSize);

                Handles.matrix = prevMatrix;
            }

            public float MouseDistance()
            {
                GenerateCircleVertices();

                var prevMatrix = Handles.matrix;
                var transform = prevMatrix * this.EllipsoidTransform;
                Handles.matrix = transform;
                try
                {
                    if (TotalAngles != 360)
                        return HandleUtility.DistanceToArc(Vector3.zero, Vector3.up, Vector3.forward, totalAngles, 1.0f);
                    return HandleUtility.DistanceToDisc(Vector3.zero, Vector3.up, 1.0f);
                }
                finally
                {
                    Handles.matrix = prevMatrix;
                }
            }

            public bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true)
            {
                var mouseRay = MouseRay;
                var plane = new Plane(Normal, Center);
                closestPoint = Vector3.zero;
                if (!plane.Raycast(mouseRay, out var intersectionDist))
                    return false;

                var intersectionPoint = mouseRay.GetPoint(intersectionDist);

                var transform = EllipsoidTransform;
                var invTransform = transform.inverse;

                var closestPointOnCircle = invTransform.MultiplyPoint(intersectionPoint).normalized;

                if (interpolate)
                {
                    var angle = (720 + Vector3.SignedAngle(closestPointOnCircle, Vector3.right, Vector3.up)) % 360;
                    if (angle < 0 || angle >= totalAngles)
                        return false;
                    closestPoint = transform.MultiplyPoint(closestPointOnCircle);
                    return true;
                }

                var maxLines = s_CircleVertices.Length;
                var count = Mathf.Clamp(Mathf.CeilToInt((maxLines / 360.0f) * totalAngles) + 1, 1, maxLines);

                var cmpClosestPoint = transform.MultiplyPoint(closestPointOnCircle);
                var currVertex = transform.MultiplyPoint(s_CircleVertices[0]);
                var currDist = math.length(cmpClosestPoint - currVertex);
                closestPoint = currVertex;
                for (int i = 1; i < count; i++)
                {
                    currVertex = transform.MultiplyPoint(s_CircleVertices[i]);
                    var newDist = math.length(cmpClosestPoint - currVertex);
                    if (newDist < currDist)
                    {
                        closestPoint = currVertex;
                        currDist = newDist;
                    }
                }
                return true;
            }
        }

        public IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, Color color, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0) { return new EllipsoidHandle { Center = center, Normal = normal, DiameterX = diameterX, DiameterZ = diameterZ, Color = color, Rotation = rotation, StartAngle = startAngle, TotalAngles = angles, DashSize = dashSize }; }
        public IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0) { return new EllipsoidHandle { Center = center, Normal = normal, DiameterX = diameterX, DiameterZ = diameterZ, Color = color, Rotation = rotation, StartAngle = startAngle, TotalAngles = angles, DashSize = dashSize }; }

        public IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, Color color, float startAngle = 0, float angles = 360, float dashSize = 0) { return new EllipsoidHandle { Center = center, Normal = normal, DiameterX = diameter, DiameterZ = diameter, Color = color, StartAngle = startAngle, TotalAngles = angles, DashSize = dashSize }; }
        public IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, float startAngle = 0, float angles = 360, float dashSize = 0) { return new EllipsoidHandle { Center = center, Normal = normal, DiameterX = diameter, DiameterZ = diameter, Color = color, StartAngle = startAngle, TotalAngles = angles, DashSize = dashSize }; }

        public IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, Color color, float dashSize = 0, bool highlightOnly = false) { return new LineHandle { From = from, To = to, Color = color, DashSize = dashSize, HighlightOnly = highlightOnly }; }
        public IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, float dashSize = 0, bool highlightOnly = false) { return new LineHandle { From = from, To = to, Color = this.color, DashSize = dashSize, HighlightOnly = highlightOnly }; }

        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, Color color, float dashSize = 0) { return new LineLoopHandle { Vertices = vertices, Offset = 0, Count = vertices.Length, Color = color, DashSize = dashSize }; }
        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, float dashSize = 0) { return new LineLoopHandle { Vertices = vertices, Offset = 0, Count = vertices.Length, Color = this.color, DashSize = dashSize }; }

        public IChiselPolygonHandle CreatePolygonHandle(Vector3[] vertices, Color color, float dashSize = 0) { return new PolygonHandle { Vertices = vertices, Color = color, DashSize = dashSize }; }
        public IChiselPolygonHandle CreatePolygonHandle(Vector3[] vertices, float dashSize = 0) { return new PolygonHandle { Vertices = vertices, Color = this.color, DashSize = dashSize }; }

        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, Color color, float dashSize = 0) { return new LineLoopHandle { Vertices = vertices, Offset = offset, Count = count, Color = color, DashSize = dashSize }; }
        public IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, float dashSize = 0) { return new LineLoopHandle { Vertices = vertices, Offset = offset, Count = count, Color = this.color, DashSize = dashSize }; }

        public IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, Color color, float dashSize = 0) { return new NormalHandle { Origin = origin, Normal = normal, Color = color, DashSize = dashSize }; }
        public IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, float dashSize = 0) { return new NormalHandle { Origin = origin, Normal = normal, Color = this.color, DashSize = dashSize }; }



        public bool TryGetClosestPoint(IChiselHandle[] handles, out Vector3 closestPoint, bool interpolate = true)
        {
            Vector3? foundClosestPoint = null;
            if (handles != null && handles.Length > 0)
            {
                var mouseRay = MouseRay;
                var lineDistance = float.PositiveInfinity;
                for (int i = 0; i < handles.Length; i++)
                {
                    if (!handles[i].TryGetClosestPoint(out Vector3 point, interpolate))
                        continue;

                    var newLineDistance = HandleUtility.DistancePointLine(point, mouseRay.origin, mouseRay.direction * 10000);
                    if (newLineDistance > lineDistance)
                        continue;

                    if (foundClosestPoint.HasValue &&
                        Mathf.Abs(newLineDistance - lineDistance) < float.Epsilon)
                    {
                        if ((foundClosestPoint.Value - mouseRay.origin).sqrMagnitude < (point - mouseRay.origin).sqrMagnitude)
                            continue;
                    }

                    lineDistance = newLineDistance;
                    foundClosestPoint = point;
                }
            }
            closestPoint = foundClosestPoint ?? Vector3.zero;
            return foundClosestPoint.HasValue;
        }

        public bool TryGetClosestPoint(IChiselHandle handle, out Vector3 closestPoint, bool interpolate = true)
        {
            singleHandleArray[0] = handle;
            return TryGetClosestPoint(singleHandleArray, out closestPoint, interpolate);
        }

        public void DoRenderHandles(IChiselHandle[] handles)
        {
            var id = GUIUtility.GetControlID(s_DoSlider1DHandleHash, FocusType.Passive);
            switch (Event.current.GetTypeForControl(id))
            {
                case EventType.Repaint:
                {
                    var hot = (GUIUtility.hotControl == id);
                    var focus = lastHandleHadFocus || hot;
                    for (int i = 0; i < handles.Length; i++)
                    {
                        if (focus) highlightHandles.Add((IChiselEditorHandle)handles[i]);
                        editorHandlesToDraw.Add((IChiselEditorHandle)handles[i]);
                    }
                    break;
                }
            }
        }

        internal static int s_DoSlider1DHandleHash = "DoSlider1DHandle".GetHashCode();
        public bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null)
        {
            if (handles != null && handles.Length == 0)
                return false;
            
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoSlider1DHandleHash, FocusType.Passive);
            
            var axis = Axis.Y;
            if (snappingStep == 0)
                snappingStep = UnitySceneExtensions.Snapping.MoveSnappingSteps[(int)axis];
            var capFunction = (handles == null) ? null : UnitySceneExtensions.SceneHandles.NullCap;
            var newPosition = UnitySceneExtensions.SceneHandles.Slider1DHandle(id, axis, position, direction, snappingStep, 1.0f, capFunction);
            this.lastHandleHadFocus = focusControl == id;
            if (lastHandleHadFocus)
                mouseCursor = UnitySceneExtensions.SceneHandleUtility.GetCursorForDirection(Vector3.zero, direction);
            if (handles != null &&
                handles.Length > 0)
            {
                switch (Event.current.GetTypeForControl(id))
                {
                    case EventType.Layout:
                    {
                        var minDistance = ((IChiselEditorHandle)handles[0]).MouseDistance();
                        for (int i = 1; i < handles.Length; i++)
                            minDistance = Mathf.Min(minDistance, ((IChiselEditorHandle)handles[i]).MouseDistance());
                        UnityEditor.HandleUtility.AddControl(id, minDistance);
                        break;
                    }
                    case EventType.Repaint:
                    {
                        var hot   = (GUIUtility.hotControl == id);
                        var focus = lastHandleHadFocus || hot;
                        for (int i = 0; i < handles.Length; i++)
                        {
                            if (focus) highlightHandles.Add((IChiselEditorHandle)handles[i]);
                            editorHandlesToDraw.Add((IChiselEditorHandle)handles[i]);
                        }
                        break;
                    }
                }
            }
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            position = newPosition;
            this.modified = true;
            return true;
        }

        static readonly IChiselHandle[] singleHandleArray = new IChiselHandle[1];
        public bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null)
        {
            singleHandleArray[0] = handle;
            return DoSlider1DHandle(ref position, direction, singleHandleArray, snappingStep, undoMessage);
        }


        public bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null)
        {
            if (handles != null && handles.Length == 0)
                return false;

            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoSlider1DHandleHash, FocusType.Passive);

            var axis = Axis.Y;
            if (snappingStep == 0)
                snappingStep = UnitySceneExtensions.Snapping.MoveSnappingSteps[(int)axis];
            var capFunction = (handles == null) ? null : UnitySceneExtensions.SceneHandles.NullCap;
            direction.Normalize();
            var position = center + (direction * distance);
            var newPosition = UnitySceneExtensions.SceneHandles.Slider1DHandle(id, axis, position, direction, snappingStep, 1.0f, capFunction);
            this.lastHandleHadFocus = focusControl == id;
            if (lastHandleHadFocus)
                mouseCursor = UnitySceneExtensions.SceneHandleUtility.GetCursorForDirection(Vector3.zero, direction);
            if (handles != null &&
                handles.Length > 0)
            {
                switch (Event.current.GetTypeForControl(id))
                {
                    case EventType.Layout:
                    {
                        var minDistance = ((IChiselEditorHandle)handles[0]).MouseDistance();
                        for (int i = 1; i < handles.Length; i++)
                            minDistance = Mathf.Min(minDistance, ((IChiselEditorHandle)handles[i]).MouseDistance());
                        UnityEditor.HandleUtility.AddControl(id, minDistance);
                        break;
                    }
                    case EventType.Repaint:
                    {
                        var hot = (GUIUtility.hotControl == id);
                        var focus = lastHandleHadFocus || hot;
                        for (int i = 0; i < handles.Length; i++)
                        {
                            if (focus) highlightHandles.Add((IChiselEditorHandle)handles[i]);
                            editorHandlesToDraw.Add((IChiselEditorHandle)handles[i]);
                        }
                        break;
                    }
                }
            }
            //UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(-1, position, Quaternion.LookRotation(direction), UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, Event.current.type);
            //UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(-1, newPosition, Quaternion.LookRotation(direction), UnityEditor.HandleUtility.GetHandleSize(newPosition) * 0.05f, Event.current.type);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            distance = Vector3.Dot(newPosition - center, direction); 
            //Debug.Log(distance);
            this.modified = true;
            return true;
        }

        public bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null)
        {
            singleHandleArray[0] = handle;
            return DoSlider1DHandle(ref distance, center, direction, singleHandleArray, snappingStep, undoMessage);
        }


        public bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null)
        {
            singleHandleArray[0] = handle;
            return DoCircleRotationHandle(ref angle, center, normal, singleHandleArray, undoMessage);
        }

        internal static int s_DoCircleRotationHandleHash = "DoCircleRotationHandle".GetHashCode();
        internal static Vector3 s_RotationVector = Vector3.forward;
        public bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null)
        {
            if (handles != null && handles.Length == 0)
                return false;
            
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoCircleRotationHandleHash, FocusType.Passive);
            
            var capFunction = (handles == null) ? null : UnitySceneExtensions.SceneHandles.NullCap;

            var rotation = Quaternion.AngleAxis(angle, normal);
            if (GUIUtility.hotControl == 0)
            {
                var vectorForward   = (rotation * Vector3.forward).normalized;
                var vectorBackward  = (rotation * Vector3.back).normalized;
                var vectorLeft      = (rotation * Vector3.left).normalized;
                var vectorRight     = (rotation * Vector3.right).normalized;

                var mouseRay        = MouseRay;
                var plane           = new Plane(normal, center);
                if (!plane.Raycast(mouseRay, out var intersectionDist))
                    return false;
                var intersectionPoint   = mouseRay.GetPoint(intersectionDist);
                var mouseAngle          = (intersectionPoint - center).normalized;

                //UnitySceneExtensions.SceneHandles.DrawLine(mouseRay.origin, mouseRay.origin + mouseRay.direction);
                //UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(-1, intersectionPoint, Quaternion.LookRotation(normal), UnityEditor.HandleUtility.GetHandleSize(intersectionPoint) * 0.05f, Event.current.type);
                
                s_RotationVector = Vector3.forward;
                var testDotA = Vector3.Dot(vectorForward, mouseAngle);
                var currDot  = testDotA;

                var testDotB = Vector3.Dot(vectorBackward, mouseAngle);
                if (testDotB > currDot) { s_RotationVector = Vector3.back; currDot = testDotB;  }

                var testDotC = Vector3.Dot(vectorLeft, mouseAngle);
                if (testDotC > currDot) { s_RotationVector = Vector3.left; currDot = testDotC; }

                var testDotD = Vector3.Dot(vectorRight, mouseAngle);
                if (testDotD > currDot) { s_RotationVector = Vector3.right; currDot = testDotA; }
            }
            
            Vector3 originalVector;
            originalVector      = (rotation * s_RotationVector).normalized;
            var currVector      = originalVector;

            var position    = center + currVector;
            UnitySceneExtensions.GeometryUtility.CalculateTangents(normal, out var right, out var forward);
            position = UnitySceneExtensions.SceneHandles.Slider2DHandle(id, position, Vector3.zero, normal, forward, right, 1.0f, capFunction, noSnapping: true);
            currVector = position - center;


            //UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(-1, position, Quaternion.LookRotation(normal), UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, Event.current.type);
            //UnitySceneExtensions.SceneHandles.DrawLine(center, center + currVector);
            //UnitySceneExtensions.SceneHandles.DrawLine(center, center + originalVector);

            var angleDiff   = GeometryMath.SignedAngle(originalVector, currVector.normalized, normal);
            var newAngle    = (angle + angleDiff);
            if (Snapping.RotateSnappingActive && Snapping.RotateSnappingStep != 0)
                newAngle = (int)(newAngle / Snapping.RotateSnappingStep) * Snapping.RotateSnappingStep;
            this.lastHandleHadFocus = focusControl == id;
            if (lastHandleHadFocus)
                mouseCursor = UnitySceneExtensions.SceneHandleUtility.GetCursorForCircleTangent(center, normal);
            if (handles != null &&
                handles.Length > 0)
            {
                switch (Event.current.GetTypeForControl(id))
                {
                    case EventType.Layout:
                    {
                        var minDistance = ((IChiselEditorHandle)handles[0]).MouseDistance();
                        for (int i = 1; i < handles.Length; i++)
                            minDistance = Mathf.Min(minDistance, ((IChiselEditorHandle)handles[i]).MouseDistance());
                        UnityEditor.HandleUtility.AddControl(id, minDistance);
                        break;
                    }
                    case EventType.Repaint:
                    {
                        var hot   = (GUIUtility.hotControl == id);
                        var focus = lastHandleHadFocus || hot;
                        for (int i = 0; i < handles.Length; i++)
                        {
                            if (focus) highlightHandles.Add((IChiselEditorHandle)handles[i]);
                            editorHandlesToDraw.Add((IChiselEditorHandle)handles[i]);
                        }
                        break;
                    }
                }
            }
            if (!EditorGUI.EndChangeCheck() || angle == newAngle)
                return false;

            RecordUndo(undoMessage);
            angle = newAngle;
            this.modified = true;
            return true;
        }
        


        public bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null)
        {
            singleHandleArray[0] = handle;
            return DoDistanceHandle(ref distance, center, normal, singleHandleArray, undoMessage);
        }

        internal static int s_DoDistanceHandleHash = "DoDistanceHandle".GetHashCode();
        public bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null)
        {
            if (handles != null && handles.Length == 0)
                return false;
            
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoDistanceHandleHash, FocusType.Passive);
            
            var capFunction = (handles == null) ? null : UnitySceneExtensions.SceneHandles.NullCap;

            if (GUIUtility.hotControl == 0)
            {
                var mouseRay = MouseRay;
                var plane    = new Plane(normal, center);
                if (!plane.Raycast(mouseRay, out var intersectionDist))
                    return false;
                var intersectionPoint   = mouseRay.GetPoint(intersectionDist);
                s_RotationVector = (intersectionPoint - center).normalized;
            } else
            {
                var mouseRay = MouseRay;
                var plane = new Plane(normal, center);
                if (plane.Raycast(mouseRay, out var intersectionDist))
                {
                    var intersectionPoint = mouseRay.GetPoint(intersectionDist);
                    s_RotationVector = (intersectionPoint - center).normalized;
                }
            }

            var originalVector  = s_RotationVector;
            var currVector      = originalVector * distance;

            var position        = center + currVector;

#if true
            UnitySceneExtensions.GeometryUtility.CalculateTangents(normal, out var right, out var forward);
#else

            var delta       = (position - center).normalized;
            var activeGrid  = Grid.ActiveGrid;
            var currentAxis = Axis.X;
            var direction   = activeGrid.Right;
            var axisDot     = Mathf.Abs(Vector3.Dot(delta, direction));
            
            var testDirection   = activeGrid.Forward;
            var testAxisDot     = Mathf.Abs(Vector3.Dot(delta, testDirection));
            if (testAxisDot < axisDot) { axisDot = testAxisDot; currentAxis = Axis.Z; }
            
            testDirection   = activeGrid.Up;
            testAxisDot     = Mathf.Abs(Vector3.Dot(delta, testDirection));
            if (testAxisDot < axisDot) { axisDot = testAxisDot; currentAxis = Axis.Y; }

            //position        = UnitySceneExtensions.SceneHandles.Slider1DHandle(id, currentAxis, position, direction, capFunction);
            Vector3 right, forward;
            switch (currentAxis)
            {
                default:        { UnitySceneExtensions.GeometryUtility.CalculateTangents(normal, out right, out forward); break; }
                case Axis.X:    { right = activeGrid.Up;    forward = activeGrid.Forward; break; }
                case Axis.Y:    { right = activeGrid.Right; forward = activeGrid.Forward; break; }
                case Axis.Z:    { right = activeGrid.Right; forward = activeGrid.Up; break; }
            }
#endif
            position        = UnitySceneExtensions.SceneHandles.Slider2DHandle(id, position, Vector3.zero, normal, forward, right, 1.0f, capFunction);
            currVector      = position - center;

            var newDistance = Vector3.Dot(originalVector, currVector);
            this.lastHandleHadFocus = focusControl == id;
            if (lastHandleHadFocus)
                mouseCursor = UnitySceneExtensions.SceneHandleUtility.GetCursorForDirection(center, originalVector);
            if (handles != null &&
                handles.Length > 0)
            {
                switch (Event.current.GetTypeForControl(id))
                {
                    case EventType.Layout:
                    {
                        var minDistance = ((IChiselEditorHandle)handles[0]).MouseDistance();
                        for (int i = 1; i < handles.Length; i++)
                            minDistance = Mathf.Min(minDistance, ((IChiselEditorHandle)handles[i]).MouseDistance());
                        UnityEditor.HandleUtility.AddControl(id, minDistance);
                        break;
                    }
                    case EventType.Repaint:
                    {
                        var hot   = (GUIUtility.hotControl == id);
                        var focus = lastHandleHadFocus || hot;
                        for (int i = 0; i < handles.Length; i++)
                        {
                            if (focus) highlightHandles.Add((IChiselEditorHandle)handles[i]);
                            editorHandlesToDraw.Add((IChiselEditorHandle)handles[i]);
                        }
                        break;
                    }
                }
            }
            if (!EditorGUI.EndChangeCheck() || distance == newDistance)
                return false;

            RecordUndo(undoMessage);
            distance = newDistance;
            this.modified = true;
            return true;
        }





        internal static int s_DoDirectionHandleHash = "DoDirectionHandle".GetHashCode();
        public bool DoDirectionHandle(ref Vector3 position, Vector3 direction, float snappingStep = 0, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoDirectionHandleHash, FocusType.Passive);
            this.lastHandleHadFocus = focusControl == id;
            var prevColor = color;
            color = GetStateColor(color, lastHandleHadFocus, backfaced);
            var newPosition = UnitySceneExtensions.SceneHandles.DirectionHandle(id, position, direction, snappingStep: snappingStep);
            color = prevColor;
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            position = newPosition;
            this.modified = true;
            return true;
        }




        public void RenderBox(Bounds bounds) { HandleRendering.RenderBox(matrix, bounds); }
        public void RenderBoxMeasurements(Bounds bounds) { HandleRendering.RenderBoxMeasurements(matrix, bounds); }
        public void RenderDistanceMeasurement(Vector3 from, Vector3 to) { Measurements.DrawLength(from, to); }
        public void RenderDistanceMeasurement(Vector3 from, Vector3 to, float forceValue) { Measurements.DrawLength(from, to, forceValue); }
        public void RenderCylinder(Bounds bounds, int segments) { HandleRendering.RenderCylinder(matrix, bounds, segments); }
        public void RenderShape(Curve2D shape, float height) { HandleRendering.RenderShape(matrix, shape, height); }

        public Color GetStateColor(Color baseColor, bool hasFocus, bool isBackfaced)
        {
            var nonSelectedColor = baseColor;
            if (isBackfaced) nonSelectedColor.a *= UnitySceneExtensions.SceneHandles.backfaceAlphaMultiplier;
            var focusColor = (hasFocus) ? UnitySceneExtensions.SceneHandles.selectedColor : nonSelectedColor;
            return disabled ? Color.Lerp(focusColor, UnitySceneExtensions.SceneHandles.staticColor, UnitySceneExtensions.SceneHandles.staticBlend) : focusColor;
        }

        public void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLine(from, to, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawContinuousLines(points, 0, points.Length, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawContinuousLines(points, 0, points.Length, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawContinuousLines(points, startIndex, length, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawContinuousLines(points, startIndex, length, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLineLoop(points, startIndex, length, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLineLoop(points, startIndex, length, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLineLoop(points, 0, points.Length, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLineLoop(points, 0, points.Length, lineMode, thickness, dashSize);
        }

        void RecordUndo(string undoMessage, params UnityEngine.Object[] targets)
        {
            if (targets == null ||
                targets.Length == 0)
                return;
            if (targets.Length == 1)
            {
                Undo.RecordObject(targets[0], undoMessage);
            } else
                Undo.RecordObjects(targets, undoMessage);
        }

        void RecordUndo(string undoMessage)
        {
            if (generator)
                RecordUndo(undoMessage ?? $"Modified {generator.ChiselNodeTypeName}", generator);
        }

        public bool DoBoundsHandle(ref Bounds bounds, Vector3? snappingSteps = null, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(bounds, Quaternion.identity, snappingSteps: snappingSteps);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            bounds = newBounds;
            this.modified = true;
            return true;
        }

        public bool DoShapeHandle(ref Curve2D shape, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newShape = UnitySceneExtensions.SceneHandles.Curve2DHandle(Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(90, Vector3.right), Vector3.one), shape);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            shape = newShape;
            this.modified = true;
            return true;
        }

        public bool DoShapeHandle(ref Curve2D shape, Matrix4x4 transformation, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newShape = UnitySceneExtensions.SceneHandles.Curve2DHandle(transformation, shape);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            shape = newShape;
            this.modified = true;
            return true;
        }

        public bool DoRadiusHandle(ref float outerRadius, Vector3 normal, Vector3 position, bool renderDisc = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newOuterRadius = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, position, outerRadius, renderDisc: !renderDisc);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            outerRadius = newOuterRadius;
            this.modified = true;
            return true;
        }

        public bool DoRadius2DHandle(ref Vector3 radius, Vector3 center, Vector3 up, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius = radius;
            this.lastHandleHadFocus = UnitySceneExtensions.SceneHandles.Radius2DHandle(center, up, ref newRadius, ref newRadius, minRadius, minRadius, maxRadius, maxRadius, renderDisc);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            radius = newRadius;
            this.modified = true;
            return true;
        }

        public bool DoRadius2DHandle(ref Vector3 radius1, ref Vector3 radius2, Vector3 center, Vector3 up, float minRadius1 = 0, float minRadius2 = 0, float maxRadius1 = float.PositiveInfinity, float maxRadius2 = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius1 = radius1;
            var newRadius2 = radius2;
            this.lastHandleHadFocus = UnitySceneExtensions.SceneHandles.Radius2DHandle(center, up, ref newRadius1, ref newRadius2, minRadius1, minRadius2, maxRadius1, maxRadius2, renderDisc);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            radius1 = newRadius1;
            radius2 = newRadius2;
            this.modified = true;
            return true;
        }

        internal static int s_DoRotatableLineHandleHash = "DoDirectionHandle".GetHashCode();
        public bool DoRotatableLineHandle(ref float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoDirectionHandleHash, FocusType.Passive);
            this.lastHandleHadFocus = focusControl == id;
            var prevColor = color;
            color = GetStateColor(color, lastHandleHadFocus, backfaced);
            var newAngle = RotatableLineHandle.DoHandle(angle, origin, diameter, handleDir, slideDir1, slideDir2);
            color = prevColor;
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            angle = newAngle;
            this.modified = true;
            return true;
        }

        public bool DoTurnHandle(ref Bounds bounds, string undoMessage = null)
        {
            if (!generator || !generatorTransform)
                return false;
            var min = new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z));
            var max = new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z));

            var center = (max + min) * 0.5f;

            switch (TurnHandle.DoHandle(bounds))
            {
                case TurnState.ClockWise:
                {
                    RecordUndo(undoMessage ?? "Rotated transform", generatorTransform, generator);
                    var newSize = bounds.size;
                    var t = newSize.x; newSize.x = newSize.z; newSize.z = t;
                    bounds.size = newSize;
                    GUI.changed = true;
                    this.modified = true;
                    /*
                    if (generator is ChiselGeneratorComponent)
                        generatorTransform.RotateAround(generatorTransform.TransformPoint(center + ((ChiselGeneratorComponent)generator).PivotOffset), generatorTransform.up, 90);
                    else*/
                    if (generator is ChiselGeneratorComponent)
                        generatorTransform.RotateAround(generatorTransform.TransformPoint(center + ((ChiselGeneratorComponent)generator).PivotOffset), generatorTransform.up, 90);
                    return true;
                }
                case TurnState.AntiClockWise:
                {
                    RecordUndo(undoMessage ?? "Rotated transform", generatorTransform, generator);
                    var newSize = bounds.size;
                    var t = newSize.x; newSize.x = newSize.z; newSize.z = t;
                    bounds.size = newSize;
                    GUI.changed = true;
                    this.modified = true;
                    /*
                    if (generator is ChiselGeneratorComponent)
                        generatorTransform.RotateAround(generatorTransform.TransformPoint(center + ((ChiselGeneratorComponent)generator).PivotOffset), generatorTransform.up, -90);
                    else*/
                    if (generator is ChiselGeneratorComponent)
                        generatorTransform.RotateAround(generatorTransform.TransformPoint(center + ((ChiselGeneratorComponent)generator).PivotOffset), generatorTransform.up, -90);
                    return true;
                }
            }
            return false;
        }

        public bool DoEdgeHandle1D(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            offset = SceneHandles.Edge1DHandle(axis, from, to, snappingStep, handleSize);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            this.modified = true;
            return true;
        }

        public bool DoEdgeHandle1DOffset(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            if (renderLine)
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, snappingStep, handleSize);
            else
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, snappingStep, handleSize, capFunction: null);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            this.modified = true;
            return true;
        }

        public bool DoEdgeHandle1DOffset(out Vector3 offset, Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            if (renderLine)
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, direction, snappingStep, handleSize);
            else
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, direction, snappingStep, handleSize, capFunction: null);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            this.modified = true;
            return true;
        }

        public bool DoPathPointHandle(ref ChiselPathPoint pathPoint, string undoMessage = null)
        {
            switch (Tools.current)
            {
                case Tool.Move:     return DoPositionHandle(ref pathPoint.position, pathPoint.rotation, undoMessage);
                case Tool.Rotate:   return DoRotationHandle(ref pathPoint.rotation, pathPoint.position, undoMessage);
                case Tool.Scale:    return DoPlanarScaleHandle(ref pathPoint.scale, pathPoint.position, pathPoint.rotation, undoMessage);
                default:
                {
                    // TODO: implement
                    return false;
                }
            }
        }

        public bool DoPositionHandle(ref Vector3 position, Quaternion rotation, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newPosition = UnitySceneExtensions.SceneHandles.PositionHandle(position, rotation);
            if (!EditorGUI.EndChangeCheck())
                return false;
            
            RecordUndo(undoMessage);
            position = newPosition;
            this.modified = true;
            return true;
        }

        public bool DoRotationHandle(ref Quaternion rotation, Vector3 position, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            if (Event.current.type == EventType.Repaint)
                UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(0, position, rotation, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, Event.current.type);
            var newRotation = Handles.RotationHandle(rotation, position);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            rotation = newRotation;
            this.modified = true;
            return true;
        }

        public bool DoPlanarScaleHandle(ref Vector2 scale2D, Vector3 position, Quaternion rotation, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            // TODO: create a 2D planar/bounds scale handle
            var scale3D = UnityEditor.Handles.ScaleHandle(new Vector3(scale2D.x, 1, scale2D.y), position, rotation, UnityEditor.HandleUtility.GetHandleSize(position));
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            scale2D = new Vector2(scale3D.x, scale3D.z);
            this.modified = true;
            return true;
        }
    }

    public sealed class ChiselComponentInspectorMessageHandler : IChiselMessageHandler
    {
        public void Warning(string message, Action buttonAction, string buttonText)
        {
            // TODO: prevent duplicates
            // TODO: show button to fix the problem
            EditorGUILayout.HelpBox(message, MessageType.Warning, true);
        }

        public void Warning(string message)
        {
            // TODO: prevent duplicates
            EditorGUILayout.HelpBox(message, MessageType.Warning, true);
        }
    }
}
