using Chisel.Components;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnitySceneExtensions;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif
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
        public abstract string OptionsTitle { get; }

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
            lastSelectedTool = null;
            ChiselToolsOverlay.Register(this);
            EditorApplication.delayCall -= OnDelayedEnable;
            EditorApplication.delayCall += OnDelayedEnable;
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= OnDelayedEnable;
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
            NotifyOnSelectionChanged();
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

        public abstract void OnInSceneOptionsGUI(SceneView sceneView);


        static bool haveNodeSelection = false;

        public static void NotifyOnSelectionChanged()
        {
            haveNodeSelection = (Selection.GetFiltered<ChiselNode>(SelectionMode.Deep | SelectionMode.Editable).Length > 0);
        }

        public static void ShowDefaultOverlay()
        {
            if (!haveNodeSelection)
                return;

            ChiselOptionsOverlay.AdditionalSettings = null;

            ChiselOptionsOverlay.Show();
            ChiselToolsOverlay.Show();
            ChiselSnappingOptionsOverlay.Show();
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

            ChiselOptionsOverlay.AdditionalSettings = null;
            ChiselOptionsOverlay.SetTitle(OptionsTitle);

            OnSceneGUI(sceneView, dragArea);

            ChiselOptionsOverlay.Show();
            ChiselToolsOverlay.Show();
            ChiselSnappingOptionsOverlay.Show();
        }

        public virtual void OnActivate()
        {
            lastSelectedTool = this;
            lastRememberedToolType = this.GetType();
            UnitySceneExtensions.Snapping.SnapMask = ToolUsedSnappingModes;
        }

        public virtual void OnDeactivate()
        {
            UnitySceneExtensions.Snapping.SnapMask = UnitySceneExtensions.SnapSettings.All;
        } 

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
