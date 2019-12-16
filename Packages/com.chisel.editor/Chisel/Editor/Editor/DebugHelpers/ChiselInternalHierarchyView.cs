﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
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
        }
         
        public void OnSelectionChanged()
        {
            this.Repaint();
        }

        void OnDestroy()
        {
            windows.Remove(this);
        }

        Dictionary<int, bool> openNodes = new Dictionary<int, bool>();
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

        static int GetVisibleItems(Dictionary<int, CSGTreeNode[]> sceneHierarchies, ref Dictionary<int, bool> openNodes)
        {
            if (sceneHierarchies == null || sceneHierarchies.Count == 0)
                return 0;

            int totalCount = 0;
            foreach (var item in sceneHierarchies)
            {
                totalCount += 1; // scene foldout itself
                itemStack.Clear();
                totalCount += GetVisibleItems(item.Value, ref openNodes);
            }
            return totalCount;
        }
        
        static int GetVisibleItems(CSGTreeNode[] hierarchyItems, ref Dictionary<int, bool> openNodes)
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

                var nodeID = children[i].NodeID;
                bool isOpen;
                if (!openNodes.TryGetValue(nodeID, out isOpen))
                {
                    isOpen = true;
                    openNodes[nodeID] = true;
                }
                if (isOpen)
                {
                    var childCount = children[i].Count;
                    if (childCount > 0)
                    {
                        totalCount += childCount;
                        itemStack.Add(new StackItem(children[i].ChildrenToArray()));
                        goto ContinueOnNextStackItem;
                    }
                }
            }
            itemStack.RemoveAt(itemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, CSGTreeNode[] hierarchyItems, HashSet<int> selectedInstanceIDs, ref Dictionary<int, bool> openNodes)
        {
            if (hierarchyItems == null || hierarchyItems.Length == 0)
                return;

            var defaultColor = GUI.color;
            AddFoldOuts(ref itemRect, ref visibleArea, hierarchyItems, selectedInstanceIDs, defaultColor, ref openNodes);
            GUI.color = defaultColor;
        }

        static string NameForTreeNode(CSGTreeNode coreNode)
        {
            var userID = coreNode.UserID;
            var nodeID = coreNode.NodeID;
            var obj = (userID != 0) ? EditorUtility.InstanceIDToObject(userID) : null;
            string name;
            if (obj == null)
            {
                name = "<unknown>";
            } else
            {
                name =  obj.name;
            }
            return string.Format("{0} [{1}:{2}:{3}]", name, (nodeID-1), userID, coreNode.Type);
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, CSGTreeNode[] hierarchyItems, HashSet<int> selectedInstanceIDs, Color defaultColor, ref Dictionary<int, bool> openNodes)
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

                var nodeID		= children[i].NodeID;
                var userID		= children[i].UserID;
                var childCount	= children[i].Count;
                if (itemRect.y > visibleArea.yMin)
                {
                    var name			= NameForTreeNode(children[i]);
                    var selected		= selectedInstanceIDs.Contains(userID);
                    var labelStyle		= (childCount > 0) ?
                                            (selected ? styles.foldOutLabelSelected : styles.foldOutLabel) :
                                            (selected ? styles.emptyLabelSelected : styles.emptyLabelItem);


                    bool isOpen;
                    if (!openNodes.TryGetValue(nodeID, out isOpen))
                        openNodes[nodeID] = false;

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
                        openNodes[nodeID] = EditorGUI.Foldout(foldOutRect, isOpen, string.Empty, true, styles.foldOut);
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
                }
                itemRect.y += kItemHeight;

                if (openNodes[nodeID])
                {
                    if (childCount > 0)
                    {
                        itemStack.Add(new StackItem(children[i].ChildrenToArray(), itemRect.x + kItemIndent));
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
            
            var allNodes = CSGManager.AllTreeNodes;
            var allRootNodeList = new List<CSGTreeNode>();
            for (int i = 0; i < allNodes.Length;i++)
            {
                if (allNodes[i].Type != CSGNodeType.Tree && 
                    (allNodes[i].Tree .Valid || allNodes[i].Parent.Valid))
                    continue;
                
                allRootNodeList.Add(allNodes[i]); 
            }

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
                    var brush		= obj as ChiselBrush;
                    var operation	= obj as ChiselOperation;
                    var model		= obj as ChiselModel;
                    int nodeID = CSGTreeNode.InvalidNode.NodeID;
                    if      (brush    ) nodeID = brush.TopNode.NodeID;
                    else if (operation) nodeID = operation.Node.NodeID;
                    else if (model    ) nodeID = model.Node.NodeID;
                    else
                    {
                        for (int n = 0; n < allNodes.Length; n++)
                        {
                            if (allNodes[n].UserID == instanceID)
                            {
                                nodeID = allNodes[n].NodeID;
                                break;
                            }
                        }
                    }

                    if (nodeID != CSGTreeNode.InvalidNode.NodeID)
                    {
                        var labelArea = itemArea;
                        labelArea.x = 0;
                        labelArea.y = labelArea.height;
                        labelArea.height = kItemHeight;
                        CSGTreeNode node = CSGTreeNode.Encapsulate(nodeID);
                        GUI.Label(labelArea, "NodeID: " + (nodeID - 1)); labelArea.y += kItemHeight;
                        GUI.Label(labelArea, "UserID: " + node.UserID); labelArea.y += kItemHeight;
                        GUI.Label(labelArea, "Operation: " + node.Operation); labelArea.y += kItemHeight;
                        GUI.Label(labelArea, "Valid: " + node.Valid); labelArea.y += kItemHeight;
                        GUI.Label(labelArea, "NodeType: " + node.Type); labelArea.y += kItemHeight;
                        GUI.Label(labelArea, "ChildCount: " + node.Count); labelArea.y += kItemHeight;
                        if (node.Type != CSGNodeType.Tree)
                        { 
                            GUI.Label(labelArea, "Parent: " + (node.Parent.NodeID - 1) + " valid: " + node.Parent.Valid); labelArea.y += kItemHeight;
                            GUI.Label(labelArea, "Model: " + (node.Tree.NodeID - 1) + " valid: " + node.Tree.Valid); labelArea.y += kItemHeight;
                        }
                        if (node.Type == CSGNodeType.Brush)
                        {
                            var treeBrush = (CSGTreeBrush)node;
                            var brushMeshInstance = treeBrush.BrushMesh;
                            GUI.Label(labelArea, "BrushMeshInstance: " + brushMeshInstance.BrushMeshID + " valid: " + brushMeshInstance.Valid); labelArea.y += kItemHeight;
                        }
                    }
                }
            }
        }
    }
#endif
}

