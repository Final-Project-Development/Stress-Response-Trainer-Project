#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ensures all tour objects have WorldItemLabel + LabelAnchor + ViewAnchor for manual placement.
/// </summary>
public static class EnvironmentLearningTourLabelsSetup
{
    const string LabelPanelPrefabPath = "Assets/Prefabs/EnvironmentLearning/WorldLabelPanel.prefab";

    [MenuItem("Tools/Stress Trainer/Setup Environment Learning Tour Labels")]
    public static void SetupTourLabels()
    {
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPanelPrefabPath);
        int updated = 0;

        foreach (var entry in EnvironmentLearningTourCatalog.Items)
        {
            var go = FindByName(entry.ObjectName);
            if (go == null)
            {
                Debug.LogWarning($"Tour label: GameObject '{entry.ObjectName}' not found in scene.");
                continue;
            }

            var label = go.GetComponent<WorldItemLabel>();
            if (label == null)
                label = go.AddComponent<WorldItemLabel>();

            label.labelText = entry.DisplayName;
            if (panelPrefab != null)
                label.labelPanelPrefab = panelPrefab;

            label.labelPanelSizeOverride = Vector2.zero;
            label.useRightToLeftText = false;

            label.PruneDuplicateAnchorsForItem();
            label.EnsureLabelAnchor();
            label.EnsureViewAnchor();
            updated++;
        }

        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow != null)
            EditorUtility.SetDirty(flow);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(
            $"Environment Learning tour labels: updated {updated} objects. " +
            "Move each LabelAnchor (name tag) and ViewAnchor (stand + look) in Scene view.");
    }

    [MenuItem("Tools/Stress Trainer/Setup Environment Learning Tour View Anchors")]
    public static void SetupTourViewAnchors()
    {
        int updated = 0;

        foreach (var entry in EnvironmentLearningTourCatalog.Items)
        {
            var go = FindByName(entry.ObjectName);
            if (go == null)
            {
                Debug.LogWarning($"Tour view anchor: GameObject '{entry.ObjectName}' not found in scene.");
                continue;
            }

            var label = go.GetComponent<WorldItemLabel>();
            if (label == null)
                label = go.AddComponent<WorldItemLabel>();

            label.PruneDuplicateAnchorsForItem();
            label.EnsureViewAnchor();
            updated++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Environment Learning tour view anchors: updated {updated} objects. Place each ViewAnchor where the player should stand.");
    }

    [MenuItem("Tools/Stress Trainer/Remove Duplicate Tour Anchors")]
    public static void RemoveDuplicateTourAnchors()
    {
        int updated = 0;

        foreach (var label in Object.FindObjectsByType<WorldItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label == null)
                continue;

            label.PruneDuplicateAnchorsForItem();
            updated++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Removed duplicate LabelAnchor/ViewAnchor objects on {updated} WorldItemLabel hosts.");
    }

    static GameObject FindByName(string objectName)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == objectName)
                return go;
        }

        return null;
    }
}
#endif
