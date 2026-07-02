#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Sets up Sim1/Sim2 visual briefing grids, text, and optional scene thumbnails.</summary>
public static class MissionBriefingPanelSetupEditor
{
    const string Sim1PanelName = "Sim1Briefing_Panel";
    const string Sim2PanelName = "Sim2Briefing_Panel";
    const string ThumbnailRoot = "Assets/Resources/Briefing/Thumbnails";

    [MenuItem("Tools/Stress Trainer/Setup Simulation Briefing Panels")]
    public static void SetupAllBriefingPanels()
    {
        SetupSimulationPanel(Sim1PanelName, MissionBriefingCatalog.Simulation.Simulation1);
        SetupSimulationPanel(Sim2PanelName, MissionBriefingCatalog.Simulation.Simulation2);
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Stress Trainer", "Simulation briefing panels updated.\n\nRun \"Assign Prefab Icons to Briefing Cards\" to use each item's prefab image on the Icon.", "OK");
    }

    [MenuItem("Tools/Stress Trainer/Assign Prefab Icons to Briefing Cards")]
    public static void AssignPrefabIconsToBriefingCards()
    {
        var assigned = AssignPrefabIconsToBriefingCardsSilent();
        EditorUtility.DisplayDialog(
            "Stress Trainer",
            $"Assigned prefab icons to {assigned} briefing cards.\n\nSprites saved under Assets/Resources/Briefing/Thumbnails/",
            "OK");
    }

    public static int AssignPrefabIconsToBriefingCardsSilent()
    {
        EnsureFolder($"{ThumbnailRoot}/Sim1");
        EnsureFolder($"{ThumbnailRoot}/Sim2");

        var sim1Count = AssignIconsForPanel(Sim1PanelName, MissionBriefingCatalog.Simulation.Simulation1);
        var sim2Count = AssignIconsForPanel(Sim2PanelName, MissionBriefingCatalog.Simulation.Simulation2);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return sim1Count + sim2Count;
    }

    [MenuItem("Tools/Stress Trainer/Capture Briefing Thumbnails (from scene)")]
    public static void CaptureAllBriefingThumbnails()
    {
        AssignPrefabIconsToBriefingCards();
    }

    static void SetupSimulationPanel(string panelName, MissionBriefingCatalog.Simulation simulation)
    {
        var panel = GameObject.Find(panelName);
        if (panel == null)
        {
            Debug.LogWarning($"MissionBriefingPanelSetup: Could not find {panelName}.");
            return;
        }

        if (simulation == MissionBriefingCatalog.Simulation.Simulation2 &&
            panel.GetComponentsInChildren<MissionBriefingItemCard>(true).Length == 0)
            BuildSim2Grids(panel.transform);

        var controller = panel.GetComponent<SimulationBriefingPanelController>();
        if (controller == null)
            controller = panel.AddComponent<SimulationBriefingPanelController>();
        controller.simulation = simulation;
        controller.hideLegacyBodyText = true;

        var cards = panel.GetComponentsInChildren<MissionBriefingItemCard>(true);
        if (cards.Length == 0)
        {
            foreach (var grid in panel.GetComponentsInChildren<GridLayoutGroup>(true))
            {
                for (int i = 0; i < grid.transform.childCount; i++)
                {
                    var child = grid.transform.GetChild(i);
                    if (child.GetComponent<MissionBriefingItemCard>() == null)
                        child.gameObject.AddComponent<MissionBriefingItemCard>();
                }
            }
            cards = panel.GetComponentsInChildren<MissionBriefingItemCard>(true);
        }

        for (int i = 0; i < cards.Length; i++)
        {
            PrepareItemCard(cards[i], simulation);
            cards[i].ApplyContent();
            EditorUtility.SetDirty(cards[i]);
        }

        MissionBriefingEditorIconLoader.ClearCache();

        controller.Refresh();
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(panel);
        Debug.Log($"MissionBriefingPanelSetup: configured {panelName} ({cards.Length} item cards).");
    }

    static void PrepareItemCard(MissionBriefingItemCard card, MissionBriefingCatalog.Simulation simulation)
    {
        card.ConfigureSimulation(simulation);

        var label = card.transform.Find("Text (TMP)");
        if (label != null && card.transform.Find("Label") == null)
            label.name = "Label";

        var container = card.transform.Find("Container") as RectTransform;
        if (container == null)
            return;

        var icon = container.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
            icon.preserveAspect = true;

        var hint = container.Find("LocationHint");
        if (hint == null)
            hint = CreateLocationHint(container, card.transform.Find("Label")?.GetComponent<TextMeshProUGUI>()).transform;

        card.containerRect = container;
        card.iconImage = icon;
        card.locationHintText = hint.GetComponent<TextMeshProUGUI>();
        card.itemLabelText = card.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
    }

    static GameObject CreateLocationHint(RectTransform container, TextMeshProUGUI labelFontSource)
    {
        var hintGo = new GameObject("LocationHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(container, false);

        var hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 8f);
        hintRect.sizeDelta = new Vector2(-16f, 48f);

        var tmp = hintGo.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        if (labelFontSource != null && labelFontSource.font != null)
            tmp.font = labelFontSource.font;

        return hintGo;
    }

    static void BuildSim2Grids(Transform sim2Panel)
    {
        var sim1 = GameObject.Find(Sim1PanelName);
        if (sim1 == null)
        {
            Debug.LogWarning("MissionBriefingPanelSetup: Sim1Briefing_Panel not found; cannot clone Sim2 layout.");
            return;
        }

        var step1 = sim1.transform.Find("Step 1-Collect 5 supplies");
        var step2 = sim1.transform.Find("Steps 2\u20134-Secure the home");
        if (step1 == null || step2 == null)
        {
            Debug.LogWarning("MissionBriefingPanelSetup: Sim1 grid headers not found.");
            return;
        }

        var sim2Step1 = Object.Instantiate(step1.gameObject, sim2Panel);
        sim2Step1.name = "Step 1-Find kit and wounded";
        var sim2Step2 = Object.Instantiate(step2.gameObject, sim2Panel);
        sim2Step2.name = "Steps 2\u20134-Phone and treat";

        TrimGridChildren(sim2Step1.transform, new[] { "firstaid", "wounded" });
        TrimGridChildren(sim2Step2.transform, new[] { "phone", "treatment" });

        PositionSim2Grid(sim2Step1.GetComponent<RectTransform>(), 172.98932f);
        PositionSim2Grid(sim2Step2.GetComponent<RectTransform>(), -99f);
    }

    static void PositionSim2Grid(RectTransform rect, float y)
    {
        if (rect == null)
            return;
        rect.anchoredPosition = new Vector2(24.888966f, y);
    }

    static void TrimGridChildren(Transform gridRoot, IReadOnlyList<string> keepNames)
    {
        var keep = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < keepNames.Count; i++)
            keep.Add(keepNames[i]);

        var toRemove = new List<GameObject>();
        for (int i = 0; i < gridRoot.childCount; i++)
        {
            var child = gridRoot.GetChild(i);
            if (!keep.Contains(child.name))
                toRemove.Add(child.gameObject);
        }

        for (int i = 0; i < toRemove.Count; i++)
            Object.DestroyImmediate(toRemove[i]);
    }

    static int AssignIconsForPanel(string panelName, MissionBriefingCatalog.Simulation simulation)
    {
        var panel = GameObject.Find(panelName);
        if (panel == null)
            return 0;

        var cards = panel.GetComponentsInChildren<MissionBriefingItemCard>(true);
        var assigned = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            var key = cards[i].gameObject.name;
            if (!MissionBriefingCatalog.TryGet(simulation, key, out var entry))
                continue;

            var resourcePath = MissionBriefingCatalog.ThumbnailResourcePath(simulation, key);
            var filePath = $"Assets/Resources/{resourcePath}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);

            if (sprite == null && !string.IsNullOrWhiteSpace(entry.PrefabAssetPath))
                sprite = MissionBriefingPrefabIconCapture.CaptureAndSaveSprite(entry.PrefabAssetPath, filePath);

            if (sprite == null && !string.IsNullOrWhiteSpace(entry.SceneObjectName))
                sprite = MissionBriefingPrefabIconCapture.CaptureSceneObjectSprite(entry.SceneObjectName, filePath);

            if (sprite == null)
                sprite = MissionBriefingEditorIconLoader.GetSpriteForEntry(entry);

            if (sprite != null && AssetDatabase.LoadAssetAtPath<Sprite>(filePath) == null)
                sprite = MissionBriefingEditorIconLoader.SaveSpriteToPng(sprite, filePath) ?? sprite;

            if (sprite == null)
            {
                Debug.LogWarning($"MissionBriefingPanelSetup: could not capture icon for {key}.");
                continue;
            }

            var persisted = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
            if (persisted != null)
                sprite = persisted;

            cards[i].iconOverride = sprite;
            if (cards[i].iconImage != null)
            {
                cards[i].iconImage.sprite = sprite;
                cards[i].iconImage.preserveAspect = true;
                cards[i].iconImage.color = Color.white;
                cards[i].iconImage.enabled = true;
                EditorUtility.SetDirty(cards[i].iconImage);
            }
            cards[i].ApplyContent();
            EditorUtility.SetDirty(cards[i]);
            assigned++;
        }

        return assigned;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
