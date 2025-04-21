using UnityEditor;
using UnityEngine;

public class ModularBuildingTool : EditorWindow
{
    [MenuItem("Tools/Modular Building Tool")]
    public static void ShowWindow()
    {
        GetWindow<ModularBuildingTool>("Modular Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Modular Element Selection", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Floor"))
            LoadAndStart("Floor");

        if (GUILayout.Button("Wall"))
            LoadAndStart("Wall");

        if (GUILayout.Button("Roof"))
            LoadAndStart("Roof");

        if (GUILayout.Button("Corner"))
            LoadAndStart("WallCorner");

        EditorGUILayout.EndHorizontal();
    }

    private void LoadAndStart(string prefabName)
    {
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/{prefabName}");
        if (prefab != null)
        {
            ModularPlacer.StartPlacing(prefab);
        }
        else
        {
            Debug.LogWarning($"Prefab '{prefabName}' non trovato in Resources/Prefabs/");
        }
    }
}