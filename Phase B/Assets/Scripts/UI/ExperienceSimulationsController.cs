using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-bar progress shown only during active simulations.
/// Displays completed mission steps out of the total for Sim 1 (4) or Sim 2 (7).
/// </summary>
public class ExperienceSimulationsController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private TrainingFlowController flow;
    [SerializeField] private GameManager gameManager;

    [Header("UI refs")]
    [SerializeField] private Slider simulationsSlider;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI levelValueText;

    [Header("Config")]
    [SerializeField] private string progressFormat = "{0}/{1}";
    [SerializeField] private CanvasGroup containerCanvasGroup;

    private TrainingFlowController.Phase _lastPhase;
    private int _lastCompleted = -1;
    private int _lastTotal = -1;

    private bool DrivesTopBarProgressUi =>
        simulationsSlider != null || progressText != null;

    private void Awake()
    {
        if (!DrivesTopBarProgressUi)
            return;

        if (flow == null)
            flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        if (simulationsSlider != null)
            simulationsSlider.interactable = false;

        if (containerCanvasGroup == null)
            containerCanvasGroup = GetComponent<CanvasGroup>();
        if (containerCanvasGroup == null)
            containerCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (!DrivesTopBarProgressUi)
            return;

        _lastPhase = flow != null ? flow.CurrentPhase : TrainingFlowController.Phase.Gate;
        RefreshUi(force: true);
    }

    private void Update()
    {
        if (!DrivesTopBarProgressUi)
            return;

        UserProfileController profileController =
            FindFirstObjectByType<UserProfileController>(FindObjectsInactive.Include);
        profileController?.RefreshProfileToolbarState();

        if (flow == null)
        {
            SetContainerVisible(false);
            return;
        }

        var phase = flow.CurrentPhase;
        if (phase != _lastPhase)
        {
            _lastPhase = phase;
            _lastCompleted = -1;
            _lastTotal = -1;
            RefreshUi(force: true);
            return;
        }

        if (IsMissionProgressPhase(phase))
            RefreshMissionProgress(force: false);
    }

    public void RefreshUi(bool force = false)
    {
        if (!DrivesTopBarProgressUi)
            return;

        ApplyVisibility();

        if (!IsMissionProgressPhase(_lastPhase))
            return;

        RefreshMissionProgress(force);
    }

    private void RefreshMissionProgress(bool force)
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

        bool sim2 = _lastPhase == TrainingFlowController.Phase.Simulation2Active;
        int total = sim2
            ? gameManager != null ? gameManager.GetSim2TotalMissionCount() : GameManager.Sim2MissionStepCount
            : gameManager != null ? gameManager.GetSim1TotalMissionCount() : GameManager.Sim1MissionStepCount;
        int completed = sim2
            ? gameManager != null ? gameManager.GetSim2CompletedMissionCount() : 0
            : gameManager != null ? gameManager.GetSim1CompletedMissionCount() : 0;

        completed = Mathf.Clamp(completed, 0, total);

        if (!force && completed == _lastCompleted && total == _lastTotal)
            return;

        _lastCompleted = completed;
        _lastTotal = total;

        if (simulationsSlider != null)
        {
            simulationsSlider.minValue = 0f;
            simulationsSlider.maxValue = total;
            simulationsSlider.value = completed;
        }

        if (progressText != null)
            progressText.text = string.Format(progressFormat, completed, total);

        if (levelValueText != null)
            levelValueText.text = sim2 ? "2" : "1";
    }

    private void ApplyVisibility()
    {
        if (!DrivesTopBarProgressUi || containerCanvasGroup == null)
            return;

        bool visible = flow != null && IsMissionProgressPhase(flow.CurrentPhase);
        SetContainerVisible(visible);
    }

    private static bool IsMissionProgressPhase(TrainingFlowController.Phase phase)
    {
        return phase == TrainingFlowController.Phase.Simulation1Active
            || phase == TrainingFlowController.Phase.Simulation2Active;
    }

    private void SetContainerVisible(bool visible)
    {
        if (containerCanvasGroup == null)
            return;

        containerCanvasGroup.alpha = visible ? 1f : 0f;
        containerCanvasGroup.interactable = visible;
        containerCanvasGroup.blocksRaycasts = visible;
    }
}
