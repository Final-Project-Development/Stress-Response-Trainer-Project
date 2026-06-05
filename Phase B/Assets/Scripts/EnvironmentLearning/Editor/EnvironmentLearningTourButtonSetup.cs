#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manual tour sidebar helpers — labels, nav wiring, and layout inside Background.
/// </summary>
public static class EnvironmentLearningTourButtonSetup
{
    const string SidebarName = "EnvironmentLearningTourSidebar";
    const float ButtonLabelFontSize = 18f;
    const float HeaderLabelFontSize = 20f;
    const float HeaderHeight = 26f;
    const float ButtonHeight = 46f;
    const float ButtonSpacing = 2f;
    const float SectionGap = 6f;
    const float TopPadding = 24f;
    const float BottomPadding = 24f;
    const float ContentWidth = 200f;

    [MenuItem("Tools/Stress Trainer/Setup Selected Tour Sidebar Button")]
    public static void SetupSelectedButton()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("Stress Trainer", "Select one sidebar button in the Hierarchy.", "OK");
            return;
        }

        if (!SetupButton(Selection.activeGameObject, out string message))
            EditorUtility.DisplayDialog("Stress Trainer", message, "OK");
        else
            Debug.Log(message);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Stress Trainer/Wire All Tour Sidebar Buttons")]
    public static void WireAllButtons()
    {
        var sidebar = FindSidebar();
        if (sidebar == null)
        {
            EditorUtility.DisplayDialog(
                "Stress Trainer",
                $"Could not find '{SidebarName}' in the scene.",
                "OK");
            return;
        }

        EnsureLearningWiring(sidebar);
        ApplySectionHeaderTextOnly(sidebar.transform);

        int count = 0;
        var buttons = sidebar.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            if (IsHeaderNode(buttons[i].gameObject.name))
                continue;

            if (SetupButton(buttons[i].gameObject, out _))
                count++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Wired {count} tour sidebar buttons under '{sidebar.name}'.");
    }

    static bool SetupButton(GameObject buttonGo, out string message)
    {
        message = string.Empty;
        if (buttonGo == null)
        {
            message = "No button selected.";
            return false;
        }

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
        {
            message = $"'{buttonGo.name}' has no Button component.";
            return false;
        }

        string displayName = buttonGo.name.Trim();
        EnsureTextLabel(buttonGo.transform, displayName);

        Undo.RecordObject(button, "Clear tour button onClick");
        button.onClick = new Button.ButtonClickedEvent();

        var nav = buttonGo.GetComponent<EnvironmentLearningTourNavButton>();
        if (nav == null)
            nav = Undo.AddComponent<EnvironmentLearningTourNavButton>(buttonGo);

        if (EnvironmentLearningTourObjectNames.TryResolve(displayName, out string sceneObject, out float standoff))
        {
            nav.sceneObjectName = sceneObject;
            nav.standoffMeters = standoff;
        }
        else
        {
            nav.sceneObjectName = displayName;
            nav.standoffMeters = 0f;
            message =
                $"Added label + nav on '{displayName}'. Set Scene Object Name manually in Inspector.";
            EditorUtility.SetDirty(nav);
            return true;
        }

        EditorUtility.SetDirty(nav);
        message = $"Button '{displayName}' -> scene object '{sceneObject}'.";
        return true;
    }

    static void EnsureTextLabel(Transform button, string labelText)
    {
        FixWrongTextComponents(button);

        var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null)
        {
            var textGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(textGo, "Add tour button label");
            textGo.transform.SetParent(button, false);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;

            tmp = textGo.GetComponent<TextMeshProUGUI>();
        }

        tmp.text = labelText;
        ApplyButtonTextStyle(tmp, button);
    }

    static void ApplyButtonTextStyle(TextMeshProUGUI tmp, Transform button)
    {
        if (tmp == null)
            return;

        var buttonRect = button as RectTransform;
        float height = buttonRect != null ? buttonRect.sizeDelta.y : 70f;

        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = ButtonLabelFontSize;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = Mathf.Max(ButtonLabelFontSize, height * 0.38f);
        tmp.enableAutoSizing = true;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.09f, 0.07f, 0.23f, 1f);
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        var rect = tmp.rectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
            rect.localScale = Vector3.one;
        }

        EditorUtility.SetDirty(tmp);
    }

    static void FixWrongTextComponents(Transform button)
    {
        var children = button.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == button)
                continue;

            var child = children[i];
            if (!child.name.Contains("Text"))
                continue;

            var tmp3d = child.GetComponent<TextMeshPro>();
            if (tmp3d == null)
                continue;

            string savedText = string.IsNullOrWhiteSpace(tmp3d.text) ? button.name.Trim() : tmp3d.text;
            Undo.DestroyObjectImmediate(tmp3d);

            var meshRenderer = child.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                Undo.DestroyObjectImmediate(meshRenderer);

            var meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter != null)
                Undo.DestroyObjectImmediate(meshFilter);

            var tmpUi = child.GetComponent<TextMeshProUGUI>();
            if (tmpUi == null)
            {
                tmpUi = Undo.AddComponent<TextMeshProUGUI>(child.gameObject);
                if (child.GetComponent<CanvasRenderer>() == null)
                    Undo.AddComponent<CanvasRenderer>(child.gameObject);
            }

            tmpUi.text = savedText;
            ApplyButtonTextStyle(tmpUi, button);
        }
    }

    static void ApplySectionHeaderTextOnly(Transform sidebar)
    {
        ApplyHeaderTextOnly(FindChildByName(sidebar, "sim1_TXT"), "Simulation 1");
        ApplyHeaderTextOnly(FindChildByName(sidebar, "sim2_TXT"), "Simulation 2");
    }

    static void ApplyHeaderTextOnly(Transform header, string label)
    {
        if (header == null)
            return;

        var tmp = header.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = Undo.AddComponent<TextMeshProUGUI>(header.gameObject);

        if (header.GetComponent<CanvasRenderer>() == null)
            Undo.AddComponent<CanvasRenderer>(header.gameObject);

        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = HeaderLabelFontSize;
        tmp.enableAutoSizing = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.92f, 0.9f, 1f, 1f);
        tmp.raycastTarget = false;
        EditorUtility.SetDirty(tmp);
    }

    static void FitSidebarToBackground(Transform sidebar)
    {
        var background = FindChildByName(sidebar, "Background") as RectTransform;
        var sim1 = FindChildByName(sidebar, "sim1");
        var sim2 = FindChildByName(sidebar, "sim2");
        if (background == null)
            return;

        EnsureSectionUnderBackground(sim1, background);
        EnsureSectionUnderBackground(sim2, background);

        float topY = background.rect.height * 0.5f - TopPadding;
        float bottomLimit = -background.rect.height * 0.5f + BottomPadding;

        if (sim1 != null)
            topY = ArrangeSection(sim1, topY, bottomLimit) - SectionGap;

        if (sim2 != null)
            ArrangeSection(sim2, topY, bottomLimit);
    }

    static void EnsureSectionUnderBackground(Transform section, RectTransform background)
    {
        if (section == null || background == null)
            return;

        if (section.parent == background)
            return;

        Undo.SetTransformParent(section, background, "Move tour section under Background");
        var rect = section as RectTransform;
        if (rect != null)
        {
            Undo.RecordObject(rect, "Reset tour section transform");
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        else
        {
            Undo.RecordObject(section, "Reset tour section transform");
            section.localPosition = Vector3.zero;
            section.localScale = Vector3.one;
        }
    }

    static float ArrangeSection(Transform section, float startY, float bottomLimit)
    {
        float y = startY;

        for (int i = 0; i < section.childCount; i++)
        {
            var child = section.GetChild(i);
            var rect = child as RectTransform;
            if (rect == null)
                continue;

            if (IsHeaderNode(child.name))
            {
                float headerStep = HeaderHeight + 6f;
                y -= headerStep * 0.5f;
                Undo.RecordObject(rect, "Arrange tour header");
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(ContentWidth, HeaderHeight);
                rect.anchoredPosition = new Vector2(0f, y);
                rect.localScale = Vector3.one;
                y -= headerStep * 0.5f;
                continue;
            }

            if (child.GetComponent<Button>() == null)
                continue;

            float buttonHeight = ButtonHeight;
            float step = buttonHeight + ButtonSpacing;
            y -= step * 0.5f;

            if (y - step * 0.5f < bottomLimit)
            {
                buttonHeight = Mathf.Max(40f, buttonHeight - 4f);
                step = buttonHeight + 1f;
                y = Mathf.Max(y, bottomLimit + step * 0.5f);
            }

            Undo.RecordObject(rect, "Arrange tour button");
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ContentWidth, buttonHeight);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.localScale = Vector3.one;
            y -= step * 0.5f;
        }

        return y;
    }

    static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name.Trim().Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    static void EnsureLearningWiring(GameObject sidebar)
    {
        var learning = Object.FindFirstObjectByType<EnvironmentLearningController>(FindObjectsInactive.Include);
        if (learning == null)
            return;

        var guide = learning.GetComponent<EnvironmentLearningTourGuide>();
        if (guide == null)
            guide = Undo.AddComponent<EnvironmentLearningTourGuide>(learning.gameObject);

        learning.tourGuide = guide;
        guide.sidebarPanel = sidebar;

        var rect = sidebar.GetComponent<RectTransform>();
        if (rect == null)
            rect = sidebar.GetComponentInChildren<RectTransform>(true);

        guide.sidebarRoot = rect;
        guide.EnsureSidebarHidden();
        EditorUtility.SetDirty(learning);
        EditorUtility.SetDirty(guide);
    }

    static GameObject FindSidebar()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.name.Trim() == SidebarName)
                return go;
        }

        return null;
    }

    static bool IsHeaderNode(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName))
            return true;

        string lower = nodeName.ToLowerInvariant();
        return lower.Contains("_txt") || lower == "background";
    }
}
#endif
