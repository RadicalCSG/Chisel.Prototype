using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Chisel.Editors
{ 
    // This window is a helper window to see the UNDO stack, to help debug/test undo operations
    public sealed class HistoryWindow : EditorWindow
    {
        [MenuItem("CSG DEBUG/Undo Window")]
        static void Create()
        {
            window = (HistoryWindow)EditorWindow.GetWindow(typeof(HistoryWindow), false, "History");
            window.autoRepaintOnSceneChange = true;
        }

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            Undo.undoRedoPerformed			+= UndoRedoPerformed;
            Selection.selectionChanged		+= SelectionChanged;
            Undo.postprocessModifications	+= PostprocessModifications;

            // internal static void GetRecords(List<string> undoRecords, List<string> redoRecords)
            GetRecords_method = typeof(Undo).GetMethod("GetRecords", BindingFlags.NonPublic | BindingFlags.Static,
                                                null,
                                                new Type[] {
                                                    typeof(System.Collections.Generic.List<string>),
                                                    typeof(System.Collections.Generic.List<string>)
                                                },
                                                null);
        }

        HistoryWindow()
        {
            historyWindows.Add(this);
        }

        void OnDestroy()
        {
            historyWindows.Remove(this);
        }

        static List<HistoryWindow> historyWindows = new List<HistoryWindow>();
        static List<string> undoRecords = new List<string>();
        static List<string> redoRecords = new List<string>();
        static int undoPosition = 0;
        static HistoryWindow window;

        static bool dirty = true;
        static MethodInfo GetRecords_method;

        static void UpdateLists()
        {
            dirty = false;
            if (GetRecords_method == null)
                return;
            GetRecords_method.Invoke(null,
                new object[] {
                        undoRecords,
                        redoRecords
                });
            undoPosition = undoRecords.Count;
        }

        class Styles
        {
            public GUIStyle undoable;
            public GUIStyle redoable;
            public GUIStyle current;
        };
        static Styles styles;
        static GUISkin prevSkin;

        static void UpdateStyles()
        {
            prevSkin	= GUI.skin;
            styles = new Styles();
            styles.undoable		= new GUIStyle("PR Label");
            styles.redoable		= new GUIStyle("PR DisabledLabel");
            styles.current		= new GUIStyle("PR PrefabLabel");

            Texture2D background = styles.undoable.hover.background;
            styles.undoable.onNormal.background = background;
            styles.undoable.onActive.background = background;
            styles.undoable.onFocused.background = background;
            styles.undoable.alignment = TextAnchor.MiddleLeft;
            styles.undoable.padding.top = 2;
            styles.undoable.fixedHeight = 0f;

            background = styles.redoable.hover.background;
            styles.redoable.onNormal.background = background;
            styles.redoable.onActive.background = background;
            styles.redoable.onFocused.background = background;
            styles.redoable.alignment = TextAnchor.MiddleLeft;
            styles.redoable.padding.top = 2;
            styles.redoable.fixedHeight = 0f;

            background = styles.current.hover.background;
            styles.current.onNormal.background = background;
            styles.current.onActive.background = background;
            styles.current.onFocused.background = background;
            styles.current.alignment = TextAnchor.MiddleLeft;
            styles.current.fontStyle = FontStyle.Bold;
            styles.current.padding.top = 2;
            styles.current.fixedHeight = 0f;
        }

        void UndoUntilAtPosition(int position)
        {
            do
            {
                Undo.PerformUndo();
                GetRecords_method.Invoke(null,
                    new object[] {
                            undoRecords,
                            redoRecords
                    });
            }
            while (undoRecords.Count > position);
        }
        void RedoUntilAtPosition(int position)
        {
            while (undoRecords.Count < position)
            {
                Undo.PerformRedo();
                GetRecords_method.Invoke(null,
                    new object[] {
                            undoRecords,
                            redoRecords
                    });
            }
        }

        const int kItemHeight	= 24;
        const int kScrollWidth = 20;
        const int kIconWidth = 20;
        const int kPadding = 2;

        Vector2 m_ScrollPos;

        void OnGUI()
        {
            bool wasDirty = dirty;
            if (dirty)
                UpdateLists();

            if (styles == null ||
                prevSkin != GUI.skin)
                UpdateStyles();

            int totalCount = undoRecords.Count + redoRecords.Count + 1;
            int viewCount = Mathf.CeilToInt(position.height / kItemHeight);

            Rect scrollpos = position;
            scrollpos.x = 0;
            scrollpos.y = 0;

            Rect totalRect = position;
            totalRect.x = 0;
            totalRect.y = 0;
            totalRect.width  = position.width - kScrollWidth;
            totalRect.height = (totalCount * kItemHeight) + (2 * kPadding);

            if (wasDirty)
            {
                var selectedY0 = kPadding + (undoPosition * kItemHeight);
                if (selectedY0 < m_ScrollPos.y)
                {
                    m_ScrollPos.y = selectedY0 - kPadding;
                } else
                if (selectedY0 > m_ScrollPos.y + scrollpos.height - kItemHeight)
                {
                    m_ScrollPos.y = selectedY0 - (scrollpos.height - kItemHeight - kPadding);
                    if (m_ScrollPos.y < 0)
                        m_ScrollPos.y = 0;
                }
            }

            var firstIndex = (int)(m_ScrollPos.y / kItemHeight);
            var start = firstIndex * kItemHeight;

            m_ScrollPos = GUI.BeginScrollView(scrollpos, m_ScrollPos, totalRect);
            {
                firstIndex = Mathf.Clamp(firstIndex, 0, Mathf.Max(0, totalCount - viewCount));

                var rect		= new Rect((kPadding * 2), kPadding + start, position.width - kScrollWidth, kItemHeight);

                int undoRecordsCount = undoRecords.Count;
                for (int i = firstIndex, iCount = Mathf.Min(totalCount, firstIndex + viewCount); i < iCount; i++)
                {
                    string item;
                    if (i == 0)
                        item = "End of history";
                    else
                    if ((i - 1) < undoRecordsCount)
                        item = undoRecords[i - 1];
                    else
                        item = redoRecords[totalCount - 1 - i];

                    if (i < undoPosition)
                    {
                        if (GUI.Button(rect, item, styles.undoable))
                        {
                            UndoUntilAtPosition(i);
                            return;
                        }
                    } else if (i > undoPosition)
                    {
                        if (GUI.Button(rect, item, styles.redoable))
                        {
                            RedoUntilAtPosition(i);
                            return;
                        }
                    } else
                        EditorGUI.LabelField(rect, item, styles.current);
                    rect.y += kItemHeight;
                }
            }
            GUI.EndScrollView();
        }

        private static void RepaintHistoryWindow()
        {
            for (int i = 0; i < historyWindows.Count; i++)
            {
                if (historyWindows[i])
                    historyWindows[i].Repaint();
            }
        }

        private static void UndoRedoPerformed()
        {
            dirty = true;
            RepaintHistoryWindow();
        }

        private static void SelectionChanged()
        {
            dirty = true;
            RepaintHistoryWindow();
        }
    
        private static UndoPropertyModification[] PostprocessModifications(UndoPropertyModification[] modifications)
        {
            dirty = true;
            return modifications;
        }
    }
}