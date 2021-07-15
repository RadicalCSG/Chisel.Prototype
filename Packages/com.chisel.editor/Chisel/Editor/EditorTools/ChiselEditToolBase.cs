using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnitySceneExtensions;
using UnityObject = UnityEngine.Object;
 
namespace Chisel.Editors
{
    abstract class ChiselEditToolBase : EditorTool, IChiselToolMode
    {
        // Serialize this value to set a default value in the Inspector.
        [SerializeField] internal Texture2D m_ToolIcon = null;
        [SerializeField] internal Texture2D m_ToolIconActive = null;
        [SerializeField] internal Texture2D m_ToolIconDark = null;
        [SerializeField] internal Texture2D m_ToolIconDarkActive = null;

        public abstract string ToolName { get; }
        
        public abstract SnapSettings ToolUsedSnappingModes { get; }

        public Texture2D Icon
        {
            get
            {
                var icon = m_ToolIcon;
                if (EditorGUIUtility.isProSkin)
                    icon = m_ToolIconDark;
                return icon;
            }
        }

        public Texture2D ActiveIcon
        {
            get
            {
                var icon = m_ToolIconActive;
                if (EditorGUIUtility.isProSkin)
                    icon = m_ToolIconDarkActive;
                return icon;
            }
        }

        public override GUIContent toolbarIcon { get { return cachedToolbarContent; } }

        GUIContent cachedToolbarContent = new GUIContent();
        public virtual GUIContent Content
        {
            get 
            {
                return new GUIContent()
                {
                    image   = Icon,
                    text    = $"Chisel {ToolName} Tool",
                    tooltip = $"Chisel {ToolName} Tool"
                };
            }
        }

        public GUIContent IconContent { get; private set; } = new GUIContent();
        public GUIContent ActiveIconContent { get; private set; } = new GUIContent();

        public void OnEnable()
        {
            ChiselPlacementTool.Register(this);
            lastSelectedTool = null; 
            EditorApplication.delayCall -= OnDelayedEnable;
            EditorApplication.delayCall += OnDelayedEnable;
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= OnDelayedEnable;
            if (lastSelectedTool != null)
                lastSelectedTool.OnDeactivate();
        }

        public void Awake()
        {
            lastSelectedTool = null;
        }

        // Unity bug workaround
        void OnDelayedEnable()
        {
            EditorApplication.delayCall -= OnDelayedEnable;

            ToolNotActivatingBugWorkAround();
            UpdateIcon();
            SceneView.RepaintAll();
        }

        public void UpdateIcon()
        {
            var newContent = Content;
            cachedToolbarContent.image     = newContent.image;
            cachedToolbarContent.text      = newContent.text;
            cachedToolbarContent.tooltip   = newContent.tooltip;

            {
                var iconContent = IconContent;
                iconContent.image       = Icon;
                iconContent.tooltip     = ToolName;
            }

            {
                var activeIconContent = ActiveIconContent;
                activeIconContent.image     = ActiveIcon;
                activeIconContent.tooltip   = ToolName;
            }
        }


        static ChiselEditToolBase lastSelectedTool = null;
        static Type lastRememberedToolType = null;

        public static void ClearLastRememberedType()
        { 
            lastRememberedToolType = null; 
        }

        public void ToolNotActivatingBugWorkAround()
        {
            if (lastSelectedTool == null)
            {
                if (Tools.current != Tool.Custom &&
                    lastRememberedToolType != null)
                {
                    ToolManager.SetActiveTool(lastRememberedToolType);
                    lastRememberedToolType = null;
                } else
                if (ToolManager.activeToolType == this.GetType())
                {
                    OnActivate();
                }
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            var matrix = Handles.matrix;
            if (lastSelectedTool == null ||
                lastSelectedTool != this)
            {
                if (lastSelectedTool != null)
                    lastSelectedTool.OnDeactivate();
                OnActivate();
            }
            var sceneView = window as SceneView;
            var dragArea = sceneView.position;
            dragArea.position = Vector2.zero;

            OnSceneGUI(sceneView, dragArea);

            Handles.matrix = matrix;
        }

        public virtual void OnActivate()
        {
            lastSelectedTool = this;
            lastRememberedToolType = this.GetType();
            UnitySceneExtensions.Snapping.SnapMask = ToolUsedSnappingModes;
            SnapSettingChanged?.Invoke();
        }

        public virtual void OnDeactivate()
        {
            UnitySceneExtensions.Snapping.SnapMask = UnitySceneExtensions.SnapSettings.All;
            SnapSettingChanged?.Invoke();
        }

        public static event Action SnapSettingChanged;

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
