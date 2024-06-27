using System.Collections.Generic;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using Unity.Collections;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
// TODO: rebuild this using new API / proper treeview
#if true
    // This window is a helper window to see what the CSG tree looks like internally
    sealed class ChiselInternalHierarchyView : EditorWindow
    {
        ChiselInternalHierarchyView()
        {
            windows.Add(this);
        }

        public void Awake()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            this.Repaint();
        }

        public void OnSelectionChanged()
        {
            this.Repaint();
        }

        void OnDestroy()
        {
            windows.Remove(this);
        }

        Dictionary<CSGTreeNode, bool> openNodes = new Dictionary<CSGTreeNode, bool>();
        static List<ChiselInternalHierarchyView> windows = new List<ChiselInternalHierarchyView>();

        public static void RepaintAll()
        {
            foreach (var window in windows)
            {
                if (window)
                    window.Repaint();
            }
        }

        [MenuItem("Chisel DEBUG/Internal Chisel Hierarchy")]
        static void Create()
        {
            window = (ChiselInternalHierarchyView)EditorWindow.GetWindow(typeof(ChiselInternalHierarchyView), false, "Internal Chisel Hierarchy");
            window.autoRepaintOnSceneChange = true;
        }

        static ChiselInternalHierarchyView window;

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
            styles.foldOut.focused	= styles.foldOut.normal;
            styles.foldOut.active	= styles.foldOut.normal;
            styles.foldOut.onNormal = styles.foldOut.normal;
            styles.foldOut.onActive = styles.foldOut.normal;

            styles.foldOutSelected = new GUIStyle(EditorStyles.foldout);
            styles.foldOutSelected.normal = styles.foldOutSelected.active;
            styles.foldOutSelected.onNormal = styles.foldOutSelected.onActive;



            styles.foldOutLabel = new GUIStyle(EditorStyles.label);
            styles.foldOutLabel.active		= styles.foldOutLabel.normal;
            styles.foldOutLabel.onActive	= styles.foldOutLabel.onNormal;

            styles.foldOutLabelSelected				= new GUIStyle(EditorStyles.label);
            styles.foldOutLabelSelected.normal		= styles.foldOutLabelSelected.active;
            styles.foldOutLabelSelected.onNormal	= styles.foldOutLabelSelected.onActive;

            styles.backGroundColor = styles.foldOutLabelSelected.onNormal.textColor;
            styles.backGroundColor.a = 0.5f;
            
            GUIStyleState selected = styles.foldOutLabelSelected.normal;
            selected.textColor = Color.white;
            styles.foldOutSelected.normal		= selected;
            styles.foldOutSelected.onNormal		= selected;
            styles.foldOutSelected.active		= selected;
            styles.foldOutSelected.onActive		= selected;
            styles.foldOutSelected.focused		= selected;
            styles.foldOutSelected.onFocused	= selected;

            styles.foldOutLabelSelected.normal		= selected;
            styles.foldOutLabelSelected.onNormal	= selected;
            styles.foldOutLabelSelected.active		= selected;
            styles.foldOutLabelSelected.onActive	= selected;
            styles.foldOutLabelSelected.focused		= selected;
            styles.foldOutLabelSelected.onFocused	= selected;

            styles.emptyLabelSelected.normal = selected;
            styles.emptyLabelSelected.onNormal = selected;
            styles.emptyLabelSelected.active = selected;
            styles.emptyLabelSelected.onActive = selected;
            styles.emptyLabelSelected.focused = selected;
            styles.emptyLabelSelected.onFocused = selected;




            styles.emptyItem.active = styles.emptyItem.normal;
            styles.emptyItem.onActive = styles.emptyItem.onNormal;
        }

        const int kScrollWidth = 20;
        const int kItemIndent = 20;
        const int kIconWidth = 20;
        const int kPadding = 2;
        static Vector2 m_ScrollPos;

        sealed class StackItem
        {
            public StackItem(CSGTreeNode[] _children, float _xpos = 0) { children = _children; index = 0; count = children.Length; xpos = _xpos; }
            public int index;
            public int count;
            public float xpos;
            public CSGTreeNode[] children;
        }
        static List<StackItem>  itemStack = new List<StackItem>();

        static int GetVisibleItems(CSGTreeNode[] hierarchyItems, ref Dictionary<CSGTreeNode, bool> openNodes)
        {
            if (hierarchyItems == null)
                return 0;

            int totalCount = hierarchyItems.Length;
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

                var child = children[i];
                bool isOpen;
                if (!openNodes.TryGetValue(child, out isOpen))
                {
                    isOpen = true;
                    openNodes[child] = true;
                }
                if (isOpen)
                {
                    if (children[i].Valid)
                    {
                        var childCount = children[i].Count;
                        if (childCount > 0)
                        {
                            totalCount += childCount;
                            itemStack.Add(new StackItem(ChildrenToArray(children[i])));
                            goto ContinueOnNextStackItem;
                        }
                    }
                }
            }
            itemStack.RemoveAt(itemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static CSGTreeNode[] ChildrenToArray(CSGTreeNode node)
        {
            var children = new CSGTreeNode[node.Count];
            for (int i = 0; i < node.Count; i++)
                children[i] = node[i];
            return children;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, CSGTreeNode[] hierarchyItems, HashSet<int> selectedInstanceIDs, ref Dictionary<CSGTreeNode, bool> openNodes)
        {
            if (hierarchyItems == null || hierarchyItems.Length == 0)
                return;

            var defaultColor = GUI.color;
            AddFoldOuts(ref itemRect, ref visibleArea, hierarchyItems, selectedInstanceIDs, defaultColor, ref openNodes);
            GUI.color = defaultColor;
        }

        static string NameForTreeNode(CSGTreeNode treeNode)
        {
            var userID = treeNode.UserID;
            var obj = (userID != 0) ? EditorUtility.InstanceIDToObject(userID) : null;
            string name;
            if (obj == null)
            {
                name = "<unknown>";
            } else
            {
                name =  obj.name;
            }
            if (treeNode.Type == CSGNodeType.Brush)
            {
                var brush = (CSGTreeBrush)treeNode;
                if (treeNode.Valid)
                    return $"{name} [{treeNode}:{userID}:{brush.BrushMesh.BrushMeshID}]";
                else
                    return $"{name} [{treeNode}:{userID}:{brush.BrushMesh.BrushMeshID}] (INVALID)";
            } else
            {
                if (treeNode.Valid)
                    return $"{name} [{treeNode}:{userID}]";
                else
                    return $"{name} [{treeNode}:{userID}] (INVALID)";
            }
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, CSGTreeNode[] hierarchyItems, HashSet<int> selectedInstanceIDs, Color defaultColor, ref Dictionary<CSGTreeNode, bool> openNodes)
        {
            if (hierarchyItems == null)
                return;
            itemStack.Add(new StackItem(hierarchyItems, itemRect.x));

            ContinueOnNextStackItem:
            if (itemStack.Count == 0)
            {
                return;
            }

            float kItemHeight = EditorGUIUtility.singleLineHeight;

            var prevColor = GUI.color;
            var prevBackgroundColor = GUI.backgroundColor;
            var currentStackItem = itemStack[itemStack.Count - 1];
            var children = currentStackItem.children;
            itemRect.x = currentStackItem.xpos;
            while (currentStackItem.index < currentStackItem.count)
            {
                int i = currentStackItem.index;
                currentStackItem.index++;
                if (itemRect.y > visibleArea.yMax)
                {
                    GUI.backgroundColor = prevBackgroundColor;
                    return;
                }

                var child       = children[i];
                var userID		= child.UserID;
                var childCount	= child.Count;
                if (itemRect.y > visibleArea.yMin)
                {
                    var name			= NameForTreeNode(child);
                    var selected		= selectedInstanceIDs.Contains(userID);
                    var labelStyle		= (childCount > 0) ?
                                            (selected ? styles.foldOutLabelSelected : styles.foldOutLabel) :
                                            (selected ? styles.emptyLabelSelected : styles.emptyLabelItem);


                    bool isOpen;
                    if (!openNodes.TryGetValue(child, out isOpen))
                        openNodes[child] = false;

                    const float labelOffset = 14;

                    if (selected)
                    {
                        GUI.backgroundColor = styles.backGroundColor;
                        var extended = itemRect;
                        extended.x = 0;
                        GUI.Box(extended, GUIContent.none);
                    } else
                        GUI.backgroundColor = prevBackgroundColor;
                    EditorGUI.BeginChangeCheck();
                    var foldOutRect = itemRect;
                    foldOutRect.width = labelOffset;
                    var labelRect = itemRect;
                    labelRect.x += labelOffset;
                    labelRect.width -= labelOffset;
                    if (childCount > 0)
                        openNodes[child] = EditorGUI.Foldout(foldOutRect, isOpen, string.Empty, true, styles.foldOut);

                    if (!child.Valid)
                        GUI.color = Color.red;

                    if (EditorGUI.EndChangeCheck() ||
                        GUI.Button(labelRect, name, labelStyle))
                    {
                        var obj = EditorUtility.InstanceIDToObject(userID);
                        if (!(obj is GameObject))
                        {
                            var mono = (obj as MonoBehaviour);
                            if (mono)
                                userID = mono.gameObject.GetInstanceID();
                        }
                        Selection.instanceIDs = new[] { userID };
                    }
                    if (!child.Valid)
                        GUI.color = prevColor;
                }
                itemRect.y += kItemHeight;

                if (openNodes[child])
                {
                    if (childCount > 0)
                    {
                        itemStack.Add(new StackItem(ChildrenToArray(child), itemRect.x + kItemIndent));
                        goto ContinueOnNextStackItem;
                    }
                }
            }
            itemStack.RemoveAt(itemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }


        void OnGUI()
        {
            if (styles == null)
                UpdateStyles();
            
            var selectedInstanceIDs = new HashSet<int>();

            foreach (var instanceID in Selection.instanceIDs)
            {
                var obj = EditorUtility.InstanceIDToObject(instanceID);
                var go = obj as GameObject;
                if (go != null)
                {
                    foreach(var no in go.GetComponents<ChiselNode>())
                    {
                        var instanceID_ = no.GetInstanceID();
                        selectedInstanceIDs.Add(instanceID_);
                    }
                }
            }
            
            float kItemHeight = EditorGUIUtility.singleLineHeight;

            using (var allTreeNodes = new NativeList<CSGTreeNode>(Allocator.Temp))
            { 
                CompactHierarchyManager.GetAllTreeNodes(allTreeNodes);

                using (var allTrees = new NativeList<CSGTree>(Allocator.Temp))
                {
                    CompactHierarchyManager.GetAllTrees(allTrees);
                    var allRootNodeList = new List<CSGTreeNode>();
                    for (int i = 0; i < allTrees.Length;i++)
                        allRootNodeList.Add(allTrees[i]); 

                    var allRootNodes = allRootNodeList.ToArray();

                    var totalCount = GetVisibleItems(allRootNodes, ref openNodes); 

                    var itemArea = position;
                    itemArea.x = 0;
                    itemArea.y = 0;
                    itemArea.height -= 200;

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
                
                        AddFoldOuts(ref itemRect, ref visibleArea, allRootNodes, selectedInstanceIDs, ref openNodes);
                    }
                    GUI.EndScrollView();
                    if (selectedInstanceIDs.Count == 1)
                    {
                        var instanceID = selectedInstanceIDs.First();
                        var obj = EditorUtility.InstanceIDToObject(instanceID) as ChiselNode;
                        if (obj)
                        {
                            var brush = obj as ChiselBrushComponent;
                            var composite = obj as ChiselComposite;
                            var model = obj as ChiselModel;
                            CSGTreeNode node = CSGTreeNode.Invalid;
                            if (brush) node = brush.TopTreeNode;
                            else if (composite) node = composite.Node;
                            else if (model) node = model.Node;
                            else
                            {
                                for (int n = 0; n < allTreeNodes.Length; n++)
                                {
                                    if (allTreeNodes[n].UserID == instanceID)
                                    {
                                        node = allTreeNodes[n];
                                        break;
                                    }
                                }
                            }

                            if (node != CSGTreeNode.Invalid)
                            {
                                var labelArea = itemArea;
                                labelArea.x = 0;
                                labelArea.y = labelArea.height;
                                labelArea.height = kItemHeight;
                                GUI.Label(labelArea, $"Node: {node}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"UserID: {node.UserID}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"Operation: {node.Operation}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"Valid: {node.Valid}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"NodeType: {node.Type}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"ChildCount: {node.Count}"); labelArea.y += kItemHeight;
                                if (node.Type != CSGNodeType.Tree)
                                {
                                    GUI.Label(labelArea, $"Parent: {node.Parent} valid: {node.Parent.Valid}"); labelArea.y += kItemHeight;
                                    GUI.Label(labelArea, $"Model: {node.Tree} valid: {node.Tree.Valid}"); labelArea.y += kItemHeight;
                                }
                                if (node.Type == CSGNodeType.Brush)
                                {
                                    var treeBrush = (CSGTreeBrush)node;
                                    var brushMeshInstance = treeBrush.BrushMesh;
                                    GUI.Label(labelArea, $"BrushMeshInstance: {brushMeshInstance.BrushMeshID} valid: {brushMeshInstance.Valid}"); labelArea.y += kItemHeight;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
#endif
}

