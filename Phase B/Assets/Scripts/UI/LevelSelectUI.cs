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

    [Header("Environment Learning card (Level_Select_UI scroll list)")]
    [Tooltip("TMP under the EnvironmentLearning row. If empty, searches child named EnvironmentLearning.")]
    public TextMeshProUGUI environmentLearningCardText;
    public string environmentLearningCardLabel = "Environment Learning";

    [TextArea] public string panelTitle = "בחרי סימולציה";
    [TextArea] public string panelSubtitle = "או סיור היכרות ללמידת העיר";

    [Header("Scroll (Level_Select_UI list)")]
    [Tooltip("Scrollable_Area ScrollRect. If empty, finds ScrollRect under this panel.")]
    [SerializeField] ScrollRect levelSelectScroll;
    [SerializeField] bool scrollToTopOnOpen = true;

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

        var block = FindEnvironmentLearningBlock();
        if (block == null)
            return;

        var labels = block.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;
            if (labels[i].name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            labels[i].text = environmentLearningCardLabel;
        }
    }

    static Transform FindEnvironmentLearningBlock()
    {
        var roots = FindObjectsByType<LevelSelectUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int r = 0; r < roots.Length; r++)
        {
            var row = FindDeepChild(roots[r].transform, "EnvironmentLearning");
            if (row != null)
                return row;
        }

        return null;
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
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
