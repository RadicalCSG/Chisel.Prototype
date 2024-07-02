using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SceneObjectsWindow : EditorWindow
{
    private Vector2 scrollPosition;

    private readonly Dictionary<System.Type, bool>          sceneFoldout    = new Dictionary<System.Type, bool>();
    private readonly Dictionary<System.Type, List<Object>>  sceneTypeList   = new Dictionary<System.Type, List<Object>>();

    [MenuItem("Chisel DEBUG/Scene Objects Window")]
    static void Init()
    {
        SceneObjectsWindow window = (SceneObjectsWindow)EditorWindow.GetWindow(typeof(SceneObjectsWindow));
        window.name = "Scene Objects";
        window.UpdateValues();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Update", GUILayout.Height(30f)))
            UpdateValues();

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        foreach (var key in sceneTypeList.Keys)
        {
            var objects = sceneTypeList[key];
            sceneFoldout[key] = EditorGUILayout.Foldout(sceneFoldout[key], $"{key.Name} ({objects.Count})");
            if (sceneFoldout[key])
            {
                EditorGUI.indentLevel++;
                DrawObjects(sceneTypeList[key]);
                EditorGUI.indentLevel--;
            }
        }
        GUILayout.EndScrollView();
    }

    private void UpdateValues()
    {
        sceneTypeList.Clear();
#if UNITY_2023_1_OR_NEWER
		var sceneList = FindObjectsByType<Object>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var sceneList = FindObjectsOfType<Object>();
#endif
		for (int i = 0; i < sceneList.Length; i++)
        {
            var obj = sceneList[i];
            if ((obj.hideFlags & HideFlags.DontSaveInEditor) != HideFlags.None)
                continue;
            if (obj is Component || obj is GameObject)
                continue;
            var type = obj.GetType();
            if (!sceneTypeList.TryGetValue(type, out var list))
            {
                list = new List<Object>();
                sceneTypeList[type] = list;
                sceneFoldout[type] = false;
            }
            list.Add(obj);
        }
    }

    private void DrawObjects(List<Object> list)
    {
        foreach (Object obj in list)
        {
            if (obj == null)
                continue;
            EditorGUILayout.ObjectField(obj, obj.GetType(), allowSceneObjects: true, GUILayout.ExpandWidth(true));
        }
    }
}