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

    static readonly (string objectName, string label)[] TourObjects =
    {
        ("Home", "Home"),
        ("mamad", "Mamad"),
        ("Map", "Map"),
        ("Compass", "Compass"),
        ("firstaid", "First Aid"),
        ("waterbottle", "Water Bottle"),
        ("Flashlight", "Flashlight"),
        ("Radio", "Radio"),
        ("WoundedCharacter_TPose", "Wounded Character"),
    };

    [MenuItem("Tools/Stress Trainer/Setup Environment Learning Tour Labels")]
    public static void SetupTourLabels()
    {
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPanelPrefabPath);
        int updated = 0;

        foreach (var entry in TourObjects)
        {
            var go = FindByName(entry.objectName);
            if (go == null)
            {
                Debug.LogWarning($"Tour label: GameObject '{entry.objectName}' not found in scene.");
                continue;
            }

            var label = go.GetComponent<WorldItemLabel>();
            if (label == null)
                label = go.AddComponent<WorldItemLabel>();

            label.labelText = entry.label;
            if (panelPrefab != null)
                label.labelPanelPrefab = panelPrefab;

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
