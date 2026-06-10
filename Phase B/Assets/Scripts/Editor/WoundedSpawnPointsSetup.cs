#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates and wires wounded spawn markers for Simulation 2 (run while backup.unity is open).
/// </summary>
public static class WoundedSpawnPointsSetup
{
    const string ParentName = "WoundedSpawnPoints";
    const int DefaultSpawnPointCount = 4;

    [MenuItem("Tools/Stress Trainer/Create Wounded Spawn Points")]
    public static void CreateWoundedSpawnPoints()
    {
        var bootstrap = Object.FindFirstObjectByType<SimulationMissionBootstrap>(FindObjectsInactive.Include);
        if (bootstrap == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No SimulationMissionBootstrap in scene.", "OK");
            return;
        }

        Transform woundedTransform = ResolveWoundedTransform(bootstrap);
        if (woundedTransform == null)
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                "Could not find WoundedCharacter_TPose. Assign woundedRoot on Simulation Mission Bootstrap first.",
                "OK");
            return;
        }

        Transform parent = GameObject.Find(ParentName)?.transform;
        if (parent == null)
        {
            parent = new GameObject(ParentName).transform;
            Undo.RegisterCreatedObjectUndo(parent.gameObject, "Create Wounded Spawn Points");
        }

        var points = new Transform[DefaultSpawnPointCount];
        Vector3 basePosition = woundedTransform.position;
        Quaternion baseRotation = woundedTransform.rotation;

        Vector3[] offsets =
        {
            Vector3.zero,
            new Vector3(25f, 0f, -18f),
            new Vector3(-22f, 0f, 28f),
            new Vector3(35f, 0f, 22f)
        };

        for (int i = 0; i < DefaultSpawnPointCount; i++)
        {
            string pointName = $"WoundedSpawnPoint_{i + 1}";
            Transform point = parent.Find(pointName);
            if (point == null)
            {
                var go = new GameObject(pointName);
                Undo.RegisterCreatedObjectUndo(go, "Create Wounded Spawn Point");
                point = go.transform;
                point.SetParent(parent, true);
            }

            if (offsets[i] == Vector3.zero)
            {
                point.SetPositionAndRotation(basePosition, baseRotation);
            }
            else
            {
                point.SetPositionAndRotation(basePosition + offsets[i], baseRotation);
            }

            var marker = point.GetComponent<WoundedSpawnPointMarker>();
            if (marker == null)
                marker = point.gameObject.AddComponent<WoundedSpawnPointMarker>();

            EnsureSpawnPointAnchors(point, woundedTransform, marker);
            points[i] = point;
        }

        bootstrap.woundedSpawnPoints = points;
        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = parent.gameObject;
        Debug.Log(
            $"Created {DefaultSpawnPointCount} wounded spawn points under '{ParentName}'. " +
            "Move WoundedSpawnPoint_2..4 in the Scene view, then play Simulation 2 to test random placement.");
    }

    [MenuItem("Tools/Stress Trainer/Setup Wounded Spawn Point Label Anchors")]
    public static void SetupWoundedSpawnPointLabelAnchors()
    {
        Transform parent = GameObject.Find(ParentName)?.transform;
        if (parent == null)
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                $"No '{ParentName}' object in scene. Run Create Wounded Spawn Points first.",
                "OK");
            return;
        }

        var bootstrap = Object.FindFirstObjectByType<SimulationMissionBootstrap>(FindObjectsInactive.Include);
        Transform woundedTransform = bootstrap != null
            ? ResolveWoundedTransform(bootstrap)
            : Object.FindFirstObjectByType<WoundedMan>(FindObjectsInactive.Include)?.transform;

        if (woundedTransform == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "Could not find wounded character in scene.", "OK");
            return;
        }

        int updated = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform point = parent.GetChild(i);
            if (point == null)
                continue;

            var marker = point.GetComponent<WoundedSpawnPointMarker>();
            if (marker == null)
                marker = point.gameObject.AddComponent<WoundedSpawnPointMarker>();

            EnsureSpawnPointAnchors(point, woundedTransform, marker);
            updated++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(
            $"Ensured LabelAnchor + ViewAnchor on {updated} wounded spawn points. " +
            "Move each anchor in Scene view for that location, then save the scene.");
    }

    static void EnsureSpawnPointAnchors(Transform spawnPoint, Transform woundedTransform, WoundedSpawnPointMarker marker)
    {
        Vector3 defaultLabelLocal = new Vector3(0.06f, 2.03f, 0f);
        Vector3 defaultViewLocal = new Vector3(-0.41f, 1.06f, -3.52f);

        var woundedLabel = woundedTransform.GetComponent<WorldItemLabel>();
        if (woundedLabel != null)
        {
            if (woundedLabel.TryGetLabelAnchor(out Transform woundedLabelAnchor))
                defaultLabelLocal = woundedLabelAnchor.localPosition;

            if (woundedLabel.TryGetViewAnchor(out Transform woundedViewAnchor))
                defaultViewLocal = woundedViewAnchor.localPosition;
        }

        marker.labelAnchor = EnsureChildAnchor(spawnPoint, WorldItemLabel.LabelAnchorName, defaultLabelLocal);
        marker.viewAnchor = EnsureChildAnchor(spawnPoint, WorldItemLabel.ViewAnchorName, defaultViewLocal);
        EditorUtility.SetDirty(marker);
    }

    static Transform EnsureChildAnchor(Transform parent, string anchorName, Vector3 localPosition)
    {
        Transform anchor = parent.Find(anchorName);
        if (anchor == null)
        {
            var go = new GameObject(anchorName);
            Undo.RegisterCreatedObjectUndo(go, "Create Spawn Point Anchor");
            anchor = go.transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            anchor.localRotation = Quaternion.identity;
        }

        return anchor;
    }

    static Transform ResolveWoundedTransform(SimulationMissionBootstrap bootstrap)
    {
        if (bootstrap.woundedRoot != null)
            return bootstrap.woundedRoot.transform;

        var wounded = Object.FindFirstObjectByType<WoundedMan>(FindObjectsInactive.Include);
        return wounded != null ? wounded.transform : null;
    }
}
#endif
