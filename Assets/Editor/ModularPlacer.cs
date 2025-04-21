using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ModularPlacer
{
    private static Quaternion currentRotation = Quaternion.identity;
    private static bool placingActive = false;
    private static GameObject previewInstance;
    private static GameObject prefabToPlace;

    private static int currentFloor = 0;
    private static float floorHeight = 2.0001f;
    //private static float floorOffset = 0.01f; // Nuovo offset per i piani superiori

    static ModularPlacer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static int CurrentFloor
    {
        get => currentFloor;
        set => currentFloor = Mathf.Max(0, value);
    }

    public static void StartPlacing(GameObject prefab)
    {
        prefabToPlace = prefab;
        placingActive = false;
        currentRotation = Quaternion.identity;

        if (previewInstance != null)
            Object.DestroyImmediate(previewInstance);

        previewInstance = Object.Instantiate(prefabToPlace);
        previewInstance.layer = LayerMask.NameToLayer("Ignore Raycast");

        foreach (Transform child in previewInstance.transform)
            child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        previewInstance.hideFlags = HideFlags.HideAndDontSave;
        EditorApplication.update += DelayedActivatePlacing;
    }

    private static void DelayedActivatePlacing()
    {
        placingActive = true;
        SceneView.lastActiveSceneView.Focus();
        SceneView.RepaintAll();
        EditorApplication.update -= DelayedActivatePlacing;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!placingActive || prefabToPlace == null)
            return;

        if (previewInstance == null)
        {
            CancelPlacement();
            return;
        }

        Event e = Event.current;
        HandleInput(e);

        Plane groundPlane = new(Vector3.up, Vector3.zero);
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            Vector3 position = SnapPosition(point);
            bool isValid = IsPositionValid(position, currentRotation);

            previewInstance.transform.SetPositionAndRotation(position, currentRotation);
            SetPreviewColor(previewInstance, isValid ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f));

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (isValid)
                    PlaceObject(position, currentRotation);
                else
                    Debug.Log("Posizione non valida: modulo sovrapposto!");

                e.Use();
            }

            sceneView.Repaint();
        }
    }

    private static void HandleInput(Event e)
    {
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Z)
            {
                currentRotation *= Quaternion.Euler(0, -90, 0);
                e.Use();
            }
            else if (e.keyCode == KeyCode.X)
            {
                currentRotation *= Quaternion.Euler(0, 90, 0);
                e.Use();
            }
            else if (e.keyCode == KeyCode.PageUp)
            {
                currentFloor++;
                Debug.Log($"🔼 Piano attivo: {currentFloor}");
                e.Use();
            }
            else if (e.keyCode == KeyCode.PageDown)
            {
                currentFloor = Mathf.Max(0, currentFloor - 1);
                Debug.Log($"🔽 Piano attivo: {currentFloor}");
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                CancelPlacement();
                e.Use();
            }
        }
    }

    private static void CancelPlacement()
    {
        if (previewInstance != null)
            Object.DestroyImmediate(previewInstance);

        previewInstance = null;
        prefabToPlace = null;
        placingActive = false;
        currentRotation = Quaternion.identity;
        SceneView.RepaintAll();
    }

    private static void PlaceObject(Vector3 position, Quaternion rotation)
    {
        GameObject placed = PrefabUtility.InstantiatePrefab(prefabToPlace) as GameObject;
        placed.transform.SetPositionAndRotation(position, rotation);

        int placedLayer = LayerMask.NameToLayer("PlacedModule");
        if (placedLayer == -1)
        {
            Debug.LogWarning("Layer 'PlacedModule' not found. Please add it under Tags and Layers.");
            placedLayer = 0;
        }

        placed.layer = placedLayer;
        foreach (Transform child in placed.transform)
            child.gameObject.layer = placedLayer;

        Undo.RegisterCreatedObjectUndo(placed, "Place Modular Piece");
    }

    private static Vector3 SnapPosition(Vector3 rawPos)
    {
        Bounds bounds = GetPrefabBounds(prefabToPlace);
        Vector3 size = currentRotation * bounds.size;
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        float snapX = Mathf.Round(rawPos.x / size.x) * size.x;
        float snapZ = Mathf.Round(rawPos.z / size.z) * size.z;

        // Calcola l'altezza del piano attivo con offset per i piani superiori
        float snapY = currentFloor * floorHeight;
        // 🔧 Se è un corner, fai snap su 0.05
        if (prefabToPlace.name.ToLower().Contains("Corner"))
        {
            snapX = Mathf.Round(rawPos.x * 10f) / 10f;
            snapZ = Mathf.Round(rawPos.z * 10f) / 10f;
        }

        return new Vector3(snapX, snapY, snapZ);
    }

    private static bool IsPositionValid(Vector3 position, Quaternion rotation)
    {
        if (prefabToPlace == null) return false;

        Bounds bounds = GetPrefabBounds(prefabToPlace);
        Vector3 center = position + rotation * bounds.center;
        Vector3 halfExtents = bounds.extents - Vector3.one * 0.02f;

        int layerMask = ~LayerMask.GetMask("Ignore Raycast");
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, rotation, layerMask);
        return colliders.Length == 0;
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds combined = renderers[0].bounds;
        foreach (Renderer r in renderers)
            combined.Encapsulate(r.bounds);

        return combined;
    }

    private static void SetPreviewColor(GameObject obj, Color color)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            if (renderer != null)
            {
                MaterialPropertyBlock block = new();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}