using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to Level_Select_UI (simulation pick panel after calibration).
/// Wire buttons to the public methods below.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] TrainingFlowController trainingFlow;

    [Header("Optional copy on this panel")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;

    [Header("Environment Learning card")]
    [Tooltip("Title TMP under the EnvironmentLearning row.")]
    public TextMeshProUGUI environmentLearningCardText;
    public string environmentLearningCardLabel = "Environment Learning";
    [Tooltip("Subtitle TMP inside the card Container. Created automatically if empty.")]
    public TextMeshProUGUI environmentLearningSubtitleText;
    [TextArea] public string environmentLearningSubtitle =
        "Explore the city and learn where key items are located.";

    [Header("Simulation 1 card")]
    public TextMeshProUGUI simulation1SubtitleText;
    [TextArea] public string simulation1Subtitle =
        "Collect emergency supplies, secure your home, and reach the shelter.";
    public string simulation1LevelValue = "1";

    [Header("Simulation 2 card")]
    public TextMeshProUGUI simulation2SubtitleText;
    [TextArea] public string simulation2Subtitle =
        "Find a first aid kit, help an injured person, and call for help.";
    public string simulation2LevelValue = "2";

    [Header("Simulation level badges")]
    [SerializeField] Sprite simulationLevelBadgeSprite;
    [SerializeField] Sprite simulation1MissionIconSprite;
    [SerializeField] Sprite simulation2MissionIconSprite;

    [TextArea] public string panelTitle = "בחרי סימולציה";
    [TextArea] public string panelSubtitle = "או סיור היכרות ללמידת העיר";

    [Header("Scroll (Level_Select_UI list)")]
    [Tooltip("Scrollable_Area ScrollRect. If empty, finds ScrollRect under this panel.")]
    [SerializeField] ScrollRect levelSelectScroll;
    [SerializeField] bool scrollToTopOnOpen = true;

    const string EnvironmentLearningRowName = "EnvironmentLearning";
    const string Simulation1RowName = "Emergency preparedness";
    const string Simulation2RowName = "First Aid";
    const string SubtitleObjectName = "Subtitle";
    const string LevelIconImageName = "level-icon-image";
    const string LevelValueObjectName = "Level_Value";
    const string MissionIconObjectName = "MissionIcon";
    const string LegacyIconObjectName = "Icon";

    void Awake()
    {
        if (trainingFlow == null)
            trainingFlow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (levelSelectScroll == null)
            levelSelectScroll = GetComponentInChildren<ScrollRect>(true);
    }

    void OnEnable()
    {
        ApplyCopy();
        if (scrollToTopOnOpen)
            StartCoroutine(ScrollToTopAfterLayout());
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ApplyCopy();
    }
#endif

    /// <summary>Call from code when the panel is shown without OnEnable (optional).</summary>
    public void ScrollToTop()
    {
        if (scrollToTopOnOpen)
            StartCoroutine(ScrollToTopAfterLayout());
    }

    IEnumerator ScrollToTopAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        var scroll = levelSelectScroll != null ? levelSelectScroll : GetComponentInChildren<ScrollRect>(true);
        if (scroll == null)
            yield break;

        scroll.StopMovement();
        scroll.velocity = Vector2.zero;
        scroll.verticalNormalizedPosition = 1f;
    }

    public void ApplyCopy()
    {
        if (titleText != null)
            titleText.text = panelTitle;
        if (subtitleText != null)
            subtitleText.text = panelSubtitle;

        ApplyEnvironmentLearningCardLabel();
        ApplyCardSubtitles();
        ApplySimulationLevelBadges();
    }

    void ApplyEnvironmentLearningCardLabel()
    {
        if (string.IsNullOrEmpty(environmentLearningCardLabel))
            return;

        if (environmentLearningCardText != null)
        {
            environmentLearningCardText.text = environmentLearningCardLabel;
            return;
        }

        var block = FindCardRow(EnvironmentLearningRowName);
        if (block == null)
            return;

        var labels = block.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;
            if (IsSubtitleText(labels[i]))
                continue;
            if (labels[i].name.Equals(LevelValueObjectName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (labels[i].name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            labels[i].text = environmentLearningCardLabel;
            environmentLearningCardText = labels[i];
            return;
        }
    }

    void ApplyCardSubtitles()
    {
        environmentLearningSubtitleText = ApplySubtitleForCard(
            EnvironmentLearningRowName,
            environmentLearningSubtitleText,
            environmentLearningSubtitle);

        simulation1SubtitleText = ApplySubtitleForCard(
            Simulation1RowName,
            simulation1SubtitleText,
            simulation1Subtitle);

        simulation2SubtitleText = ApplySubtitleForCard(
            Simulation2RowName,
            simulation2SubtitleText,
            simulation2Subtitle);
    }

    TextMeshProUGUI ApplySubtitleForCard(string rowName, TextMeshProUGUI subtitleTextField, string subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle))
            return subtitleTextField;

        var row = FindCardRow(rowName);
        if (row == null)
            return subtitleTextField;

        var container = row.Find("Container") as RectTransform;
        if (container == null)
            return subtitleTextField;

        var subtitleText = subtitleTextField;
        if (subtitleText == null || subtitleText.transform.parent != container)
            subtitleText = FindSubtitleUnderContainer(container);

        if (subtitleText == null)
            subtitleText = CreateSubtitleUnderContainer(container);

        subtitleText.text = subtitle.Trim();
        return subtitleText;
    }

    static bool IsSubtitleText(TextMeshProUGUI text) =>
        text != null &&
        text.name.Equals(SubtitleObjectName, System.StringComparison.OrdinalIgnoreCase);

    static TextMeshProUGUI FindSubtitleUnderContainer(RectTransform container)
    {
        var subtitleTransform = container.Find(SubtitleObjectName);
        return subtitleTransform != null ? subtitleTransform.GetComponent<TextMeshProUGUI>() : null;
    }

    static TextMeshProUGUI CreateSubtitleUnderContainer(RectTransform container)
    {
        var referenceLabel = container.parent != null
            ? container.parent.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;

        var subtitleGo = new GameObject(SubtitleObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleGo.transform.SetParent(container, false);

        var subtitleRect = subtitleGo.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 0f);
        subtitleRect.anchorMax = new Vector2(1f, 0f);
        subtitleRect.pivot = new Vector2(0.5f, 0f);
        subtitleRect.anchoredPosition = new Vector2(0f, 10f);
        subtitleRect.sizeDelta = new Vector2(-20f, 72f);

        var subtitleText = subtitleGo.GetComponent<TextMeshProUGUI>();
        subtitleText.raycastTarget = false;
        subtitleText.fontSize = 18f;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.textWrappingMode = TextWrappingModes.Normal;
        subtitleText.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        if (referenceLabel != null && referenceLabel.font != null)
            subtitleText.font = referenceLabel.font;

        return subtitleText;
    }

    Transform FindCardRow(string rowName)
    {
        var row = FindDeepChild(transform, rowName);
        if (row != null)
            return row;

        return FindDeepChild(FindEnvironmentLearningBlockRoot(), rowName);
    }

    Transform FindEnvironmentLearningBlockRoot()
    {
        var roots = FindObjectsByType<LevelSelectUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int r = 0; r < roots.Length; r++)
        {
            var row = FindDeepChild(roots[r].transform, EnvironmentLearningRowName);
            if (row != null)
                return roots[r].transform;
        }

        return transform;
    }

    void ApplySimulationLevelBadges()
    {
        ApplySimulationLevelBadge(
            Simulation1RowName,
            simulation1LevelValue,
            simulation1MissionIconSprite ?? MissionBriefingCatalog.LoadThumbnail(
                MissionBriefingCatalog.Simulation.Simulation1, "mamad"));

        ApplySimulationLevelBadge(
            Simulation2RowName,
            simulation2LevelValue,
            simulation2MissionIconSprite ?? MissionBriefingCatalog.LoadThumbnail(
                MissionBriefingCatalog.Simulation.Simulation2, "firstaid"));
    }

    void ApplySimulationLevelBadge(string rowName, string levelValue, Sprite missionIconSprite)
    {
        if (string.IsNullOrWhiteSpace(levelValue))
            return;

        var row = FindCardRow(rowName);
        if (row == null)
            return;

        var container = row.Find("Container") as RectTransform;
        if (container == null)
            return;

        var badgeImage = FindLevelBadgeImage(container);
        if (badgeImage == null)
            return;

        if (simulationLevelBadgeSprite != null)
            badgeImage.sprite = simulationLevelBadgeSprite;

        badgeImage.preserveAspect = true;
        badgeImage.color = Color.white;
        badgeImage.enabled = true;

        var badgeRect = badgeImage.rectTransform;
        badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = Vector2.zero;
        badgeRect.sizeDelta = new Vector2(100f, 114f);

        var levelValueText = FindOrCreateLevelValueText(badgeRect);
        levelValueText.text = levelValue.Trim();

        ApplyMissionIconOverlay(badgeRect, missionIconSprite);
    }

    static Image FindLevelBadgeImage(RectTransform container)
    {
        var badge = container.Find(LevelIconImageName);
        if (badge == null)
            badge = container.Find(LegacyIconObjectName);
        return badge != null ? badge.GetComponent<Image>() : null;
    }

    static TextMeshProUGUI FindOrCreateLevelValueText(RectTransform badgeRect)
    {
        var levelValue = badgeRect.Find(LevelValueObjectName);
        TextMeshProUGUI levelValueText;

        if (levelValue == null)
        {
            var levelValueGo = new GameObject(LevelValueObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            levelValueGo.transform.SetParent(badgeRect, false);
            levelValue = levelValueGo.transform;
            levelValueText = levelValueGo.GetComponent<TextMeshProUGUI>();
            levelValueText.raycastTarget = false;
            levelValueText.fontSize = 36f;
            levelValueText.enableAutoSizing = true;
            levelValueText.fontSizeMin = 6f;
            levelValueText.fontSizeMax = 72f;
            levelValueText.alignment = TextAlignmentOptions.Center;
            levelValueText.verticalAlignment = VerticalAlignmentOptions.Middle;
            levelValueText.color = new Color(0.17254902f, 0.121568635f, 0.30588236f, 1f);

            var referenceLabel = badgeRect.parent != null
                ? badgeRect.parent.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            if (referenceLabel != null && referenceLabel.font != null)
                levelValueText.font = referenceLabel.font;
        }
        else
        {
            levelValue.SetParent(badgeRect, false);
            levelValueText = levelValue.GetComponent<TextMeshProUGUI>();
        }

        var levelValueRect = levelValue as RectTransform;
        levelValueRect.anchorMin = new Vector2(0.5f, 0.5f);
        levelValueRect.anchorMax = new Vector2(0.5f, 0.5f);
        levelValueRect.pivot = new Vector2(0.5f, 0.5f);
        levelValueRect.anchoredPosition = new Vector2(-0.2622f, 8.1872f);
        levelValueRect.sizeDelta = new Vector2(71.238f, 73.9067f);
        levelValueRect.localScale = Vector3.one;

        return levelValueText;
    }

    static void ApplyMissionIconOverlay(RectTransform badgeRect, Sprite missionIconSprite)
    {
        var missionIconTransform = badgeRect.Find(MissionIconObjectName);
        if (missionIconSprite == null)
        {
            if (missionIconTransform != null)
                missionIconTransform.gameObject.SetActive(false);
            return;
        }

        Image missionIconImage;
        if (missionIconTransform == null)
        {
            var missionIconGo = new GameObject(MissionIconObjectName, typeof(RectTransform), typeof(Image));
            missionIconGo.transform.SetParent(badgeRect, false);
            missionIconImage = missionIconGo.GetComponent<Image>();
            missionIconImage.raycastTarget = false;
        }
        else
        {
            missionIconImage = missionIconTransform.GetComponent<Image>();
            missionIconTransform.gameObject.SetActive(true);
        }

        var missionIconRect = missionIconImage.rectTransform;
        missionIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        missionIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        missionIconRect.pivot = new Vector2(0.5f, 0.5f);
        missionIconRect.anchoredPosition = new Vector2(0f, 6f);
        missionIconRect.sizeDelta = new Vector2(56f, 56f);
        missionIconRect.SetAsFirstSibling();

        missionIconImage.sprite = missionIconSprite;
        missionIconImage.preserveAspect = true;
        missionIconImage.color = Color.white;
        missionIconImage.enabled = true;
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == childName)
                return child;
            var found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    public void SelectSimulation1()
    {
        if (trainingFlow != null)
            trainingFlow.UI_PickSimulation1AfterCalibration();
    }

    public void SelectSimulation2()
    {
        if (trainingFlow != null)
            trainingFlow.UI_PickSimulation2AfterCalibration();
    }

    /// <summary>Third option — walk the city with labels on important objects.</summary>
    public void SelectEnvironmentLearning()
    {
        if (trainingFlow != null)
            trainingFlow.UI_PickEnvironmentLearningAfterCalibration();
    }
}
