using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// My Profile panel: email, completed simulation count, and recent SCI trend chart.
/// </summary>
public class UserProfileController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject profilePanelRoot;

    [Header("Profile details")]
    [SerializeField] private TextMeshProUGUI emailValueText;
    [SerializeField] private TextMeshProUGUI sessionsCountValueText;

    [Header("My Sessions")]
    [SerializeField] private RectTransform mySessionsSection;
    [SerializeField] private SimpleStressLineGraph sessionsChart;
    [SerializeField] private GameObject sessionsChartRoot;
    [SerializeField] private TextMeshProUGUI sessionsEmptyText;

    [Header("Navigation")]
    [SerializeField] private Button profileOpenButton;
    [SerializeField] private UINavigationManager navigationManager;
    [SerializeField] private TrainingFlowController trainingFlow;

    [Header("Config")]
    [SerializeField] private int maxChartPoints = 8;
    [SerializeField] private float chartAreaPaddingLeft = 56f;
    [SerializeField] private float chartAreaPaddingRight = 56f;
    [SerializeField] private float chartAreaPaddingBottom = 40f;
    [SerializeField] private float chartAreaPaddingTop = 16f;
    [SerializeField] private float sectionTitleTopOffset = 18f;
    [SerializeField] private float captionGapBelowTitle = 8f;
    [SerializeField] private float captionHeight = 22f;
    [SerializeField] private float chartGapBelowCaption = 10f;
    [TextArea]
    [SerializeField] private string emptySessionsMessage =
        "No completed simulations yet. Finish Simulation 1 or Simulation 2 to see your progress here.";

    private bool _isOpen;
    private string _lastLoggedInEmail;
    private Coroutine _refreshChartRoutine;
    private bool _captionReparented;
    private float _computedChartTopInset = 110f;
    private TrainingFlowController.Phase _lastPhase = TrainingFlowController.Phase.Gate;

    public bool IsProfileOpen => _isOpen;

    void Awake()
    {
        if (profilePanelRoot == null)
            profilePanelRoot = gameObject;

        if (navigationManager == null)
            navigationManager = FindFirstObjectByType<UINavigationManager>(FindObjectsInactive.Include);

        if (trainingFlow == null)
            trainingFlow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (sessionsChart == null && sessionsChartRoot != null)
            sessionsChart = sessionsChartRoot.GetComponent<SimpleStressLineGraph>();

        if (sessionsChart == null)
            sessionsChart = GetComponentInChildren<SimpleStressLineGraph>(true);

        if (sessionsChartRoot == null && sessionsChart != null)
            sessionsChartRoot = sessionsChart.gameObject;

        if (mySessionsSection == null && sessionsChartRoot != null)
            mySessionsSection = sessionsChartRoot.transform.parent as RectTransform;

        _isOpen = false;
        SetPanelActive(false);
    }

    void Start()
    {
        RefreshProfileButtonVisibility();
    }

    void Update()
    {
        RefreshProfileAccessState();
    }

    /// <summary>Profile is available only after a successful login, not from Hub / Login screens.</summary>
    public bool CanOpenProfile()
    {
        if (string.IsNullOrEmpty(LocalAuthStore.GetCurrentLoggedInEmail()))
            return false;

        if (trainingFlow == null)
            trainingFlow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (trainingFlow == null)
            return false;

        TrainingFlowController.Phase phase = trainingFlow.CurrentPhase;
        return phase != TrainingFlowController.Phase.Gate
            && phase != TrainingFlowController.Phase.Login;
    }

    private void RefreshProfileAccessState()
    {
        string currentEmail = LocalAuthStore.GetCurrentLoggedInEmail() ?? string.Empty;
        TrainingFlowController.Phase phase = trainingFlow != null
            ? trainingFlow.CurrentPhase
            : TrainingFlowController.Phase.Gate;

        bool emailChanged = currentEmail != _lastLoggedInEmail;
        bool phaseChanged = phase != _lastPhase;
        _lastLoggedInEmail = currentEmail;
        _lastPhase = phase;

        if (!CanOpenProfile() && _isOpen)
            CloseProfile();

        if (emailChanged || phaseChanged)
            RefreshProfileButtonVisibility();
    }

    public void ToggleProfile()
    {
        if (_isOpen)
            CloseProfile();
        else if (CanOpenProfile())
            OpenProfile();
    }

    public void OpenProfile()
    {
        if (!CanOpenProfile())
            return;

        trainingFlow?.UI_StopAllAudio();
        navigationManager?.CloseHelp();
        _isOpen = true;
        SetPanelActive(true);
        RefreshContent();
        navigationManager?.ApplyPlayerCursorMode();
    }

    public void CloseProfile()
    {
        _isOpen = false;
        SetPanelActive(false);
        navigationManager?.ApplyPlayerCursorMode();
    }

    public void RefreshProfileButtonVisibility()
    {
        if (profileOpenButton == null)
            return;

        profileOpenButton.gameObject.SetActive(CanOpenProfile());

        TopBarLayoutController topBarLayout = FindFirstObjectByType<TopBarLayoutController>(FindObjectsInactive.Include);
        topBarLayout?.ApplyLayout();
    }

    /// <summary>Called from always-active top bar UI because this panel may stay inactive.</summary>
    public void RefreshProfileToolbarState()
    {
        RefreshProfileAccessState();
    }

    private void RefreshContent()
    {
        string email = LocalAuthStore.GetCurrentLoggedInEmail();
        int completedCount = SessionHistoryStore.CountCompletedSimulations(email);

        if (emailValueText != null)
        {
            emailValueText.text = string.IsNullOrEmpty(email) ? "—" : email;
            emailValueText.color = new Color(0.92f, 0.94f, 1f, 1f);
        }

        if (sessionsCountValueText != null)
        {
            sessionsCountValueText.text = completedCount.ToString();
            sessionsCountValueText.color = new Color(0.92f, 0.94f, 1f, 1f);
        }

        List<float> sciValues = SessionHistoryStore.GetRecentSimulationMeanSciValues(email, maxChartPoints);
        bool hasData = sciValues != null && sciValues.Count > 0;

        if (sessionsEmptyText != null)
        {
            sessionsEmptyText.text = emptySessionsMessage;
            sessionsEmptyText.alignment = TextAlignmentOptions.Center;
            sessionsEmptyText.verticalAlignment = VerticalAlignmentOptions.Middle;
            sessionsEmptyText.gameObject.SetActive(!hasData);
        }

        if (sessionsChartRoot != null)
            sessionsChartRoot.SetActive(hasData);

        string captionText = hasData
            ? $"Last {Mathf.Min(completedCount, maxChartPoints)} simulation runs"
            : string.Empty;

        LayoutMySessionsSectionHeader(captionText);
        ConfigureProfileChartPresentation();

        if (!hasData)
        {
            if (sessionsChart?.chartInfoText != null)
                sessionsChart.chartInfoText.gameObject.SetActive(false);
            sessionsChart?.Clear();
            return;
        }

        if (sciValues.Count == 1)
            sciValues = new List<float> { sciValues[0], sciValues[0] };

        if (sessionsChart != null)
        {
            sessionsChart.SetFromValues(sciValues, sessionsChart.maxSciDisplay);
            ScheduleChartRefreshAfterLayout(captionText);
        }
    }

    private void LayoutMySessionsSectionHeader(string captionText)
    {
        if (mySessionsSection == null)
            return;

        RectTransform titleRt = mySessionsSection.Find("MySessionsTitle_TXT") as RectTransform;
        if (titleRt != null)
        {
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -sectionTitleTopOffset);
            titleRt.sizeDelta = new Vector2(420f, 34f);
        }

        float titleBlockBottom = sectionTitleTopOffset + (titleRt != null ? titleRt.sizeDelta.y : 34f);
        float captionTop = titleBlockBottom + captionGapBelowTitle;
        _computedChartTopInset = captionTop + captionHeight + chartGapBelowCaption + chartAreaPaddingTop;

        TextMeshProUGUI caption = sessionsChart != null ? sessionsChart.chartInfoText : null;
        if (caption == null)
            return;

        bool showCaption = !string.IsNullOrEmpty(captionText);
        caption.gameObject.SetActive(showCaption);
        if (!showCaption)
            return;

        if (!_captionReparented || caption.transform.parent != mySessionsSection)
        {
            caption.rectTransform.SetParent(mySessionsSection, false);
            _captionReparented = true;
        }

        RectTransform captionRt = caption.rectTransform;
        captionRt.anchorMin = new Vector2(0.5f, 1f);
        captionRt.anchorMax = new Vector2(0.5f, 1f);
        captionRt.pivot = new Vector2(0.5f, 1f);
        captionRt.anchoredPosition = new Vector2(0f, -captionTop);
        captionRt.sizeDelta = new Vector2(760f, captionHeight);
        caption.fontSize = 17f;
        caption.alignment = TextAlignmentOptions.Center;
        caption.color = new Color(0.88f, 0.9f, 1f, 1f);
        caption.text = captionText;

        if (titleRt != null)
            titleRt.SetSiblingIndex(0);
        captionRt.SetSiblingIndex(1);
    }

    private void ConfigureProfileChartPresentation()
    {
        ApplyChartAreaRect();

        if (sessionsChart == null)
            return;

        sessionsChart.updateTitleAtRuntime = false;
        sessionsChart.updateInfoAtRuntime = false;
        sessionsChart.chartTitle = string.Empty;
        sessionsChart.lineColor = new Color(0.45f, 0.88f, 1f, 1f);
        sessionsChart.chartLineWidth = 4;
        sessionsChart.pointColor = new Color(1f, 0.85f, 0.35f, 1f);
        sessionsChart.gridColor = new Color(0.75f, 0.82f, 0.95f, 0.35f);
        sessionsChart.axisColor = new Color(0.85f, 0.9f, 1f, 0.85f);

        sessionsChart.ApplyInsetFillLayout(
            chartAreaPaddingLeft,
            chartAreaPaddingBottom,
            chartAreaPaddingRight,
            chartAreaPaddingTop,
            0f);
    }

    private void ApplyChartAreaRect()
    {
        RectTransform chartArea = sessionsChartRoot != null
            ? sessionsChartRoot.transform as RectTransform
            : sessionsChart != null
                ? sessionsChart.transform as RectTransform
                : null;

        if (chartArea == null)
            return;

        chartArea.anchorMin = Vector2.zero;
        chartArea.anchorMax = Vector2.one;
        chartArea.pivot = new Vector2(0.5f, 0.5f);
        chartArea.anchoredPosition = Vector2.zero;
        chartArea.offsetMin = new Vector2(chartAreaPaddingLeft, chartAreaPaddingBottom);
        chartArea.offsetMax = new Vector2(-chartAreaPaddingRight, -_computedChartTopInset);
    }

    private void ScheduleChartRefreshAfterLayout(string captionText)
    {
        if (!isActiveAndEnabled)
            return;

        if (_refreshChartRoutine != null)
            StopCoroutine(_refreshChartRoutine);

        _refreshChartRoutine = StartCoroutine(RefreshChartAfterLayout(captionText));
    }

    private IEnumerator RefreshChartAfterLayout(string captionText)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        LayoutMySessionsSectionHeader(captionText);
        ConfigureProfileChartPresentation();

        if (sessionsChart == null)
        {
            _refreshChartRoutine = null;
            yield break;
        }

        string email = LocalAuthStore.GetCurrentLoggedInEmail();
        List<float> values = SessionHistoryStore.GetRecentSimulationMeanSciValues(email, maxChartPoints);
        if (values.Count == 1)
            values = new List<float> { values[0], values[0] };

        if (values.Count > 0)
        {
            sessionsChart.SetFromValues(values, sessionsChart.maxSciDisplay);
        }

        _refreshChartRoutine = null;
    }

    private void SetPanelActive(bool on)
    {
        if (profilePanelRoot != null && profilePanelRoot.activeSelf != on)
            profilePanelRoot.SetActive(on);
    }
}
