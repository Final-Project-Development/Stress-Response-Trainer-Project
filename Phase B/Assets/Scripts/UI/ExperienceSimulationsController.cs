using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Experience_simulations_Container UI.
/// - Level value: current simulation number (1 or 2)
/// - Progress text/slider: completed simulations out of total (X/2)
/// - Character name: currently logged-in user
/// </summary>
public class ExperienceSimulationsController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private TrainingFlowController flow;

    [Header("UI refs")]
    [SerializeField] private Slider simulationsSlider;
    [SerializeField] private TextMeshProUGUI progressText;      // Example: 1/2
    [SerializeField] private TextMeshProUGUI levelValueText;    // Example: 1 or 2
    [SerializeField] private TextMeshProUGUI characterNameText; // Logged-in email

    [Header("Config")]
    [SerializeField] private int totalSimulations = 2;
    [SerializeField] private string guestName = "Guest";
    [SerializeField] private bool showOnlyAfterLogin = true;
    [SerializeField] private CanvasGroup containerCanvasGroup;

    private TrainingFlowController.Phase _lastPhase;
    private bool _sim1Completed;
    private bool _sim2Completed;

    private void Awake()
    {
        if (flow == null)
            flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (simulationsSlider != null)
            simulationsSlider.interactable = false;

        if (containerCanvasGroup == null)
            containerCanvasGroup = GetComponent<CanvasGroup>();
        if (containerCanvasGroup == null)
            containerCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        _lastPhase = flow != null ? flow.CurrentPhase : TrainingFlowController.Phase.Gate;
        RefreshUi();
    }

    private void Update()
    {
        ApplyVisibility();

        if (flow == null)
        {
            // Safe default: hide until flow is available and login state can be evaluated.
            SetContainerVisible(false);
            return;
        }

        var phase = flow.CurrentPhase;
        if (phase != _lastPhase)
        {
            if (phase == TrainingFlowController.Phase.Gate || phase == TrainingFlowController.Phase.IntroNarration)
            {
                // New run starts: reset completion counters.
                _sim1Completed = false;
                _sim2Completed = false;
            }
            else if (phase == TrainingFlowController.Phase.Simulation1Results)
            {
                _sim1Completed = true;
            }
            else if (phase == TrainingFlowController.Phase.Simulation2Results)
            {
                _sim2Completed = true;
            }

            _lastPhase = phase;
            RefreshUi();
            return;
        }

        // Keep username in sync in case login happened while panel was already active.
        RefreshCharacterNameOnly();
    }

    public void RefreshUi()
    {
        ApplyVisibility();

        int total = Mathf.Max(1, totalSimulations);
        int completed = (_sim1Completed ? 1 : 0) + (_sim2Completed ? 1 : 0);
        completed = Mathf.Clamp(completed, 0, total);
        int currentSimulation = GetCurrentSimulationNumber();

        if (simulationsSlider != null)
        {
            simulationsSlider.minValue = 0f;
            simulationsSlider.maxValue = total;
            simulationsSlider.value = completed;
        }

        if (progressText != null)
            progressText.text = $"{completed}/{total}";

        if (levelValueText != null)
            levelValueText.text = currentSimulation.ToString();

        RefreshCharacterNameOnly();
    }

    private void RefreshCharacterNameOnly()
    {
        if (characterNameText == null)
            return;

        string user = LocalAuthStore.GetCurrentLoggedInEmail();
        if (string.IsNullOrEmpty(user))
            user = LocalAuthStore.GetLastLoggedInEmail();

        characterNameText.text = string.IsNullOrEmpty(user) ? guestName : user;
    }

    private void ApplyVisibility()
    {
        if (containerCanvasGroup == null)
            return;

        if (!showOnlyAfterLogin)
        {
            SetContainerVisible(true);
            return;
        }

        string user = LocalAuthStore.GetCurrentLoggedInEmail();
        bool loggedIn = !string.IsNullOrEmpty(user);
        bool inHubOrLogin = flow != null
            && (flow.CurrentPhase == TrainingFlowController.Phase.Gate
                || flow.CurrentPhase == TrainingFlowController.Phase.Login);
        bool visible = loggedIn && !inHubOrLogin;

        SetContainerVisible(visible);
    }

    private void SetContainerVisible(bool visible)
    {
        if (containerCanvasGroup == null)
            return;

        containerCanvasGroup.alpha = visible ? 1f : 0f;
        containerCanvasGroup.interactable = visible;
        containerCanvasGroup.blocksRaycasts = visible;
    }

    private int GetCurrentSimulationNumber()
    {
        if (flow == null)
            return 1;

        switch (flow.CurrentPhase)
        {
            case TrainingFlowController.Phase.SimulationPick:
                return 1;
            case TrainingFlowController.Phase.Simulation2Briefing:
            case TrainingFlowController.Phase.Simulation2Active:
            case TrainingFlowController.Phase.Simulation2Results:
                return 2;
            default:
                return 1;
        }
    }
}
