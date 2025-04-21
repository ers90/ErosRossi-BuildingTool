using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ModularPlacer
{
    // cache delle layer/mask e del MaterialPropertyBlock
    private static readonly int IgnoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    private static readonly int IgnoreRaycastMask = ~LayerMask.GetMask("Ignore Raycast");
    private static readonly int PlacedLayer = LayerMask.NameToLayer("PlacedModule");
    private static readonly MaterialPropertyBlock s_Block = new MaterialPropertyBlock();

    private static Quaternion currentRotation = Quaternion.identity;
    private static bool placingActive = false;
    private static GameObject previewInstance;
    private static GameObject prefabToPlace;

    private static int currentFloor = 0;
    private static float floorHeight = 2.0001f;
    public static string placementError = "";

    static ModularPlacer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // per sloggare l'evento quando gli script vengono disabilitati/richiusi
    public static void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
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
        previewInstance.layer = IgnoreRaycastLayer;
        foreach (var t in previewInstance.GetComponentsInChildren<Transform>())
            t.gameObject.layer = IgnoreRaycastLayer;
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
        if (!placingActive || prefabToPlace == null || previewInstance == null)
            return;

        var e = Event.current;
        HandleInput(e);
        if (!placingActive || previewInstance == null)
            return;

        var plane = new Plane(Vector3.up, Vector3.zero);
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!plane.Raycast(ray, out var enter))
            return;

        var point = ray.GetPoint(enter);
        var position = GetSnappedPosition(point);
        var valid = CanPlaceHere(position, currentRotation);

        previewInstance.transform.SetPositionAndRotation(position, currentRotation);
        SetPreviewColor(previewInstance, valid
            ? new Color(0, 1, 0, 0.5f)
            : new Color(1, 0, 0, 0.5f)
        );

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (valid)
                PlaceObject(position, currentRotation);
            else
                Debug.Log(placementError);
            e.Use();
        }

        sceneView.Repaint();
    }

    private static void HandleInput(Event e)
    {
        if (e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.Z:
                currentRotation *= Quaternion.Euler(0, -90, 0);
                e.Use();
                break;
            case KeyCode.X:
                currentRotation *= Quaternion.Euler(0, 90, 0);
                e.Use();
                break;
            case KeyCode.PageUp:
                currentFloor++;
                Debug.Log($"🔼 Active floor: {currentFloor}");
                e.Use();
                break;
            case KeyCode.PageDown:
                currentFloor = Mathf.Max(0, currentFloor - 1);
                Debug.Log($"🔽 Active floor: {currentFloor}");
                e.Use();
                break;
            case KeyCode.Escape:
                CancelPlacement();
                e.Use();
                break;
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
        var placed = PrefabUtility.InstantiatePrefab(prefabToPlace) as GameObject;
        placed.transform.SetPositionAndRotation(position, rotation);
        placed.layer = PlacedLayer;
        foreach (var t in placed.GetComponentsInChildren<Transform>())
            t.gameObject.layer = PlacedLayer;

        Undo.RegisterCreatedObjectUndo(placed, "Place Modular Piece");
    }

    private static Vector3 GetSnappedPosition(Vector3 rawPos)
    {
        var bounds = GetPrefabBounds(prefabToPlace);
        var size = currentRotation * bounds.size;
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        float x = Mathf.Round(rawPos.x / size.x) * size.x;
        float z = Mathf.Round(rawPos.z / size.z) * size.z;
        float y = currentFloor * floorHeight;

        if (prefabToPlace != null && prefabToPlace.name.ToLower().Contains("corner"))
        {
            x = Mathf.Round(rawPos.x * 10f) / 10f;
            z = Mathf.Round(rawPos.z * 10f) / 10f;
        }

        return new Vector3(x, y, z);
    }

    private static bool CanPlaceHere(Vector3 position, Quaternion rotation)
    {
        if (prefabToPlace == null)
        {
            placementError = "No prefab selected";
            return false;
        }

        if (prefabToPlace.name.Contains("Roof") && currentFloor == 0)
        {
            placementError = "The roof can only be placed starting from the first floor";
            return false;
        }

        var bounds = GetPrefabBounds(prefabToPlace);
        var center = position + rotation * bounds.center;
        var halfExtents = bounds.extents - Vector3.one * 0.02f;

        var hits = Physics.OverlapBox(center, halfExtents, rotation, IgnoreRaycastMask);
        if (hits.Length > 0)
        {
            placementError = "Position not valid: you can't stack on top of other modules!";
            return false;
        }

        placementError = "";
        return true;
    }

    private static Bounds GetPrefabBounds(GameObject prefab)
    {
        if (prefab == null)
            return new Bounds(Vector3.zero, Vector3.one);

        var renders = prefab.GetComponentsInChildren<Renderer>();
        if (renders.Length == 0)
            return new Bounds(prefab.transform.position, Vector3.one);

        var b = renders[0].bounds;
        foreach (var r in renders)
            b.Encapsulate(r.bounds);
        return b;
    }

    private static void SetPreviewColor(GameObject obj, Color color)
    {
        s_Block.SetColor("_Color", color);
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
            if (r != null)
                r.SetPropertyBlock(s_Block);
    }
}