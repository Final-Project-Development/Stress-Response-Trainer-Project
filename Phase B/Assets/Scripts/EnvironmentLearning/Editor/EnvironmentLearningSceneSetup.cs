#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        if (!TryWireExistingLearningHud(flow, learning))
        {
            Debug.LogWarning(
                "Environment Learning: create and style EnvironmentLearningHud under your Canvas, " +
                "then run Tools → Stress Trainer → Wire Environment Learning HUD.");
        }

        flow.environmentLearningUseGateSpawn = false;
        flow.environmentLearningSpawnHeightMode = PlayerGroundSnap.SpawnHeightMode.FeetAtMarker;

        SnapEnvironmentLearningSpawnToSimulation2(flow, createIfMissing: flow.environmentLearningSpawnPoint == null);

        EditorUtility.SetDirty(flow);
        EditorUtility.SetDirty(learning);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Environment Learning setup done. Tour starts at Simulation2SpawnPoint.");
    }

    [MenuItem("Tools/Stress Trainer/Wire Environment Learning HUD")]
    public static void WireLearningHudMenu()
    {
        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        var learning = Object.FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);
        if (flow == null || learning == null)
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                "Need FlowManager (TrainingFlowController) and EnvironmentLearning in the scene.",
                "OK");
            return;
        }

        if (!TryWireExistingLearningHud(flow, learning))
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                "No GameObject named EnvironmentLearningHud found. Create it under the Canvas and design it in the Inspector.",
                "OK");
            return;
        }

        learning.applyDefaultHudTextAtStart = false;
        EditorUtility.SetDirty(flow);
        EditorUtility.SetDirty(learning);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Wired EnvironmentLearningHud. Design stays as you set in the scene.");
    }

    [MenuItem("Tools/Stress Trainer/Snap Environment Learning Spawn To Simulation 2")]
    public static void SnapSpawnOutsideHomeMenu()
    {
        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No TrainingFlowController in scene.", "OK");
            return;
        }

        SnapEnvironmentLearningSpawnToSimulation2(flow, createIfMissing: true);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void SnapEnvironmentLearningSpawnToSimulation2(TrainingFlowController flow, bool createIfMissing)
    {
        if (flow.simulation2SpawnPoint == null)
        {
            Debug.LogWarning("Simulation2SpawnPoint is not assigned on FlowManager.");
            return;
        }

        flow.environmentLearningSpawnPoint = flow.simulation2SpawnPoint;
        flow.environmentLearningFallbackToSim2Spawn = true;
        flow.environmentLearningUseGateSpawn = false;

        Transform spawn = GameObject.Find("EnvironmentLearningSpawn")?.transform;
        if (spawn == null && createIfMissing)
            spawn = new GameObject("EnvironmentLearningSpawn").transform;

        if (spawn == null)
            return;

        Transform homeRoot = flow.simulation2SpawnPoint.parent;
        if (homeRoot != null)
            spawn.SetParent(homeRoot, false);

        spawn.SetPositionAndRotation(
            flow.simulation2SpawnPoint.position,
            flow.simulation2SpawnPoint.rotation);
    }

    static bool TryWireExistingLearningHud(TrainingFlowController flow, EnvironmentLearningController learning)
    {
        var hud = GameObject.Find("EnvironmentLearningHud");
        if (hud == null)
            return false;

        flow.environmentLearningHudPanel = hud;
        learning.learningHudRoot = hud;
        learning.learningHudBodyText = hud.GetComponentInChildren<TextMeshProUGUI>(true);
        learning.applyDefaultHudTextAtStart = false;

        if (!hud.activeSelf)
            hud.SetActive(false);

        return true;
    }
}
#endif
