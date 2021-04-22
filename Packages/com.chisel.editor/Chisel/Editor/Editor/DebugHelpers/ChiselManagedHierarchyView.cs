using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chisel.Components;

namespace Chisel.Editors
{
    // This window is a helper window to see what the CSG tree looks like internally, on the managed side
    sealed class ChiselManagedHierarchyView : EditorWindow
    {
        ChiselManagedHierarchyView()
        {
            windows.Add(this);
        }

        void OnDestroy()
        {
            windows.Remove(this);
        }

        static List<ChiselManagedHierarchyView> windows = new List<ChiselManagedHierarchyView>();

        public static void RepaintAll()
        {
            // Prevent infinite loops
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
                return;
            foreach (var window in windows)
            {
                if (window)
                    window.Repaint();
            }
        }

        [MenuItem("Chisel DEBUG/Managed Chisel Hierarchy")]
        static void Create()
        {
            window = (ChiselManagedHierarchyView)EditorWindow.GetWindow(typeof(ChiselManagedHierarchyView), false, "Managed Chisel Hierarchy");
            window.autoRepaintOnSceneChange = true;
        }

        static ChiselManagedHierarchyView window;

        class Styles
        {
            public GUIStyle emptyItem;
            public GUIStyle emptySelected;
            public GUIStyle foldOut;
            public GUIStyle foldOutSelected;

            public GUIStyle emptyLabelItem;
            public GUIStyle emptyLabelSelected;
            public GUIStyle foldOutLabel;
            public GUIStyle foldOutLabelSelected;

            public Color backGroundColor;
        };

        static Styles styles;

        static void UpdateStyles()
        {
            styles = new Styles();
            styles.emptyItem = new GUIStyle(EditorStyles.foldout);

            styles.emptyItem.active.background = null;
            styles.emptyItem.hover.background = null;
            styles.emptyItem.normal.background = null;
            styles.emptyItem.focused.background = null;

            styles.emptyItem.onActive.background = null;
            styles.emptyItem.onHover.background = null;
            styles.emptyItem.onNormal.background = null;
            styles.emptyItem.onFocused.background = null;

            styles.emptySelected = new GUIStyle(styles.emptyItem);
            styles.emptySelected.normal = styles.emptySelected.active;
            styles.emptySelected.onNormal = styles.emptySelected.onActive;


            styles.emptyLabelItem = new GUIStyle(EditorStyles.label);
            styles.emptyLabelSelected = new GUIStyle(styles.emptyLabelItem);
            styles.emptyLabelSelected.normal = styles.emptyLabelSelected.active;
            styles.emptyLabelSelected.onNormal = styles.emptyLabelSelected.onActive;


            styles.foldOut = new GUIStyle(EditorStyles.foldout);
            styles.foldOut.focused = styles.foldOut.normal;
            styles.foldOut.active = styles.foldOut.normal;
            styles.foldOut.onNormal = styles.foldOut.normal;
            styles.foldOut.onActive = styles.foldOut.normal;

            styles.foldOutSelected = new GUIStyle(EditorStyles.foldout);
            styles.foldOutSelected.normal = styles.foldOutSelected.active;
            styles.foldOutSelected.onNormal = styles.foldOutSelected.onActive;



            styles.foldOutLabel = new GUIStyle(EditorStyles.label);
            styles.foldOutLabel.active = styles.foldOutLabel.normal;
            styles.foldOutLabel.onActive = styles.foldOutLabel.onNormal;

            styles.foldOutLabelSelected = new GUIStyle(EditorStyles.label);
            styles.foldOutLabelSelected.normal = styles.foldOutLabelSelected.active;
            styles.foldOutLabelSelected.onNormal = styles.foldOutLabelSelected.onActive;

            styles.backGroundColor = styles.foldOutLabelSelected.onNormal.textColor;
            styles.backGroundColor.a = 0.5f;

            GUIStyleState selected = styles.foldOutLabelSelected.normal;
            selected.textColor = Color.white;
            styles.foldOutSelected.normal = selected;
            styles.foldOutSelected.onNormal = selected;
            styles.foldOutSelected.active = selected;
            styles.foldOutSelected.onActive = selected;
            styles.foldOutSelected.focused = selected;
            styles.foldOutSelected.onFocused = selected;

            styles.foldOutLabelSelected.normal = selected;
            styles.foldOutLabelSelected.onNormal = selected;
            styles.foldOutLabelSelected.active = selected;
            styles.foldOutLabelSelected.onActive = selected;
            styles.foldOutLabelSelected.focused = selected;
            styles.foldOutLabelSelected.onFocused = selected;

            styles.emptyLabelSelected.normal = selected;
            styles.emptyLabelSelected.onNormal = selected;
            styles.emptyLabelSelected.active = selected;
            styles.emptyLabelSelected.onActive = selected;
            styles.emptyLabelSelected.focused = selected;
            styles.emptyLabelSelected.onFocused = selected;




            styles.emptyItem.active = styles.emptyItem.normal;
            styles.emptyItem.onActive = styles.emptyItem.onNormal;
        }


