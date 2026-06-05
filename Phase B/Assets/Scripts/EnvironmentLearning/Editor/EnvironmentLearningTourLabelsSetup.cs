#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ensures all tour objects have WorldItemLabel + optional LabelAnchor for manual placement.
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

            if (entry.ObjectName == "mamad")
            {
                label.labelPanelSizeOverride = new Vector2(96f, 40f);
                label.useRightToLeftText = true;
            }

            label.EnsureLabelAnchor();
            updated++;
        }

        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow != null)
            EditorUtility.SetDirty(flow);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Environment Learning tour labels: updated {updated} objects. Move each LabelAnchor in Scene view.");
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
