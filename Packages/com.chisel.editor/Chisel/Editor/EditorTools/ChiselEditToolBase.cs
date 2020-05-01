using Chisel.Components;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityObject = UnityEngine.Object;
 
namespace Chisel.Editors
{
    abstract class ChiselEditToolBase : EditorTool, IChiselToolMode
    {
        // Serialize this value to set a default value in the Inspector.
        [SerializeField] internal Texture2D m_ToolIcon = null;

        public abstract string ToolName { get; }

        public override GUIContent toolbarIcon { get { return cachedIconContent; } }
        
        GUIContent cachedIconContent = new GUIContent();
        public virtual GUIContent Content
        {
            get 
            {
                return new GUIContent()
                {
                    image = m_ToolIcon,
                    text = $"Chisel {ToolName} Tool",
                    tooltip = $"Chisel {ToolName} Tool"
                };
            }
        }

        public void OnEnable()
        {
            lastSelectedNode = null;
            ChiselOptionsOverlay.Register(this);
            ToolNotActivatingBugWorkAround(); 
            UpdateIcon();
            NotifyOnSelectionChanged();
        }

        public void Awake()
        {
            lastSelectedNode = null;
            ToolNotActivatingBugWorkAround();
            NotifyOnSelectionChanged();
        }

        public void UpdateIcon()
        {
            var newContent = Content;
            cachedIconContent.image     = newContent.image;
            cachedIconContent.text      = newContent.text;
            cachedIconContent.tooltip   = newContent.tooltip;
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

            ChiselOptionsOverlay.AdditionalSettings = ChiselEditGeneratorTool.DefaultSceneSettingsGUI;
            ChiselOptionsOverlay.SetTitle(Convert.ToString(Tools.current)); // TODO: cache these strings

            ChiselOptionsOverlay.Show();
            ChiselGridOptionsOverlay.Show();
        }


        static ChiselEditToolBase lastSelectedNode = null;

        public void ToolNotActivatingBugWorkAround()
        {
            if (lastSelectedNode == null)
            {
                if (EditorTools.activeToolType == this.GetType())
                {
                    OnActivate();
                    lastSelectedNode = this;
                }
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (lastSelectedNode == null ||
                lastSelectedNode != this)
            {
                if (lastSelectedNode != null)
                    lastSelectedNode.OnDeactivate();
                lastSelectedNode = this;
                OnActivate();
            }
            var sceneView = window as SceneView;
            var dragArea = sceneView.position;
            dragArea.position = Vector2.zero;

            ChiselOptionsOverlay.AdditionalSettings = null;
            ChiselOptionsOverlay.SetTitle(ToolName);
            OnSceneGUI(sceneView, dragArea);

            ChiselOptionsOverlay.Show();
            ChiselGridOptionsOverlay.Show();
        }

        public virtual void OnActivate()
        {
            lastSelectedNode = this;
        }

        public virtual void OnDeactivate()
        {
        } 

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