        const int kItemHeight  = 20;
        const int kScrollWidth = 20;
        const int kItemIndent = 20;
        const int kIconWidth = 20;
        const int kPadding = 2;
        static Vector2 m_ScrollPos;

        sealed class StackItem
        {
            public StackItem(List<ChiselHierarchyItem> _children, float _xpos = 0) { children = _children; index = 0; count = children.Count; xpos = _xpos; }
            public int index;
            public int count;
            public float xpos;
            public List<ChiselHierarchyItem> children;
        }
        static List<StackItem>  itemStack = new List<StackItem>();

        static int GetVisibleItems(Dictionary<Scene, ChiselSceneHierarchy> sceneHierarchies)
        {
            if (sceneHierarchies == null || sceneHierarchies.Count == 0)
                return 0;

            int totalCount = 0;
            foreach (var item in sceneHierarchies)
            {
                totalCount += 1; // scene foldout itself
                itemStack.Clear();
                totalCount += GetVisibleItems(item.Value.RootItems);
            }
            return totalCount;
        }
        
        static int GetVisibleItems(List<ChiselHierarchyItem> hierarchyItems)
        {
            if (hierarchyItems == null)
                return 0;

            int totalCount = hierarchyItems.Count;
            itemStack.Add(new StackItem(hierarchyItems));

            ContinueOnNextStackItem:
            if (itemStack.Count == 0)
                return totalCount;

            var currentStackItem = itemStack[itemStack.Count - 1];
            var children = currentStackItem.children;

            while (currentStackItem.index < currentStackItem.count)
            {
                int i = currentStackItem.index;
                currentStackItem.index++;
                if (children[i] == null)
                    continue;

                if (children[i].IsOpen && children[i].Children != null && children[i].Children.Count > 0)
                {
                    totalCount += children[i].Children.Count;
                    itemStack.Add(new StackItem(children[i].Children));
                    //totalCount += GetVisibleItems(hierarchyItems[i].Children);
                    goto ContinueOnNextStackItem;
                }
            }
            itemStack.RemoveAt(itemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, HashSet<Transform> selectedTransforms, Dictionary<Scene, ChiselSceneHierarchy> sceneHierarchies)
        {
            if (sceneHierarchies == null || sceneHierarchies.Count == 0)
                return;

            var defaultColor = GUI.color;
            foreach (var item in sceneHierarchies)
            {
                var scene = item.Key;
                if (itemRect.Overlaps(visibleArea))
                {
                    var name = scene.name;
                    if (string.IsNullOrEmpty(name))
                        EditorGUI.LabelField(itemRect, "Untitled");
                    else
                        EditorGUI.LabelField(itemRect, name);
                }
                itemRect.y += kItemHeight;
                itemRect.x += kItemIndent;
                itemStack.Clear();
                AddFoldOuts(ref itemRect, ref visibleArea, selectedTransforms, item.Value.RootItems);
                itemRect.x -= kItemIndent;
            }
            GUI.color = defaultColor;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, HashSet<Transform> selectedTransforms, List<ChiselHierarchyItem> hierarchyItems)
        {
            if (hierarchyItems == null)
                return;
            itemStack.Add(new StackItem(hierarchyItems, itemRect.x));

            ContinueOnNextStackItem:
            if (itemStack.Count == 0)
                return;

            var prevBackgroundColor = GUI.backgroundColor;
            var currentStackItem = itemStack[itemStack.Count - 1];
            var children = currentStackItem.children;
            itemRect.x = currentStackItem.xpos;
            while (currentStackItem.index < currentStackItem.count)
            {
                int i = currentStackItem.index; currentStackItem.index++;
                if (itemRect.y > visibleArea.yMax)
                {
                    GUI.backgroundColor = prevBackgroundColor;
                    return;
                }
                if (itemRect.y > visibleArea.yMin)
                {
                    var name = NameForTreeNode(children[i]);
                    var childCount = (children[i].Children == null) ? 0 : children[i].Children.Count;
                    var selected = selectedTransforms.Contains(children[i].Transform);

                    var foldOutStyle = (childCount > 0) ? styles.foldOut : styles.emptyItem;
                    var labelStyle = (childCount > 0) ?
                                            (selected ? styles.foldOutLabelSelected : styles.foldOutLabel) :
                                            (selected ? styles.emptyLabelSelected : styles.emptyLabelItem);

                    //				GUI.enabled = children[i].Enabled;

                    const float labelOffset = 14;

                    if (selected)
                    {
                        GUI.backgroundColor = styles.backGroundColor;
                        var extended = itemRect;
                        extended.x = 0;
                        extended.height -= 4;
                        GUI.Box(extended, GUIContent.none);
                    } else
                        GUI.backgroundColor = prevBackgroundColor;
                    EditorGUI.BeginChangeCheck();
                    var foldOutRect = itemRect;
                    foldOutRect.width = labelOffset;
                    var labelRect = itemRect;
                    labelRect.x += labelOffset;
                    labelRect.width -= labelOffset;
                    children[i].IsOpen = EditorGUI.Foldout(foldOutRect, children[i].IsOpen, string.Empty, true, foldOutStyle);
                    if (EditorGUI.EndChangeCheck() ||
                        GUI.Button(labelRect, name, labelStyle))
                    {
                        Selection.activeTransform = children[i].Transform;
                    }
                }
                itemRect.y += kItemHeight;

                if (children[i].IsOpen && children[i].Children != null && children[i].Children.Count > 0)
                {
                    itemStack.Add(new StackItem(children[i].Children, itemRect.x + kItemIndent));
                    goto ContinueOnNextStackItem;
                }
            }
            itemStack.RemoveAt(itemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static string NameForTreeNode(ChiselHierarchyItem node)
        {
            var nodeID = node.Component.TopNode.NodeID;
            var instanceID = node.Component.GetInstanceID();
            var obj = node.Transform;
            if (!obj)
                return string.Format("<unknown> [{0}:{1}]", nodeID, instanceID);
            return obj.name + string.Format(" [{0}:{1}]", nodeID, instanceID);
        }

        void OnGUI()
        {
            ChiselNodeHierarchyManager.Update();
            if (styles == null)
                UpdateStyles();

            var selectedTransforms = new HashSet<Transform>();
            foreach (var transform in Selection.transforms)
                selectedTransforms.Add(transform);

            var totalCount = GetVisibleItems(ChiselNodeHierarchyManager.sceneHierarchies);

            var itemArea = position;
            itemArea.x = 0;
            itemArea.y = 0;

            var totalRect = position;
            totalRect.x = 0;
            totalRect.y = 0;
            totalRect.width = position.width - kScrollWidth;
            totalRect.height = (totalCount * kItemHeight) + (2 * kPadding);

            var itemRect = position;
            itemRect.x = 0;
            itemRect.y = kPadding;
            itemRect.height = kItemHeight;

            m_ScrollPos = GUI.BeginScrollView(itemArea, m_ScrollPos, totalRect);
            {
                Rect visibleArea = itemArea;
                visibleArea.x += m_ScrollPos.x;
                visibleArea.y += m_ScrollPos.y;

                AddFoldOuts(ref itemRect, ref visibleArea, selectedTransforms, ChiselNodeHierarchyManager.sceneHierarchies);
            }
            GUI.EndScrollView();
        }

    }
}