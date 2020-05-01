using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace Chisel.Editors
{
    public class ChiselOverlay
    {
        public const int kMinWidth = 248 + 36;
        public static readonly GUILayoutOption kMinWidthLayout = GUILayout.MinWidth(kMinWidth);

        public delegate void WindowFunction(SceneView sceneView);
        delegate void InternalWindowFunction(UnityEngine.Object target, SceneView sceneView);

        const float kTopPadding = 22;
        const float kBottomPadding = 4;

        public GUIContent TitleContent { get { return title; } set { title.text = value.text; title.tooltip = value.tooltip; title.image = value.image; } }
        public string Title { get { return title.text; } set { title.text = value; } }

        readonly GUIContent title;
        readonly WindowFunction sceneViewFunc;

        void OuterWindowFunc(UnityEngine.Object target, SceneView sceneView)
        {
            if (!sceneView)
                return;

            var startRect = EditorGUILayout.GetControlRect(false, height: 0);
            sceneViewFunc(sceneView);
            var endRect = EditorGUILayout.GetControlRect(false, height: 0);
            switch (Event.current.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDown:
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == 0)
                    {
                        startRect.yMin -= kTopPadding;
                        endRect.yMin += kBottomPadding;
                        var rect = new Rect(startRect.xMin, startRect.yMin, startRect.width, endRect.y - startRect.y);
                        if (rect.Contains(Event.current.mousePosition))
                        {
                            Event.current.Use();
                        }
                    }
                    break;
                }
            }
        }

        public ChiselOverlay(string title, WindowFunction sceneViewFunc, int primaryOrder)
        {
            this.sceneViewFunc = sceneViewFunc;
            this.title = new GUIContent(title);
            Initialize(primaryOrder);
        }

        public ChiselOverlay(GUIContent title, WindowFunction sceneViewFunc, int primaryOrder)
        {
            this.sceneViewFunc = sceneViewFunc;
            this.title = title;
            Initialize(primaryOrder);
        }

        public void Show()
        {
            if (windowMethod != null)
                windowMethod.Invoke(null, windowMethod_parameters);
        }

        static Type s_SceneViewOverlayType;
        static Type s_WindowFunctionType;
        static object s_WindowMethod_parameter_overlay;
        static System.Reflection.MethodInfo windowMethod;
        static Type s_OverlayWindowType;

        object[] windowMethod_parameters;

        void Initialize(int primaryOrder)
        {
            if (windowMethod == null)
            { 
#if UNITY_2019_3
                s_OverlayWindowType     = ReflectionExtensions.GetTypeByName("UnityEditor.SceneViewOverlay+OverlayWindow");
#elif UNITY_2020_1_OR_NEWER
                s_OverlayWindowType = ReflectionExtensions.GetTypeByName("UnityEditor.OverlayWindow");
#endif
                s_WindowFunctionType = ReflectionExtensions.GetTypeByName("UnityEditor.SceneViewOverlay+WindowFunction");
                s_SceneViewOverlayType = ReflectionExtensions.GetTypeByName("UnityEditor.SceneViewOverlay");

                var windowDisplayOptionType = ReflectionExtensions.GetTypeByName("UnityEditor.SceneViewOverlay+WindowDisplayOption");
                s_WindowMethod_parameter_overlay = Enum.Parse(windowDisplayOptionType, "OneWindowPerTarget");

#if UNITY_2019_3
                windowMethod = s_SceneViewOverlayType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).FirstOrDefault(t => t.Name == "Window" && t.GetParameters().Length == 4);
#elif UNITY_2020_1_OR_NEWER  
                //public static void ShowWindow(OverlayWindow window)
                windowMethod = s_SceneViewOverlayType.GetMethod("ShowWindow", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
#endif
            }

            InternalWindowFunction outerWindowFunc = OuterWindowFunc;
            var sceneViewFuncDelegate = Delegate.CreateDelegate(s_WindowFunctionType, this, outerWindowFunc.Method);

#if UNITY_2019_3
            windowMethod_parameters = new object[]
            { 
                title, sceneViewFuncDelegate, primaryOrder, s_WindowMethod_parameter_overlay
            };
#elif UNITY_2020_1_OR_NEWER
            //public OverlayWindow(GUIContent title, SceneViewOverlay.WindowFunction guiFunction, int primaryOrder, Object target, SceneViewOverlay.WindowDisplayOption option)
            var overlayWindow = Activator.CreateInstance(s_OverlayWindowType, title, sceneViewFuncDelegate, primaryOrder, null, s_WindowMethod_parameter_overlay);
            windowMethod_parameters = new object[] { overlayWindow };
#endif
        }
    }
}
