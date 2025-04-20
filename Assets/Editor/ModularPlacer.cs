using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ModularPlacer
{
    private static GameObject previewInstance;
    private static GameObject prefabToPlace;

    static ModularPlacer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static void StartPlacing(GameObject prefab)
    {
        prefabToPlace = prefab;

        if (previewInstance != null)
            Object.DestroyImmediate(previewInstance);

        previewInstance = PrefabUtility.InstantiatePrefab(prefabToPlace) as GameObject;
        SetPreviewMaterial(previewInstance);
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (previewInstance == null || prefabToPlace == null)
            return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 position = SnapPosition(hit.point);
            previewInstance.transform.position = position;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceObject(position);
                e.Use();  // Blocca l'evento per evitare interazioni strane
            }

            sceneView.Repaint();
        }
    }

    private static void PlaceObject(Vector3 position)
    {
        GameObject placed = PrefabUtility.InstantiatePrefab(prefabToPlace) as GameObject;
        placed.transform.position = position;
        Undo.RegisterCreatedObjectUndo(placed, "Place Modular Piece");
    }

    private static Vector3 SnapPosition(Vector3 rawPos)
    {
        float grid = 1f;
        return new Vector3(
            Mathf.Round(rawPos.x / grid) * grid,
            Mathf.Round(rawPos.y / grid) * grid,
            Mathf.Round(rawPos.z / grid) * grid
        );
    }

    private static void SetPreviewMaterial(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0, 1, 0, 0.3f);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            r.sharedMaterial = mat;
        }
    }
}
