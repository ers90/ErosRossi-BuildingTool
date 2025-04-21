using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BuildingTool : EditorWindow
{
    private static readonly string[] prefabNames = { "Floor", "Wall", "WallCorner", "Roof" };

    private class PrefabEntry
    {
        public string Name;
        public GameObject Prefab;
        public Texture2D Icon;
    }

    private List<PrefabEntry> entries;
    private GUIContent[] slotContents;
    private GUIStyle errorLabelStyle;
    private const int ICON_SIZE = 128;

    [MenuItem("Tools/Modular Building Tool")]
    public static void ShowWindow()
    {
        GetWindow<BuildingTool>("Modular Builder")
            .minSize = new Vector2(500, 300);
    }

    private void OnEnable()
    {
        LoadPrefabs();
        PrepareSlotContents();
        SetupStyles();
    }

    private void LoadPrefabs()
    {
        entries = prefabNames
            .Select(name => new PrefabEntry
            {
                Name = name,
                Prefab = Resources.Load<GameObject>($"Prefabs/{name}")
            })
            .ToList();
    }

    private void PrepareSlotContents()
    {
        slotContents = new GUIContent[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            slotContents[i] = new GUIContent(entries[i].Name);
    }

    private void SetupStyles()
    {
        errorLabelStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.red },
            wordWrap = true,
            margin = new RectOffset(10, 10, 10, 10)
        };
    }

    private void OnGUI()
    {
        GUILayout.Space(20);

        EditorGUILayout.HelpBox(
        "Controls:\n" + "Z / X → rotate the module\n" +
        "PageUp / PageDown → change floor\n" +
        "ESC → abort the placement\n" +
        "Left click → confirm the placement",
        MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();
        foreach (var e in entries)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ICON_SIZE));
            {
                GUILayout.Label(e.Name, EditorStyles.boldLabel,
                GUILayout.Width(ICON_SIZE), GUILayout.Height(20));

                if (e.Prefab != null && e.Icon == null)
                {
                    var thumb = AssetPreview.GetAssetPreview(e.Prefab);
                    if (thumb != null)
                        e.Icon = thumb;
                }

                if (e.Icon != null)
                {
                    if (GUILayout.Button(e.Icon,
                        GUILayout.Width(ICON_SIZE),
                        GUILayout.Height(ICON_SIZE)))
                    {
                        ModularPlacer.StartPlacing(e.Prefab);
                    }
                }
                else
                {
                    GUILayout.Box("", GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE));
                }
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        // Errore di piazzamento
        if (!string.IsNullOrEmpty(ModularPlacer.placementError))
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(ModularPlacer.placementError, errorLabelStyle);
            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(20);
        if (GUILayout.Button("Save Building Data"))
            SaveBuildingData();
        if (GUILayout.Button("Load Building Data"))
            LoadBuildingDataDialog();
    }

    private void Update()
    {
        Repaint();
    }

    private void SaveBuildingData()
    {
        int layer = LayerMask.NameToLayer("PlacedModule");
        var all = GameObject
            .FindObjectsOfType<GameObject>()
            .Where(go => go.layer == layer && go.scene.isLoaded);

        var roots = all
            .Where(go => go.transform.parent == null
                      || go.transform.parent.gameObject.layer != layer)
            .ToList();

        if (roots.Count == 0)
        {
            EditorUtility.DisplayDialog("Save Building", "There are no modules to save!", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Building Data",
            "NewBuildingData",
            "asset",
            "Choose where to save");

        if (string.IsNullOrEmpty(path)) return;

        var data = ScriptableObject.CreateInstance<BuildingData>();
        foreach (var go in roots)
        {
            var prefabRef = PrefabUtility.GetCorrespondingObjectFromSource(go) ?? go;
            data.modules.Add(new BuildingData.ModuleEntry
            {
                prefab = prefabRef,
                position = go.transform.position,
                rotation = go.transform.rotation
            });
        }

        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Building Saved",
            $"Saved {roots.Count} modules to:\n{path}",
            "OK");

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = data;
    }

    private void LoadBuildingDataDialog()
    {
        string path = EditorUtility.OpenFilePanel("Load Building Data", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;
        path = FileUtil.GetProjectRelativePath(path);
        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (data != null) LoadBuildingData(data);
    }

    private void LoadBuildingData(BuildingData data)
    {
        int layer = LayerMask.NameToLayer("PlacedModule");
        var existing = GameObject
            .FindObjectsOfType<GameObject>()
            .Where(go => go.layer == layer && go.scene.isLoaded)
            .ToList();
        foreach (var go in existing)
            Undo.DestroyObjectImmediate(go);

        foreach (var entry in data.modules)
        {
            var inst = PrefabUtility.InstantiatePrefab(entry.prefab) as GameObject;
            inst.transform.SetPositionAndRotation(entry.position, entry.rotation);
            inst.layer = layer;
            Undo.RegisterCreatedObjectUndo(inst, "Load Building Module");
        }
    }
}