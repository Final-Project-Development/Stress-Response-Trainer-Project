#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates MissionStatus_Panel on the Canvas and wires FlowManager + GameManager.
/// </summary>
public static class MissionStatusPanelSetup
{
    const string PanelName = "MissionStatus_Panel";

    [MenuItem("Tools/Stress Trainer/Wire Mission Status Panel (manual UI)")]
    public static void WireManualMissionStatusPanel()
    {
        var panel = Object.FindFirstObjectByType<MissionStatusPanelController>(FindObjectsInactive.Include);
        if (panel == null)
        {
            var outer = GameObject.Find("MissionStatus_Panel");
            if (outer == null)
                outer = GameObject.Find("MissionStatus_Panel ");
            if (outer != null && outer.GetComponent<MissionStatusPanelController>() == null)
                panel = outer.AddComponent<MissionStatusPanelController>();
        }

        if (panel == null)
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                "Add MissionStatusPanelController to your outer MissionStatus_Panel, assign the TMP fields, then run this again.",
                "OK");
            return;
        }

        AutoWirePanelReferences(panel);

        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow != null)
            flow.missionStatusPanel = panel;

        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
            gm.missionStatusPanel = panel;

        EditorUtility.SetDirty(panel);
        if (flow != null) EditorUtility.SetDirty(flow);
        if (gm != null) EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("Mission Status Panel wired to FlowManager and GameManager.");
    }

    [MenuItem("Tools/Stress Trainer/Create Mission Status Panel")]
    public static void CreateMissionStatusPanel()
    {
        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No TrainingFlowController (FlowManager) in scene.", "OK");
            return;
        }

        var watchPanel = GameObject.Find("WatchHrChart_Panel");
        Transform canvas = watchPanel != null
            ? watchPanel.transform.parent
            : Object.FindFirstObjectByType<Canvas>()?.transform;

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "No Canvas found in scene.", "OK");
            return;
        }

        var existing = GameObject.Find(PanelName);
        MissionStatusPanelController controller;
        if (existing != null)
        {
            controller = existing.GetComponent<MissionStatusPanelController>();
            if (controller == null)
                controller = existing.AddComponent<MissionStatusPanelController>();
        }
        else
        {
            existing = BuildPanelHierarchy(canvas, watchPanel);
            controller = existing.GetComponent<MissionStatusPanelController>();
        }

        WireFlowAndGameManager(flow, controller);
        existing.SetActive(false);

        EditorUtility.SetDirty(flow);
        EditorUtility.SetDirty(controller);
        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
            EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = existing;
        Debug.Log("Mission Status Panel created/updated. Position it in the Canvas like WatchHrChart_Panel, then save the scene.");
    }

    static GameObject BuildPanelHierarchy(Transform canvas, GameObject watchPanel)
    {
        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas, false);

        var panelRect = panel.GetComponent<RectTransform>();
        if (watchPanel != null)
        {
            var watchRect = watchPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = watchRect.anchorMin;
            panelRect.anchorMax = watchRect.anchorMax;
            panelRect.pivot = watchRect.pivot;
            panelRect.sizeDelta = new Vector2(420f, 150f);
            panelRect.anchoredPosition = new Vector2(-637f, watchRect.anchoredPosition.y);
            panelRect.localScale = watchRect.localScale;
        }
        else
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(420f, 150f);
        }

        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.18f, 0.92f);
        bg.raycastTarget = false;

        var title = CreateTmp(panel.transform, "MissionTitle", "Mission", 22, FontStyles.Bold,
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f));

        var completed = CreateTmp(panel.transform, "CompletedText", "Completed: —", 18, FontStyles.Italic,
            new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.68f));

        var objective = CreateTmp(panel.transform, "ObjectiveText", "Next: —", 20, FontStyles.Normal,
            new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.4f));

        var hintGo = new GameObject("HintButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hintGo.transform.SetParent(panel.transform, false);
        var hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.72f, 0.02f);
        hintRect.anchorMax = new Vector2(0.95f, 0.12f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;
        var hintImage = hintGo.GetComponent<Image>();
        hintImage.color = new Color(0.2f, 0.45f, 0.75f, 1f);
        var hintLabel = CreateTmp(hintGo.transform, "Label", "Hint", 18, FontStyles.Bold,
            Vector2.zero, Vector2.one);
        hintLabel.raycastTarget = false;

        var controller = panel.AddComponent<MissionStatusPanelController>();
        controller.panelRoot = panel;
        controller.completedText = completed;
        controller.objectiveText = objective;
        controller.hintButton = hintGo.GetComponent<Button>();
        controller.hintButtonLabel = hintLabel;

        return panel;
    }

    static TextMeshProUGUI CreateTmp(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    static void WireFlowAndGameManager(TrainingFlowController flow, MissionStatusPanelController controller)
    {
        flow.missionStatusPanel = controller;

        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
            gm.missionStatusPanel = controller;
    }

    static void AutoWirePanelReferences(MissionStatusPanelController panel)
    {
        if (panel.panelRoot == null)
            panel.panelRoot = panel.gameObject;

        if (panel.missionTitleText == null)
            panel.missionTitleText = FindTmpInChildren(panel.transform, "MissionTitle");
        if (panel.completedText == null)
            panel.completedText = FindTmpInChildren(panel.transform, "CompletedText");
        if (panel.objectiveText == null)
            panel.objectiveText = FindTmpInChildren(panel.transform, "ObjectiveText");

        if (panel.hintButton == null)
        {
            var hint = FindChildByNameContains(panel.transform, "HintButton");
            if (hint != null)
                panel.hintButton = hint.GetComponent<Button>();
        }

        if (panel.hintButtonLabel == null && panel.hintButton != null)
            panel.hintButtonLabel = panel.hintButton.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    static TextMeshProUGUI FindTmpInChildren(Transform root, string namePart)
    {
        var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            if (tmps[i] != null && tmps[i].name.Trim().StartsWith(namePart, System.StringComparison.OrdinalIgnoreCase))
                return tmps[i];
        }

        return null;
    }

    static Transform FindChildByNameContains(Transform root, string namePart)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.Trim().IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return all[i];
        }

        return null;
    }
}
#endif
