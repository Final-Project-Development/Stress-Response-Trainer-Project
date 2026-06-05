using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Free-roam city learning: shows world labels, hides mission gameplay.
/// </summary>
public class EnvironmentLearningController : MonoBehaviour
{
    public static EnvironmentLearningController Instance { get; private set; }

    [Header("HUD (design in scene — no auto layout)")]
    [Tooltip("Your EnvironmentLearningHud GameObject from the Canvas. Image, size, sprite, colors = edit in Inspector only.")]
    public GameObject learningHudRoot;

    [Tooltip("Optional TMP for dynamic text. Leave empty if all copy is static in the hierarchy.")]
    public TextMeshProUGUI learningHudBodyText;

    [Tooltip("When off, Play mode keeps the text you typed in the scene / prefab.")]
    public bool applyDefaultHudTextAtStart;

    [TextArea]
    public string learningHudDefaultText =
        "Environment Learning Tour\n\n" +
        "Walk around and read the names above important items.\n" +
        "No mission, no pickups, and no siren in this phase.\n\n" +
        "When finished — press Back or Esc to return to simulation selection.";

    [Header("Labels")]
    [Tooltip("Background for world item name panels (inventory-highlight-large 1_0).")]
    public Sprite worldLabelPanelSprite;

    public Color worldLabelPanelColor = Color.white;

    [Header("World label panel size (HUB above items)")]
    [Tooltip("World-space canvas scale. Smaller = smaller panel (try 0.004–0.008).")]
    public float worldLabelWorldScale = 0.006f;

    public Vector2 worldLabelPanelSize = new Vector2(140f, 44f);

    [Tooltip("Text size on the panel. 0 = keep prefab value.")]
    public float worldLabelFontSize = 18f;

    [Tooltip("Optional parent of all WorldItemLabel objects.")]
    public Transform labelsRoot;

    [Header("Tour guide sidebar")]
    public EnvironmentLearningTourGuide tourGuide;

    readonly List<WorldItemLabel> _labels = new List<WorldItemLabel>();
    bool _active;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveTourGuide();
        tourGuide?.EnsureSidebarHidden();
    }

    void Start()
    {
        if (!_active)
            tourGuide?.EnsureSidebarHidden();
    }

    void ResolveTourGuide()
    {
        if (tourGuide == null)
            tourGuide = GetComponent<EnvironmentLearningTourGuide>();

        if (tourGuide == null)
            tourGuide = gameObject.AddComponent<EnvironmentLearningTourGuide>();

        tourGuide.ResolveSidebarReferences();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(WorldItemLabel label)
    {
        if (label == null || _labels.Contains(label))
            return;
        _labels.Add(label);
        label.SetVisible(_active);
    }

    public void Unregister(WorldItemLabel label)
    {
        if (label != null)
            _labels.Remove(label);
    }

    public void BeginLearning()
    {
        _active = true;

        if (applyDefaultHudTextAtStart && learningHudBodyText != null && !string.IsNullOrWhiteSpace(learningHudDefaultText))
            learningHudBodyText.text = learningHudDefaultText;

        if (learningHudRoot != null)
            learningHudRoot.SetActive(true);

        ResolveTourGuide();
        tourGuide?.BeginGuide();
        RefreshAllTourLabels(true);
    }

    public void EndLearning()
    {
        _active = false;
        ResolveTourGuide();
        tourGuide?.EndGuide();
        if (learningHudRoot != null)
            learningHudRoot.SetActive(false);
        RefreshAllTourLabels(false);
    }

    public void RefreshAllTourLabels(bool visible)
    {
        _labels.Clear();

        WorldItemLabel[] labels = labelsRoot != null
            ? labelsRoot.GetComponentsInChildren<WorldItemLabel>(true)
            : FindObjectsByType<WorldItemLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < labels.Length; i++)
        {
            WorldItemLabel label = labels[i];
            if (label == null)
                continue;

            EnsureHostHierarchyActive(label.gameObject);
            label.EnsureLabelBuilt();
            label.ApplyAppearanceFromController();
            Register(label);
            label.SetVisible(visible);
        }
    }

    static void EnsureHostHierarchyActive(GameObject host)
    {
        if (host == null)
            return;

        Transform current = host.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
            current = current.parent;
        }
    }
}
