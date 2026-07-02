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

        EnsureTourGuide(learning);

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

    [MenuItem("Tools/Stress Trainer/Wire Environment Learning Tour Sidebar")]
    public static void WireTourSidebarMenu()
    {
        var learning = Object.FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);
        if (learning == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No EnvironmentLearningController in scene.", "OK");
            return;
        }

        EnsureTourGuide(learning);

        if (!TryWireTourSidebar(learning.tourGuide))
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                "Create a panel named EnvironmentLearningTourSidebar under your Canvas, design it manually, " +
                "add EnvironmentLearningTourNavButton to each Button, then run this menu again.",
                "OK");
            return;
        }

        EditorUtility.SetDirty(learning);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Wired EnvironmentLearningTourSidebar. Design and button targets stay as you set in the scene.");
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

        EnsureTourGuide(learning);

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

    static void EnsureTourGuide(EnvironmentLearningController learning)
    {
        if (learning == null)
            return;

        var guide = learning.GetComponent<EnvironmentLearningTourGuide>();
        if (guide == null)
            guide = learning.gameObject.AddComponent<EnvironmentLearningTourGuide>();

        learning.tourGuide = guide;
        TryWireTourSidebar(guide);
    }

    static bool TryWireTourSidebar(EnvironmentLearningTourGuide guide)
    {
        if (guide == null)
            return false;

        if (guide.sidebarPanel != null)
            return true;

        GameObject sidebar = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.name.Trim() == "EnvironmentLearningTourSidebar")
            {
                sidebar = go;
                break;
            }
        }

        if (sidebar == null)
            return false;

        guide.sidebarPanel = sidebar;
        guide.sidebarRoot = sidebar.GetComponent<RectTransform>();
        if (guide.sidebarRoot == null)
            guide.sidebarRoot = sidebar.GetComponentInChildren<RectTransform>(true);

        if (!sidebar.activeSelf)
            sidebar.SetActive(false);

        return true;
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
