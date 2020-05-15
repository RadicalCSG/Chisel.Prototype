using Chisel.Components;
using System;
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
            ToolNotActivatingBugWorkAround(); 
            UpdateIcon();
            NotifyOnSelectionChanged();
        }

        public void Awake()
        {
            lastSelectedTool = null;
            ToolNotActivatingBugWorkAround();
            NotifyOnSelectionChanged();
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

        public abstract void OnSceneSettingsGUI(SceneView sceneView);


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

        public void ToolNotActivatingBugWorkAround()
        {
            if (lastSelectedTool == null)
            {
                if (EditorTools.activeToolType == this.GetType())
                {
                    OnActivate();
                    lastSelectedTool = this;
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
                lastSelectedTool = this;
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
            UnitySceneExtensions.Snapping.SnapMask = ToolUsedSnappingModes;
        }

        public virtual void OnDeactivate()
        {
            UnitySceneExtensions.Snapping.SnapMask = UnitySceneExtensions.SnapSettings.All;
        } 

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
