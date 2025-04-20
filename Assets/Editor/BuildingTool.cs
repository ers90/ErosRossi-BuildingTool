using UnityEditor;
using UnityEngine;

public class BuildingTool : EditorWindow
{
    private GameObject selectedModule;

    [MenuItem("Tools/Modular Building Tool")]
    public static void ShowWindow()
    {
        GetWindow<BuildingTool>("Modular Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Modular Element Selection", EditorStyles.boldLabel);

        selectedModule = (GameObject)EditorGUILayout.ObjectField(
            "Prefab", selectedModule, typeof(GameObject), false);

        GUI.enabled = selectedModule != null;
        if (GUILayout.Button("Start Placement"))
        {
            ModularPlacer.StartPlacing(selectedModule);
        }
        GUI.enabled = true;
    }
}
