#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click helpers for backup scene wiring (run while backup.unity is open).
/// </summary>
public static class EnvironmentLearningSceneSetup
{
    [MenuItem("Tools/Stress Trainer/Setup Environment Learning (open scene)")]
    public static void SetupInOpenScene()
    {
        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No TrainingFlowController in scene.", "OK");
            return;
        }

        var learning = Object.FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);
        if (learning == null)
        {
            var go = new GameObject("EnvironmentLearning");
            learning = go.AddComponent<EnvironmentLearningController>();
        }

        var levelSelect = GameObject.Find("Level_Select_UI");
        if (levelSelect != null && levelSelect.GetComponent<LevelSelectUI>() == null)
            levelSelect.AddComponent<LevelSelectUI>();

        if (flow.environmentLearningController == null)
            flow.environmentLearningController = learning;

        if (flow.environmentLearningHudPanel == null)
        {
            var hud = CreateLearningHud(flow.transform);
            flow.environmentLearningHudPanel = hud;
            learning.learningHudRoot = hud;
            learning.learningHudBodyText = hud.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        flow.environmentLearningUseGateSpawn = false;
        flow.environmentLearningSpawnHeightMode = PlayerGroundSnap.SpawnHeightMode.FeetAtMarker;

        SnapEnvironmentLearningSpawnOutsideHome(flow, createIfMissing: flow.environmentLearningSpawnPoint == null);

        EditorUtility.SetDirty(flow);
        EditorUtility.SetDirty(learning);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Environment Learning setup done. EnvironmentLearningSpawn is outside Home (near Simulation1SpawnPoint).");
    }

    [MenuItem("Tools/Stress Trainer/Snap Environment Learning Spawn Outside Home")]
    public static void SnapSpawnOutsideHomeMenu()
    {
        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No TrainingFlowController in scene.", "OK");
            return;
        }

        SnapEnvironmentLearningSpawnOutsideHome(flow, createIfMissing: true);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void SnapEnvironmentLearningSpawnOutsideHome(TrainingFlowController flow, bool createIfMissing)
    {
        if (flow.simulation1SpawnPoint == null)
        {
            Debug.LogWarning("Simulation1SpawnPoint is not assigned on FlowManager.");
            return;
        }

        Transform spawn = flow.environmentLearningSpawnPoint;
        if (spawn == null)
            spawn = GameObject.Find("EnvironmentLearningSpawn")?.transform;

        if (spawn == null && createIfMissing)
        {
            var go = new GameObject("EnvironmentLearningSpawn");
            spawn = go.transform;
        }

        if (spawn == null)
            return;

        Transform homeRoot = flow.simulation1SpawnPoint.parent;
        if (homeRoot != null)
            spawn.SetParent(homeRoot, false);

        Vector3 outdoorOffset = flow.simulation1SpawnPoint.forward * 2f;
        spawn.SetPositionAndRotation(
            flow.simulation1SpawnPoint.position + outdoorOffset,
            flow.simulation1SpawnPoint.rotation);

        flow.environmentLearningSpawnPoint = spawn;
        flow.environmentLearningFallbackToSim1Spawn = true;
        flow.environmentLearningUseGateSpawn = false;
    }

    static GameObject CreateLearningHud(Transform parentHint)
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : parentHint;

        var hud = new GameObject("EnvironmentLearningHud");
        hud.transform.SetParent(parent, false);
        var rect = hud.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(720f, 120f);

        var bg = hud.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.1f, 0.15f, 0.88f);
        bg.raycastTarget = true;

        var textGo = new GameObject("Body");
        textGo.transform.SetParent(hud.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 16f);
        textRect.offsetMax = new Vector2(-16f, -16f);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = "סיור היכרות — Back / Esc לחזרה";

        hud.SetActive(false);
        return hud;
    }
}
#endif
