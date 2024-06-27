using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{ 
    // This window is a helper window to see the UNDO stack, to help debug/test undo operations
    public sealed class HistoryWindow : EditorWindow
    {
        [MenuItem("Chisel DEBUG/Undo Window")]
        static void Create()
        {
            s_Window = (HistoryWindow)EditorWindow.GetWindow(typeof(HistoryWindow), false, "History");
            s_Window.autoRepaintOnSceneChange = true;
        }
        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            Undo.undoRedoPerformed			-= UndoRedoPerformed;
            Undo.undoRedoPerformed			+= UndoRedoPerformed;
            Selection.selectionChanged		-= SelectionChanged;
            Selection.selectionChanged		+= SelectionChanged;
            Undo.postprocessModifications	-= PostprocessModifications;
            Undo.postprocessModifications	+= PostprocessModifications;

        }

        static readonly List<HistoryWindow> s_HistoryWindows = new();
        static readonly List<string> s_UndoRecords = new();
        static readonly List<string> s_RedoRecords = new();
        static int s_UndoPosition = 0;
        static HistoryWindow s_Window;

        static bool dirty = true;
        
        delegate void GetRecordsDelegate(System.Collections.Generic.List<string> undoRecords, System.Collections.Generic.List<string> redoRecords);
        static GetRecordsDelegate GetRecords = typeof(Undo).CreateDelegate<GetRecordsDelegate>("GetRecords");

        HistoryWindow()
        {
            s_HistoryWindows.Add(this);
        }

        void OnDestroy()
        {
            s_HistoryWindows.Remove(this);
        }
        static void UpdateLists()
        {
            dirty = false;
            GetRecords?.Invoke(s_UndoRecords, s_RedoRecords);
            s_UndoPosition = s_UndoRecords.Count;
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
                GetRecords?.Invoke(s_UndoRecords, s_RedoRecords);
            }
            while (s_UndoRecords.Count > position);
        }
        void RedoUntilAtPosition(int position)
        {
            while (s_UndoRecords.Count < position)
            {
                Undo.PerformRedo();
                GetRecords?.Invoke(s_UndoRecords, s_RedoRecords);
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

            int totalCount = s_UndoRecords.Count + s_RedoRecords.Count + 1;
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
                var selectedY0 = kPadding + (s_UndoPosition * kItemHeight);
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

                int undoRecordsCount = s_UndoRecords.Count;
                for (int i = firstIndex, iCount = Mathf.Min(totalCount, firstIndex + viewCount); i < iCount; i++)
                {
                    string item;
                    if (i == 0)
                        item = "End of history";
                    else
                    if ((i - 1) < undoRecordsCount)
                        item = s_UndoRecords[i - 1];
                    else
                        item = s_RedoRecords[totalCount - 1 - i];

                    if (i < s_UndoPosition)
                    {
                        if (GUI.Button(rect, item, styles.undoable))
                        {
                            UndoUntilAtPosition(i);
                            return;
                        }
                    } else if (i > s_UndoPosition)
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
            for (int i = 0; i < s_HistoryWindows.Count; i++)
            {
                if (s_HistoryWindows[i])
                    s_HistoryWindows[i].Repaint();
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